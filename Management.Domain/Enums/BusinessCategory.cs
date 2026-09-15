namespace Management.Domain.Enums
{
    /// <summary>
    /// The 6 operational business archetypes governed inside the C# desktop codebase.
    /// </summary>
    public enum BusinessCategory
    {
        PosInventory,        // 1. POS & Order/Inventory-based (Retail, Cafe, Restaurant, Pharmacy)
        AppointmentService,  // 2. Appointment & Service-based (Salons, Spas, Clinics, Barbers)
        MembershipSession,   // 3. Membership & Session-based (Gyms, Fitness, Studios, Dojos)
        ProjectMilestone,    // 4. Project & Milestone-based (Architecture, Engineering, Law, Agencies)
        RentalBooking,       // 5. Rental & Space Booking-based (Coworking, Venues, Equipment)
        EducationCohort      // 6. Education & Cohort-based (Schools, Institutes, Bootcamps)
    }
}
