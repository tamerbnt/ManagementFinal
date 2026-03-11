using System;

namespace Management.Presentation.Models
{
    public class ToastMessage
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; } = ToastType.Info;
        public DateTime Timestamp { get; } = DateTime.Now;
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
