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
        public required ICommand DismissCommand { get; set; }

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
