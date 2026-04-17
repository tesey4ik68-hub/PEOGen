using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AGenerator.Services;
using AGenerator.ViewModels;
using AGenerator.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AGenerator.Models;
using AGenerator.Views;

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

        // Подписка на событие добавления организации
        DataContextChanged += EmployeesView_DataContextChanged;
    }

    private void EmployeesView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EmployeesViewModel oldVm)
            oldVm.RequestAddOrganization -= OnRequestAddOrganization;
        if (e.NewValue is EmployeesViewModel newVm)
            newVm.RequestAddOrganization += OnRequestAddOrganization;
    }

    private async void OnRequestAddOrganization(object? sender, EventArgs e)
    {
        try
        {
            if (DataContext is not EmployeesViewModel vm) return;

            var contextFactory = App.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            var orgVm = new OrganizationsViewModel(contextFactory, vm.EditingEmployee.ConstructionObjectId);

            var window = new Window
            {
                Title = "Справочник организаций",
                Content = new OrganizationsView { DataContext = orgVm },
                Width = 900,
                Height = 550,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                Background = System.Windows.Application.Current.Resources["GlassBrush"] as System.Windows.Media.Brush,
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                ResizeMode = ResizeMode.NoResize
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            var header = new Border 
            { 
                Background = System.Windows.Application.Current.Resources["GlassBrush"] as System.Windows.Media.Brush,
                Padding = new Thickness(15),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            var headerContent = new Grid();
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerContent.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            
            var title = new TextBlock 
            { 
                Text = "🏢 Справочник организаций", 
                FontSize = 16, 
                FontWeight = FontWeights.Bold,
                Foreground = System.Windows.Application.Current.Resources["TextPrimaryBrush"] as System.Windows.Media.Brush,
                VerticalAlignment = VerticalAlignment.Center
            };
            
            var closeBtn = new Button 
            { 
                Content = "✕", 
                Width = 30, 
                Height = 30,
                Background = System.Windows.Media.Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 14,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            closeBtn.Click += (s, args) => window.Close();
            
            Grid.SetColumn(title, 0);
            Grid.SetColumn(closeBtn, 1);
            headerContent.Children.Add(title);
            headerContent.Children.Add(closeBtn);
            header.Child = headerContent;
            Grid.SetRow(header, 0);
            
            var content = window.Content as System.Windows.Controls.UserControl;
            window.Content = null;
            Grid.SetRow(content, 1);
            mainGrid.Children.Add(header);
            mainGrid.Children.Add(content);
            
            window.Content = mainGrid;
            
            header.MouseLeftButtonDown += (s, args) => { if (args.LeftButton == System.Windows.Input.MouseButtonState.Pressed) window.DragMove(); };
            
            window.ShowDialog();

            await vm.LoadOrganizationsFromDirectoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OrganizationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is EmployeesViewModel vm)
        {
            vm.RefreshSelectedOrganization();
            if (vm.SelectedOrganizationForEmployee != null)
            {
                vm.OnOrganizationSelected();
            }
        }
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
            vm.EditingEmployee.OrderDateText = vm.EditingEmployee.OrderDate.Value.ToString("dd.MM.yyyy");
            vm.RefreshEditingEmployee();
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
            vm.EditingEmployee.NrsDateText = vm.EditingEmployee.NrsDate.Value.ToString("dd.MM.yyyy");
            vm.RefreshEditingEmployee();
            NrsDateCalendarPopup.IsOpen = false;
        }
    }

    private void BtnPickWorkStartDate_Click(object sender, RoutedEventArgs e)
    {
        WorkStartDateCalendarPopup.IsOpen = true;
    }

    private void WorkStartDateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkStartDateCalendar.SelectedDate.HasValue && DataContext is EmployeesViewModel vm)
        {
            vm.EditingEmployee.WorkStartDate = WorkStartDateCalendar.SelectedDate.Value;
            vm.EditingEmployee.WorkStartDateText = vm.EditingEmployee.WorkStartDate.Value.ToString("dd.MM.yyyy");
            vm.RefreshEditingEmployee();
            WorkStartDateCalendarPopup.IsOpen = false;
        }
    }

    private void BtnPickWorkEndDate_Click(object sender, RoutedEventArgs e)
    {
        WorkEndDateCalendarPopup.IsOpen = true;
    }

    private void WorkEndDateCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
    {
        if (WorkEndDateCalendar.SelectedDate.HasValue && DataContext is EmployeesViewModel vm)
        {
            vm.EditingEmployee.WorkEndDate = WorkEndDateCalendar.SelectedDate.Value;
            vm.EditingEmployee.WorkEndDateText = vm.EditingEmployee.WorkEndDate.Value.ToString("dd.MM.yyyy");
            vm.RefreshEditingEmployee();
            WorkEndDateCalendarPopup.IsOpen = false;
        }
    }

    private void Modal_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is EmployeesViewModel vm && vm.IsEditing)
        {
            vm.CancelEditCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void ModalBorder_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true && sender is Border border)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var scrollViewer = FindVisualChild<ScrollViewer>(border);
                scrollViewer?.ScrollToTop();
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T result)
                return result;
            var childOfChild = FindVisualChild<T>(child);
            if (childOfChild != null)
                return childOfChild;
        }
        return null;
    }
}
