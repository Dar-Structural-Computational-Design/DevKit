using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DevKit.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string p = null)
        { if (Equals(field, value)) return false; field = value; OnPropertyChanged(p); return true; }
    }
}
