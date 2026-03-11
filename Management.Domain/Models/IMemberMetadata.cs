using System;

namespace Management.Domain.Models
{
    /// <summary>
    /// Base interface for facility-specific member metadata.
    /// </summary>
    public interface IMemberMetadata
    {
        string SegmentType { get; }
    }

    /// <summary>
    /// Metadata for Gym facility members.
    /// </summary>
    public record GymMemberMetadata : IMemberMetadata
    {
        public string SegmentType => "Gym";
        public Guid? MembershipPlanId { get; init; }
        public string CardId { get; init; } = string.Empty;
        public int RemainingSessions { get; init; }
        public DateTime StartDate { get; init; }
        public DateTime ExpirationDate { get; init; }
    }

    /// <summary>
    /// Metadata for Salon facility clients.
    /// </summary>
    public record SalonMemberMetadata : IMemberMetadata
    {
        public string SegmentType => "Salon";
        public string PreferredStylistId { get; init; } = string.Empty;
        public DateTime? LastAppointmentDate { get; init; }
        public decimal TotalSpent { get; init; }
    }

    /// <summary>
    /// Metadata for Restaurant facility guests.
    /// </summary>
    public record RestaurantMemberMetadata : IMemberMetadata
    {
        public string SegmentType => "Restaurant";
        public int TypicalPartySize { get; init; }
        public string DietaryNotes { get; init; } = string.Empty;
        public bool IsVIP { get; init; }
    }
}
