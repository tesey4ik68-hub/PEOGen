using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AGenerator.Models;
using AGenerator.Services;
using AGenerator.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AGenerator.ViewModels;

/// <summary>
/// ViewModel для окна настроек приложения
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly DocumentSettingsService _documentSettingsService;

    #region Категории настроек

    [ObservableProperty]
    private string _selectedCategory = "Design";

    public ObservableCollection<string> Categories { get; } = new()
    {
        "Design",
        "Documents"
    };

    #endregion

    #region Настройки темы

    [ObservableProperty]
    private ThemePresetViewModel? _selectedThemePreset;

    public ObservableCollection<ThemePresetViewModel> ThemePresets { get; } = new();

    [ObservableProperty]
    private string _primaryColor = "#4FC3F7";

    [ObservableProperty]
    private string _secondaryColor = "#2196F3";

    [ObservableProperty]
    private string _accentColor = "#1976D2";

    [ObservableProperty]
    private string _primaryColorLabel = "#4FC3F7";

    [ObservableProperty]
    private string _secondaryColorLabel = "#2196F3";

    [ObservableProperty]
    private string _accentColorLabel = "#1976D2";

    #endregion

    #region Настройки документов

    [ObservableProperty]
    private DocumentSettings _documentSettings = new();

    /// <summary>
    /// Коллекция порядка приложений для UI (ObservableCollection для привязки)
    /// </summary>
    public ObservableCollection<ApplicationOrderItem> ApplicationOrder { get; } = new();

    #endregion

    public SettingsViewModel()
    {
        _themeService = new ThemeService();
        _documentSettingsService = new DocumentSettingsService();
        InitializeThemePresets();
        LoadCurrentTheme();
        LoadDocumentSettings();
    }

    private void LoadDocumentSettings()
    {
        _documentSettings = _documentSettingsService.LoadSettings();

        ApplicationOrder.Clear();
        foreach (var item in _documentSettings.ApplicationOrder.OrderBy(x => x.Order))
        {
            ApplicationOrder.Add(item);
        }
    }

    #region Команды управления порядком приложений

    [RelayCommand]
    private void MoveOrderUp(ApplicationOrderItem item)
    {
        if (item == null) return;
        var index = ApplicationOrder.IndexOf(item);
        if (index <= 0) return;

        ApplicationOrder.Move(index, index - 1);
        RenumberOrder();
        SaveDocumentSettings();
    }

    [RelayCommand]
    private void MoveOrderDown(ApplicationOrderItem item)
    {
        if (item == null) return;
        var index = ApplicationOrder.IndexOf(item);
        if (index < 0 || index >= ApplicationOrder.Count - 1) return;

        ApplicationOrder.Move(index, index + 1);
        RenumberOrder();
        SaveDocumentSettings();
    }

    private void RenumberOrder()
    {
        for (int i = 0; i < ApplicationOrder.Count; i++)
        {
            ApplicationOrder[i].Order = i;
        }
    }

    private void SaveDocumentSettings()
    {
        // Синхронизируем ObservableCollection обратно в DocumentSettings
        _documentSettings.ApplicationOrder = ApplicationOrder.ToList();
        _documentSettingsService.SaveSettings(_documentSettings);
    }

    #endregion

    private void InitializeThemePresets()
    {
        var presets = new[]
        {
            new ThemePresetViewModel { Name = "Ocean Breeze", Description = "Морской бриз", PrimaryColor = "#4FC3F7", SecondaryColor = "#2196F3", AccentColor = "#1976D2" },
            new ThemePresetViewModel { Name = "Forest Dawn", Description = "Лесной рассвет", PrimaryColor = "#66BB6A", SecondaryColor = "#43A047", AccentColor = "#2E7D32" },
            new ThemePresetViewModel { Name = "Sunset Glow", Description = "Закатное сияние", PrimaryColor = "#FFA726", SecondaryColor = "#FB8C00", AccentColor = "#E65100" },
            new ThemePresetViewModel { Name = "Royal Purple", Description = "Королевский", PrimaryColor = "#AB47BC", SecondaryColor = "#8E24AA", AccentColor = "#6A1B9A" },
            new ThemePresetViewModel { Name = "Midnight Steel", Description = "Полночная сталь", PrimaryColor = "#78909C", SecondaryColor = "#546E7A", AccentColor = "#37474F" }
        };

        foreach (var preset in presets)
            ThemePresets.Add(preset);
    }

    private void LoadCurrentTheme()
    {
        try
        {
            var resources = Application.Current.Resources;
            var gradient = resources["MainGradient"] as LinearGradientBrush;
            string primary = "#4FC3F7", secondary = "#2196F3", accent = "#1976D2";

            if (gradient != null && gradient.GradientStops.Count >= 2)
            {
                primary = gradient.GradientStops[0].Color.ToString();
                secondary = gradient.GradientStops[1].Color.ToString();
            }

            if (resources["AccentColor"] is Color accentColor)
                accent = accentColor.ToString();

            PrimaryColor = primary;
            SecondaryColor = secondary;
            AccentColor = accent;

            PrimaryColorLabel = primary;
            SecondaryColorLabel = secondary;
            AccentColorLabel = accent;

            // Пытаемся найти соответствующий пресет
            SelectedThemePreset = ThemePresets.FirstOrDefault(p =>
                p.PrimaryColor == primary && p.SecondaryColor == secondary && p.AccentColor == accent);
        }
        catch
        {
            // Используем значения по умолчанию
        }
    }

    #region Команды управления темой

    [RelayCommand]
    private void SelectThemePreset(ThemePresetViewModel preset)
    {
        if (preset == null) return;

        SelectedThemePreset = preset;
        PrimaryColor = preset.PrimaryColor;
        SecondaryColor = preset.SecondaryColor;
        AccentColor = preset.AccentColor;

        PrimaryColorLabel = preset.PrimaryColor;
        SecondaryColorLabel = preset.SecondaryColor;
        AccentColorLabel = preset.AccentColor;

        // Предварительное применение темы
        ApplyThemePreview();
    }

    [RelayCommand]
    private void SelectPrimaryColor()
    {
        var picker = new ColorPickerWindow(PrimaryColor);
        if (picker.ShowDialog() == true)
        {
            PrimaryColor = picker.SelectedColor;
            PrimaryColorLabel = picker.SelectedColor;
            ApplyThemePreview();
        }
    }

    [RelayCommand]
    private void SelectSecondaryColor()
    {
        var picker = new ColorPickerWindow(SecondaryColor);
        if (picker.ShowDialog() == true)
        {
            SecondaryColor = picker.SelectedColor;
            SecondaryColorLabel = picker.SelectedColor;
            ApplyThemePreview();
        }
    }

    [RelayCommand]
    private void SelectAccentColor()
    {
        var picker = new ColorPickerWindow(AccentColor);
        if (picker.ShowDialog() == true)
        {
            AccentColor = picker.SelectedColor;
            AccentColorLabel = picker.SelectedColor;
            ApplyThemePreview();
        }
    }

    [RelayCommand]
    private void ApplyTheme()
    {
        var settings = new ThemeSettings
        {
            PrimaryColor = PrimaryColor,
            SecondaryColor = SecondaryColor,
            AccentColor = AccentColor,
            TextColor = "#2D3748"
        };

        _themeService.ApplyAndSaveTheme(settings);
        RefreshAllWindows();
    }

    [RelayCommand]
    private void ResetTheme()
    {
        PrimaryColor = "#4FC3F7";
        SecondaryColor = "#2196F3";
        AccentColor = "#1976D2";

        PrimaryColorLabel = "#4FC3F7";
        SecondaryColorLabel = "#2196F3";
        AccentColorLabel = "#1976D2";

        SelectedThemePreset = ThemePresets.FirstOrDefault(p => p.Name == "Ocean Breeze");

        var settings = new ThemeSettings
        {
            PrimaryColor = PrimaryColor,
            SecondaryColor = SecondaryColor,
            AccentColor = AccentColor,
            TextColor = "#2D3748"
        };

        _themeService.ApplyAndSaveTheme(settings);
        RefreshAllWindows();
    }

    #endregion

    private void ApplyThemePreview()
    {
        var resources = Application.Current.Resources;

        // Обновляем градиент
        var gradient = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1)
        };
        gradient.GradientStops.Add(new GradientStop(ParseColor(PrimaryColor), 0));
        gradient.GradientStops.Add(new GradientStop(ParseColor(SecondaryColor), 1));
        resources["MainGradient"] = gradient;

        // Обновляем кисти акцентного цвета
        var accentColor = ParseColor(AccentColor);
        resources["AccentBrush"] = new SolidColorBrush(accentColor);
        resources["AccentLightBrush"] = new SolidColorBrush(Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B));

        // Вычисляем контрастный цвет текста для кнопок
        var luminance = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255.0;
        var accentTextColor = luminance < 0.5 ? Colors.White : Color.FromRgb(0x1A, 0x20, 0x2C);
        resources["AccentTextBrush"] = new SolidColorBrush(accentTextColor);

        // Обновляем цвета текста
        var textColor = CalculateContrastTextColor(PrimaryColor, SecondaryColor);
        var secondaryTextColor = CalculateSecondaryTextColor(textColor);
        resources["TextPrimaryBrush"] = new SolidColorBrush(ParseColor(textColor));
        resources["TextSecondaryBrush"] = new SolidColorBrush(ParseColor(secondaryTextColor));
        resources["TextPrimary"] = ParseColor(textColor);
        resources["TextSecondary"] = ParseColor(secondaryTextColor);

        // Обновляем GlassBrush
        resources["GlassBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
        resources["GlassBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));

        RefreshAllWindows();
    }

    private void RefreshAllWindows()
    {
        foreach (Window window in Application.Current.Windows)
        {
            window.InvalidateVisual();
            window.UpdateLayout();
            RefreshVisualTree(window);
        }
    }

    private void RefreshVisualTree(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button btn)
            {
                btn.InvalidateProperty(Button.BackgroundProperty);
                btn.InvalidateProperty(Button.ForegroundProperty);
                btn.UpdateLayout();
            }
            else if (child is Control ctrl)
            {
                ctrl.InvalidateProperty(Control.BackgroundProperty);
                ctrl.InvalidateProperty(Control.BorderBrushProperty);
                ctrl.InvalidateProperty(Control.ForegroundProperty);
                ctrl.UpdateLayout();
            }
            else if (child is Border border)
            {
                border.InvalidateProperty(Border.BackgroundProperty);
                border.InvalidateProperty(Border.BorderBrushProperty);
                border.UpdateLayout();
            }
            else if (child is Panel panel)
            {
                panel.InvalidateProperty(Panel.BackgroundProperty);
                panel.UpdateLayout();
            }
            else if (child is TextBlock tb)
            {
                tb.InvalidateProperty(TextBlock.ForegroundProperty);
                tb.UpdateLayout();
            }
            RefreshVisualTree(child);
        }
    }

    private static string CalculateContrastTextColor(string primaryColor, string secondaryColor)
    {
        var primary = ParseColor(primaryColor);
        var secondary = ParseColor(secondaryColor);

        var primaryLuminance = (0.299 * primary.R + 0.587 * primary.G + 0.114 * primary.B) / 255.0;
        var secondaryLuminance = (0.299 * secondary.R + 0.587 * secondary.G + 0.114 * secondary.B) / 255.0;
        var avgLuminance = (primaryLuminance + secondaryLuminance) / 2.0;

        return avgLuminance < 0.5 ? "#F0F0F0" : "#1A202C";
    }

    private static string CalculateSecondaryTextColor(string primaryTextColor)
    {
        var color = ParseColor(primaryTextColor);
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;

        return luminance > 0.5 ? "#4A5568" : "#CBD5E0";
    }

    private static Color ParseColor(string hex)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return Colors.White; }
    }

    /// <summary>
    /// Получить текущий порядок приложений для использования в WordDocumentService.
    /// Статический метод для удобства вызова из других сервисов.
    /// </summary>
    public static List<ApplicationOrderItem> GetApplicationOrder()
    {
        try
        {
            var service = new DocumentSettingsService();
            var settings = service.LoadSettings();
            return settings.ApplicationOrder.OrderBy(x => x.Order).ToList();
        }
        catch
        {
            return DocumentSettings.CreateDefaultOrder();
        }
    }
}

/// <summary>
/// ViewModel для пресета темы
/// </summary>
public partial class ThemePresetViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private string _primaryColor = "#4FC3F7";

    [ObservableProperty]
    private string _secondaryColor = "#2196F3";

    [ObservableProperty]
    private string _accentColor = "#1976D2";
}
