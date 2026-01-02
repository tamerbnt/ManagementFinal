using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Management.Presentation.Services
{
    public interface IDispatcher
    {
        bool CheckAccess();
        void Invoke(Action action);
        Task InvokeAsync(Action action);
        Task InvokeAsync(Func<Task> function);
        Task InvokeAsync(Action action, DispatcherPriority priority);
        Task<T> InvokeAsync<T>(Func<T> func);
    }
}
