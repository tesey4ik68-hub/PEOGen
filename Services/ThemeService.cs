using System;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace AGenerator.Services;

/// <summary>
/// Сервис управления темой оформления с сохранением в файл
/// </summary>
public class ThemeService
{
    private readonly string _themeFilePath;

    public ThemeService()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _themeFilePath = Path.Combine(appDir, "theme.json");
    }

    /// <summary>
    /// Применить тему из сохранённых настроек
    /// </summary>
    public void ApplySavedTheme()
    {
        if (!File.Exists(_themeFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_themeFilePath);
            var theme = System.Text.Json.JsonSerializer.Deserialize<ThemeSettings>(json);
            if (theme != null)
                ApplyTheme(theme);
        }
        catch
        {
            // Игнорируем ошибки — используем тему по умолчанию
        }
    }

    /// <summary>
    /// Сохранить и применить тему
    /// </summary>
    public void ApplyAndSaveTheme(ThemeSettings theme)
    {
        ApplyTheme(theme);
        SaveTheme(theme);
    }

    private void ApplyTheme(ThemeSettings theme)
    {
        var resources = Application.Current.Resources;

        // Обновляем цвета градиента
        SetResource(resources, "GradientStart", ParseColor(theme.PrimaryColor));
        SetResource(resources, "GradientEnd", ParseColor(theme.SecondaryColor));
        SetResource(resources, "AccentColor", ParseColor(theme.AccentColor));

        // Вычисляем контрастный цвет текста на основе яркости градиента
        var textColor = CalculateContrastTextColor(theme.PrimaryColor, theme.SecondaryColor);
        var secondaryTextColor = CalculateSecondaryTextColor(textColor);

        // Обновляем кисти текста (обновляем цвет внутри существующих кистей)
        UpdateBrushColor(resources, "TextPrimaryBrush", ParseColor(textColor));
        UpdateBrushColor(resources, "TextSecondaryBrush", ParseColor(secondaryTextColor));
        SetResource(resources, "TextPrimary", ParseColor(textColor));
        SetResource(resources, "TextSecondary", ParseColor(secondaryTextColor));

        // Обновляем градиент
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        gradient.GradientStops.Add(new GradientStop(ParseColor(theme.PrimaryColor), 0));
        gradient.GradientStops.Add(new GradientStop(ParseColor(theme.SecondaryColor), 1));
        SetResource(resources, "MainGradient", gradient);

        // Обновляем кисти акцентного цвета (для кнопок)
        var accentColor = ParseColor(theme.AccentColor);
        UpdateBrushColor(resources, "AccentBrush", accentColor);
        UpdateBrushColor(resources, "AccentLightBrush", Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B));

        // Вычисляем контрастный цвет текста для акцентных кнопок
        var accentTextBrushColor = CalculateContrastTextColorForButton(accentColor);
        UpdateBrushColor(resources, "AccentTextBrush", accentTextBrushColor);

        // Обновляем стеклянные кисти (полупрозрачный белый поверх градиента)
        UpdateBrushColor(resources, "GlassBrush", Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
        UpdateBrushColor(resources, "GlassBorderBrush", Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
    }

    private void SaveTheme(ThemeSettings theme)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(theme, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_themeFilePath, json);
        }
        catch
        {
            // Игнорируем ошибки сохранения
        }
    }

    private static void SetResource(ResourceDictionary resources, string key, object value)
    {
        if (resources.Contains(key))
            resources[key] = value;
    }

    /// <summary>
    /// Обновляет цвет кисти в ресурсах. Создаёт новую кисть и заменяет старую.
    /// </summary>
    private static void UpdateBrushColor(ResourceDictionary resources, string brushKey, Color newColor)
    {
        if (resources.Contains(brushKey))
        {
            resources[brushKey] = new SolidColorBrush(newColor);
        }
    }

    /// <summary>
    /// Вычисляет контрастный цвет текста для акцентных кнопок.
    /// Для тёмных акцентов — белый текст, для светлых — тёмный.
    /// </summary>
    private static Color CalculateContrastTextColorForButton(Color accentColor)
    {
        var luminance = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255.0;
        
        // Если акцент тёмный (яркость < 0.5) — белый текст
        // Если акцент светлый — тёмный текст
        if (luminance < 0.5)
        {
            return Colors.White;
        }
        else
        {
            return Color.FromRgb(0x1A, 0x20, 0x2C); // Тёмный текст
        }
    }

    private static Color ParseColor(string hex)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(hex);
        }
        catch
        {
            return Colors.White;
        }
    }

    /// <summary>
    /// Вычисляет контрастный цвет текста на основе яркости цветов градиента.
    /// Для светлых тем — тёмный текст, для тёмных — светлый.
    /// </summary>
    private static string CalculateContrastTextColor(string primaryColor, string secondaryColor)
    {
        var primary = ParseColor(primaryColor);
        var secondary = ParseColor(secondaryColor);

        // Вычисляем среднюю яркость обоих цветов (формула воспринимаемой яркости)
        var primaryLuminance = (0.299 * primary.R + 0.587 * primary.G + 0.114 * primary.B) / 255.0;
        var secondaryLuminance = (0.299 * secondary.R + 0.587 * secondary.G + 0.114 * secondary.B) / 255.0;
        var avgLuminance = (primaryLuminance + secondaryLuminance) / 2.0;

        // Если яркость меньше 0.5 — тема тёмная, используем светлый текст
        // Иначе — светлая тема, используем тёмный текст
        if (avgLuminance < 0.5)
        {
            return "#F0F0F0"; // Светлый текст для тёмных тем
        }
        else
        {
            return "#1A202C"; // Тёмный текст для светлых тем
        }
    }

    /// <summary>
    /// Вычисляет вторичный цвет текста на основе основного.
    /// Делает его чуть светлее/темнее для иерархии.
    /// </summary>
    private static string CalculateSecondaryTextColor(string primaryTextColor)
    {
        var color = ParseColor(primaryTextColor);

        // Определяем, светлая тема или тёмная
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;

        if (luminance > 0.5)
        {
            // Светлая тема — делаем текст чуть светлее
            return "#4A5568";
        }
        else
        {
            // Тёмная тема — делаем текст чуть темнее
            return "#CBD5E0";
        }
    }
}

/// <summary>
/// Настройки темы для сохранения
/// </summary>
public class ThemeSettings
{
    public string PrimaryColor { get; set; } = "#4FC3F7";
    public string SecondaryColor { get; set; } = "#2196F3";
    public string AccentColor { get; set; } = "#1976D2";
    public string TextColor { get; set; } = "#2D3748";
}
