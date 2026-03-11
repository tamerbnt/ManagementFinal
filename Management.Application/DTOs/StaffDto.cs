using System;
using System.Collections.Generic;
using Management.Domain.Enums;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record StaffDto
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public StaffRole Role { get; set; }
        public DateTime HireDate { get; set; }
        public decimal Salary { get; set; }
        public int PaymentDay { get; set; }
        public bool IsOwner { get; set; }
        public string Status { get; set; } = "Active";
        public string? Password { get; set; } 
        public Guid? SupabaseUserId { get; set; }
        public List<string> AllowedModules { get; set; } = new();
        public List<PermissionDto> Permissions { get; set; } = new();
    }
}
