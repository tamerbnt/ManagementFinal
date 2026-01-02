using Management.Domain.Interfaces;
using Management.Domain.Primitives;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Members.Queries.GetMemberMetrics
{
    public class GetMemberMetricsQueryHandler : 
        IRequestHandler<GetActiveMemberCountQuery, Result<int>>,
        IRequestHandler<GetExpiringMemberCountQuery, Result<int>>
    {
        private readonly IMemberRepository _memberRepository;

        public GetMemberMetricsQueryHandler(IMemberRepository memberRepository)
        {
            _memberRepository = memberRepository;
        }

        public async Task<Result<int>> Handle(GetActiveMemberCountQuery request, CancellationToken cancellationToken)
        {
            var count = await _memberRepository.GetActiveCountAsync();
            return Result.Success(count);
        }

        public async Task<Result<int>> Handle(GetExpiringMemberCountQuery request, CancellationToken cancellationToken)
        {
            var threshold = DateTime.UtcNow.AddDays(7);
            var count = await _memberRepository.GetExpiringCountAsync(threshold);
            return Result.Success(count);
        }
    }
}
