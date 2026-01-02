using Management.Application.Stores;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Members.Commands.UpdateMember
{
    public class UpdateMemberCommandHandler : IRequestHandler<UpdateMemberCommand, Result>
    {
        private readonly IMemberRepository _memberRepository;

        public UpdateMemberCommandHandler(IMemberRepository memberRepository)
        {
            _memberRepository = memberRepository;
        }

        public async Task<Result> Handle(UpdateMemberCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Member;
            var member = await _memberRepository.GetByIdAsync(dto.Id);

            if (member == null)
            {
                return Result.Failure(new Error("Member.NotFound", $"Member with ID {dto.Id} was not found."));
            }

            var emailResult = Email.Create(dto.Email);
            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            
            if (emailResult.IsFailure) return Result.Failure(emailResult.Error);
            if (phoneResult.IsFailure) return Result.Failure(phoneResult.Error);

            member.UpdateDetails(
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.CardId,
                dto.ProfileImageUrl,
                dto.Notes);

            if (!string.IsNullOrEmpty(dto.EmergencyContactName) && !string.IsNullOrEmpty(dto.EmergencyContactPhone))
            {
                 var emerPhone = PhoneNumber.Create(dto.EmergencyContactPhone);
                 if (emerPhone.IsSuccess)
                 {
                    member.UpdateEmergencyContact(
                        dto.EmergencyContactName,
                        emerPhone.Value);
                 }
            }

            await _memberRepository.UpdateAsync(member);

            return Result.Success();
        }
    }
}
