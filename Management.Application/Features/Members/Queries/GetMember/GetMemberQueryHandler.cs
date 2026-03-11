using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Members.Queries.GetMember
{
    public class GetMemberQueryHandler : IRequestHandler<GetMemberQuery, Result<MemberDto>>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly IMembershipPlanRepository _planRepository;
        private readonly IAccessEventRepository _accessEventRepository;

        public GetMemberQueryHandler(IMemberRepository memberRepository, IMembershipPlanRepository planRepository, IAccessEventRepository accessEventRepository)
        {
            _memberRepository = memberRepository;
            _planRepository = planRepository;
            _accessEventRepository = accessEventRepository;
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
            var plan = entity.MembershipPlanId.HasValue ? await _planRepository.GetByIdAsync(entity.MembershipPlanId.Value) : null;
            string planName = plan?.Name ?? "None";
            decimal planPrice = (decimal)(plan?.Price.Amount ?? 0);

            var visitCount = await _accessEventRepository.GetVisitCountAsync(entity.Id);
            var accessEvents = await _accessEventRepository.GetByMemberIdAsync(entity.Id);

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
                Balance = planPrice,
                VisitCount = visitCount,
                AccessEvents = accessEvents.Select(e => new AccessEventDto
                {
                    Timestamp = e.Timestamp,
                    IsAccessGranted = e.IsAccessGranted,
                    FailureReason = e.FailureReason ?? "Granted"
                }).ToList(),
                EmergencyContactName = entity.EmergencyContactName,
                EmergencyContactPhone = entity.EmergencyContactPhone?.Value ?? string.Empty,
                Notes = entity.Notes
            };
        }
    }
}
