namespace Management.Domain.DTOs
{
    public record AppearanceSettingsDto(
        bool IsLightMode,
        string Language,
        string DateFormat,
        bool HighContrast,
        bool ReducedMotion,
        string TextScale
    );
}