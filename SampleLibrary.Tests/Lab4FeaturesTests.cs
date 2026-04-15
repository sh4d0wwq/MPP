using SampleLibrary;
using TestFramework.Assertions;
using TestFramework.Attributes;

namespace SampleLibrary.Tests;

[TestClass(Priority = 4)]
[Category("Lab4")]
[Author("Демо ЛР4")]
public class Lab4FeaturesTests
{
    public static IEnumerable<object[]> DepositCasesFromIterator()
    {
        yield return new object[] { 50, 1050 };
        yield return new object[] { 100, 1100 };
        yield return new object[] { 250, 1250 };
    }

    public static IEnumerable<object> SingleValueCases()
    {
        yield return "alpha";
        yield return "beta";
        yield return "gamma";
    }

    [TestMethod]
    [Category("ParameterSource")]
    [TestCaseSource(nameof(DepositCasesFromIterator))]
    public void Deposit_FromYieldIterator_UpdatesBalance(int amount, int expectedBalance)
    {
        var account = new BankAccount("Тест", 1000);
        account.Deposit(amount);
        Assert.AreEqual(expectedBalance, account.Balance);
    }

    [TestMethod]
    [Category("ParameterSource")]
    [TestCaseSource(nameof(SingleValueCases))]
    public void OwnerName_LengthGreaterThanZero(string suffix)
    {
        var account = new BankAccount(suffix, 0);
        Assert.IsTrue(account.Owner.Length > 0);
    }

    [TestMethod]
    [Category("ExpressionAssert")]
    public void ExpressionAssert_PassesWhenExpressionTrue()
    {
        var a = 7;
        var b = 3;
        Assert.That(() => a + b == 10);
    }

    [TestMethod]
    [Category("FilteringDemo")]
    [Author("Иванов")]
    public void OnlyForAuthorFilter_Demo()
    {
        Assert.IsTrue(true);
    }

    [TestMethod]
    [Category("FilteringDemo")]
    [Author("Петров")]
    public void AnotherAuthor_Demo()
    {
        Assert.IsTrue(true);
    }
}
