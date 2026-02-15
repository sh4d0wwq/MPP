using TestFramework.Exceptions;

namespace TestFramework.Assertions;

public static class Assert
{
    public static void AreEqual<T>(T expected, T actual, string? message = null)
    {
        if (!Equals(expected, actual))
        {
            throw new AssertFailedException(
                message ?? $"Ожидалось: {expected}, но было: {actual}");
        }
    }

    public static void AreNotEqual<T>(T notExpected, T actual, string? message = null)
    {
        if (Equals(notExpected, actual))
        {
            throw new AssertFailedException(
                message ?? $"Значения не должны быть равны: {actual}");
        }
    }

    public static void IsTrue(bool condition, string? message = null)
    {
        if (!condition)
        {
            throw new AssertFailedException(
                message ?? "Ожидалось true, но было false");
        }
    }

    public static void IsFalse(bool condition, string? message = null)
    {
        if (condition)
        {
            throw new AssertFailedException(
                message ?? "Ожидалось false, но было true");
        }
    }

    public static void IsNull(object? obj, string? message = null)
    {
        if (obj != null)
        {
            throw new AssertFailedException(
                message ?? $"Ожидалось null, но было: {obj}");
        }
    }

    public static void IsNotNull(object? obj, string? message = null)
    {
        if (obj == null)
        {
            throw new AssertFailedException(
                message ?? "Ожидалось не-null значение");
        }
    }

    public static void IsInstanceOfType(object? obj, Type expectedType, string? message = null)
    {
        if (obj == null || !expectedType.IsInstanceOfType(obj))
        {
            throw new AssertFailedException(
                message ?? $"Ожидался тип {expectedType.Name}, но был {obj?.GetType().Name ?? "null"}");
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, T item, string? message = null)
    {
        if (!collection.Contains(item))
        {
            throw new AssertFailedException(
                message ?? $"Коллекция не содержит элемент: {item}");
        }
    }

    public static void Greater(int value, int other, string? message = null)
    {
        if (value <= other)
        {
            throw new AssertFailedException(
                message ?? $"Ожидалось {value} > {other}");
        }
    }

    public static void Less(int value, int other, string? message = null)
    {
        if (value >= other)
        {
            throw new AssertFailedException(
                message ?? $"Ожидалось {value} < {other}");
        }
    }

    public static TException ThrowsException<TException>(Action action, string? message = null) 
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertFailedException(
                message ?? $"Ожидалось исключение {typeof(TException).Name}, но было {ex.GetType().Name}");
        }

        throw new AssertFailedException(
            message ?? $"Ожидалось исключение {typeof(TException).Name}, но исключение не было выброшено");
    }

    public static async Task<TException> ThrowsExceptionAsync<TException>(Func<Task> action, string? message = null)
        where TException : Exception
    {
        try
        {
            await action();
        }
        catch (TException ex)
        {
            return ex;
        }
        catch (Exception ex)
        {
            throw new AssertFailedException(
                message ?? $"Ожидалось исключение {typeof(TException).Name}, но было {ex.GetType().Name}");
        }

        throw new AssertFailedException(
            message ?? $"Ожидалось исключение {typeof(TException).Name}, но исключение не было выброшено");
    }

    public static void Fail(string? message = null)
    {
        throw new AssertFailedException(message ?? "Тест провален");
    }
}
