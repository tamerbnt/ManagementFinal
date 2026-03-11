namespace Management.Application.Interfaces.App
{
    public interface IToastService
    {
        void ShowSuccess(string message, string? title = null);
        void ShowError(string message, string? title = null);
        void ShowWarning(string message, string? title = null);
        void ShowInfo(string message, string? title = null);
    }
}
