using System.Collections.Concurrent;

namespace TestFramework.ThreadPool;

public class ThreadPoolOptions
{
    public int MinThreads { get; set; } = 2;
    public int MaxThreads { get; set; } = Environment.ProcessorCount * 2;
    public int IdleTimeoutMs { get; set; } = 5000;
    public int ScaleUpThreshold { get; set; } = 3;
    public int TaskWaitTimeoutMs { get; set; } = 1000;
    public int HungThreadTimeoutMs { get; set; } = 30000;
    public bool EnableMonitoring { get; set; } = true;
    public int MonitoringIntervalMs { get; set; } = 500;
}

public class PoolStatistics
{
    public int ActiveThreads { get; set; }
    public int BusyThreads { get; set; }
    public int IdleThreads { get; set; }
    public int QueuedTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int FailedTasks { get; set; }
    public int ThreadsCreated { get; set; }
    public int ThreadsDestroyed { get; set; }
    public int HungThreadsReplaced { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class WorkItem
{
    public Action Task { get; }
    public DateTime EnqueuedAt { get; }
    public string Name { get; }

    public WorkItem(Action task, string name = "")
    {
        Task = task;
        EnqueuedAt = DateTime.Now;
        Name = name;
    }

    public TimeSpan WaitTime => DateTime.Now - EnqueuedAt;
}

public class CustomThreadPool : IDisposable
{
    private readonly ThreadPoolOptions _options;
    private readonly ConcurrentQueue<WorkItem> _taskQueue = new();
    private readonly List<WorkerThread> _workers = new();
    private readonly object _workersLock = new();
    private readonly SemaphoreSlim _taskAvailable = new(0);
    private readonly ManualResetEventSlim _shutdownEvent = new(false);
    private readonly Thread _monitorThread;
    private readonly Thread _scalerThread;
    private readonly Action<string>? _logger;
    
    private volatile bool _isDisposed;
    private int _completedTasks;
    private int _failedTasks;
    private int _threadsCreated;
    private int _threadsDestroyed;
    private int _hungThreadsReplaced;

    public event Action<PoolStatistics>? OnStatisticsUpdated;
    public event Action<string>? OnLog;

    public CustomThreadPool(ThreadPoolOptions? options = null, Action<string>? logger = null)
    {
        _options = options ?? new ThreadPoolOptions();
        _logger = logger;

        for (int i = 0; i < _options.MinThreads; i++)
        {
            CreateWorker();
        }

        _monitorThread = new Thread(MonitorLoop) { IsBackground = true, Name = "PoolMonitor" };
        _scalerThread = new Thread(ScalerLoop) { IsBackground = true, Name = "PoolScaler" };
        
        _monitorThread.Start();
        _scalerThread.Start();

        Log($"Пул создан: MinThreads={_options.MinThreads}, MaxThreads={_options.MaxThreads}");
    }

    public void QueueTask(Action task, string name = "")
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CustomThreadPool));

        var workItem = new WorkItem(task, name);
        _taskQueue.Enqueue(workItem);
        _taskAvailable.Release();
        
        Log($"Задача добавлена: {name}, в очереди: {_taskQueue.Count}");
    }

    public Task QueueTaskAsync(Action task, string name = "")
    {
        var tcs = new TaskCompletionSource<bool>();
        
        QueueTask(() =>
        {
            try
            {
                task();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }, name);

        return tcs.Task;
    }

    public PoolStatistics GetStatistics()
    {
        lock (_workersLock)
        {
            var busy = _workers.Count(w => w.IsBusy);
            return new PoolStatistics
            {
                ActiveThreads = _workers.Count,
                BusyThreads = busy,
                IdleThreads = _workers.Count - busy,
                QueuedTasks = _taskQueue.Count,
                CompletedTasks = _completedTasks,
                FailedTasks = _failedTasks,
                ThreadsCreated = _threadsCreated,
                ThreadsDestroyed = _threadsDestroyed,
                HungThreadsReplaced = _hungThreadsReplaced
            };
        }
    }

    public void WaitForCompletion(int timeoutMs = -1)
    {
        var deadline = timeoutMs > 0 ? DateTime.Now.AddMilliseconds(timeoutMs) : DateTime.MaxValue;
        
        while (!_isDisposed && DateTime.Now < deadline)
        {
            if (_taskQueue.IsEmpty)
            {
                lock (_workersLock)
                {
                    if (_workers.All(w => !w.IsBusy))
                        return;
                }
            }
            Thread.Sleep(50);
        }
    }

    private void CreateWorker()
    {
        lock (_workersLock)
        {
            if (_workers.Count >= _options.MaxThreads)
                return;

            var worker = new WorkerThread(this, _workers.Count + 1);
            _workers.Add(worker);
            _threadsCreated++;
            
            Log($"Поток создан: {worker.Name}, всего потоков: {_workers.Count}");
        }
    }

    private void RemoveWorker(WorkerThread worker)
    {
        lock (_workersLock)
        {
            if (_workers.Count <= _options.MinThreads)
                return;

            if (_workers.Remove(worker))
            {
                _threadsDestroyed++;
                Log($"Поток завершён: {worker.Name}, осталось потоков: {_workers.Count}");
            }
        }
    }

    private void ReplaceHungWorker(WorkerThread worker)
    {
        lock (_workersLock)
        {
            if (_workers.Remove(worker))
            {
                _hungThreadsReplaced++;
                _threadsDestroyed++;
                Log($"Зависший поток заменён: {worker.Name}");
                
                if (_workers.Count < _options.MaxThreads)
                {
                    CreateWorker();
                }
            }
        }
    }

    private void ScalerLoop()
    {
        while (!_shutdownEvent.Wait(100))
        {
            try
            {
                ScaleIfNeeded();
            }
            catch (Exception ex)
            {
                Log($"Ошибка в ScalerLoop: {ex.Message}");
            }
        }
    }

    private void ScaleIfNeeded()
    {
        var queueSize = _taskQueue.Count;
        
        lock (_workersLock)
        {
            var idleCount = _workers.Count(w => !w.IsBusy);

            if (queueSize > _options.ScaleUpThreshold && idleCount == 0 && _workers.Count < _options.MaxThreads)
            {
                CreateWorker();
                return;
            }

            if (_taskQueue.TryPeek(out var oldestTask))
            {
                if (oldestTask.WaitTime.TotalMilliseconds > _options.TaskWaitTimeoutMs && 
                    _workers.Count < _options.MaxThreads)
                {
                    Log($"Задача ждёт слишком долго ({oldestTask.WaitTime.TotalMilliseconds:F0} мс), создаём поток");
                    CreateWorker();
                }
            }
        }
    }

    private void MonitorLoop()
    {
        while (!_shutdownEvent.Wait(_options.MonitoringIntervalMs))
        {
            try
            {
                CheckHungThreads();
                
                if (_options.EnableMonitoring)
                {
                    var stats = GetStatistics();
                    OnStatisticsUpdated?.Invoke(stats);
                }
            }
            catch (Exception ex)
            {
                Log($"Ошибка в MonitorLoop: {ex.Message}");
            }
        }
    }

    private void CheckHungThreads()
    {
        List<WorkerThread> hungWorkers;
        
        lock (_workersLock)
        {
            hungWorkers = _workers
                .Where(w => w.IsBusy && w.CurrentTaskDuration.TotalMilliseconds > _options.HungThreadTimeoutMs)
                .ToList();
        }

        foreach (var worker in hungWorkers)
        {
            Log($"Обнаружен зависший поток: {worker.Name}, время выполнения: {worker.CurrentTaskDuration.TotalSeconds:F1} сек");
            worker.Abort();
            ReplaceHungWorker(worker);
        }
    }

    internal bool TryGetTask(out WorkItem? task, int timeoutMs)
    {
        task = null;
        
        if (_taskAvailable.Wait(timeoutMs))
        {
            return _taskQueue.TryDequeue(out task);
        }
        
        return false;
    }

    internal void ReportTaskCompleted(bool success)
    {
        if (success)
            Interlocked.Increment(ref _completedTasks);
        else
            Interlocked.Increment(ref _failedTasks);
    }

    internal bool ShouldWorkerExit(WorkerThread worker)
    {
        if (_isDisposed) return true;
        
        lock (_workersLock)
        {
            return _workers.Count > _options.MinThreads && 
                   worker.IdleTime.TotalMilliseconds > _options.IdleTimeoutMs;
        }
    }

    internal void OnWorkerExit(WorkerThread worker)
    {
        RemoveWorker(worker);
    }

    internal bool IsShutdown => _isDisposed;
    internal int IdleTimeoutMs => _options.IdleTimeoutMs;

    private void Log(string message)
    {
        var msg = $"[{DateTime.Now:HH:mm:ss.fff}] [Pool] {message}";
        _logger?.Invoke(msg);
        OnLog?.Invoke(msg);
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        Log("Завершение работы пула...");
        _shutdownEvent.Set();

        try
        {
            lock (_workersLock)
            {
                foreach (var worker in _workers.ToList())
                {
                    worker.Stop();
                }
            }

            _monitorThread.Join(1000);
            _scalerThread.Join(1000);
        }
        catch { }

        try
        {
            _taskAvailable.Dispose();
            _shutdownEvent.Dispose();
        }
        catch { }

        Log($"Пул завершён. Выполнено задач: {_completedTasks}, ошибок: {_failedTasks}");
    }
}
