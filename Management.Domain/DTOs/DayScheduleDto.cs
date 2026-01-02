namespace Management.Domain.DTOs
{
    public record DayScheduleDto(string Day, string Open, string Close, bool IsActive);
}