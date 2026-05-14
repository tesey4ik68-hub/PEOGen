using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using AGenerator.ViewModels;

namespace AGenerator.Views;

/// <summary>
/// Логика взаимодействия для ActsView.xaml
/// </summary>
public partial class ActsView : UserControl
{
    public ActsView()
    {
        InitializeComponent();
    }

    private void MyDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.EditAction != DataGridEditAction.Commit) return;

        var act = e.Row.Item as Models.Act;
        if (act == null) return;

        var viewModel = DataContext as ActsViewModel;
        if (viewModel == null) return;

        // Проверяем, является ли редактируемый столбец столбцом даты акта
        if (e.Column.Header?.ToString() == "Дата акта")
        {
            // Получаем новое значение даты из редактирующего элемента
            var datePicker = e.EditingElement as DatePicker;
            if (datePicker?.SelectedDate.HasValue == true)
            {
                // Устанавливаем флаг, что дата была изменена вручную
                viewModel.OnActDateManuallyChanged(act);
            }
        }

        // Пересчитываем и сохраняем акт
        viewModel.RecalculateAndSaveAct(act);
    }

    private void MyDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
    {
        // Отменяем автоматическую сортировку DataGrid
        e.Handled = true;

        // Получаем ViewModel
        var viewModel = DataContext as ActsViewModel;
        if (viewModel == null) return;

        // Вызываем пользовательскую сортировку
        viewModel.SortActsByColumnToggle(e.Column.Header.ToString());
    }
}