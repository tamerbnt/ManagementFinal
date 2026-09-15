using System.Text.Json.Serialization;

namespace Management.Domain.Enums
{
    /// <summary>
    /// Defines the facility / business operating archetype for a tenant workspace.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FacilityType
    {
        General = 0,
        Gym = 1,
        Pool = 2,
        Sauna = 3,
        Studio = 4,
        Salon = 5,
        Restaurant = 6,
        Admin = 99,
        LadiesOnly = 100,

        // ── Phase 2 — Six Business Operating Archetypes ──────────────────────
        /// <summary>Retail, Wholesale, Supermarkets, Food &amp; Beverage POS</summary>
        PosAndInventory = 10,
        /// <summary>Salons, Spas, Barbershops, Beauty Clinics</summary>
        AppointmentAndService = 11,
        /// <summary>Gyms, Fitness Studios, Martial Arts, Sports Clubs</summary>
        MembershipAndSession = 12,
        /// <summary>Architecture, Law Firms, Creative Agencies, Consultancies</summary>
        ProjectAndMilestone = 13,
        /// <summary>Coworking Spaces, Event Venues, Equipment Rental</summary>
        RentalAndBooking = 14,
        /// <summary>Training Centers, Academies, Bootcamps, Institutes</summary>
        EducationAndCohort = 15,
    }
}
