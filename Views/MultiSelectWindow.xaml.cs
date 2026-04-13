using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace AGenerator.Views
{
    /// <summary>
    /// Универсальное окно множественного выбора элементов.
    /// DisplayMemberPath устанавливается из кода для корректного отображения.
    /// </summary>
    public partial class MultiSelectWindow : Window
    {
        /// <summary>
        /// Результат выбора — список выбранных элементов.
        /// </summary>
        public IList<object> SelectedItemsResult { get; private set; } = new List<object>();

        /// <summary>
        /// Создать окно множественного выбора.
        /// </summary>
        /// <param name="displayMemberPath">Имя свойства для отображения в ListBox (например, "Name", "FullName")</param>
        public MultiSelectWindow(string displayMemberPath = "Name")
        {
            InitializeComponent();

            // Используем DisplayText из DisplayItem<T> для отображения
            var binding = new System.Windows.Data.Binding("DisplayText");
            AvailableListBox.SetBinding(ListBox.DisplayMemberPathProperty, binding);
            SelectedListBox.SetBinding(ListBox.DisplayMemberPathProperty, binding);
        }

        private void Ok_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext != null)
            {
                // Получаем SelectedItems через рефлексию (универсальный подход)
                var vmType = DataContext.GetType();
                var selectedItemsProp = vmType.GetProperty("SelectedItems");
                if (selectedItemsProp != null)
                {
                    var selectedItems = selectedItemsProp.GetValue(DataContext) as System.Collections.IEnumerable;
                    if (selectedItems != null)
                    {
                        var result = new List<object>();
                        foreach (var item in selectedItems)
                            result.Add(item);
                        SelectedItemsResult = result;
                    }
                }
            }

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
