namespace SampleLibrary;

public class BankAccount
{
    public string Owner { get; }
    public decimal Balance { get; private set; }

    public BankAccount(string owner, decimal initialBalance = 0)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("Имя владельца не может быть пустым");
        if (initialBalance < 0)
            throw new ArgumentException("Начальный баланс не может быть отрицательным");

        Owner = owner;
        Balance = initialBalance;
    }

    public void Deposit(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Сумма пополнения должна быть положительной");

        Balance += amount;
    }

    public void Withdraw(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("Сумма снятия должна быть положительной");
        if (amount > Balance)
            throw new InvalidOperationException("Недостаточно средств на счёте");

        Balance -= amount;
    }

    public void Transfer(BankAccount target, decimal amount)
    {
        if (target == null)
            throw new ArgumentNullException(nameof(target));
        if (target == this)
            throw new InvalidOperationException("Нельзя перевести на тот же счёт");

        Withdraw(amount);
        target.Deposit(amount);
    }

    public async Task<bool> TransferAsync(BankAccount target, decimal amount)
    {
        await Task.Delay(10);
        Transfer(target, amount);
        return true;
    }

    public async Task<decimal> GetBalanceAsync()
    {
        await Task.Delay(10);
        return Balance;
    }
}
