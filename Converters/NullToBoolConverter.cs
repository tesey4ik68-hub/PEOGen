using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AGenerator.Converters
{
    /// <summary>
    /// Конвертер: null -> false, не-null -> true.
    /// С параметром Inverted: null -> Visible (скрыть), не-null -> Collapsed (показать).
    /// </summary>
    public class NullToBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNotNull;
            if (value is string str)
                isNotNull = !string.IsNullOrEmpty(str);
            else
                isNotNull = value != null;

            // По умолчанию: показывать элемент если значение НЕ null
            // С параметром Inverted: скрывать элемент если значение НЕ null
            if (parameter is string s && s == "Inverted")
                return isNotNull ? Visibility.Collapsed : Visibility.Visible;

            return isNotNull ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return DependencyProperty.UnsetValue;
        }
    }
}
