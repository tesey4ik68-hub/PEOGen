using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AGenerator.Converters;

/// <summary>
/// Конвертер для преобразования сравнения строк в Visibility
/// Используется для показа/скрытия разделов настроек по выбранной категории
/// </summary>
public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not string currentCategory || parameter is not string targetCategory)
            return Visibility.Collapsed;

        return currentCategory.Equals(targetCategory, StringComparison.OrdinalIgnoreCase)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
