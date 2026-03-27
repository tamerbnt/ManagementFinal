using System;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record PayrollEntryDto
    {
        public Guid Id { get; set; }
        public Guid StaffId { get; set; }
        public string StaffName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime PayPeriodStart { get; set; }
        public DateTime PayPeriodEnd { get; set; }
        public bool IsPaid { get; set; }
        public decimal NetPay { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime? ProcessedAt { get; set; }
        public string Notes { get; set; } = string.Empty;
        
        // Hardening fields
        public decimal BaseSalary { get; set; }
        public int AbsenceCount { get; set; }
        public decimal AbsenceDeduction { get; set; }
    }
}
