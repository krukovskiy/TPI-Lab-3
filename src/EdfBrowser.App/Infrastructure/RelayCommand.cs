using CommunityToolkit.Mvvm.Input;

namespace EdfBrowser.App.Infrastructure;

/// <summary>
/// Convenience factory methods for creating relay commands.
/// Wraps <see cref="CommunityToolkit.Mvvm.Input.RelayCommand"/> and
/// <see cref="CommunityToolkit.Mvvm.Input.AsyncRelayCommand"/> for cleaner API usage.
/// </summary>
public static class CommandHelper
{
    /// <summary>
    /// Creates a synchronous <see cref="RelayCommand"/> with an optional canExecute predicate.
    /// </summary>
    /// <param name="execute">The action to invoke when the command is executed.</param>
    /// <param name="canExecute">An optional predicate that determines whether the command can execute.</param>
    /// <returns>A new <see cref="RelayCommand"/> instance.</returns>
    public static RelayCommand Create(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return canExecute is null
            ? new RelayCommand(execute)
            : new RelayCommand(execute, canExecute);
    }

    /// <summary>
    /// Creates a synchronous <see cref="RelayCommand{T}"/> with an optional canExecute predicate.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    /// <param name="execute">The action to invoke when the command is executed.</param>
    /// <param name="canExecute">An optional predicate that determines whether the command can execute.</param>
    /// <returns>A new <see cref="RelayCommand{T}"/> instance.</returns>
    public static RelayCommand<T> Create<T>(Action<T?> execute, Predicate<T?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return canExecute is null
            ? new RelayCommand<T>(execute)
            : new RelayCommand<T>(execute, canExecute);
    }

    /// <summary>
    /// Creates an asynchronous <see cref="AsyncRelayCommand"/> with an optional canExecute predicate.
    /// </summary>
    /// <param name="execute">The asynchronous function to invoke when the command is executed.</param>
    /// <param name="canExecute">An optional predicate that determines whether the command can execute.</param>
    /// <returns>A new <see cref="AsyncRelayCommand"/> instance.</returns>
    public static AsyncRelayCommand CreateAsync(Func<Task> execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return canExecute is null
            ? new AsyncRelayCommand(execute)
            : new AsyncRelayCommand(execute, canExecute);
    }

    /// <summary>
    /// Creates an asynchronous <see cref="AsyncRelayCommand{T}"/> with an optional canExecute predicate.
    /// </summary>
    /// <typeparam name="T">The type of the command parameter.</typeparam>
    /// <param name="execute">The asynchronous function to invoke when the command is executed.</param>
    /// <param name="canExecute">An optional predicate that determines whether the command can execute.</param>
    /// <returns>A new <see cref="AsyncRelayCommand{T}"/> instance.</returns>
    public static AsyncRelayCommand<T> CreateAsync<T>(Func<T?, Task> execute, Predicate<T?>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);
        return canExecute is null
            ? new AsyncRelayCommand<T>(execute)
            : new AsyncRelayCommand<T>(execute, canExecute);
    }
}
