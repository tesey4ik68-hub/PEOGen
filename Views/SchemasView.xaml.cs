using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AGenerator.Services;
using AGenerator.ViewModels;

namespace AGenerator.Views;

public partial class SchemasView : UserControl
{
    private readonly DataGridColumnWidthService _columnWidthService;

    public SchemasView()
    {
        InitializeComponent();
        _columnWidthService = new DataGridColumnWidthService();

        SchemasDataGrid.Loaded += SchemasDataGrid_Loaded;
    }

    private void SchemasDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _columnWidthService.RestoreColumnWidths(SchemasDataGrid, "SchemasDataGrid");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _columnWidthService.AutoFitColumns(SchemasDataGrid, 300);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void SchemasDataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _columnWidthService.SaveColumnWidths(SchemasDataGrid, "SchemasDataGrid");
    }

    private void SchemasDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SchemasViewModel vm)
            vm.EditSelectedSchemaCommand.Execute(null);
    }

    private void BtnPickSchemaDate_Click(object sender, RoutedEventArgs e)
    {
        SchemaDateCalendarPopup.IsOpen = true;
    }

    private void SchemaDateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SchemaDateCalendar.SelectedDate.HasValue && DataContext is SchemasViewModel vm)
        {
            vm.EditingSchema.Date = SchemaDateCalendar.SelectedDate.Value;
            SchemaDateCalendarPopup.IsOpen = false;
        }
    }

    // ==================== DRAG-AND-DROP ДЛЯ ФАЙЛА СХЕМЫ ====================

    private void SchemaDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private async void SchemaDropZone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not SchemasViewModel vm) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            await vm.HandleSchemaFileDrop(files);
        }
    }

    private void Modal_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is SchemasViewModel vm && vm.IsEditing)
        {
            vm.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
    }
}
