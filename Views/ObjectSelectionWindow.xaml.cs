using System.Windows;
using System.Windows.Input;
using AGenerator.Models;
using AGenerator.ViewModels;

namespace AGenerator.Views;

public partial class ObjectSelectionWindow : Window
{
    public ObjectSelectionWindow()
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

    private void ObjectsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Двойной клик = выбрать объект
        if (DataContext is ObjectSelectionViewModel vm && vm.SelectedObject != null)
        {
            e.Handled = true;

            if (vm.SelectObjectCommand.CanExecute(null))
            {
                vm.SelectObjectCommand.Execute(null);
            }
        }
    }

    private void EditObjectButton_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ObjectSelectionViewModel vm && vm.SelectedObject != null)
        {
            vm.EditObjectCommand.Execute(null);
        }
    }
}
