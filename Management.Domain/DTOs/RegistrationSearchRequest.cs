using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class RegistrationSearchRequest
    {
        public string SearchTerm { get; set; }
        public RegistrationFilterType FilterType { get; set; } = RegistrationFilterType.All;
    }
}