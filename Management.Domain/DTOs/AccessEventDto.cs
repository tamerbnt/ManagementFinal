using System;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class AccessEventDto
    {
        public Guid Id { get; set; }
        public DateTime Timestamp { get; set; }


        public Guid? MemberId { get; set; }
        public string MemberName { get; set; }
        public string CardId { get; set; }

        public string FacilityName { get; set; } // Flattened from FacilityType
        public FacilityType FacilityType { get; set; }

        public string AccessStatus { get; set; } // "Granted", "Denied"
        public bool IsAccessGranted { get; set; } // For Green/Red logic
        public string FailureReason { get; set; } // Optional detail
    }
}