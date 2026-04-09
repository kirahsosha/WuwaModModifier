using System.Windows.Input;

namespace WuwaModModifier.ViewModels
{
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            if (parameter is T typedParameter)
                return _canExecute?.Invoke(typedParameter) ?? true;
            if (parameter == null && default(T) == null)
                return _canExecute?.Invoke(default(T)) ?? true;
            return false;
        }

        public void Execute(object? parameter)
        {
            if (parameter is T typedParameter)
            {
                _execute(typedParameter);
            }
            else if (parameter == null && default(T) == null)
            {
                _execute(default(T));
            }
        }
    }
}

