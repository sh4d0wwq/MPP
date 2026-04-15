using System.Linq.Expressions;
using System.Text;
using TestFramework.Exceptions;

namespace TestFramework.Assertions;

public static partial class Assert
{
    public static void That(Expression<Func<bool>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var compiled = expression.Compile();
        if (compiled())
            return;

        var message = ExpressionFailureFormatter.Format(expression);
        throw new AssertFailedException(message);
    }
}

internal static class ExpressionFailureFormatter
{
    public static string Format(Expression<Func<bool>> expression)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Условие ложно (разбор дерева выражений):");
        sb.AppendLine($"  Выражение: {expression}");
        sb.AppendLine("  ---");
        AppendNode(sb, expression.Body, 2);
        return sb.ToString().TrimEnd();
    }

    private static void AppendNode(StringBuilder sb, Expression node, int indent)
    {
        var pad = new string(' ', indent);

        switch (node)
        {
            case BinaryExpression binary:
                sb.AppendLine($"{pad}Оператор: {GetOperatorSymbol(binary.NodeType)} ({binary.NodeType})");
                sb.AppendLine($"{pad}Левый операнд:");
                AppendOperand(sb, binary.Left, indent + 2);
                sb.AppendLine($"{pad}Правый операнд:");
                AppendOperand(sb, binary.Right, indent + 2);
                break;

            case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                sb.AppendLine($"{pad}Оператор: NOT");
                sb.AppendLine($"{pad}Операнд:");
                AppendOperand(sb, unary.Operand, indent + 2);
                break;

            case MethodCallExpression call:
                sb.AppendLine($"{pad}Вызов метода: {call.Method.DeclaringType?.Name}.{call.Method.Name}");
                for (var i = 0; i < call.Arguments.Count; i++)
                {
                    sb.AppendLine($"{pad}  Аргумент[{i}]: {FormatValue(call.Arguments[i])}");
                }
                break;

            default:
                sb.AppendLine($"{pad}Узел: {node.NodeType}");
                sb.AppendLine($"{pad}Значение: {FormatValue(node)}");
                break;
        }
    }

    private static void AppendOperand(StringBuilder sb, Expression expr, int indent)
    {
        var pad = new string(' ', indent);
        sb.AppendLine($"{pad}Представление: {expr}");
        sb.AppendLine($"{pad}Значение: {FormatValue(expr)}");
    }

    private static string FormatValue(Expression expr)
    {
        try
        {
            if (expr is ConstantExpression c)
                return FormatObject(c.Value);

            var converted = Expression.Convert(expr, typeof(object));
            var lambda = Expression.Lambda<Func<object>>(converted);
            var fn = lambda.Compile();
            return FormatObject(fn());
        }
        catch (Exception ex)
        {
            return $"<не удалось вычислить: {ex.Message}>";
        }
    }

    private static string FormatObject(object? value)
    {
        if (value == null) return "null";
        if (value is string s) return $"\"{s}\"";
        return value.ToString() ?? "null";
    }

    private static string GetOperatorSymbol(ExpressionType type) => type switch
    {
        ExpressionType.Equal => "==",
        ExpressionType.NotEqual => "!=",
        ExpressionType.GreaterThan => ">",
        ExpressionType.LessThan => "<",
        ExpressionType.GreaterThanOrEqual => ">=",
        ExpressionType.LessThanOrEqual => "<=",
        ExpressionType.AndAlso => "&&",
        ExpressionType.OrElse => "||",
        ExpressionType.Add => "+",
        ExpressionType.Subtract => "-",
        ExpressionType.Multiply => "*",
        ExpressionType.Divide => "/",
        _ => type.ToString()
    };
}
