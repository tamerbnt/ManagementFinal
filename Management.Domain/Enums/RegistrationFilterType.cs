namespace Management.Domain.Enums
{
    /// <summary>
    /// UI Logic: Defines the filtering criteria for the Registrations Inbox.
    /// </summary>
    public enum RegistrationFilterType
    {
        All = 0,
        New = 1,     // Created < 24h ago
        Priority = 2 // Specific sources (Walk-in/Referral)
    }
}
