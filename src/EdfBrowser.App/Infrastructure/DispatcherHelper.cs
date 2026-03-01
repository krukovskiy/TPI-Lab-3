using System.Windows;
using System.Windows.Threading;

namespace EdfBrowser.App.Infrastructure;

/// <summary>
/// Provides helper methods for dispatching work to the WPF UI thread.
/// Must be initialized from <see cref="Application.OnStartup"/> by calling <see cref="Initialize"/>.
/// </summary>
public static class DispatcherHelper
{
    private static Dispatcher? _dispatcher;

    /// <summary>
    /// Initializes the dispatcher helper with the current thread's dispatcher.
    /// This must be called from <c>App.OnStartup</c> on the UI thread.
    /// </summary>
    public static void Initialize()
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
    }

    /// <summary>
    /// Executes the specified action synchronously on the UI thread.
    /// If already on the UI thread, the action is invoked directly.
    /// </summary>
    /// <param name="action">The action to execute on the UI thread.</param>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="Initialize"/> has not been called.</exception>
    public static void RunOnUiThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = GetDispatcher();

        if (dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            dispatcher.Invoke(action, DispatcherPriority.Normal);
        }
    }

    /// <summary>
    /// Executes the specified action asynchronously on the UI thread.
    /// Returns a <see cref="Task"/> that completes when the action has been executed.
    /// </summary>
    /// <param name="action">The action to execute on the UI thread.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="Initialize"/> has not been called.</exception>
    public static Task RunOnUiThreadAsync(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        var dispatcher = GetDispatcher();

        if (dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action, DispatcherPriority.Normal).Task;
    }

    /// <summary>
    /// Executes the specified function asynchronously on the UI thread
    /// and returns the result.
    /// </summary>
    /// <typeparam name="T">The return type of the function.</typeparam>
    /// <param name="func">The function to execute on the UI thread.</param>
    /// <returns>A task containing the result of the function.</returns>
    /// <exception cref="InvalidOperationException">Thrown if <see cref="Initialize"/> has not been called.</exception>
    public static Task<T> RunOnUiThreadAsync<T>(Func<T> func)
    {
        ArgumentNullException.ThrowIfNull(func);

        var dispatcher = GetDispatcher();

        if (dispatcher.CheckAccess())
        {
            return Task.FromResult(func());
        }

        return dispatcher.InvokeAsync(func, DispatcherPriority.Normal).Task;
    }

    /// <summary>
    /// Returns whether the calling thread is the UI thread.
    /// </summary>
    /// <returns><c>true</c> if the caller is on the UI thread; otherwise <c>false</c>.</returns>
    public static bool CheckAccess()
    {
        var dispatcher = _dispatcher;
        return dispatcher is null || dispatcher.CheckAccess();
    }

    private static Dispatcher GetDispatcher()
    {
        return _dispatcher ?? throw new InvalidOperationException(
            "DispatcherHelper has not been initialized. Call DispatcherHelper.Initialize() from App.OnStartup.");
    }
}
