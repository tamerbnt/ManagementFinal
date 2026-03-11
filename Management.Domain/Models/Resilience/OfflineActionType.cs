namespace Management.Domain.Models.Resilience
{
    /// <summary>
    /// Defines the type of offline action to be performed.
    /// </summary>
    public enum OfflineActionType
    {
        Create,
        Update,
        Delete,
        Sync
    }
}
