namespace EdfBrowser.App.Views.Docks;

using System.Windows.Controls;
using System.Windows.Input;

public partial class AnnotationDockView : UserControl
{
    public AnnotationDockView()
    {
        InitializeComponent();
    }

    private void OnAnnotationDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ViewModels.AnnotationListViewModel vm)
        {
            vm.NavigateToAnnotationCommand.Execute(null);
        }
    }
}
