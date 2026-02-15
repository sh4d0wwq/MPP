using System.Reflection;
using TestFramework.Attributes;
using TestFramework.Exceptions;

namespace TestFramework.Runner;

public enum TestResult
{
    Passed,
    Failed,
    Skipped
}

public class TestResultInfo
{
    public string ClassName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public TestResult Result { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan Duration { get; set; }
}

public class TestRunner
{
    private readonly List<TestResultInfo> _results = new();
    private readonly TextWriter _output;

    public TestRunner(TextWriter? output = null)
    {
        _output = output ?? Console.Out;
    }

    public async Task<List<TestResultInfo>> RunTestsAsync(Assembly assembly)
    {
        _results.Clear();
        var testClasses = DiscoverTestClasses(assembly);

        _output.WriteLine("=== Запуск тестов ===\n");

        foreach (var testClass in testClasses)
        {
            await RunTestClassAsync(testClass);
        }

        PrintSummary();
        return _results;
    }

    public async Task<List<TestResultInfo>> RunTestsFromFileAsync(string assemblyPath)
    {
        var assembly = Assembly.LoadFrom(assemblyPath);
        return await RunTestsAsync(assembly);
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
            _output.WriteLine($"[ПРОПУЩЕН] Класс {testClass.Name}: {ignoreAttr.Reason ?? "без причины"}");
            return;
        }

        var classAttr = testClass.GetCustomAttribute<TestClassAttribute>()!;
        _output.WriteLine($"--- {testClass.Name} (приоритет: {classAttr.Priority}) ---");

        var instance = Activator.CreateInstance(testClass);
        var beforeEach = GetMethodWithAttribute<BeforeEachAttribute>(testClass);
        var afterEach = GetMethodWithAttribute<AfterEachAttribute>(testClass);
        var testMethods = GetTestMethods(testClass);

        foreach (var method in testMethods)
        {
            var testCases = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
            
            if (testCases.Length > 0)
            {
                foreach (var testCase in testCases)
                {
                    await RunTestMethodAsync(instance!, method, beforeEach, afterEach, testCase.Parameters);
                }
            }
            else
            {
                await RunTestMethodAsync(instance!, method, beforeEach, afterEach, null);
            }
        }

        _output.WriteLine();
    }

    private async Task RunTestMethodAsync(object instance, MethodInfo method, 
        MethodInfo? beforeEach, MethodInfo? afterEach, object?[]? parameters)
    {
        var methodAttr = method.GetCustomAttribute<TestMethodAttribute>()!;
        var displayName = parameters != null 
            ? $"{method.Name}({string.Join(", ", parameters)})" 
            : method.Name;

        var result = new TestResultInfo
        {
            ClassName = instance.GetType().Name,
            MethodName = displayName
        };

        var ignoreAttr = method.GetCustomAttribute<IgnoreAttribute>();
        if (ignoreAttr != null)
        {
            result.Result = TestResult.Skipped;
            result.ErrorMessage = ignoreAttr.Reason ?? "Тест пропущен";
            _output.WriteLine($"  [ПРОПУЩЕН] {displayName}: {result.ErrorMessage}");
            _results.Add(result);
            return;
        }

        var expectedExceptionAttr = method.GetCustomAttribute<ExpectedExceptionAttribute>();
        var startTime = DateTime.Now;

        try
        {
            if (beforeEach != null)
            {
                await InvokeMethodAsync(beforeEach, instance, null);
            }

            await InvokeMethodAsync(method, instance, parameters);

            if (expectedExceptionAttr != null)
            {
                throw new AssertFailedException(
                    $"Ожидалось исключение {expectedExceptionAttr.ExceptionType.Name}");
            }

            result.Result = TestResult.Passed;
            _output.WriteLine($"  [OK] {displayName}");
        }
        catch (Exception ex)
        {
            var actualException = ex.InnerException ?? ex;

            if (expectedExceptionAttr != null && 
                expectedExceptionAttr.ExceptionType.IsInstanceOfType(actualException))
            {
                result.Result = TestResult.Passed;
                _output.WriteLine($"  [OK] {displayName} (исключение {actualException.GetType().Name})");
            }
            else if (actualException is TestSkippedException skipEx)
            {
                result.Result = TestResult.Skipped;
                result.ErrorMessage = skipEx.Message;
                _output.WriteLine($"  [ПРОПУЩЕН] {displayName}: {skipEx.Message}");
            }
            else
            {
                result.Result = TestResult.Failed;
                result.ErrorMessage = actualException.Message;
                _output.WriteLine($"  [ПРОВАЛЕН] {displayName}: {actualException.Message}");
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
                _output.WriteLine($"    Ошибка в AfterEach: {ex.Message}");
            }

            result.Duration = DateTime.Now - startTime;
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

    private void PrintSummary()
    {
        var passed = _results.Count(r => r.Result == TestResult.Passed);
        var failed = _results.Count(r => r.Result == TestResult.Failed);
        var skipped = _results.Count(r => r.Result == TestResult.Skipped);

        _output.WriteLine("=== Результаты ===");
        _output.WriteLine($"Всего: {_results.Count}");
        _output.WriteLine($"Успешно: {passed}");
        _output.WriteLine($"Провалено: {failed}");
        _output.WriteLine($"Пропущено: {skipped}");
    }
}
