namespace TestFramework.Exceptions;

public class TestSkippedException : Exception
{
    public TestSkippedException(string reason) : base(reason)
    {
    }
}
