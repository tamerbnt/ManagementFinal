using System.Collections.Generic;

namespace Management.Domain.DTOs
{
    public class FacilitySettingsDto
    {
        public int MaxOccupancy { get; set; }
        public bool IsMaintenanceMode { get; set; }

        public List<DayScheduleDto> Schedule { get; set; } = new List<DayScheduleDto>();
        public List<ZoneDto> Zones { get; set; } = new List<ZoneDto>();
    }
}