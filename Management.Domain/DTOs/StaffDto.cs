using System;
using System.Collections.Generic;
using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class StaffDto
    {
        public Guid Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public StaffRole Role { get; set; }
        public DateTime HireDate { get; set; }
        public string Status { get; set; } // "Active", "On Leave"

        public List<PermissionDto> Permissions { get; set; } = new List<PermissionDto>();
    }
}