using System.Collections;
using System.Windows;
using System.Windows.Input;
using AGenerator.ViewModels;

namespace AGenerator.Views;

public partial class MultiSelectWindow : Window
{
    public IList SelectedItemsResult { get; private set; } = new ArrayList();

    public MultiSelectWindow(string displayProperty)
    {
        InitializeComponent();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        // DataContext — это MultiSelectViewModel<T>, у которого есть свойство SelectedItemsResult
        var dc = DataContext;
        var prop = dc?.GetType().GetProperty("SelectedItemsResult");
        if (prop != null)
        {
            SelectedItemsResult = (System.Collections.IList)prop.GetValue(dc)!;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
