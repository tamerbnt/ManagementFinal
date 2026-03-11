namespace Management.Application.DTOs
{
    public class LicenseCheckResult
    {
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }

        public static LicenseCheckResult Success() => new() { IsValid = true };
        public static LicenseCheckResult Failure(string message) => new() { IsValid = false, ErrorMessage = message };
    }
}
