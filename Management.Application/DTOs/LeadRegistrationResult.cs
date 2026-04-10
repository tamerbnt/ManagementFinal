using System;

namespace Management.Application.DTOs
{
    public class LeadRegistrationResult
    {
        public bool Success { get; set; }
        public Guid MemberId { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public static LeadRegistrationResult Failed(string message) => new() { Success = false, ErrorMessage = message };
        public static LeadRegistrationResult Succeeded(Guid memberId) => new() { Success = true, MemberId = memberId };
    }
}
