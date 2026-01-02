using System.Collections.Generic;

namespace Management.Domain.DTOs
{
    public record FacilitySettingsDto(
        int MaxOccupancy,
        bool IsMaintenanceMode,
        List<DayScheduleDto> Schedule,
        List<ZoneDto> Zones
    );
}