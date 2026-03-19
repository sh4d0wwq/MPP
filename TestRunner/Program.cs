using System.Reflection;
using TestFramework.Runner;
using TestFramework.ThreadPool;

Console.OutputEncoding = System.Text.Encoding.UTF8;

PrintHelp();

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

void PrintHelp()
{
    Console.WriteLine("=== Тестовый фреймворк (Лабораторная работа 3) ===");
    Console.WriteLine("Собственный пул потоков с динамическим масштабированием\n");
    Console.WriteLine("Параметры:");
    Console.WriteLine("  --parallel            Параллельное выполнение (по умолчанию)");
    Console.WriteLine("  --sequential          Последовательное выполнение");
    Console.WriteLine("  --custom-pool         Использовать собственный пул потоков");
    Console.WriteLine("  --min-threads=N       Минимальное количество потоков (по умолчанию: 2)");
    Console.WriteLine("  --max-threads=N       Максимальное количество потоков (по умолчанию: кол-во ядер * 2)");
    Console.WriteLine("  --compare             Сравнить производительность режимов");
    Console.WriteLine("  --load-test           Моделирование нагрузки (50+ тестов)");
    Console.WriteLine("  --custom-pool-demo    Демонстрация работы собственного пула");
    Console.WriteLine("  --output=FILE         Файл для сохранения результатов");
    Console.WriteLine();
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
