using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AGenerator.Services;
using AGenerator.ViewModels;

namespace AGenerator.Views;

public partial class ProjectDocsView : UserControl
{
    private readonly DataGridColumnWidthService _columnWidthService;

    public ProjectDocsView()
    {
        InitializeComponent();
        _columnWidthService = new DataGridColumnWidthService();

        ProjectDocsDataGrid.Loaded += ProjectDocsDataGrid_Loaded;
    }

    private void ProjectDocsDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        _columnWidthService.RestoreColumnWidths(ProjectDocsDataGrid, "ProjectDocsDataGrid");

        Dispatcher.BeginInvoke(new Action(() =>
        {
            _columnWidthService.AutoFitColumns(ProjectDocsDataGrid, 300);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private void ProjectDocsDataGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _columnWidthService.SaveColumnWidths(ProjectDocsDataGrid, "ProjectDocsDataGrid");
    }

    private void ProjectDocsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ProjectDocsViewModel vm)
            vm.EditSelectedProjectDocCommand.Execute(null);
    }

    // ==================== DRAG-AND-DROP ДЛЯ ФАЙЛА ПРОЕКТА ====================

    private void ProjectFileDropZone_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            e.Effects = DragDropEffects.Copy;
        else
            e.Effects = DragDropEffects.None;

        e.Handled = true;
    }

    private async void ProjectFileDropZone_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not ProjectDocsViewModel vm) return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            await vm.HandleProjectFileDrop(files);
        }
    }
}
