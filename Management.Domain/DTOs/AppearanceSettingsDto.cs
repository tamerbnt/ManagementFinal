namespace Management.Domain.DTOs
{
    public class AppearanceSettingsDto
    {
        public bool IsLightMode { get; set; }
        public string Language { get; set; }
        public string DateFormat { get; set; }
        public bool HighContrast { get; set; }
        public bool ReducedMotion { get; set; }
        public string TextScale { get; set; }
    }
}