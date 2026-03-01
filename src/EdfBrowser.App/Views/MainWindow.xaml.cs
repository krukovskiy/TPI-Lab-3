namespace EdfBrowser.App.Views;

using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using EdfBrowser.App.ViewModels;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<MainWindowViewModel>();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        base.OnClosing(e);
        if (DataContext is MainWindowViewModel vm)
        {
            vm.ExitCommand.Execute(null);
        }
    }
}
