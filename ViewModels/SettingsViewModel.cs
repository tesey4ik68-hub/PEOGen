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

public partial class SettingsViewModel : ViewModelBase
{
    private readonly ThemeService _themeService;
    private readonly DocumentSettingsService _documentSettingsService;

    [ObservableProperty] private string _selectedCategory = "Design";
    public ObservableCollection<string> Categories { get; } = new() { "Design", "Documents" };

    [ObservableProperty] private ThemePresetViewModel? _selectedThemePreset;
    public ObservableCollection<ThemePresetViewModel> ThemePresets { get; } = new();
    [ObservableProperty] private string _primaryColor = "#4FC3F7";
    [ObservableProperty] private string _secondaryColor = "#2196F3";
    [ObservableProperty] private string _accentColor = "#1976D2";
    [ObservableProperty] private string _primaryColorLabel = "#4FC3F7";
    [ObservableProperty] private string _secondaryColorLabel = "#2196F3";
    [ObservableProperty] private string _accentColorLabel = "#1976D2";

    [ObservableProperty] private DocumentSettings _documentSettings = new();
    public ObservableCollection<ApplicationOrderItem> ApplicationOrder { get; } = new();

    [ObservableProperty] private ActNumberMaskSettings _actNumberMask = ActNumberMaskSettings.CreateDefault();
    public IReadOnlyList<ActFieldDescriptor> ActFields => ActNumberMaskSettings.AvailableFields;
    [ObservableProperty] private string _actNumberPreview = "";

    public SettingsViewModel()
    {
        _themeService = new ThemeService();
        _documentSettingsService = new DocumentSettingsService();
        InitializeThemePresets();
        LoadCurrentTheme();
        LoadDocumentSettings();
        LoadActNumberMaskSettings();
        UpdateActNumberPreview();
    }

    private void LoadDocumentSettings()
    {
        _documentSettings = _documentSettingsService.LoadSettings();
        ApplicationOrder.Clear();
        foreach (var item in _documentSettings.ApplicationOrder.OrderBy(x => x.Order))
            ApplicationOrder.Add(item);
    }

    private void LoadActNumberMaskSettings()
    {
        _actNumberMask = _documentSettingsService.LoadActNumberMaskSettings();
        _actNumberMask.PropertyChanged += (_, _) => { UpdateActNumberPreview(); SaveActNumberMaskSettings(); };
    }

    private void UpdateActNumberPreview()
    {
        try
        {
            var testAct = new Act { Type = "АОСР", ActNumber = "123", ActDate = new DateTime(2026, 4, 15), WorkName = "Бетонирование фундамента", Interval = "К1", IntervalType = "в камере", Level1 = "Этаж 1", Level2 = "Ось А-В", Level3 = "", Mark = "+0.000", InAxes = "1-4", Volume = "15", UnitOfMeasure = "м3" };
            ActNumberPreview = BuildActNumberPreview(testAct, ActNumberMask);
        }
        catch { ActNumberPreview = "— ошибка предпросмотра —"; }
    }

    private static string BuildActNumberPreview(Act act, ActNumberMaskSettings mask)
    {
        var segments = new List<string>();
        AddSeg(segments, mask.Segment1Text);
        AddSeg(segments, PrevField(act, mask.Segment2Field));
        AddSeg(segments, mask.Segment3Text);
        AddSeg(segments, PrevField(act, mask.Segment4Field));
        AddSeg(segments, mask.Segment5Text);
        AddSeg(segments, PrevField(act, mask.Segment6Field));
        AddSeg(segments, mask.Segment7Text);
        AddSeg(segments, PrevField(act, mask.Segment8Field));
        AddSeg(segments, mask.Segment9Text);
        AddSeg(segments, PrevField(act, mask.Segment10Field));
        AddSeg(segments, mask.Segment11Text);
        AddSeg(segments, PrevField(act, mask.Segment12Field));
        var result = string.Join("", segments);
        var parts = result.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        result = string.Join(" ", parts);
        return string.IsNullOrWhiteSpace(result) ? "—" : result;
    }

    private static string PrevField(Act act, string fn)
    {
        if (string.IsNullOrWhiteSpace(fn)) return "";
        return fn.Trim() switch
        {
            "Type" => act.Type ?? "", "ActNumber" => act.ActNumber ?? "",
            "ActDate" => act.ActDate.Year.ToString(),
            "ActDateMonth" => MonthRu(act.ActDate.Month),
            "ActDateDay" => act.ActDate.Day.ToString("D2"),
            "WorkName" => act.WorkName ?? "", "Interval" => act.Interval ?? "",
            "IntervalType" => act.IntervalType ?? "",
            "Level1" => act.Level1 ?? "", "Level2" => act.Level2 ?? "", "Level3" => act.Level3 ?? "",
            "Mark" => act.Mark ?? "", "InAxes" => act.InAxes ?? "",
            "Volume" => act.Volume ?? "", "UnitOfMeasure" => act.UnitOfMeasure ?? "", _ => ""
        };
    }

    private static string MonthRu(int m)
    {
        var ms = new[] { "", "января", "февраля", "марта", "апреля", "мая", "июня", "июля", "августа", "сентября", "октября", "ноября", "декабря" };
        return ms[m];
    }

    private static void AddSeg(List<string> s, string v) { if (!string.IsNullOrWhiteSpace(v)) s.Add(v.Trim()); }

    private void SaveActNumberMaskSettings() { _documentSettingsService.SaveActNumberMaskSettings(ActNumberMask); }

    [RelayCommand]
    private void MoveOrderUp(ApplicationOrderItem item)
    {
        if (item == null) return;
        var index = ApplicationOrder.IndexOf(item);
        if (index <= 0) return;
        ApplicationOrder.Move(index, index - 1); RenumberOrder(); SaveDocumentSettings();
    }

    [RelayCommand]
    private void MoveOrderDown(ApplicationOrderItem item)
    {
        if (item == null) return;
        var index = ApplicationOrder.IndexOf(item);
        if (index < 0 || index >= ApplicationOrder.Count - 1) return;
        ApplicationOrder.Move(index, index + 1); RenumberOrder(); SaveDocumentSettings();
    }

    private void RenumberOrder() { for (int i = 0; i < ApplicationOrder.Count; i++) ApplicationOrder[i].Order = i; }

    private void SaveDocumentSettings()
    {
        _documentSettings.ApplicationOrder = ApplicationOrder.ToList();
        _documentSettingsService.SaveSettings(_documentSettings);
    }

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
        foreach (var p in presets) ThemePresets.Add(p);
    }

    private void LoadCurrentTheme()
    {
        try
        {
            var resources = Application.Current.Resources;
            var gradient = resources["MainGradient"] as LinearGradientBrush;
            string primary = "#4FC3F7", secondary = "#2196F3", accent = "#1976D2";
            if (gradient != null && gradient.GradientStops.Count >= 2) { primary = gradient.GradientStops[0].Color.ToString(); secondary = gradient.GradientStops[1].Color.ToString(); }
            if (resources["AccentColor"] is Color accentColor) accent = accentColor.ToString();
            PrimaryColor = primary; SecondaryColor = secondary; AccentColor = accent;
            PrimaryColorLabel = primary; SecondaryColorLabel = secondary; AccentColorLabel = accent;
            SelectedThemePreset = ThemePresets.FirstOrDefault(p => p.PrimaryColor == primary && p.SecondaryColor == secondary && p.AccentColor == accent);
        }
        catch { }
    }

    [RelayCommand]
    private void SelectThemePreset(ThemePresetViewModel preset)
    {
        if (preset == null) return;
        SelectedThemePreset = preset; PrimaryColor = preset.PrimaryColor; SecondaryColor = preset.SecondaryColor; AccentColor = preset.AccentColor;
        PrimaryColorLabel = preset.PrimaryColor; SecondaryColorLabel = preset.SecondaryColor; AccentColorLabel = preset.AccentColor;
        ApplyThemePreview();
    }

    [RelayCommand] private void SelectPrimaryColor() { var picker = new ColorPickerWindow(PrimaryColor); if (picker.ShowDialog() == true) { PrimaryColor = picker.SelectedColor; PrimaryColorLabel = picker.SelectedColor; ApplyThemePreview(); } }
    [RelayCommand] private void SelectSecondaryColor() { var picker = new ColorPickerWindow(SecondaryColor); if (picker.ShowDialog() == true) { SecondaryColor = picker.SelectedColor; SecondaryColorLabel = picker.SelectedColor; ApplyThemePreview(); } }
    [RelayCommand] private void SelectAccentColor() { var picker = new ColorPickerWindow(AccentColor); if (picker.ShowDialog() == true) { AccentColor = picker.SelectedColor; AccentColorLabel = picker.SelectedColor; ApplyThemePreview(); } }

    [RelayCommand]
    private void ApplyTheme()
    {
        var settings = new ThemeSettings { PrimaryColor = PrimaryColor, SecondaryColor = SecondaryColor, AccentColor = AccentColor, TextColor = "#2D3748" };
        _themeService.ApplyAndSaveTheme(settings); RefreshAllWindows();
    }

    [RelayCommand]
    private void ResetTheme()
    {
        PrimaryColor = "#4FC3F7"; SecondaryColor = "#2196F3"; AccentColor = "#1976D2";
        PrimaryColorLabel = "#4FC3F7"; SecondaryColorLabel = "#2196F3"; AccentColorLabel = "#1976D2";
        SelectedThemePreset = ThemePresets.FirstOrDefault(p => p.Name == "Ocean Breeze");
        var settings = new ThemeSettings { PrimaryColor = PrimaryColor, SecondaryColor = SecondaryColor, AccentColor = AccentColor, TextColor = "#2D3748" };
        _themeService.ApplyAndSaveTheme(settings); RefreshAllWindows();
    }

    private void ApplyThemePreview()
    {
        var resources = Application.Current.Resources;
        var gradient = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
        gradient.GradientStops.Add(new GradientStop(ParseColor(PrimaryColor), 0));
        gradient.GradientStops.Add(new GradientStop(ParseColor(SecondaryColor), 1));
        resources["MainGradient"] = gradient;
        var accentColor = ParseColor(AccentColor);
        resources["AccentBrush"] = new SolidColorBrush(accentColor);
        resources["AccentLightBrush"] = new SolidColorBrush(Color.FromArgb(0x40, accentColor.R, accentColor.G, accentColor.B));
        var luminance = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255.0;
        var accentTextColor = luminance < 0.5 ? Colors.White : Color.FromRgb(0x1A, 0x20, 0x2C);
        resources["AccentTextBrush"] = new SolidColorBrush(accentTextColor);
        var textColor = CalcContrast(PrimaryColor, SecondaryColor);
        var secTextColor = CalcSecondary(textColor);
        resources["TextPrimaryBrush"] = new SolidColorBrush(ParseColor(textColor));
        resources["TextSecondaryBrush"] = new SolidColorBrush(ParseColor(secTextColor));
        resources["TextPrimary"] = ParseColor(textColor);
        resources["TextSecondary"] = ParseColor(secTextColor);
        resources["GlassBrush"] = new SolidColorBrush(Color.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
        resources["GlassBorderBrush"] = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
        RefreshAllWindows();
    }

    private void RefreshAllWindows() { foreach (Window w in Application.Current.Windows) { w.InvalidateVisual(); w.UpdateLayout(); RefreshVT(w); } }

    private void RefreshVT(DependencyObject p)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(p); i++)
        {
            var c = VisualTreeHelper.GetChild(p, i);
            if (c is Button b) { b.InvalidateProperty(Button.BackgroundProperty); b.InvalidateProperty(Button.ForegroundProperty); b.UpdateLayout(); }
            else if (c is Control ctrl) { ctrl.InvalidateProperty(Control.BackgroundProperty); ctrl.InvalidateProperty(Control.BorderBrushProperty); ctrl.InvalidateProperty(Control.ForegroundProperty); ctrl.UpdateLayout(); }
            else if (c is Border br) { br.InvalidateProperty(Border.BackgroundProperty); br.InvalidateProperty(Border.BorderBrushProperty); br.UpdateLayout(); }
            else if (c is Panel pn) { pn.InvalidateProperty(Panel.BackgroundProperty); pn.UpdateLayout(); }
            else if (c is TextBlock tb) { tb.InvalidateProperty(TextBlock.ForegroundProperty); tb.UpdateLayout(); }
            RefreshVT(c);
        }
    }

    private static string CalcContrast(string pc, string sc)
    {
        var p = ParseColor(pc); var s = ParseColor(sc);
        var pl = (0.299 * p.R + 0.587 * p.G + 0.114 * p.B) / 255.0;
        var sl = (0.299 * s.R + 0.587 * s.G + 0.114 * s.B) / 255.0;
        return (pl + sl) / 2.0 < 0.5 ? "#F0F0F0" : "#1A202C";
    }

    private static string CalcSecondary(string pt)
    {
        var c = ParseColor(pt);
        var l = (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
        return l > 0.5 ? "#4A5568" : "#CBD5E0";
    }

    private static Color ParseColor(string hex) { try { return (Color)ColorConverter.ConvertFromString(hex); } catch { return Colors.White; } }

    public static List<ApplicationOrderItem> GetApplicationOrder()
    {
        try { var s = new DocumentSettingsService(); var st = s.LoadSettings(); return st.ApplicationOrder.OrderBy(x => x.Order).ToList(); }
        catch { return DocumentSettings.CreateDefaultOrder(); }
    }
}

public partial class ThemePresetViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _primaryColor = "#4FC3F7";
    [ObservableProperty] private string _secondaryColor = "#2196F3";
    [ObservableProperty] private string _accentColor = "#1976D2";
}
