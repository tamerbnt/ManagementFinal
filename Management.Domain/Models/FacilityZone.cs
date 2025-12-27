using System;

namespace Management.Domain.Models
{
    /// <summary>
    /// Defined physical areas within the gym.
    /// </summary>
    public class FacilityZone : Entity
    {
        public string Name { get; set; } // "Cardio Area", "Pool"
        public int Capacity { get; set; }
    }
}