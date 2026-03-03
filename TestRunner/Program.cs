using System.Reflection;
using TestFramework.Runner;

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

    using var fileWriter = new StreamWriter(outputFile);
    var output = new CompositeWriter(Console.Out, fileWriter);
    
    var runner = new TestRunner(output, options);

    if (compareMode)
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
    Console.WriteLine("=== Тестовый фреймворк (Лабораторная работа 2) ===");
    Console.WriteLine("Поддержка многопоточности и параллельного выполнения тестов\n");
    Console.WriteLine("Параметры:");
    Console.WriteLine("  --parallel         Параллельное выполнение (по умолчанию)");
    Console.WriteLine("  --sequential       Последовательное выполнение");
    Console.WriteLine("  --max-threads=N    Максимальное количество потоков (по умолчанию: кол-во ядер)");
    Console.WriteLine("  --compare          Сравнить производительность параллельного и последовательного режимов");
    Console.WriteLine("  --output=FILE      Файл для сохранения результатов (по умолчанию: results.txt)");
    Console.WriteLine("  --no-method-parallel  Не параллелить методы внутри класса");
    Console.WriteLine();
}

TestRunnerOptions ParseOptions(string[] args)
{
    var options = new TestRunnerOptions();

    if (args.Contains("--sequential"))
    {
        options.RunInParallel = false;
    }

    if (args.Contains("--no-method-parallel"))
    {
        options.ParallelizeTestMethods = false;
    }

    var maxThreadsArg = args.FirstOrDefault(a => a.StartsWith("--max-threads="));
    if (maxThreadsArg != null)
    {
        var value = maxThreadsArg.Split('=')[1];
        if (int.TryParse(value, out var maxThreads) && maxThreads > 0)
        {
            options.MaxDegreeOfParallelism = maxThreads;
        }
    }

    return options;
}

string? GetArgValue(string[] args, string prefix)
{
    var arg = args.FirstOrDefault(a => a.StartsWith(prefix + "="));
    return arg?.Split('=')[1];
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
