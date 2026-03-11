using Management.Application.DTOs;
using Management.Domain.Services;
using Management.Domain.Interfaces;
using Management.Domain.Models;
using Management.Domain.Primitives;
using Management.Domain.ValueObjects;
using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Features.Registrations.Commands.SubmitRegistration
{
    public class SubmitRegistrationCommandHandler : IRequestHandler<SubmitRegistrationCommand, Result<Guid>>
    {
        private readonly IRegistrationRepository _registrationRepository;
        private readonly IFacilityContextService _facilityContext;

        public SubmitRegistrationCommandHandler(
            IRegistrationRepository registrationRepository,
            IFacilityContextService facilityContext)
        {
            _registrationRepository = registrationRepository;
            _facilityContext = facilityContext;
        }

        public async Task<Result<Guid>> Handle(SubmitRegistrationCommand request, CancellationToken cancellationToken)
        {
            var dto = request.Registration;

            var emailResult = Email.Create(dto.Email);
            if (emailResult.IsFailure) return Result.Failure<Guid>(emailResult.Error);

            var phoneResult = PhoneNumber.Create(dto.PhoneNumber);
            if (phoneResult.IsFailure) return Result.Failure<Guid>(phoneResult.Error);

            // Polymorphic Metadata Deserialization
            IRegistrationMetadata? metadata = null;
            if (!string.IsNullOrWhiteSpace(dto.MetadataJson))
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(dto.MetadataJson);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("SegmentType", out var typeProp))
                    {
                        var type = typeProp.GetString();
                        metadata = type switch
                        {
                            "Gym" => System.Text.Json.JsonSerializer.Deserialize<GymRegistrationMetadata>(dto.MetadataJson),
                            "Salon" => System.Text.Json.JsonSerializer.Deserialize<SalonRegistrationMetadata>(dto.MetadataJson),
                            "Restaurant" => System.Text.Json.JsonSerializer.Deserialize<RestaurantRegistrationMetadata>(dto.MetadataJson),
                            _ => null
                        };
                    }
                }
                catch { /* Fallback to legacy fields if JSON is malformed */ }
            }

            var registrationResult = Registration.Submit(
                dto.FullName,
                emailResult.Value,
                phoneResult.Value,
                dto.Source,
                dto.PreferredPlanId,
                dto.PreferredStartDate,
                dto.Notes,
                metadata);

            if (registrationResult.IsFailure) return Result.Failure<Guid>(registrationResult.Error);

            var registration = registrationResult.Value;
            // Ensure facility context is set if not provided by UI
            if (registration.FacilityId == Guid.Empty)
            {
                registration.FacilityId = _facilityContext.CurrentFacilityId;
            }

            await _registrationRepository.AddAsync(registration);

            return Result.Success(registration.Id);
        }
    }
}
