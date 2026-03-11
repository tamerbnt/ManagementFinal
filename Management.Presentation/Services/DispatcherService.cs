using System;
using System.Threading.Tasks;
using System.Windows;
using Management.Application.Interfaces;

namespace Management.Presentation.Services
{
    public class DispatcherService : IDispatcherService
    {
        public bool CheckAccess()
        {
            return System.Windows.Application.Current?.Dispatcher?.CheckAccess() ?? true;
        }

        public void Invoke(Action action)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(action);
            }
            else
            {
                // Fallback for non-UI scenarios or unit tests
                action();
            }
        }

        public Task InvokeAsync(Action action)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                return System.Windows.Application.Current.Dispatcher.InvokeAsync(action).Task;
            }
            
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                return System.Windows.Application.Current.Dispatcher.InvokeAsync(func).Task;
            }
            
            return Task.FromResult(func());
        }
    }
}
