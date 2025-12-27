namespace Management.Domain.Enums
{
    /// <summary>
    /// UI Logic: Defines the filtering criteria for the Members Grid.
    /// </summary>
    public enum MemberFilterType
    {
        All = 0,
        Active = 1,
        Expiring = 2, // Active + Expires within threshold
        Expired = 3
    }
}