using System;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;

namespace Management.Domain.Models
{
    public class PayrollEntry : Entity, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }

        public Guid StaffId { get; private set; }
        public DateTime PayPeriodStart { get; private set; }
        public DateTime PayPeriodEnd { get; private set; }
        public Money Amount { get; private set; } = null!;
        public Money PaidAmount { get; private set; } = null!;
        
        // Hardened Fields (Senior Dev Review)
        public decimal BaseSalary { get; private set; }
        public int AbsenceCount { get; private set; }
        public decimal AbsenceDeduction { get; private set; }

        public bool IsPaid => PaidAmount.Amount >= Amount.Amount;

        private PayrollEntry(Guid id, Guid staffId, DateTime start, DateTime end, Money amount, decimal baseSalary, int absenceCount, decimal absenceDeduction) : base(id)
        {
            StaffId = staffId;
            PayPeriodStart = start;
            PayPeriodEnd = end;
            Amount = amount;
            PaidAmount = new Money(0, amount.Currency);
            BaseSalary = baseSalary;
            AbsenceCount = absenceCount;
            AbsenceDeduction = absenceDeduction;
        }

        private PayrollEntry() { }

        public static Result<PayrollEntry> Create(Guid staffId, DateTime start, DateTime end, Money amount, decimal baseSalary = 0, int absenceCount = 0, decimal absenceDeduction = 0)
        {
            return Result.Success(new PayrollEntry(Guid.NewGuid(), staffId, start, end, amount, baseSalary, absenceCount, absenceDeduction));
        }

        public Result Pay(Money payment)
        {
            if (payment.Currency != Amount.Currency)
                return Result.Failure(new Error("Payroll.CurrencyMismatch", "Currency mismatch"));

            PaidAmount = new Money(PaidAmount.Amount + payment.Amount, PaidAmount.Currency);
            UpdateTimestamp();
            return Result.Success();
        }

        public void MarkAsPaid()
        {
            PaidAmount = Amount;
            UpdateTimestamp();
        }
    }
}
