using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AGenerator.Services;
using AGenerator.ViewModels;

namespace AGenerator.Views;

/// <summary>
/// Interaction logic for EmployeesView.xaml
/// </summary>
public partial class EmployeesView : UserControl
{
    private readonly DataGridColumnWidthService _columnWidthService;

    public EmployeesView()
    {
        InitializeComponent();
        _columnWidthService = new DataGridColumnWidthService();

        // Восстанавливаем сохранённые ширины столбцов
        EmployeesDataGrid.Loaded += EmployeesDataGrid_Loaded;
        EmployeesDataGrid.ColumnReordered += EmployeesDataGrid_ColumnReordered;
    }

    private void EmployeesDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        // Восстанавливаем сохранённые ширины
        _columnWidthService.RestoreColumnWidths(EmployeesDataGrid, "EmployeesDataGrid");

        // Автоподбор ширины после загрузки данных (с задержкой)
        Dispatcher.BeginInvoke(new Action(() =>
        {
            _columnWidthService.AutoFitColumns(EmployeesDataGrid, 300);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void EmployeesDataGrid_ColumnReordered(object sender, DataGridColumnEventArgs e)
    {
        _columnWidthService.SaveColumnWidths(EmployeesDataGrid, "EmployeesDataGrid");
    }

    private void EmployeesDataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // Сохраняем ширины столбцов после изменения размера
        _columnWidthService.SaveColumnWidths(EmployeesDataGrid, "EmployeesDataGrid");
    }

    private void EmployeesDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is EmployeesViewModel vm)
            vm.EditSelectedEmployeeCommand.Execute(null);
    }

    // ==================== DRAG-AND-DROP ДЛЯ ФАЙЛА ПРИКАЗА ====================

    private void OrderDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private void OrderDropZone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not EmployeesViewModel vm) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            vm.HandleOrderFileDrop(files);
        }
    }

    // ==================== КАЛЕНДАРИ ДЛЯ ДАТ ====================

    private void BtnPickOrderDate_Click(object sender, RoutedEventArgs e)
    {
        OrderDateCalendarPopup.IsOpen = true;
    }

    private void OrderDateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (OrderDateCalendar.SelectedDate.HasValue && DataContext is EmployeesViewModel vm)
        {
            vm.EditingEmployee.OrderDate = OrderDateCalendar.SelectedDate.Value;
            OrderDateCalendarPopup.IsOpen = false;
        }
    }

    private void BtnPickNrsDate_Click(object sender, RoutedEventArgs e)
    {
        NrsDateCalendarPopup.IsOpen = true;
    }

    private void NrsDateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (NrsDateCalendar.SelectedDate.HasValue && DataContext is EmployeesViewModel vm)
        {
            vm.EditingEmployee.NrsDate = NrsDateCalendar.SelectedDate.Value;
            NrsDateCalendarPopup.IsOpen = false;
        }
    }
}
