namespace TestFramework.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public class AfterEachAttribute : Attribute
{
}
