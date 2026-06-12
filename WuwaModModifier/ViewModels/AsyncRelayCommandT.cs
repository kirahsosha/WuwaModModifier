using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Async variant of <see cref="RelayCommand{T}"/> with cancellation and
    /// <see cref="IsExecuting"/> state support.
    /// </summary>
    public class AsyncRelayCommand<T> : ICommand
    {
        private readonly Func<T?, CancellationToken, Task> _execute;
        private readonly Func<T?, bool>? _canExecute;
        private CancellationTokenSource? _cts;
        private bool _isExecuting;

        public bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (_isExecuting == value) return;
                _isExecuting = value;
                RaiseCanExecuteChanged();
            }
        }

        public AsyncRelayCommand(Func<T?, CancellationToken, Task> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public AsyncRelayCommand(Func<T?, Task> execute, Func<T?, bool>? canExecute = null)
            : this((p, _) => execute(p), canExecute)
        {
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }

        public bool CanExecute(object? parameter)
        {
            if (_isExecuting) return false;
            var typed = parameter is T t ? t : default;
            return _canExecute?.Invoke(typed) ?? true;
        }

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;

            CancelPendingExecution();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            IsExecuting = true;
            try
            {
                var typed = parameter is T t ? t : default;
                await _execute(typed, token);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancel
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
                IsExecuting = false;
            }
        }

        public void CancelPendingExecution()
        {
            _cts?.Cancel();
        }
    }
}
