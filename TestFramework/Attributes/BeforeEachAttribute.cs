namespace TestFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class BeforeEachAttribute : Attribute
{
}
