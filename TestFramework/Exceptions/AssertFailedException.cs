namespace TestFramework.Exceptions;

public class AssertFailedException : Exception
{
    public AssertFailedException(string message) : base(message)
    {
    }
}
