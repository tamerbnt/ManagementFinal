namespace Management.Domain.DTOs
{
    public record IntegrationDto
    {
        public Guid Id { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }
}