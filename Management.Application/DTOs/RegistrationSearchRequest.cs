using Management.Domain.Enums;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record RegistrationSearchRequest(
        string SearchTerm,
        RegistrationFilterType FilterType = RegistrationFilterType.All
    );
}