using System.Text.Json.Serialization;

namespace Management.Domain.Enums
{
    /// <summary>
    /// Defines specific physical zones within the gym for access logging.
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FacilityType
    {
        General = 0,
        Gym = 1,
        Pool = 2,
        Sauna = 3,
        Studio = 4,
        Salon = 5,
        Restaurant = 6,
        Admin = 99,
        LadiesOnly = 100
    }
}
