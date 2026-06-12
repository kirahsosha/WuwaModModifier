using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WuwaModModifier.ViewModels
{
    /// <summary>
    /// Async variant of <see cref="RelayCommand"/> that supports
    /// async execution, cancellation, and <see cref="IsExecuting"/> state.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        private readonly Func<CancellationToken, Task> _execute;
        private readonly Func<bool>? _canExecute;
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

        public AsyncRelayCommand(Func<CancellationToken, Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
            : this(_ => execute(), canExecute)
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
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
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
                await _execute(token);
            }
            catch (OperationCanceledException)
            {
                // Expected on cancel — no-op
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
