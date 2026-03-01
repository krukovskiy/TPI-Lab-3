namespace EdfBrowser.App;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using EdfBrowser.App.Infrastructure;
using EdfBrowser.App.Services;
using EdfBrowser.App.ViewModels;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherHelper.Initialize();

        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            MessageBox.Show($"Unhandled error: {ex?.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        };

        DispatcherUnhandledException += (_, args) =>
        {
            MessageBox.Show($"UI error: {args.Exception.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
        };

        var mainWindow = _serviceProvider.GetRequiredService<Views.MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Services
        services.AddSingleton<IFileManagerService, FileManagerService>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ISessionService, SessionService>();
        services.AddSingleton<IMontageService, MontageService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<IRemoteCommandService, RemoteCommandService>();
        services.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        services.AddSingleton<MainWindowViewModel>();
        services.AddTransient<FilterDialogViewModel>();
        services.AddTransient<HeaderEditorViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Views
        services.AddSingleton<Views.MainWindow>();
    }
}
