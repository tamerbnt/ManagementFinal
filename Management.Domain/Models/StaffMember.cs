using System;
using Management.Domain.Enums;

namespace Management.Domain.Models
{
    public class StaffMember : Entity
    {
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }

        // Authorization
        public StaffRole Role { get; set; }
        public string PinCode { get; set; } // Hashed/Encrypted in production

        // Employment Status
        public DateTime HireDate { get; set; }
        public bool IsActive { get; set; }
    }
}