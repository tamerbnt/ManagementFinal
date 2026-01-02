namespace Management.Domain.DTOs
{
    public record GeneralSettingsDto(
        string GymName,
        string Address,
        string Email,
        string PhoneNumber,
        string Website,
        string TaxId,
        string LogoUrl
    );
}