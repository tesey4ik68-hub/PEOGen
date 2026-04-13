using System;
using System.Globalization;
using System.Windows.Data;
using AGenerator.Models;

namespace AGenerator.Converters;

/// <summary>
/// Конвертер enum RepresentativeType в русское отображаемое имя.
/// </summary>
public class RepresentativeTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is RepresentativeType role)
        {
            return role switch
            {
                RepresentativeType.SK_Zakazchika => "СК Заказчика",
                RepresentativeType.GenPodryadchik => "Генподрядчик",
                RepresentativeType.SK_GenPodryadchika => "СК Генподрядчика",
                RepresentativeType.Podryadchik => "Подрядчик",
                RepresentativeType.AvtorskiyNadzor => "Авторский надзор",
                RepresentativeType.InoeLico => "Иное лицо",
                _ => role.ToString()
            };
        }
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
