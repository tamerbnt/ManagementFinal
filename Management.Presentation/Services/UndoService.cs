using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Management.Presentation.Services
{
    public interface IUndoableAction
    {
        string Description { get; }
        Task UndoAsync();
        Task RedoAsync();
    }

    public interface IUndoService
    {
        void Push(IUndoableAction action);
        void Push(string description, Func<Task> undoAction); // Overload
        Task UndoAsync();
        bool CanUndo { get; }
        bool IsBannerVisible { get; }
        event EventHandler CanUndoChanged;
        event EventHandler VisibilityChanged;
    }

    public class UndoService : IUndoService
    {
        private readonly Stack<IUndoableAction> _actions = new();
        private const int MaxHistory = 10;
        private bool _isBannerVisible;
        private System.Threading.Timer? _timer;

        public bool CanUndo => _actions.Count > 0;
        public bool IsBannerVisible => _isBannerVisible;

        public event EventHandler? CanUndoChanged;
        public event EventHandler? VisibilityChanged;

        public void Push(IUndoableAction action)
        {
            if (_actions.Count >= MaxHistory)
            {
                var list = new List<IUndoableAction>(_actions);
                list.RemoveAt(0);
                _actions.Clear();
                foreach (var item in list) _actions.Push(item);
            }
            
            _actions.Push(action);
            ShowBanner();
            CanUndoChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Push(string description, Func<Task> undoAction)
        {
            Push(new GenericUndoAction(description, undoAction));
        }

        private class GenericUndoAction : IUndoableAction
        {
            public string Description { get; }
            private readonly Func<Task> _undo;

            public GenericUndoAction(string description, Func<Task> undo)
            {
                Description = description;
                _undo = undo;
            }

            public Task UndoAsync() => _undo();
            public Task RedoAsync() => Task.CompletedTask; // Redo not implemented in this overload
        }

        private void ShowBanner()
        {
            _isBannerVisible = true;
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
            
            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => 
            {
                _isBannerVisible = false;
                VisibilityChanged?.Invoke(this, EventArgs.Empty);
            }, null, 5000, System.Threading.Timeout.Infinite);
        }

        public async Task UndoAsync()
        {
            if (_actions.Count == 0) return;

            var action = _actions.Pop();
            await action.UndoAsync();
            
            _isBannerVisible = false;
            _timer?.Dispose();
            
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
            CanUndoChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
