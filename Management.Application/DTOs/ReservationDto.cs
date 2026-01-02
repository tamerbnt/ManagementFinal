using System;
using Management.Application.DTOs;

namespace Management.Application.DTOs
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