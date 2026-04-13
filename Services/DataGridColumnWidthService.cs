using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace AGenerator.Services;

/// <summary>
/// Сервис сохранения и восстановления ширины столбцов DataGrid.
/// </summary>
public class DataGridColumnWidthService
{
    private readonly string _storagePath;
    private Dictionary<string, Dictionary<string, double>> _savedWidths;

    public DataGridColumnWidthService()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _storagePath = Path.Combine(appDir, "column_widths.json");
        _savedWidths = LoadSavedWidths();
    }

    /// <summary>
    /// Восстановить сохранённые ширины столбцов для DataGrid.
    /// </summary>
    public void RestoreColumnWidths(DataGrid dataGrid, string gridName)
    {
        if (!_savedWidths.ContainsKey(gridName)) return;

        var gridWidths = _savedWidths[gridName];

        foreach (var column in dataGrid.Columns)
        {
            var header = column.Header?.ToString();
            if (header != null && gridWidths.ContainsKey(header))
            {
                column.Width = gridWidths[header];
            }
        }
    }

    /// <summary>
    /// Сохранить текущие ширины столбцов DataGrid.
    /// </summary>
    public void SaveColumnWidths(DataGrid dataGrid, string gridName)
    {
        if (!_savedWidths.ContainsKey(gridName))
        {
            _savedWidths[gridName] = new Dictionary<string, double>();
        }

        var gridWidths = _savedWidths[gridName];

        foreach (var column in dataGrid.Columns)
        {
            var header = column.Header?.ToString();
            if (header != null)
            {
                gridWidths[header] = column.ActualWidth;
            }
        }

        SaveWidths();
    }

    /// <summary>
    /// Автоматически подобрать ширину столбцов на основе содержимого.
    /// Ширина = максимальная ширина текста (заголовок + ячейки), но не более maxWidth.
    /// </summary>
    public void AutoFitColumns(DataGrid dataGrid, double maxWidth = 300)
    {
        // Создаём FormattedText для измерения
        var typeface = new Typeface(new FontFamily("GOST"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
        double headerFontSize = 13;
        double cellFontSize = 12;
        var pixelsPerDip = VisualTreeHelper.GetDpi(dataGrid).PixelsPerDip;

        foreach (var column in dataGrid.Columns)
        {
            double maxWidthFound = 0;

            // Измеряем ширину заголовка (жирный шрифт 13px)
            var headerText = column.Header?.ToString() ?? "";
            var headerFormattedText = new FormattedText(
                headerText,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                headerFontSize,
                Brushes.Black,
                new NumberSubstitution(),
                pixelsPerDip);

            maxWidthFound = headerFormattedText.Width + 16; // + отступы

            // Измеряем содержимое ячеек в зависимости от типа колонки
            foreach (var item in dataGrid.Items)
            {
                string cellText = null;

                if (column is DataGridTextColumn textColumn)
                {
                    var binding = textColumn.Binding as Binding;
                    if (binding != null && !string.IsNullOrEmpty(binding.Path?.Path))
                    {
                        var propertyName = binding.Path.Path;
                        var value = item?.GetType().GetProperty(propertyName)?.GetValue(item);

                        // Для enum значений используем описание или ToString
                        if (value != null && value.GetType().IsEnum)
                        {
                            // Пробуем получить DisplayAttribute или DescriptionAttribute
                            var field = value.GetType().GetField(value.ToString());
                            var displayAttr = field?.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
                                .FirstOrDefault() as System.ComponentModel.DataAnnotations.DisplayAttribute;
                            
                            if (displayAttr != null)
                            {
                                cellText = displayAttr.GetName();
                            }
                            else
                            {
                                // Для RepresentativeType используем кастомное преобразование
                                cellText = GetEnumDisplayText(value);
                            }
                        }
                        else if (binding.Converter != null)
                        {
                            try
                            {
                                cellText = binding.Converter.Convert(value, typeof(string),
                                    binding.ConverterParameter,
                                    CultureInfo.CurrentCulture)?.ToString();
                            }
                            catch
                            {
                                cellText = value?.ToString();
                            }
                        }
                        else
                        {
                            cellText = value?.ToString();
                        }
                    }
                }
                else if (column is DataGridComboBoxColumn comboColumn)
                {
                    var binding = comboColumn.SelectedValueBinding as Binding;
                    if (binding != null && !string.IsNullOrEmpty(binding.Path?.Path))
                    {
                        var propertyName = binding.Path.Path;
                        cellText = item?.GetType().GetProperty(propertyName)?.GetValue(item)?.ToString();
                    }
                }
                else if (column is DataGridTemplateColumn templateColumn)
                {
                    // Для шаблонных колонок используем минимальную ширину
                    maxWidthFound = Math.Max(maxWidthFound, 60);
                    continue;
                }

                if (!string.IsNullOrEmpty(cellText))
                {
                    var cellTypeface = new Typeface(new FontFamily("GOST"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                    var formattedText = new FormattedText(
                        cellText,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        cellTypeface,
                        cellFontSize,
                        Brushes.Black,
                        new NumberSubstitution(),
                        pixelsPerDip);

                    var width = formattedText.Width + 16;
                    if (width > maxWidthFound) maxWidthFound = width;
                }
            }

            // Ограничиваем максимальной шириной
            column.Width = Math.Min(maxWidthFound, maxWidth);
        }
    }

    private Dictionary<string, Dictionary<string, double>> LoadSavedWidths()
    {
        try
        {
            if (File.Exists(_storagePath))
            {
                var json = File.ReadAllText(_storagePath);
                return JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(json)
                       ?? new Dictionary<string, Dictionary<string, double>>();
            }
        }
        catch
        {
            // Игнорируем ошибки
        }

        return new Dictionary<string, Dictionary<string, double>>();
    }

    private void SaveWidths()
    {
        try
        {
            var json = JsonSerializer.Serialize(_savedWidths, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_storagePath, json);
        }
        catch
        {
            // Игнорируем ошибки сохранения
        }
    }

    /// <summary>
    /// Получить отображаемый текст для enum значения.
    /// </summary>
    private static string GetEnumDisplayText(object enumValue)
    {
        if (enumValue == null) return "";

        var typeName = enumValue.GetType().Name;
        var valueName = enumValue.ToString();

        // RepresentativeType -> конвертер
        if (typeName == "RepresentativeType")
        {
            return valueName switch
            {
                "SK_Zakazchika" => "СК Заказчика",
                "GenPodryadchik" => "Ген. Подрядчик",
                "SK_GenPodryadchika" => "СК Ген. Подрядчика",
                "Podryadchik" => "Подрядчик",
                "AvtorskiyNadzor" => "Авторский Надзор",
                "InoeLico" => "Иное лицо",
                _ => valueName
            };
        }

        // MaterialDocType
        if (typeName == "MaterialDocType")
        {
            return valueName switch
            {
                "DeclarationOfConformity" => "Декларация о соответствии",
                "QualityDocument" => "Документ о качестве",
                "RefusalLetter" => "Отказное письмо",
                "Passport" => "Паспорт",
                "QualityPassport" => "Паспорт качества",
                "SanitaryEpidemiologicalConclusion" => "Сан.-эпид. заключение",
                "Certificate" => "Свидетельство",
                "StateRegistrationCertificate" => "Свидетельство о гос.регистрации",
                "CertificateOfConformity" => "Сертификат соответствия",
                "TechnicalPassport" => "Технический паспорт",
                _ => valueName
            };
        }

        // ProtocolDocType
        if (typeName == "ProtocolDocType")
        {
            return valueName switch
            {
                "TestProtocol" => "Протокол испытаний",
                "Conclusion" => "Заключение",
                _ => valueName
            };
        }

        return valueName;
    }
}
