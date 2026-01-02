// ******************************************************************************************
//  Management.Presentation/Services/WpfDispatcher.cs
//  FINAL PRODUCTION VERSION – v1.2.0-production
//  Design System: Apple 2025 Edition – v1.2 FINAL (LOCKED)
//  Status: PRODUCTION READY
// ******************************************************************************************

using System;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Management.Presentation.Services
{
    /// <summary>
    /// WPF implementation of IDispatcher for UI thread operations
    /// </summary>
    public sealed class WpfDispatcher : IDispatcher
    {
        private readonly Dispatcher _dispatcher;

        public WpfDispatcher(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public bool CheckAccess() => _dispatcher.CheckAccess();

        public void Invoke(Action action) => _dispatcher.Invoke(action);

        public Task InvokeAsync(Action action) => _dispatcher.InvokeAsync(action).Task;

        public Task<T> InvokeAsync<T>(Func<T> func) => _dispatcher.InvokeAsync(func).Task;

        public Task InvokeAsync(Action action, DispatcherPriority priority) =>
            _dispatcher.InvokeAsync(action, priority).Task;

        public Task InvokeAsync(Func<Task> function)
        {
            return _dispatcher.InvokeAsync(function).Task.Unwrap();
        }
    }
}