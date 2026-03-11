using Management.Domain.Primitives;

namespace Management.Domain.ValueObjects
{
    public record Money(decimal Amount, string Currency)
    {
        public static Money Zero(string currency = "DA") => new(0m, currency);
        
        public static Money operator +(Money a, Money b)
        {
            if (a.Currency != b.Currency)
                throw new System.InvalidOperationException("Currencies do not match");

            return new Money(a.Amount + b.Amount, a.Currency);
        }
    }
}
