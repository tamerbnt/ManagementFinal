using Management.Domain.Primitives;
using Management.Application.DTOs;
using Management.Domain.Models;
using MediatR;

namespace Management.Application.Features.Settings
{
    public record UpdateGeneralSettingsCommand(System.Guid FacilityId, GeneralSettingsDto Settings) : IRequest<Result>;
    public record UpdateFacilitySettingsCommand(System.Guid FacilityId, FacilitySettingsDto Settings) : IRequest<Result>;
    public record UpdateAppearanceSettingsCommand(System.Guid FacilityId, AppearanceSettingsDto Settings) : IRequest<Result>;
    public record UpdateThemeModeCommand(System.Guid FacilityId, bool IsLightMode) : IRequest<Result>;
    public record UpdateLightPaletteCommand(System.Guid FacilityId, string LightPalette) : IRequest<Result>;
    
    public record GetGymSettingsQuery(System.Guid FacilityId) : IRequest<Result<GymSettings>>;
    
    public record GetSalonSettingsQuery(System.Guid FacilityId) : IRequest<Result<SalonSettings>>;
    public record UpdateSalonSettingsCommand(System.Guid FacilityId, SalonSettingsDto Settings) : IRequest<Result>;
}
