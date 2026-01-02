using System.Collections.Generic;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record FacilitySettingsDto(
        int MaxOccupancy,
        bool IsMaintenanceMode,
        List<DayScheduleDto> Schedule,
        List<ZoneDto> Zones
    );
}