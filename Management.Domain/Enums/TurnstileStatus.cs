namespace Management.Domain.Enums
{
    /// <summary>
    /// Represents the physical operational state of a hardware entry point.
    /// </summary>
    public enum TurnstileStatus
    {
        Unknown = 0,
        Operational = 1,
        Maintenance = 2,
        Locked = 3,
        OutOfOrder = 4
    }
}