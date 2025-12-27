using System;

namespace Management.Domain.DTOs
{
    public class FailedPaymentDto
    {
        public Guid Id { get; set; }
        public string MemberName { get; set; }
        public decimal Amount { get; set; }
        public string Reason { get; set; } // e.g. "Insufficient Funds"

        // FIX: Renamed 'Date' to 'AttemptDate' to match the Service code
        public DateTime AttemptDate { get; set; }
    }
}