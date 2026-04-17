using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using AGenerator.Services;
using AGenerator.ViewModels;

namespace AGenerator.Views;

public partial class MaterialsView : UserControl
{
    private readonly DataGridColumnWidthService _columnWidthService;

    public MaterialsView()
    {
        InitializeComponent();
        _columnWidthService = new DataGridColumnWidthService();

        MaterialsDataGrid.Loaded += MaterialsDataGrid_Loaded;
    }

    private void MaterialsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _columnWidthService.RestoreColumnWidths(MaterialsDataGrid, "MaterialsDataGrid");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _columnWidthService.AutoFitColumns(MaterialsDataGrid, 300);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void MaterialsDataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _columnWidthService.SaveColumnWidths(MaterialsDataGrid, "MaterialsDataGrid");
    }

    private void MaterialsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MaterialsViewModel vm)
            vm.EditSelectedMaterialCommand.Execute(null);
    }

    private void BtnPickDate_Click(object sender, RoutedEventArgs e)
    {
        CalendarPopup.IsOpen = true;
    }

    private void CertCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CertCalendar.SelectedDate.HasValue)
        {
            var dateStr = CertCalendar.SelectedDate.Value.ToString("dd.MM.yyyy");

            // Обновляем ViewModel напрямую
            if (DataContext is MaterialsViewModel vm)
            {
                vm.EditingMaterial.CertificateDateText = dateStr;
            }

            // Обновляем текст в TextBox
            CertDateTextBox.Text = dateStr;
            // Обновляем binding source
            CertDateTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            CalendarPopup.IsOpen = false;
        }
    }

    private void BtnPickDeliveryDate_Click(object sender, RoutedEventArgs e)
    {
        DeliveryDateCalendarPopup.IsOpen = true;
    }

    private void DeliveryDateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DeliveryDateCalendar.SelectedDate.HasValue && DataContext is MaterialsViewModel vm)
        {
            vm.EditingMaterial.DeliveryDate = DeliveryDateCalendar.SelectedDate.Value;
            DeliveryDateCalendarPopup.IsOpen = false;
        }
    }

    private void CertificateDropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void CertificateDropZone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MaterialsViewModel vm) return;
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            vm.HandleCertificateFileDrop(files);
        }
    }

    private void Modal_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is MaterialsViewModel vm && vm.IsEditing)
        {
            vm.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
    }
}
