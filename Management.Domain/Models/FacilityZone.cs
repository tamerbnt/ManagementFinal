using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class FacilityZone : Entity
    {
        public string Name { get; private set; }
        public int Capacity { get; private set; }
        public FacilityType Type { get; private set; }
        public bool IsOperational { get; private set; }

        private FacilityZone(Guid id, string name, int capacity, FacilityType type) : base(id)
        {
            Name = name;
            Capacity = capacity;
            Type = type;
            IsOperational = true;
        }
        
        private FacilityZone() 
        {
            Name = default!;
        }

        public static Result<FacilityZone> Create(string name, int capacity, FacilityType type)
        {
             if (capacity < 0)
                return Result.Failure<FacilityZone>(new Error("Zone.InvalidCapacity", "Capacity cannot be negative"));

            return Result.Success(new FacilityZone(Guid.NewGuid(), name, capacity, type));
        }
    }
}