namespace Management.Application.Interfaces.App
{
    public interface IToastService
    {
        void ShowSuccess(string message, string? title = null);
        void ShowSuccess(string message, string undoLabel, System.Func<System.Threading.Tasks.Task> undoAction);
        void ShowError(string message, string? title = null);
        void ShowWarning(string message, string? title = null);
        void ShowInfo(string message, string? title = null);
    }
}
