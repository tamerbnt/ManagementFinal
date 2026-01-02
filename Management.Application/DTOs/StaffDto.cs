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
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public StaffRole Role { get; set; }
        public DateTime HireDate { get; set; }
        public string Status { get; set; } = "Active";
        public List<PermissionDto> Permissions { get; set; } = new();
    }
}