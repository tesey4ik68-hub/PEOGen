using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AGenerator.Services;
using AGenerator.ViewModels;

namespace AGenerator.Views;

public partial class ProtocolsView : UserControl
{
    private readonly DataGridColumnWidthService _columnWidthService;

    public ProtocolsView()
    {
        InitializeComponent();
        _columnWidthService = new DataGridColumnWidthService();

        ProtocolsDataGrid.Loaded += ProtocolsDataGrid_Loaded;
    }

    private void ProtocolsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _columnWidthService.RestoreColumnWidths(ProtocolsDataGrid, "ProtocolsDataGrid");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _columnWidthService.AutoFitColumns(ProtocolsDataGrid, 300);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ProtocolsDataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _columnWidthService.SaveColumnWidths(ProtocolsDataGrid, "ProtocolsDataGrid");
    }

    private void ProtocolsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProtocolsViewModel vm)
            vm.EditSelectedProtocolCommand.Execute(null);
    }

    private void BtnPickProtocolDate_Click(object sender, RoutedEventArgs e)
    {
        ProtocolDateCalendarPopup.IsOpen = true;
    }

    private void ProtocolDateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ProtocolDateCalendar.SelectedDate.HasValue && DataContext is ProtocolsViewModel vm)
        {
            vm.EditingProtocol.Date = ProtocolDateCalendar.SelectedDate.Value;
            ProtocolDateCalendarPopup.IsOpen = false;
        }
    }

    // ==================== DRAG-AND-DROP ДЛЯ ФАЙЛА ПРОТОКОЛА ====================

    private void ProtocolDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private async void ProtocolDropZone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not ProtocolsViewModel vm) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            await vm.HandleProtocolFileDrop(files);
        }
    }
}
