namespace Management.Domain.Enums
{
    /// <summary>
    /// Defines how a transaction was settled.
    /// </summary>
    public enum PaymentMethod
    {
        Unknown = 0,
        Cash = 1,
        CreditCard = 2,
        BankTransfer = 3,
        Account = 4, // Charged to member balance
        Mixed = 5
    }
}
