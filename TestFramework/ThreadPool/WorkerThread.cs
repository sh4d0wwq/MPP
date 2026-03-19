namespace TestFramework.ThreadPool;

internal class WorkerThread
{
    private readonly CustomThreadPool _pool;
    private readonly Thread _thread;
    private readonly object _stateLock = new();
    
    private volatile bool _isBusy;
    private volatile bool _shouldStop;
    private DateTime _lastTaskTime;
    private DateTime _taskStartTime;
    private string _currentTaskName = "";

    public string Name { get; }
    public bool IsBusy => _isBusy;
    
    public TimeSpan IdleTime
    {
        get
        {
            lock (_stateLock)
            {
                return _isBusy ? TimeSpan.Zero : DateTime.Now - _lastTaskTime;
            }
        }
    }

    public TimeSpan CurrentTaskDuration
    {
        get
        {
            lock (_stateLock)
            {
                return _isBusy ? DateTime.Now - _taskStartTime : TimeSpan.Zero;
            }
        }
    }

    public WorkerThread(CustomThreadPool pool, int id)
    {
        _pool = pool;
        _lastTaskTime = DateTime.Now;
        Name = $"Worker-{id}";
        
        _thread = new Thread(WorkLoop)
        {
            IsBackground = true,
            Name = Name
        };
        _thread.Start();
    }

    private void WorkLoop()
    {
        while (!_shouldStop && !_pool.IsShutdown)
        {
            try
            {
                if (_pool.TryGetTask(out var workItem, _pool.IdleTimeoutMs / 2))
                {
                    if (workItem != null)
                    {
                        ExecuteTask(workItem);
                    }
                }
                else
                {
                    if (_pool.ShouldWorkerExit(this))
                    {
                        _pool.OnWorkerExit(this);
                        return;
                    }
                }
            }
            catch (ThreadInterruptedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{Name}] Критическая ошибка в WorkLoop: {ex.Message}");
            }
        }
    }

    private void ExecuteTask(WorkItem workItem)
    {
        lock (_stateLock)
        {
            _isBusy = true;
            _taskStartTime = DateTime.Now;
            _currentTaskName = workItem.Name;
        }

        bool success = false;
        try
        {
            workItem.Task();
            success = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{Name}] Ошибка в задаче '{workItem.Name}': {ex.Message}");
        }
        finally
        {
            lock (_stateLock)
            {
                _isBusy = false;
                _lastTaskTime = DateTime.Now;
                _currentTaskName = "";
            }
            
            _pool.ReportTaskCompleted(success);
        }
    }

    public void Stop()
    {
        _shouldStop = true;
        try
        {
            _thread.Interrupt();
            _thread.Join(1000);
        }
        catch (ThreadInterruptedException) { }
        catch (ThreadStateException) { }
    }

    public void Abort()
    {
        _shouldStop = true;
        try
        {
            _thread.Interrupt();
        }
        catch { }
    }
}
