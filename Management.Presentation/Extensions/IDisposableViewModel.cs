using System;

namespace Management.Presentation.Extensions
{
    public interface IDisposableViewModel : IDisposable
    {
        bool IsDisposed { get; }
    }
}
