namespace Ledger.Desktop.Models;

public enum AccountType
{
    Cash,
    Bank,
    Wallet
}

public enum CategoryKind
{
    Income,
    Expense
}

public enum TransactionType
{
    Income,
    Expense,
    TransferOut,
    TransferIn
}
