namespace Management.Application.DTOs
{
    public record DayScheduleDto(string Day, string Open, string Close, bool IsActive);
}