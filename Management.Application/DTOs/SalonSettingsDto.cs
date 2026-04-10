namespace Management.Application.DTOs
{
    public record SalonSettingsDto(
        int TotalChairs,
        decimal DailyRevenueTarget,
        string OperatingHoursJson
    );
}
