using System.Reflection;

namespace TestFramework.Runner;

public sealed class TestMetadata
{
    public Type TestClass { get; init; } = null!;
    public string ClassName { get; init; } = "";
    public MethodInfo Method { get; init; } = null!;
    public string MethodName { get; init; } = "";
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public string? Author { get; init; }
    public int ClassPriority { get; init; }
    public int MethodPriority { get; init; }
}
