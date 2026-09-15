namespace Management.Domain.Enums
{
    /// <summary>
    /// Defines permission levels and job titles for staff members.
    /// </summary>
    public enum StaffRole
    {
        None = 0,
        Manager = 1,
        Cashier = 2,
        Technician = 3,
        Waiter = 4,
        Staff = 7,   // Preserved legacy value
        Owner = 8    // Preserved legacy value
    }
}
