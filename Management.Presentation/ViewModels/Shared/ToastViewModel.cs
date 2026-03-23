using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Presentation.Models;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels.Shared
{
    public class ToastViewModel : ViewModelBase
    {
        public Guid Id { get; set; }
        public ToastType Type { get; set; }
        public required string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICommand DismissCommand { get; set; } = null!;

        private bool _hasUndo;
        public bool HasUndo
        {
            get => _hasUndo;
            set => SetProperty(ref _hasUndo, value);
        }

        private string? _undoLabel;
        public string? UndoLabel
        {
            get => _undoLabel;
            set => SetProperty(ref _undoLabel, value);
        }

        public ICommand? UndoCommand { get; set; }

        private bool _isPaused;
        public bool IsPaused
        {
            get => _isPaused;
            set => SetProperty(ref _isPaused, value);
        }

        private bool _isExiting;
        public bool IsExiting
        {
            get => _isExiting;
            set => SetProperty(ref _isExiting, value);
        }
    }
}
