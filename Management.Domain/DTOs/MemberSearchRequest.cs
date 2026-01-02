using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public record MemberSearchRequest(
        string SearchTerm,
        MemberFilterType FilterType = MemberFilterType.All
    );
}