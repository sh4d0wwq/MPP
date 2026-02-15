using SampleLibrary;
using TestFramework.Assertions;
using TestFramework.Attributes;

namespace SampleLibrary.Tests;

[TestClass(Priority = 1)]
public class BankAccountTests
{
    private BankAccount _account = null!;

    [BeforeEach]
    public void Setup()
    {
        _account = new BankAccount("Иван", 1000);
    }

    [AfterEach]
    public void Cleanup()
    {
        _account = null!;
    }

    [TestMethod(Priority = 1)]
    public void Constructor_ValidData_CreatesAccount()
    {
        var account = new BankAccount("Пётр", 500);
        Assert.AreEqual("Пётр", account.Owner);
        Assert.AreEqual(500, account.Balance);
    }

    [TestMethod(Priority = 2)]
    public void Deposit_ValidAmount_IncreasesBalance()
    {
        _account.Deposit(500);
        Assert.AreEqual(1500, _account.Balance);
    }

    [TestMethod]
    [TestCase(100)]
    [TestCase(200)]
    [TestCase(500)]
    public void Deposit_VariousAmounts_IncreasesBalance(int amount)
    {
        var initialBalance = _account.Balance;
        _account.Deposit(amount);
        Assert.AreEqual(initialBalance + amount, _account.Balance);
    }

    [TestMethod]
    [TestCase(100, 200, 300)]
    [TestCase(50, 50, 100)]
    [TestCase(0, 0, 0)]
    public void Add_TwoNumbers_ReturnsSum(int a, int b, int expected)
    {
        var result = a + b;
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Withdraw_ValidAmount_DecreasesBalance()
    {
        _account.Withdraw(300);
        Assert.AreEqual(700, _account.Balance);
    }

    [TestMethod]
    public void Balance_AfterOperations_IsCorrect()
    {
        _account.Deposit(200);
        _account.Withdraw(100);
        Assert.IsTrue(_account.Balance == 1100);
    }

    [TestMethod]
    public void Account_IsNotNull_AfterSetup()
    {
        Assert.IsNotNull(_account);
    }

    [TestMethod]
    public void Account_IsCorrectType()
    {
        Assert.IsInstanceOfType(_account, typeof(BankAccount));
    }

    [TestMethod]
    public void Balance_IsPositive_AfterDeposit()
    {
        _account.Deposit(100);
        Assert.Greater((int)_account.Balance, 0);
    }

    [TestMethod]
    public void Balance_IsLessThanInitial_AfterWithdraw()
    {
        _account.Withdraw(100);
        Assert.Less((int)_account.Balance, 1000);
    }

    [TestMethod]
    public void Owner_IsNotEmpty()
    {
        Assert.IsFalse(string.IsNullOrEmpty(_account.Owner));
    }

    [TestMethod]
    public void Transfer_MovesMoneyBetweenAccounts()
    {
        var target = new BankAccount("Мария", 0);
        _account.Transfer(target, 400);
        
        Assert.AreEqual(600, _account.Balance);
        Assert.AreEqual(400, target.Balance);
    }

    [TestMethod]
    public void MultipleAccounts_ContainsOwner()
    {
        var accounts = new List<string> { "Иван", "Пётр", "Мария" };
        Assert.Contains(accounts, _account.Owner);
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void Constructor_EmptyOwner_ThrowsException()
    {
        new BankAccount("");
    }

    [TestMethod]
    [ExpectedException(typeof(InvalidOperationException))]
    public void Withdraw_InsufficientFunds_ThrowsException()
    {
        _account.Withdraw(5000);
    }

    [TestMethod]
    public void Deposit_NegativeAmount_ThrowsException()
    {
        var ex = Assert.ThrowsException<ArgumentException>(() =>
        {
            _account.Deposit(-100);
        });
        Assert.IsNotNull(ex);
    }

    [TestMethod]
    public async Task GetBalanceAsync_ReturnsCorrectBalance()
    {
        var balance = await _account.GetBalanceAsync();
        Assert.AreEqual(1000, balance);
    }

    [TestMethod]
    public async Task TransferAsync_MovesMoneySuccessfully()
    {
        var target = new BankAccount("Анна", 0);
        var result = await _account.TransferAsync(target, 200);
        
        Assert.IsTrue(result);
        Assert.AreEqual(800, _account.Balance);
    }

    [TestMethod]
    public async Task TransferAsync_InsufficientFunds_ThrowsException()
    {
        var target = new BankAccount("Олег", 0);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
        {
            await _account.TransferAsync(target, 5000);
        });
    }

    [TestMethod]
    [Ignore("Демонстрация пропуска теста")]
    public void IgnoredTest_NotExecuted()
    {
        Assert.Fail("Этот тест не должен выполняться");
    }

    [TestMethod(Priority = 99)]
    public void FailedTest_WrongBalance()
    {
        _account.Deposit(100);
        Assert.AreEqual(9999, _account.Balance);
    }

    [TestMethod(Priority = 99)]
    public void FailedTest_ExplicitFail()
    {
        Assert.Fail("Демонстрация явного провала теста");
    }

    [TestMethod(Priority = 99)]
    public void FailedTest_ExpectedExceptionNotThrown()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            _account.Deposit(100);
        });
    }
}

[TestClass(Priority = 2)]
public class ParameterizedTests
{
    [TestMethod]
    [TestCase("Анна", 100)]
    [TestCase("Борис", 500)]
    [TestCase("Виктор", 0)]
    public void CreateAccount_WithParameters_Works(string owner, int balance)
    {
        var account = new BankAccount(owner, balance);
        Assert.AreEqual(owner, account.Owner);
        Assert.AreEqual(balance, account.Balance);
    }

    [TestMethod]
    [TestCase(10, 5, 2)]
    [TestCase(100, 25, 4)]
    [TestCase(9, 3, 3)]
    public void Divide_TwoNumbers_ReturnsQuotient(int a, int b, int expected)
    {
        Assert.AreEqual(expected, a / b);
    }
}

[TestClass]
[Ignore("Весь класс пропущен")]
public class IgnoredTestClass
{
    [TestMethod]
    public void Test_ShouldNotRun()
    {
        Assert.Fail("Этот тест не должен выполняться");
    }
}
