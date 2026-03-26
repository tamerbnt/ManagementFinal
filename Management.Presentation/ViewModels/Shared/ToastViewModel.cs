using System;
using System.Diagnostics;

using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Presentation.Models;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels.Shared
{
    public partial class ToastViewModel : ViewModelBase
    {
        public Guid Id { get; set; }
        public ToastType Type { get; set; }
        public required string Message { get; set; }
        public DateTime CreatedAt { get; set; }
        public ICommand DismissCommand { get; set; } = null!;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasUndo))]
        private ICommand? _undoCommand;

        partial void OnUndoCommandChanged(ICommand? value)
        {
            OnPropertyChanged(nameof(HasUndo));
        }

        public bool HasUndo => UndoCommand != null;
        public string UndoLabel { get; set; } = "Undo";


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
