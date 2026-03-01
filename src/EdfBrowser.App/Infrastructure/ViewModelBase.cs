using CommunityToolkit.Mvvm.ComponentModel;

namespace EdfBrowser.App.Infrastructure;

/// <summary>
/// Base class for all view models in the application.
/// Inherits from <see cref="ObservableObject"/> which provides
/// <see cref="ObservableObject.SetProperty{T}(ref T, T, string?)"/> and
/// <see cref="ObservableObject.OnPropertyChanged(string?)"/> for change notification.
/// </summary>
public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string _title = string.Empty;

    /// <summary>
    /// Gets or sets whether the view model is currently performing a long-running operation.
    /// </summary>
    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    /// <summary>
    /// Gets or sets a display title for the view associated with this view model.
    /// </summary>
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    /// <summary>
    /// Raises <see cref="ObservableObject.PropertyChanged"/> for the specified property
    /// on the UI thread, ensuring thread safety for WPF binding updates.
    /// </summary>
    /// <param name="propertyName">The name of the property that changed.</param>
    protected void RaisePropertyChangedOnUiThread(string propertyName)
    {
        DispatcherHelper.RunOnUiThread(() => OnPropertyChanged(propertyName));
    }

    /// <summary>
    /// Sets a property value and raises change notification on the UI thread.
    /// Useful when property changes originate from background threads.
    /// </summary>
    protected bool SetPropertyOnUiThread<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        RaisePropertyChangedOnUiThread(propertyName!);
        return true;
    }
}
