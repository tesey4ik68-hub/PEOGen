using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace AGenerator.Behaviors;

/// <summary>
/// Attached behavior для автоматического изменения ширины колонок DataGrid
/// и высоты строк при изменении данных.
/// </summary>
public static class DataGridAutoSizeColumnsBehavior
{
    private static readonly Dictionary<DataGrid, bool> _isRefreshing = new();

    public static bool GetIsEnabled(DataGrid obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DataGrid obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached(
            "IsEnabled",
            typeof(bool),
            typeof(DataGridAutoSizeColumnsBehavior),
            new PropertyMetadata(false, OnIsEnabledChanged));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not DataGrid dataGrid) return;

        if ((bool)e.NewValue)
        {
            dataGrid.Loaded += (s, args) => RefreshDataGrid(dataGrid);

            if (dataGrid.ItemsSource is INotifyCollectionChanged notify)
            {
                notify.CollectionChanged += (s, args) =>
                    dataGrid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                        new Action(() => RefreshDataGrid(dataGrid)));
            }

            // Обновление при завершении редактирования ячейки
            dataGrid.CellEditEnding += (s, args) =>
            {
                dataGrid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                    new Action(() => RefreshDataGrid(dataGrid)));
            };

            // Обновление при сортировке
            dataGrid.Sorting += (s, args) =>
            {
                dataGrid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                    new Action(() => RefreshDataGrid(dataGrid)));
            };

            // Подписка на изменение свойств элементов при загрузке строк
            dataGrid.LoadingRow += (s, args) =>
            {
                if (args.Row.DataContext is INotifyPropertyChanged item)
                {
                    item.PropertyChanged += (sender, propArgs) =>
                    {
                        dataGrid.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
                            new Action(() => RefreshDataGrid(dataGrid)));
                    };
                }
            };
        }
    }

    public static void RefreshDataGrid(DataGrid dataGrid)
    {
        // Защита от повторного вызова
        if (_isRefreshing.ContainsKey(dataGrid) && _isRefreshing[dataGrid])
            return;

        try
        {
            _isRefreshing[dataGrid] = true;

            if (dataGrid == null || dataGrid.Items.Count == 0) return;

            // Проверяем, находится ли CollectionView в режиме редактирования
            // Refresh нельзя вызывать во время AddNew или EditItem
            var editableCollectionView = dataGrid.Items as System.ComponentModel.IEditableCollectionView;
            if (editableCollectionView?.IsEditingItem == true || editableCollectionView?.IsAddingNew == true)
            {
                return;
            }

            // Убедимся, что строки используют Auto-высоту (перенос текста)
            dataGrid.RowHeight = double.NaN;

            dataGrid.UpdateLayout();

            // Переключаем каждую колонку в Auto для пересчёта ширины
            foreach (var column in dataGrid.Columns)
            {
                column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
            }

            dataGrid.UpdateLayout();

            // Возвращаем Star-колонки в Star режим
            foreach (var column in dataGrid.Columns)
            {
                if (column.MinWidth > 0)
                    column.MinWidth = 0;
            }

            dataGrid.UpdateLayout();

            // Принудительно пересчитываем высоту всех строк (только если не в режиме редактирования)
            if (editableCollectionView == null || (!editableCollectionView.IsEditingItem && !editableCollectionView.IsAddingNew))
            {
                dataGrid.Items.Refresh();
            }
        }
        finally
        {
            _isRefreshing[dataGrid] = false;
        }
    }
}
