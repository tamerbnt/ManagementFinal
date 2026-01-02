using Management.Domain.Primitives;
using Management.Domain.DTOs;
using Management.Domain.Models;
using MediatR;

namespace Management.Application.Features.Settings
{
    public record UpdateGeneralSettingsCommand(GeneralSettingsDto Settings) : IRequest<Result>;
    public record UpdateFacilitySettingsCommand(FacilitySettingsDto Settings) : IRequest<Result>;
    public record UpdateAppearanceSettingsCommand(AppearanceSettingsDto Settings) : IRequest<Result>;
    
    public record GetGymSettingsQuery() : IRequest<Result<GymSettings>>;
}
