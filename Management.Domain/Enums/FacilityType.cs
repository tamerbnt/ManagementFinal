namespace Management.Domain.Enums
{
    /// <summary>
    /// Defines specific physical zones within the gym for access logging.
    /// </summary>
    public enum FacilityType
    {
        General = 0,
        Gym = 1,
        Pool = 2,
        Sauna = 3,
        Studio = 4,
        StaffOnly = 99
    }
}