namespace Management.Domain.Enums
{
    /// <summary>
    /// Defines permission levels and job titles for staff members.
    /// </summary>
    public enum StaffRole
    {
        None = 0,
        Admin = 1,      // Full Access
        Manager = 2,    // Can edit settings/finance
        Trainer = 3,    // Can view members/schedule
        Reception = 4   // Can check-in/sell items
    }
}