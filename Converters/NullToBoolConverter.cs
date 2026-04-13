using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AGenerator.Converters
{
    /// <summary>
    /// Конвертер: null -> false, не-null -> true.
    /// С параметром Inverted: null -> Visible, не-null -> Collapsed.
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

            if (parameter is string s && s == "Inverted")
                return isNotNull ? Visibility.Collapsed : Visibility.Visible;

            return isNotNull;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Read-only — не поддерживаем обратную конвертацию
            return DependencyProperty.UnsetValue;
        }
    }
}
