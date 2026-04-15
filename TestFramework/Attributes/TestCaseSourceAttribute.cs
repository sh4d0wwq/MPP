namespace TestFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class TestCaseSourceAttribute : Attribute
{
    public string MemberName { get; }
    public Type? SourceType { get; }

    public TestCaseSourceAttribute(string memberName)
    {
        MemberName = memberName;
    }

    public TestCaseSourceAttribute(Type sourceType, string memberName)
    {
        SourceType = sourceType;
        MemberName = memberName;
    }
}
