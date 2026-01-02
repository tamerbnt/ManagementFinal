using Management.Application.DTOs;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Queries.GetRegistration
{
    public class GetRegistrationQueryHandler : IRequestHandler<GetRegistrationQuery, Result<RegistrationDto>>
    {
        private readonly IRepository<Registration> _registrationRepository;

        public GetRegistrationQueryHandler(IRepository<Registration> registrationRepository)
        {
            _registrationRepository = registrationRepository;
        }

        public async Task<Result<RegistrationDto>> Handle(GetRegistrationQuery request, CancellationToken cancellationToken)
        {
            var entity = await _registrationRepository.GetByIdAsync(request.RegistrationId);
            
            if (entity == null)
            {
                return Result.Failure<RegistrationDto>(new Error("Registration.NotFound", $"Registration with ID {request.RegistrationId} was not found."));
            }

            var dto = new RegistrationDto
            {
                Id = entity.Id,
                FullName = entity.FullName,
                Email = entity.Email.Value,
                PhoneNumber = entity.PhoneNumber.Value,
                PreferredPlanId = entity.PreferredPlanId,
                Status = entity.Status,
                CreatedAt = entity.CreatedAt,
                Source = entity.Source,
                Notes = entity.Notes,
                PreferredStartDate = entity.PreferredStartDate
            };

            return Result.Success(dto);
        }
    }
}
