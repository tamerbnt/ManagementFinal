using Management.Domain.Enums;

namespace Management.Domain.DTOs
{
    public class MemberSearchRequest
    {
        public string SearchTerm { get; set; }
        public MemberFilterType FilterType { get; set; } = MemberFilterType.All;
    }
}