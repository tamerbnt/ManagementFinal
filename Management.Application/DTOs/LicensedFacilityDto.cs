using System;

namespace Management.Application.DTOs
{
    public class LicensedFacilityDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Type { get; set; } // 0: Gym, 1: Salon, 2: Restaurant
    }
}
