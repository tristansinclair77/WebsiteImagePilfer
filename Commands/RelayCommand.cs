using System.Windows.Input;

namespace WebsiteImagePilfer.Commands
{
    /// <summary>
    /// A command implementation that relays its functionality to delegates.
    /// Provides a simple ICommand implementation for MVVM pattern.
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
 private readonly Func<object?, bool>? _canExecute;

        /// <summary>
        /// Creates a new RelayCommand that can always execute.
        /// </summary>
      /// <param name="execute">The execution logic.</param>
        public RelayCommand(Action<object?> execute) : this(execute, null)
   {
        }

        /// <summary>
      /// Creates a new RelayCommand.
        /// </summary>
        /// <param name="execute">The execution logic.</param>
     /// <param name="canExecute">The execution status logic.</param>
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute)
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
        /// Determines whether the command can execute in its current state.
        /// </summary>
     /// <param name="parameter">Data used by the command.</param>
     /// <returns>true if this command can be executed; otherwise, false.</returns>
public bool CanExecute(object? parameter)
        {
       return _canExecute == null || _canExecute(parameter);
   }

        /// <summary>
     /// Executes the command.
     /// </summary>
        /// <param name="parameter">Data used by the command.</param>
     public void Execute(object? parameter)
        {
 _execute(parameter);
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
