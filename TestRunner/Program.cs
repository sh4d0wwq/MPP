using System.Reflection;
using TestFramework.Runner;

Console.OutputEncoding = System.Text.Encoding.UTF8;

Console.WriteLine("=== Тестовый фреймворк ===\n");

try
{
    Assembly testAssembly;
    
    if (args.Length > 0 && File.Exists(args[0]))
    {
        testAssembly = Assembly.LoadFrom(args[0]);
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

    TextWriter output;
    var outputFile = args.Length > 1 ? args[1] : "results.txt";
    
    using var fileWriter = new StreamWriter(outputFile);
    output = new CompositeWriter(Console.Out, fileWriter);
    
    var runner = new TestRunner(output);
    var results = await runner.RunTestsAsync(testAssembly);

    Console.WriteLine($"\nРезультаты сохранены в: {outputFile}");
    
    var failed = results.Count(r => r.Result == TestResult.Failed);
    return failed > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Ошибка: {ex.Message}");
    return 2;
}

class CompositeWriter : TextWriter
{
    private readonly TextWriter[] _writers;

    public CompositeWriter(params TextWriter[] writers)
    {
        _writers = writers;
    }

    public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

    public override void WriteLine(string? value)
    {
        foreach (var w in _writers)
            w.WriteLine(value);
    }

    public override void Write(string? value)
    {
        foreach (var w in _writers)
            w.Write(value);
    }
}
