using System;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    public class PayrollEntry : Entity
    {
        public Guid StaffMemberId { get; set; }

        public DateTime PayDate { get; set; }
        public decimal Amount { get; set; }
        public decimal Bonus { get; set; }

        // e.g. "Monthly Salary", "Performance Bonus", "Adjustment"
        public string PaymentType { get; set; }

        // e.g. "Paid", "Pending", "Failed"
        public string Status { get; set; }

        public string TransactionReference { get; set; } // Bank trans ID
    }
}