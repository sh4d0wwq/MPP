using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using TestFramework.Attributes;
using TestFramework.Exceptions;

namespace TestFramework.Runner;

public enum TestResult
{
    Passed,
    Failed,
    Skipped,
    Timeout
}

public class TestResultInfo
{
    public string ClassName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public TestResult Result { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
    public int ThreadId { get; set; }
}

public class TestRunnerOptions
{
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
    public bool RunInParallel { get; set; } = true;
    public bool ParallelizeTestMethods { get; set; } = true;
}

public class TestRunner
{
    private readonly ConcurrentBag<TestResultInfo> _results = new();
    private readonly TextWriter _output;
    private readonly object _outputLock = new();
    private readonly object _consoleLock = new();
    private readonly TestRunnerOptions _options;

    public TestRunner(TextWriter? output = null, TestRunnerOptions? options = null)
    {
        _output = output ?? Console.Out;
        _options = options ?? new TestRunnerOptions();
    }

    private void WriteColored(string message, ConsoleColor color)
    {
        lock (_consoleLock)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = color;
            lock (_outputLock)
            {
                _output.WriteLine(message);
            }
            Console.ForegroundColor = originalColor;
        }
    }

    private void WriteSuccess(string message) => WriteColored(message, ConsoleColor.Green);
    private void WriteFailure(string message) => WriteColored(message, ConsoleColor.Red);
    private void WriteSkipped(string message) => WriteColored(message, ConsoleColor.Yellow);
    private void WriteInfo(string message) => WriteColored(message, ConsoleColor.Cyan);
    private void WriteTimeout(string message) => WriteColored(message, ConsoleColor.Magenta);

    public async Task<List<TestResultInfo>> RunTestsAsync(Assembly assembly)
    {
        _results.Clear();
        var testClasses = DiscoverTestClasses(assembly).ToList();

        var mode = _options.RunInParallel ? "ПАРАЛЛЕЛЬНЫЙ" : "ПОСЛЕДОВАТЕЛЬНЫЙ";
        WriteInfo($"=== Запуск тестов ({mode}, MaxDegreeOfParallelism: {_options.MaxDegreeOfParallelism}) ===\n");

        var stopwatch = Stopwatch.StartNew();

        if (_options.RunInParallel)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism
            };

            await Parallel.ForEachAsync(testClasses, parallelOptions, async (testClass, ct) =>
            {
                await RunTestClassAsync(testClass);
            });
        }
        else
        {
            foreach (var testClass in testClasses)
            {
                await RunTestClassAsync(testClass);
            }
        }

        stopwatch.Stop();
        PrintSummary(stopwatch.Elapsed);
        return _results.ToList();
    }

    public async Task<List<TestResultInfo>> RunTestsFromFileAsync(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        return await RunTestsAsync(assembly);
    }

    public async Task<(TimeSpan Sequential, TimeSpan Parallel)> ComparePerformanceAsync(Assembly assembly)
    {
        WriteInfo("=== СРАВНЕНИЕ ПРОИЗВОДИТЕЛЬНОСТИ ===\n");

        var sequentialOptions = new TestRunnerOptions { RunInParallel = false };
        var sequentialRunner = new TestRunner(_output, sequentialOptions);
        
        var sw1 = Stopwatch.StartNew();
        await sequentialRunner.RunTestsAsync(assembly);
        sw1.Stop();
        var sequentialTime = sw1.Elapsed;

        WriteInfo("\n" + new string('=', 50) + "\n");

        var parallelOptions = new TestRunnerOptions 
        { 
            RunInParallel = true, 
            MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism,
            ParallelizeTestMethods = _options.ParallelizeTestMethods
        };
        var parallelRunner = new TestRunner(_output, parallelOptions);
        
        var sw2 = Stopwatch.StartNew();
        await parallelRunner.RunTestsAsync(assembly);
        sw2.Stop();
        var parallelTime = sw2.Elapsed;

        WriteInfo("\n=== ИТОГИ СРАВНЕНИЯ ===");
        WriteInfo($"Последовательное выполнение: {sequentialTime.TotalMilliseconds:F0} мс");
        WriteInfo($"Параллельное выполнение:     {parallelTime.TotalMilliseconds:F0} мс");
        
        var speedup = sequentialTime.TotalMilliseconds / parallelTime.TotalMilliseconds;

        WriteInfo($"Ускорение: {speedup:F2}x");

        return (sequentialTime, parallelTime);
    }

    private IEnumerable<Type> DiscoverTestClasses(Assembly assembly)
    {
        return assembly.GetTypes()
            .Where(t => t.GetCustomAttribute<TestClassAttribute>() != null)
            .OrderBy(t => t.GetCustomAttribute<TestClassAttribute>()!.Priority);
    }

    private async Task RunTestClassAsync(Type testClass)
    {
        var ignoreAttr = testClass.GetCustomAttribute<IgnoreAttribute>();
        if (ignoreAttr != null)
        {
            WriteSkipped($"[ПРОПУЩЕН] Класс {testClass.Name}: {ignoreAttr.Reason ?? "без причины"}");
            return;
        }

        var classAttr = testClass.GetCustomAttribute<TestClassAttribute>()!;
        WriteInfo($"--- {testClass.Name} (приоритет: {classAttr.Priority}, поток: {Environment.CurrentManagedThreadId}) ---");

        var beforeEach = GetMethodWithAttribute<BeforeEachAttribute>(testClass);
        var afterEach = GetMethodWithAttribute<AfterEachAttribute>(testClass);
        var testMethods = GetTestMethods(testClass).ToList();

        var testExecutions = new List<(MethodInfo Method, object?[]? Parameters)>();

        foreach (var method in testMethods)
        {
            var testCases = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
            
            if (testCases.Length > 0)
            {
                foreach (var testCase in testCases)
                {
                    testExecutions.Add((method, testCase.Parameters));
                }
            }
            else
            {
                testExecutions.Add((method, null));
            }
        }

        if (_options.RunInParallel && _options.ParallelizeTestMethods)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism
            };

            await Parallel.ForEachAsync(testExecutions, parallelOptions, async (execution, ct) =>
            {
                var instance = Activator.CreateInstance(testClass);
                await RunTestMethodAsync(instance!, execution.Method, beforeEach, afterEach, execution.Parameters);
            });
        }
        else
        {
            var instance = Activator.CreateInstance(testClass);
            foreach (var execution in testExecutions)
            {
                await RunTestMethodAsync(instance!, execution.Method, beforeEach, afterEach, execution.Parameters);
            }
        }

        lock (_outputLock)
        {
            _output.WriteLine();
        }
    }

    private async Task RunTestMethodAsync(object instance, MethodInfo method, 
        MethodInfo? beforeEach, MethodInfo? afterEach, object?[]? parameters)
    {
        var displayName = parameters != null 
            ? $"{method.Name}({string.Join(", ", parameters)})" 
            : method.Name;

        var result = new TestResultInfo
        {
            ClassName = instance.GetType().Name,
            MethodName = displayName,
            ThreadId = Environment.CurrentManagedThreadId
        };

        var ignoreAttr = method.GetCustomAttribute<IgnoreAttribute>();
        if (ignoreAttr != null)
        {
            result.Result = TestResult.Skipped;
            result.ErrorMessage = ignoreAttr.Reason ?? "Тест пропущен";
            WriteSkipped($"  [ПРОПУЩЕН] {displayName}: {result.ErrorMessage}");
            _results.Add(result);
            return;
        }

        var timeoutAttr = method.GetCustomAttribute<TimeoutAttribute>();
        var expectedExceptionAttr = method.GetCustomAttribute<ExpectedExceptionAttribute>();
        var startTime = Stopwatch.StartNew();

        try
        {
            if (beforeEach != null)
            {
                await InvokeMethodAsync(beforeEach, instance, null);
            }

            if (timeoutAttr != null)
            {
                await InvokeWithTimeoutAsync(method, instance, parameters, timeoutAttr.Milliseconds);
            }
            else
            {
                await InvokeMethodAsync(method, instance, parameters);
            }

            if (expectedExceptionAttr != null)
            {
                throw new AssertFailedException(
                    $"Ожидалось исключение {expectedExceptionAttr.ExceptionType.Name}");
            }

            result.Result = TestResult.Passed;
            WriteSuccess($"  [OK] {displayName} (поток: {Environment.CurrentManagedThreadId})");
        }
        catch (TimeoutException)
        {
            result.Result = TestResult.Timeout;
            result.ErrorMessage = $"Превышено время ожидания ({timeoutAttr!.Milliseconds} мс)";
            WriteTimeout($"  [ТАЙМАУТ] {displayName}: {result.ErrorMessage}");
        }
        catch (Exception ex)
        {
            var actualException = ex.InnerException ?? ex;

            if (expectedExceptionAttr != null && 
                expectedExceptionAttr.ExceptionType.IsInstanceOfType(actualException))
            {
                result.Result = TestResult.Passed;
                WriteSuccess($"  [OK] {displayName} (исключение {actualException.GetType().Name})");
            }
            else
            {
                result.Result = TestResult.Failed;
                result.ErrorMessage = actualException.Message;
                WriteFailure($"  [ПРОВАЛЕН] {displayName}: {actualException.Message}");
            }
        }
        finally
        {
            try
            {
                if (afterEach != null)
                {
                    await InvokeMethodAsync(afterEach, instance, null);
                }
            }
            catch (Exception ex)
            {
                WriteFailure($"    Ошибка в AfterEach: {ex.Message}");
            }

            startTime.Stop();
            result.Duration = startTime.Elapsed;
            _results.Add(result);
        }
    }

    private static async Task InvokeMethodAsync(MethodInfo method, object instance, object?[]? parameters)
    {
        var result = method.Invoke(instance, parameters);
        if (result is Task task)
        {
            await task;
        }
    }

    private static async Task InvokeWithTimeoutAsync(MethodInfo method, object instance, object?[]? parameters, int timeoutMs)
    {
        using var cts = new CancellationTokenSource();
        
        var testTask = Task.Run(async () =>
        {
            var result = method.Invoke(instance, parameters);
            if (result is Task task)
            {
                await task;
            }
        });

        var timeoutTask = Task.Delay(timeoutMs, cts.Token);
        var completedTask = await Task.WhenAny(testTask, timeoutTask);

        if (completedTask == timeoutTask)
        {
            throw new TimeoutException($"Тест не завершился за {timeoutMs} мс");
        }

        cts.Cancel();
        await testTask;
    }

    private static MethodInfo? GetMethodWithAttribute<T>(Type type) where T : Attribute
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.GetCustomAttribute<T>() != null);
    }

    private static IEnumerable<MethodInfo> GetTestMethods(Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttribute<TestMethodAttribute>() != null)
            .OrderBy(m => m.GetCustomAttribute<TestMethodAttribute>()!.Priority);
    }

    private void PrintSummary(TimeSpan totalTime)
    {
        var passed = _results.Count(r => r.Result == TestResult.Passed);
        var failed = _results.Count(r => r.Result == TestResult.Failed);
        var skipped = _results.Count(r => r.Result == TestResult.Skipped);
        var timeout = _results.Count(r => r.Result == TestResult.Timeout);

        WriteInfo("=== Результаты ===");
        lock (_outputLock)
        {
            _output.WriteLine($"Всего: {_results.Count}");
        }
        WriteSuccess($"Успешно: {passed}");
        WriteFailure($"Провалено: {failed}");
        WriteTimeout($"Таймаут: {timeout}");
        WriteSkipped($"Пропущено: {skipped}");
        WriteInfo($"Общее время: {totalTime.TotalMilliseconds:F0} мс");
    }
}
