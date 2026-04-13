using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using AGenerator.Models;

namespace AGenerator.Converters;

/// <summary>
/// Конвертер MaterialDocType → русское название
/// </summary>
public class MaterialDocTypeToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is MaterialDocType type)
        {
            return type switch
            {
                MaterialDocType.DeclarationOfConformity => "Декларация о соответствии",
                MaterialDocType.QualityDocument => "Документ о качестве",
                MaterialDocType.RefusalLetter => "Отказное письмо",
                MaterialDocType.Passport => "Паспорт",
                MaterialDocType.QualityPassport => "Паспорт качества",
                MaterialDocType.SanitaryEpidemiologicalConclusion => "Сан.-эпид. заключение",
                MaterialDocType.Certificate => "Свидетельство",
                MaterialDocType.StateRegistrationCertificate => "Свидетельство о гос.регистрации",
                MaterialDocType.CertificateOfConformity => "Сертификат соответствия",
                MaterialDocType.TechnicalPassport => "Технический паспорт",
                _ => type.ToString()
            };
        }
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return DependencyProperty.UnsetValue;
    }
}
