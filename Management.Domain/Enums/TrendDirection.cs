namespace Management.Domain.Enums
{
    /// <summary>
    /// Visual Logic: Determines the color and icon direction of financial indicators (Growth/Loss).
    /// </summary>
    public enum TrendDirection
    {
        Stable = 0, // Gray/Flat
        Up = 1,     // Green/Arrow Up
        Down = 2    // Red/Arrow Down
    }
}
