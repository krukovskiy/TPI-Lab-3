namespace EdfBrowser.App.Views.Dialogs;

using System.Collections.ObjectModel;
using System.Windows;

public partial class SignalSelectionDialog : Window
{
    public ObservableCollection<SignalSelectionItem> Signals { get; } = new();

    public SignalSelectionDialog()
    {
        InitializeComponent();
        SignalList.ItemsSource = Signals;
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var s in Signals) s.IsSelected = true;
        SignalList.Items.Refresh();
    }

    private void DeselectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (var s in Signals) s.IsSelected = false;
        SignalList.Items.Refresh();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

public sealed class SignalSelectionItem
{
    public int SignalIndex { get; set; }
    public string Label { get; set; } = "";
    public bool IsSelected { get; set; } = true;
    public double SampleRate { get; set; }
    public string PhysicalDimension { get; set; } = "";
}
