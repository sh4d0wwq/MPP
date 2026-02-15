namespace TestFramework.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class ExpectedExceptionAttribute : Attribute
{
    public Type ExceptionType { get; }

    public ExpectedExceptionAttribute(Type exceptionType)
    {
        ExceptionType = exceptionType;
    }
}
