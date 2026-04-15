using System.Reflection;
using TestFramework.Assertions;
using TestFramework.Exceptions;
using TestFramework.Runner;
using TestFramework.ThreadPool;

Console.OutputEncoding = System.Text.Encoding.UTF8;

PrintBanner();

try
{
    Assembly testAssembly;
    
    var assemblyArg = args.FirstOrDefault(a => !a.StartsWith("--") && !a.StartsWith("-"));
    
    if (assemblyArg != null && File.Exists(assemblyArg))
    {
        testAssembly = Assembly.LoadFrom(assemblyArg);
    }
    else
    {
        var testsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleLibrary.Tests.dll");
        
        if (File.Exists(testsPath))
        {
            testAssembly = Assembly.LoadFrom(testsPath);
        }
        else
        {
            testAssembly = typeof(SampleLibrary.Tests.BankAccountTests).Assembly;
        }
    }

    Console.WriteLine($"Сборка тестов: {testAssembly.GetName().Name}\n");

    var options = ParseOptions(args);
    var outputFile = GetArgValue(args, "--output") ?? "results.txt";
    var compareMode = args.Contains("--compare");
    var loadTestMode = args.Contains("--load-test");
    var customPoolDemo = args.Contains("--custom-pool-demo");

    using var fileWriter = new StreamWriter(outputFile);
    var output = new CompositeWriter(Console.Out, fileWriter);
    
    var runner = new TestRunner(output, options);

    if (loadTestMode)
    {
        await RunLoadTest(output);
    }
    else if (customPoolDemo)
    {
        await RunCustomPoolDemo(output, options, testAssembly);
    }
    else if (compareMode)
    {
        await runner.ComparePerformanceAsync(testAssembly);
    }
    else
    {
        if (!args.Contains("--skip-lab4-demo"))
        {
            await RunLab4Showcase(output, testAssembly);
        }

        WriteColored(output, "\n========== Полный прогон тестов ==========\n", ConsoleColor.White);
        var results = await runner.RunTestsAsync(testAssembly);
        
        Console.WriteLine($"\nРезультаты сохранены в: {outputFile}");
        
        var failed = results.Count(r => r.Result == TestResult.Failed || r.Result == TestResult.Timeout);
        return failed > 0 ? 1 : 0;
    }
    
    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 2;
}

void PrintBanner()
{
    Console.WriteLine("=== Тестовый фреймворк | ЛР 3–4 ===");
    Console.WriteLine("ЛР4: yield TestCaseSource, события пула, фильтр (--category / --author), Assert.That(expr)");
    Console.WriteLine("Флаги: --skip-lab4-demo  --category=X  --author=X  --compare  --load-test  --custom-pool-demo\n");
}

TestRunnerOptions ParseOptions(string[] args)
{
    var options = new TestRunnerOptions();

    if (args.Contains("--sequential"))
    {
        options.RunInParallel = false;
    }

    if (args.Contains("--custom-pool"))
    {
        options.UseCustomThreadPool = true;
    }

    if (args.Contains("--no-method-parallel"))
    {
        options.ParallelizeTestMethods = false;
    }

    var minThreadsArg = args.FirstOrDefault(a => a.StartsWith("--min-threads="));
    if (minThreadsArg != null)
    {
        var value = minThreadsArg.Split('=')[1];
        if (int.TryParse(value, out var minThreads) && minThreads > 0)
        {
            options.MinThreads = minThreads;
        }
    }

    var maxThreadsArg = args.FirstOrDefault(a => a.StartsWith("--max-threads="));
    if (maxThreadsArg != null)
    {
        var value = maxThreadsArg.Split('=')[1];
        if (int.TryParse(value, out var maxThreads) && maxThreads > 0)
        {
            options.MaxDegreeOfParallelism = maxThreads;
            options.MaxThreads = maxThreads;
        }
    }

    var category = GetArgValue(args, "--category");
    var author = GetArgValue(args, "--author");
    var filterParts = new List<Func<TestMetadata, bool>>();
    if (!string.IsNullOrWhiteSpace(category))
    {
        var c = category.Trim();
        filterParts.Add(m => m.Categories.Any(x => string.Equals(x, c, StringComparison.OrdinalIgnoreCase)));
    }
    if (!string.IsNullOrWhiteSpace(author))
    {
        var a = author.Trim();
        filterParts.Add(m => string.Equals(m.Author, a, StringComparison.OrdinalIgnoreCase));
    }
    if (filterParts.Count > 0)
    {
        options.TestFilter = m => filterParts.All(f => f(m));
    }

    return options;
}

string? GetArgValue(string[] args, string prefix)
{
    var arg = args.FirstOrDefault(a => a.StartsWith(prefix + "="));
    return arg?.Split('=')[1];
}

async Task RunLoadTest(TextWriter output)
{
    WriteColored(output, "=== МОДЕЛИРОВАНИЕ НАГРУЗКИ (50+ тестов) ===\n", ConsoleColor.Cyan);
    
    var poolOptions = new ThreadPoolOptions
    {
        MinThreads = 2,
        MaxThreads = 8,
        IdleTimeoutMs = 2000,
        ScaleUpThreshold = 3,
        TaskWaitTimeoutMs = 500,
        EnableMonitoring = true,
        MonitoringIntervalMs = 300
    };

    using var pool = new CustomThreadPool(poolOptions, msg => 
    {
        WriteColored(output, msg, ConsoleColor.DarkGray);
    });

    pool.OnStatisticsUpdated += stats =>
    {
        WriteColored(output, 
            $"[МОНИТОР] Потоков: {stats.ActiveThreads} (занято: {stats.BusyThreads}), " +
            $"Очередь: {stats.QueuedTasks}, Выполнено: {stats.CompletedTasks}", 
            ConsoleColor.DarkYellow);
    };

    int taskId = 0;
    var random = new Random(42);

    WriteColored(output, "\n--- Фаза 1: Начальная нагрузка (10 задач) ---", ConsoleColor.Yellow);
    for (int i = 0; i < 10; i++)
    {
        var id = ++taskId;
        var delay = random.Next(100, 500);
        pool.QueueTask(() =>
        {
            Thread.Sleep(delay);
        }, $"Task-{id}");
    }
    await Task.Delay(1000);

    WriteColored(output, "\n--- Фаза 2: Период бездействия (3 сек) ---", ConsoleColor.Yellow);
    await Task.Delay(3000);

    WriteColored(output, "\n--- Фаза 3: Пиковая нагрузка (30 задач одновременно) ---", ConsoleColor.Yellow);
    for (int i = 0; i < 30; i++)
    {
        var id = ++taskId;
        var delay = random.Next(200, 800);
        pool.QueueTask(() =>
        {
            Thread.Sleep(delay);
        }, $"Task-{id}");
    }
    await Task.Delay(2000);

    WriteColored(output, "\n--- Фаза 4: Единичные подачи (10 задач с паузами) ---", ConsoleColor.Yellow);
    for (int i = 0; i < 10; i++)
    {
        var id = ++taskId;
        var delay = random.Next(100, 300);
        pool.QueueTask(() =>
        {
            Thread.Sleep(delay);
        }, $"Task-{id}");
        await Task.Delay(random.Next(200, 500));
    }

    WriteColored(output, "\n--- Фаза 5: Финальный всплеск (10 задач) ---", ConsoleColor.Yellow);
    var completionTasks = new List<Task>();
    for (int i = 0; i < 10; i++)
    {
        var id = ++taskId;
        var delay = random.Next(100, 400);
        var task = pool.QueueTaskAsync(() =>
        {
            Thread.Sleep(delay);
        }, $"Task-{id}");
        completionTasks.Add(task);
    }

    WriteColored(output, "\n--- Ожидание завершения всех задач ---", ConsoleColor.Yellow);
    await Task.WhenAll(completionTasks);
    pool.WaitForCompletion(5000);

    var finalStats = pool.GetStatistics();
    WriteColored(output, "\n=== ИТОГИ МОДЕЛИРОВАНИЯ ===", ConsoleColor.Cyan);
    WriteColored(output, $"Всего задач выполнено: {finalStats.CompletedTasks}", ConsoleColor.Green);
    WriteColored(output, $"Ошибок: {finalStats.FailedTasks}", ConsoleColor.Red);
    WriteColored(output, $"Потоков создано: {finalStats.ThreadsCreated}", ConsoleColor.White);
    WriteColored(output, $"Потоков завершено: {finalStats.ThreadsDestroyed}", ConsoleColor.White);
    WriteColored(output, $"Зависших потоков заменено: {finalStats.HungThreadsReplaced}", ConsoleColor.Magenta);
    WriteColored(output, $"Активных потоков в конце: {finalStats.ActiveThreads}", ConsoleColor.White);

    WriteColored(output, $"\n--- Демонстрация динамического масштабирования завершена ---", ConsoleColor.Cyan);
    WriteColored(output, $"Всего выполнено {taskId} задач (больше 50 требуемых)", ConsoleColor.Green);
}

async Task RunLab4Showcase(TextWriter output, Assembly testAssembly)
{
    WriteColored(output, "\n╔══════════════════════════════════════════════════════════╗", ConsoleColor.Cyan);
    WriteColored(output, "║ Лабораторная работа 4 — демонстрация возможностей       ║", ConsoleColor.Cyan);
    WriteColored(output, "╚══════════════════════════════════════════════════════════╝", ConsoleColor.Cyan);

    WriteColored(output, "\n--- 1) События жизненного цикла пула потоков ---", ConsoleColor.Yellow);
    var poolOpts = new ThreadPoolOptions
    {
        MinThreads = 1,
        MaxThreads = 4,
        IdleTimeoutMs = 8000,
        ScaleUpThreshold = 2,
        TaskWaitTimeoutMs = 400,
        EnableMonitoring = false
    };
    using (var pool = new CustomThreadPool(poolOpts, null))
    {
        pool.PoolCreated += (_, e) =>
            WriteColored(output, $"  [EVT] PoolCreated: Min={e.MinThreads}, Max={e.MaxThreads}", ConsoleColor.Green);
        pool.WorkerCreated += (_, e) =>
            WriteColored(output, $"  [EVT] WorkerCreated: {e.WorkerName}, активных={e.ActiveWorkerCount}", ConsoleColor.Green);
        pool.TaskEnqueued += (_, e) =>
            WriteColored(output, $"  [EVT] TaskEnqueued: {e.TaskName}, очередь={e.QueueLength}", ConsoleColor.DarkCyan);
        pool.TaskStarted += (_, e) =>
            WriteColored(output, $"  [EVT] TaskStarted: {e.TaskName} на {e.WorkerName}", ConsoleColor.DarkGreen);
        pool.TaskCompleted += (_, e) =>
            WriteColored(output,
                $"  [EVT] TaskCompleted: {e.TaskName} успех={e.Success}" +
                (e.Error != null ? $" ({e.Error.Message})" : ""), ConsoleColor.DarkGreen);
        pool.PoolScaledUp += (_, e) =>
            WriteColored(output, $"  [EVT] PoolScaledUp: {e.Reason}, потоков={e.NewWorkerCount}", ConsoleColor.Magenta);
        pool.PoolDisposing += (_, e) =>
            WriteColored(output, $"  [EVT] PoolDisposing: выполнено={e.CompletedTasks}, ошибок={e.FailedTasks}", ConsoleColor.Yellow);

        for (var i = 0; i < 5; i++)
        {
            var id = i;
            pool.QueueTask(() => Thread.Sleep(30), $"lab4-task-{id}");
        }
        await Task.Delay(400);
        pool.WaitForCompletion(3000);
    }

    WriteColored(output, "\n--- 2) Assert.That(Expression): сообщение при провале ---", ConsoleColor.Yellow);
    try
    {
        var x = 14;
        var y = 2;
        Assert.That(() => x / y == 10);
    }
    catch (AssertFailedException ex)
    {
        WriteColored(output, ex.Message, ConsoleColor.Red);
    }

    WriteColored(output, "\n--- 3) Фильтрация тестов (делегат): только категория ParameterSource ---", ConsoleColor.Yellow);
    var filterOpts = new TestRunnerOptions
    {
        RunInParallel = false,
        TestFilter = m => m.Categories.Contains("ParameterSource", StringComparer.OrdinalIgnoreCase)
    };
    var filterRunner = new TestRunner(output, filterOpts);
    await filterRunner.RunTestsAsync(testAssembly);

    WriteColored(output, "\n--- Конец блока ЛР4, далее полный прогон ---\n", ConsoleColor.Cyan);
}

async Task RunCustomPoolDemo(TextWriter output, TestRunnerOptions options, Assembly assembly)
{
    WriteColored(output, "=== ДЕМОНСТРАЦИЯ СОБСТВЕННОГО ПУЛА ПОТОКОВ ===\n", ConsoleColor.Cyan);
    
    options.UseCustomThreadPool = true;
    options.MinThreads = 2;
    options.MaxThreads = 6;
    
    var runner = new TestRunner(output, options);
    
    await runner.RunTestsWithCustomPoolAsync(assembly, stats =>
    {
        WriteColored(output, 
            $"[POOL] Потоков: {stats.ActiveThreads}/{options.MaxThreads} " +
            $"(занято: {stats.BusyThreads}), Очередь: {stats.QueuedTasks}", 
            ConsoleColor.DarkYellow);
    });
}

void WriteColored(TextWriter output, string message, ConsoleColor color)
{
    lock (output)
    {
        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        output.WriteLine(message);
        Console.ForegroundColor = originalColor;
    }
}

class CompositeWriter : TextWriter
{
    private readonly TextWriter[] _writers;
    private readonly object _lock = new();

    public CompositeWriter(params TextWriter[] writers)
    {
        _writers = writers;
    }

    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

    public override void WriteLine(string? value)
    {
        lock (_lock)
        {
            foreach (var w in _writers)
                w.WriteLine(value);
        }
    }

    public override void Write(string? value)
    {
        lock (_lock)
        {
            foreach (var w in _writers)
                w.Write(value);
        }
    }
}
