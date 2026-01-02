using Management.Domain.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Members.Queries.GetMember
{
    public class GetMemberQueryHandler : IRequestHandler<GetMemberQuery, Result<MemberDto>>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMembershipPlanRepository _planRepository;

        public GetMemberQueryHandler(IMemberRepository memberRepository, IMembershipPlanRepository planRepository)
        {
            _memberRepository = memberRepository;
            _planRepository = planRepository;
        }

        public async Task<Result<MemberDto>> Handle(GetMemberQuery request, CancellationToken cancellationToken)
        {
            var entity = await _memberRepository.GetByIdAsync(request.Id);
            if (entity == null)
            {
                return Result.Failure<MemberDto>(new Error("Member.NotFound", $"Member with ID {request.Id} was not found."));
            }

            return Result.Success(await MapToDto(entity));
        }

        private async Task<MemberDto> MapToDto(Member entity)
        {
            string planName = "None";
            if (entity.MembershipPlanId.HasValue)
            {
                // Note: In N+1 scenarios this is bad, but for single item get it's fine.
                // In Search, we should prefer a Repo method that includes the plan or does a join.
                var plan = await _planRepository.GetByIdAsync(entity.MembershipPlanId.Value);
                if (plan != null) planName = plan.Name;
            }

            return new MemberDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email.Value,
                PhoneNumber = entity.PhoneNumber.Value,
                CardId = entity.CardId,
                Status = entity.Status,
                StartDate = entity.StartDate,
                ExpirationDate = entity.ExpirationDate,
                ProfileImageUrl = entity.ProfileImageUrl,
                MembershipPlanName = planName,
                MembershipPlanId = entity.MembershipPlanId,
                EmergencyContactName = entity.EmergencyContactName,
                EmergencyContactPhone = entity.EmergencyContactPhone?.Value ?? string.Empty,
                Notes = entity.Notes
            };
        }
    }
}
