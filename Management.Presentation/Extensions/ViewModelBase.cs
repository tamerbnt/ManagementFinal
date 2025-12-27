using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Management.Presentation.Extensions
{
    // This class implements INotifyPropertyChanged so your UI updates automatically
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Updates a value and raises the PropertyChanged event if it actually changed.
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}