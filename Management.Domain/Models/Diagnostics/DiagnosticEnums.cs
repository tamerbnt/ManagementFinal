namespace Management.Domain.Models.Diagnostics
{
    public enum DiagnosticSeverity
    {
        Info,
        Warning,
        Error,
        Fatal
    }

    public enum DiagnosticCategory
    {
        System,
        UI,
        Network,
        Database,
        Security,
        Integration,
        Unexpected,
        Application
    }

}
