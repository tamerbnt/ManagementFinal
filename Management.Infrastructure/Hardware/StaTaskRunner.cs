using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Management.Infrastructure.Hardware
{
    /// <summary>
    /// Runs tasks on a dedicated STA thread.
    /// Required for legacy STA COM objects like ZKTeco SDK
    /// that must be created and used from the same thread.
    /// </summary>
    public sealed class StaTaskRunner : IDisposable
    {
        private readonly Thread _staThread;
        private readonly BlockingCollection<Action> _queue = new();
        private bool _disposed;

        public StaTaskRunner()
        {
            _staThread = new Thread(ProcessQueue)
            {
                IsBackground = true,
                Name = "ZKTeco-STA-Thread"
            };
            _staThread.SetApartmentState(ApartmentState.STA);
            _staThread.Start();
        }

        public Task<T> RunAsync<T>(Func<T> func)
        {
            var tcs = new TaskCompletionSource<T>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Add(() =>
            {
                try { tcs.SetResult(func()); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task RunAsync(Action action)
        {
            var tcs = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _queue.Add(() =>
            {
                try { action(); tcs.SetResult(); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        private void ProcessQueue()
        {
            foreach (var action in _queue.GetConsumingEnumerable())
            {
                if (_disposed) break;
                action();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _queue.CompleteAdding();
            // We don't necessarily need to wait for the thread here if it's a background thread,
            // but for COM cleanup it's sometimes better to let it finish.
            _queue.Dispose();
        }
    }
}
