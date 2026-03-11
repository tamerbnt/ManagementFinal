using System;
using Management.Domain.Enums;
using Management.Domain.Primitives;

namespace Management.Domain.Models
{
    public class Reservation : AggregateRoot, ITenantEntity, IFacilityEntity
    {
        public Guid TenantId { get; set; }
        public Guid FacilityId { get; set; }
        public Guid MemberId { get; private set; }
        public Guid? ResourceId { get; private set; } // e.g. Specific Court or PT
        public string ResourceType { get; private set; } // "Class", "VipArea"
        public Guid? ServiceId { get; private set; } // Salon Service ID
        
        public DateTime StartTime { get; private set; }
        public DateTime EndTime { get; private set; }
        public string Status { get; private set; } // "Confirmed", "Cancelled", "Completed"

        private Reservation(Guid id, Guid memberId, Guid? resourceId, string resourceType, DateTime startTime, DateTime endTime, Guid? serviceId = null) : base(id)
        {
            MemberId = memberId;
            ResourceId = resourceId;
            ResourceType = resourceType;
            StartTime = startTime;
            EndTime = endTime;
            ServiceId = serviceId;
            Status = "Confirmed";
        }

        private Reservation() 
        {
            ResourceType = default!;
            Status = default!;
        }

        public static Result<Reservation> Book(Guid memberId, Guid? resourceId, string resourceType, DateTime start, DateTime end, Guid? serviceId = null)
        {
            if (end <= start)
                return Result.Failure<Reservation>(new Error("Reservation.InvalidTime", "End time must be after start time"));

            return Result.Success(new Reservation(Guid.NewGuid(), memberId, resourceId, resourceType, start, end, serviceId));
        }

        public void Cancel()
        {
            Status = "Cancelled";
            UpdateTimestamp();
        }

        public void CheckIn()
        {
             if (Status == "Cancelled")
                throw new InvalidOperationException("Cannot check in to cancelled reservation");
            
            Status = "Completed";
            UpdateTimestamp();
        }
    }
}
