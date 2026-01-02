using Management.Domain.Enums;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public record MemberSearchRequest(
        string SearchTerm,
        MemberFilterType FilterType = MemberFilterType.All
    );
}