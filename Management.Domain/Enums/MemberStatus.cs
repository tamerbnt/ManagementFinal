namespace Management.Domain.Enums
{
    /// <summary>
    /// Represents the current standing of a member's contract.
    /// </summary>
    public enum MemberStatus
    {
        Unknown = 0,
        Active = 1,
        Expired = 2,
        Frozen = 3,
        Suspended = 4,
        Pending = 5
    }
}