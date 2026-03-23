using System;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;

namespace Management.Presentation.Models
{
    public class ToastMessage : ObservableObject
    {
        public string Id { get; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public ToastType Type { get; set; } = ToastType.Info;
        public DateTime Timestamp { get; } = DateTime.Now;

        // Undo Support
        public bool HasUndo { get; set; }
        public string UndoLabel { get; set; } = "Undo";
        public ICommand? UndoCommand { get; set; }

        private bool _isExiting;
        public bool IsExiting
        {
            get => _isExiting;
            set => SetProperty(ref _isExiting, value);
        }

        public ICommand? DismissCommand { get; set; }
    }

    public enum ToastType
    {
        Info,
        Success,
        Warning,
        Error
    }
}
