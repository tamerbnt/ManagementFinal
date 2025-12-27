namespace Management.Domain.DTOs
{
    public class IntegrationDto
    {
        public string Name { get; set; } // "Stripe"
        public string Description { get; set; }
        public string IconKey { get; set; } // "IconCreditCard"
        public bool IsConnected { get; set; }
    }
}