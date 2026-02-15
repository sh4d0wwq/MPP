namespace TestFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class TestCaseAttribute : Attribute
{
    public object?[] Parameters { get; }

    public TestCaseAttribute(params object?[] parameters)
    {
        Parameters = parameters;
    }
}
