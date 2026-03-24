using System;
using System.Windows.Input;

namespace DevKit.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Func<object, bool> _canExecute;
        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public RelayCommand(Action execute, Func<bool> canExecute = null) : this(_ => execute(), canExecute == null ? null : new Func<object, bool>(_ => canExecute())) { }
        public event EventHandler CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
        public bool CanExecute(object p) => _canExecute?.Invoke(p) ?? true;
        public void Execute(object p) => _execute(p);
    }

    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Func<T, bool> _canExecute;
        public RelayCommand(Action<T> execute, Func<T, bool> canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public event EventHandler CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
        public bool CanExecute(object p) => _canExecute?.Invoke((T)p) ?? true;
        public void Execute(object p) => _execute((T)p);
    }
}
