using System;
using System.Windows;
using System.Windows.Controls;
using AGenerator.Models;
using AGenerator.ViewModels;

namespace AGenerator.Views;

public partial class OrganizationsView : UserControl
{
    public OrganizationsView()
    {
        InitializeComponent();
    }

    private void OrganizationsDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Row.Item is Organization org && DataContext is OrganizationsViewModel vm)
        {
            vm.OnOrganizationChanged(org);
        }
    }

    private void OrganizationsDataGrid_AutoGeneratingColumn(object? sender, DataGridAutoGeneratingColumnEventArgs e)
    {
        if (e.Column is DataGridTextColumn textColumn && Resources["WrappingCellText"] is Style style)
        {
            textColumn.ElementStyle = style;
            textColumn.MaxWidth = 500;
            textColumn.MinWidth = 80;
        }
    }
}
