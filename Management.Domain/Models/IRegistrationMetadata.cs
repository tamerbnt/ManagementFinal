using System;

namespace Management.Domain.Models
{
    /// <summary>
    /// Base interface for facility-specific registration interest metadata.
    /// </summary>
    public interface IRegistrationMetadata
    {
        string SegmentType { get; }
    }

    /// <summary>
    /// Metadata for Gym facility leads.
    /// </summary>
    public record GymRegistrationMetadata : IRegistrationMetadata
    {
        public string SegmentType => "Gym";
        public Guid? PreferredPlanId { get; init; }
        public DateTime? PreferredStartDate { get; init; }
    }

    /// <summary>
    /// Metadata for Salon facility inquiries.
    /// </summary>
    public record SalonRegistrationMetadata : IRegistrationMetadata
    {
        public string SegmentType => "Salon";
        public string PreferredStylistId { get; init; } = string.Empty;
        public string ServiceInterests { get; init; } = string.Empty; // e.g. "Haircut, Coloring"
    }

    /// <summary>
    /// Metadata for Restaurant facility reservations/inquiries.
    /// </summary>
    public record RestaurantRegistrationMetadata : IRegistrationMetadata
    {
        public string SegmentType => "Restaurant";
        public int PartySize { get; init; }
        public bool HighChairRequired { get; init; }
        public string Occasion { get; init; } = string.Empty; // e.g. "Birthday"
    }

    /// <summary>
    /// Metadata for registrations originating from the public website.
    /// </summary>
    public record WebsiteRegistrationMetadata : IRegistrationMetadata
    {
        public string SegmentType => "Website";
        public string DesiredPlanText { get; init; } = string.Empty;
        public string FacilitySlug { get; init; } = string.Empty;
        public string Gender { get; init; } = string.Empty;
        public string WebsiteRequestId { get; init; } = string.Empty; // Supabase registration_requests.id
    }
}
