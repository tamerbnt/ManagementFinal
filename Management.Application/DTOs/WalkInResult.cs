namespace Management.Application.DTOs
{
    public class WalkInResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ReceiptNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
    }
}
