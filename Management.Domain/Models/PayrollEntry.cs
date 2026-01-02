using System;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class PayrollEntry : Entity
    {
        public Guid StaffMemberId { get; private set; }
        public DateTime PayPeriodStart { get; private set; }
        public DateTime PayPeriodEnd { get; private set; }
        public Money Amount { get; private set; } = null!;
        public bool IsPaid { get; private set; }

        private PayrollEntry(Guid id, Guid staffId, DateTime start, DateTime end, Money amount) : base(id)
        {
            StaffMemberId = staffId;
            PayPeriodStart = start;
            PayPeriodEnd = end;
            Amount = amount;
            IsPaid = false;
        }

        private PayrollEntry() { }

        public static Result<PayrollEntry> Create(Guid staffId, DateTime start, DateTime end, Money amount)
        {
            return Result.Success(new PayrollEntry(Guid.NewGuid(), staffId, start, end, amount));
        }

        public void MarkAsPaid()
        {
            IsPaid = true;
            UpdateTimestamp();
        }
    }
}