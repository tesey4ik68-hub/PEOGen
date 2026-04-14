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
}
