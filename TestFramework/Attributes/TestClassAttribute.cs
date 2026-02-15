namespace TestFramework.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class TestClassAttribute : Attribute
{
    public int Priority { get; set; } = 0;
}
