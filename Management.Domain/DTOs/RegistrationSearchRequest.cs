using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public record RegistrationSearchRequest(
        string SearchTerm,
        RegistrationFilterType FilterType = RegistrationFilterType.All
    );
}