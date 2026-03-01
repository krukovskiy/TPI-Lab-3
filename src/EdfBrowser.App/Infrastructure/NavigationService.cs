using System.Windows;

namespace EdfBrowser.App.Infrastructure;

/// <summary>
/// Provides navigation services for showing dialogs and windows.
/// </summary>
public interface INavigationService
{
    /// <summary>
    /// Shows a modal dialog of the specified window type.
    /// </summary>
    /// <typeparam name="TWindow">The window type to show.</typeparam>
    /// <returns>The dialog result.</returns>
    bool? ShowDialog<TWindow>() where TWindow : Window, new();

    /// <summary>
    /// Shows a modal dialog of the specified window type with the given view model as DataContext.
    /// </summary>
    /// <typeparam name="TWindow">The window type to show.</typeparam>
    /// <param name="viewModel">The view model to set as the DataContext.</param>
    /// <returns>The dialog result.</returns>
    bool? ShowDialog<TWindow>(object viewModel) where TWindow : Window, new();

    /// <summary>
    /// Shows a non-modal window of the specified type.
    /// </summary>
    /// <typeparam name="TWindow">The window type to show.</typeparam>
    void ShowWindow<TWindow>() where TWindow : Window, new();

    /// <summary>
    /// Shows a non-modal window of the specified type with the given view model as DataContext.
    /// </summary>
    /// <typeparam name="TWindow">The window type to show.</typeparam>
    /// <param name="viewModel">The view model to set as the DataContext.</param>
    void ShowWindow<TWindow>(object viewModel) where TWindow : Window, new();
}

/// <summary>
/// Default implementation of <see cref="INavigationService"/> using WPF Window instances.
/// </summary>
public sealed class NavigationService : INavigationService
{
    /// <inheritdoc />
    public bool? ShowDialog<TWindow>() where TWindow : Window, new()
    {
        var window = new TWindow
        {
            Owner = GetActiveWindow()
        };
        return window.ShowDialog();
    }

    /// <inheritdoc />
    public bool? ShowDialog<TWindow>(object viewModel) where TWindow : Window, new()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var window = new TWindow
        {
            DataContext = viewModel,
            Owner = GetActiveWindow()
        };
        return window.ShowDialog();
    }

    /// <inheritdoc />
    public void ShowWindow<TWindow>() where TWindow : Window, new()
    {
        var window = new TWindow
        {
            Owner = GetActiveWindow()
        };
        window.Show();
    }

    /// <inheritdoc />
    public void ShowWindow<TWindow>(object viewModel) where TWindow : Window, new()
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var window = new TWindow
        {
            DataContext = viewModel,
            Owner = GetActiveWindow()
        };
        window.Show();
    }

    /// <summary>
    /// Returns the currently active window, or the main window if no active window is found.
    /// </summary>
    private static Window? GetActiveWindow()
    {
        if (Application.Current is null)
            return null;

        var activeWindow = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(w => w.IsActive);

        return activeWindow ?? Application.Current.MainWindow;
    }
}
