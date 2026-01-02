using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Members.Commands.CreateMember
{
    public class CreateMemberCommandHandler : IRequestHandler<CreateMemberCommand, Result<Guid>>
    {
        private readonly IMemberRepository _memberRepository;
        private readonly Domain.Services.ITenantService _tenantService;

        public CreateMemberCommandHandler(
            IMemberRepository memberRepository, 
            Domain.Services.ITenantService tenantService)
        {
            _memberRepository = memberRepository;
            _tenantService = tenantService;
        }

        public async Task<Result<Guid>> Handle(CreateMemberCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Member;

            var emailResult = Email.Create(dto.Email);
            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            
            if (emailResult.IsFailure) return Result.Failure<Guid>(emailResult.Error);
            if (phoneResult.IsFailure) return Result.Failure<Guid>(phoneResult.Error);

            var result = Member.Register(
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.CardId,
                dto.MembershipPlanId);

            if (result.IsFailure)
            {
                return Result.Failure<Guid>(result.Error);
            }

            var member = result.Value;
            
            var tenantId = _tenantService.GetTenantId();
            if (tenantId.HasValue)
            {
                member.TenantId = tenantId.Value;
            }

            if (!string.IsNullOrEmpty(dto.Notes)) 
            {
                member.UpdateDetails(
                    member.FullName, 
                    member.Email, 
                    member.PhoneNumber, 
                    member.CardId, 
                    dto.ProfileImageUrl, 
                    dto.Notes);
            }

            if (!string.IsNullOrEmpty(dto.EmergencyContactName) && !string.IsNullOrEmpty(dto.EmergencyContactPhone))
            {
                var emerPhone = PhoneNumber.Create(dto.EmergencyContactPhone);
                if (emerPhone.IsSuccess)
                {
                    member.UpdateEmergencyContact(dto.EmergencyContactName, emerPhone.Value);
                }
            }
            
            await _memberRepository.AddAsync(member);

            return Result.Success(member.Id);
        }
    }
}
