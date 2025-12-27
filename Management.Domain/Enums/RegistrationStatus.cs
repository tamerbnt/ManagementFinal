namespace Management.Domain.Enums
{
    /// <summary>
    /// Represents the lifecycle state of a potential member lead.
    /// </summary>
    public enum RegistrationStatus
    {
        Unknown = 0,
        Pending = 1,
        Approved = 2,
        Declined = 3
    }
}