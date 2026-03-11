using System;

namespace Management.Domain.Models.Diagnostics
{
    public enum BreadcrumbCategory
    {
        Navigation,
        Action,
        Service,
        Security,
        Data,
        System
    }

    public enum BreadcrumbLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }

    public class Breadcrumb
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
        public BreadcrumbCategory Category { get; set; }
        public BreadcrumbLevel Level { get; set; }
        public string? Context { get; set; } // e.g., Page name or Method
    }
}
