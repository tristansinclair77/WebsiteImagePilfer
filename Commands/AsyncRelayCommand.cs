using System.Windows.Input;

namespace WebsiteImagePilfer.Commands
{
    /// <summary>
  /// An async command implementation that relays its functionality to async delegates.
    /// Provides ICommand implementation for async operations in MVVM pattern.
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
      private readonly Func<object?, Task> _execute;
 private readonly Func<object?, bool>? _canExecute;
   private bool _isExecuting;

        /// <summary>
    /// Creates a new AsyncRelayCommand that can always execute.
      /// </summary>
     /// <param name="execute">The async execution logic.</param>
 public AsyncRelayCommand(Func<object?, Task> execute) : this(execute, null)
      {
 }

   /// <summary>
   /// Creates a new AsyncRelayCommand.
     /// </summary>
  /// <param name="execute">The async execution logic.</param>
     /// <param name="canExecute">The execution status logic.</param>
        public AsyncRelayCommand(Func<object?, Task> execute, Func<object?, bool>? canExecute)
      {
  _execute = execute ?? throw new ArgumentNullException(nameof(execute));
  _canExecute = canExecute;
        }

        /// <summary>
        /// Occurs when changes occur that affect whether the command should execute.
        /// </summary>
        public event EventHandler? CanExecuteChanged
   {
    add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
        }

        /// <summary>
  /// Gets whether the command is currently executing.
        /// </summary>
   public bool IsExecuting
        {
            get => _isExecuting;
     private set
      {
       if (_isExecuting != value)
     {
           _isExecuting = value;
                    RaiseCanExecuteChanged();
    }
    }
        }

        /// <summary>
        /// Determines whether the command can execute in its current state.
   /// </summary>
     /// <param name="parameter">Data used by the command.</param>
        /// <returns>true if this command can be executed; otherwise, false.</returns>
        public bool CanExecute(object? parameter)
   {
            return !IsExecuting && (_canExecute == null || _canExecute(parameter));
        }

    /// <summary>
        /// Executes the command asynchronously.
  /// </summary>
 /// <param name="parameter">Data used by the command.</param>
   public async void Execute(object? parameter)
        {
            await ExecuteAsync(parameter);
     }

        /// <summary>
        /// Executes the command asynchronously and returns the task.
        /// </summary>
        /// <param name="parameter">Data used by the command.</param>
   public async Task ExecuteAsync(object? parameter)
        {
            if (!CanExecute(parameter))
      return;

            IsExecuting = true;
   try
{
         await _execute(parameter);
         }
finally
 {
          IsExecuting = false;
        }
        }

   /// <summary>
        /// Raises the CanExecuteChanged event.
        /// </summary>
        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
  }
    }
}
