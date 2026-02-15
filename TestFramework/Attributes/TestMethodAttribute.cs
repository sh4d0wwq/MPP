namespace TestFramework.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class TestMethodAttribute : Attribute
{
    public int Priority { get; set; } = 0;
}
