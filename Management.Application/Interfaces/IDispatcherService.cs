using System;
using System.Threading.Tasks;

namespace Management.Application.Interfaces
{
    public interface IDispatcherService
    {
        bool CheckAccess();
        void Invoke(Action action);
        Task InvokeAsync(Action action);
        Task<T> InvokeAsync<T>(Func<T> func);
    }
}
