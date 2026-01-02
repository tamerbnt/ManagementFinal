using System;

namespace Management.Domain.DTOs
{
    public record ReservationDto(
        Guid Id,
        string ActivityName,
        string InstructorName,
        string Location,
        DateTime StartTime,
        DateTime EndTime
    );
}