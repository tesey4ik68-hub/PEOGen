using System;
using System.Collections.Specialized;
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
        }
    }

    public static void RefreshDataGrid(DataGrid dataGrid)
    {
        if (dataGrid == null || dataGrid.Items.Count == 0) return;

        // Убедимся, что строки используют Auto-высоту (перенос текста)
        dataGrid.RowHeight = double.NaN;

        dataGrid.UpdateLayout();

        // Переключаем каждую колонку в Auto для пересчёта ширины
        foreach (var column in dataGrid.Columns)
        {
            var originalWidth = column.Width;
            column.Width = new DataGridLength(1, DataGridLengthUnitType.Auto);
        }

        dataGrid.UpdateLayout();

        // Возвращаем Star-колонки в Star режим
        foreach (var column in dataGrid.Columns)
        {
            // Определяем Star по исходной ширине > 0
            if (column.MinWidth > 0)
                column.MinWidth = 0;
        }

        dataGrid.UpdateLayout();

        // Принудительно пересчитываем высоту всех строк
        dataGrid.Items.Refresh();
    }
}
