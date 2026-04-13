using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AGenerator.Views;

/// <summary>
/// Окно выбора цвета с палитрой
/// </summary>
public partial class ColorPickerWindow : Window
{
    public string SelectedColor { get; private set; } = "#FFFFFF";

    public ColorPickerWindow(string initialColor)
    {
        SelectedColor = initialColor;
        Title = "Выбор цвета";
        Width = 340;
        Height = 450; // Увеличено на 30% (320 * 1.3 = 416)
        MinWidth = 280;
        MinHeight = 350; // Увеличено на 30% (250 * 1.3 = 325)
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.CanResize;
        ShowInTaskbar = false;

        var mainGrid = new Grid();
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Фон с градиентом
        mainGrid.Background = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString("#4FC3F7"),
            (Color)ColorConverter.ConvertFromString("#2196F3"),
            new Point(0, 0), new Point(1, 1));

        // Белая панель
        var panel = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF)),
            CornerRadius = new CornerRadius(12),
            Margin = new Thickness(10),
            Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                Opacity = 0.15,
                BlurRadius = 20,
                ShadowDepth = 0
            }
        };

        var contentPanel = new StackPanel { Margin = new Thickness(15) };

        // Заголовок
        contentPanel.Children.Add(new TextBlock
        {
            Text = "Выберите цвет",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = ParseBrush("#2D3748"),
            Margin = new Thickness(0, 0, 0, 10)
        });

        // Превью
        var previewPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var previewBorder = new Border
        {
            Width = 32,
            Height = 32,
            CornerRadius = new CornerRadius(5),
            Background = ParseBrush(initialColor),
            BorderBrush = Brushes.LightGray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0)
        };
        var colorLabel = new TextBlock
        {
            Text = initialColor,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = new FontFamily("Consolas"),
            Foreground = ParseBrush("#2D3748")
        };
        previewPanel.Children.Add(previewBorder);
        previewPanel.Children.Add(colorLabel);
        contentPanel.Children.Add(previewPanel);

        // Палитра в ScrollViewer
        var scrollViewer = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 234 // Увеличено на 30% (180 * 1.3 = 234)
        };

        var palette = new WrapPanel { Orientation = Orientation.Horizontal };
        var colors = new[]
        {
            "#FF0000", "#FF5722", "#FF9800", "#FFC107", "#FFEB3B", "#CDDC39",
            "#8BC34A", "#4CAF50", "#009688", "#00BCD4", "#03A9F4", "#2196F3",
            "#3F51B5", "#673AB7", "#9C27B0", "#E91E63", "#F44336", "#795548",
            "#9E9E9E", "#607D8B", "#FFFFFF", "#000000", "#4FC3F7", "#2196F3",
            "#66BB6A", "#43A047", "#FFA726", "#FB8C00", "#AB47BC", "#8E24AA",
            "#78909C", "#546E7A", "#1976D2", "#2E7D32", "#E65100", "#6A1B9A",
            "#EF5350", "#EC407A", "#BA68C8", "#7E57C2", "#5C6BC0", "#42A5F5",
            "#29B6F6", "#26C6DA", "#26A69A", "#66BB6A", "#9CCC65", "#D4E157",
            "#FFEE58", "#FFCA28", "#FFA726", "#FF7043", "#8D6E63", "#BDBDBD"
        };

        foreach (var color in colors)
        {
            var btn = new Button
            {
                Width = 28,
                Height = 28,
                Margin = new Thickness(2),
                Background = ParseBrush(color),
                BorderBrush = Brushes.LightGray,
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                ToolTip = color
            };

            btn.Click += (s, e) =>
            {
                SelectedColor = color;
                previewBorder.Background = ParseBrush(color);
                colorLabel.Text = color;
            };

            palette.Children.Add(btn);
        }

        scrollViewer.Content = palette;
        contentPanel.Children.Add(scrollViewer);

        // Кнопки
        var btnPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };

        // Получаем цвет акцента из ресурсов
        var accentColor = Color.FromRgb(0x66, 0x7E, 0xEA); // Значение по умолчанию
        if (Application.Current.Resources.Contains("AccentColor"))
        {
            accentColor = (Color)Application.Current.Resources["AccentColor"];
        }

        // Вычисляем контрастный цвет текста для кнопки
        var luminance = (0.299 * accentColor.R + 0.587 * accentColor.G + 0.114 * accentColor.B) / 255.0;
        var buttonText = luminance < 0.5 ? Brushes.White : ParseBrush("#1A202C");

        var okBtn = new Button
        {
            Content = "✅ OK",
            Padding = new Thickness(18, 7, 18, 7),
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
            Cursor = Cursors.Hand,
            Background = new SolidColorBrush(accentColor),
            Foreground = buttonText,
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold
        };
        okBtn.Click += (s, e) => { DialogResult = true; Close(); };

        var cancelBtn = new Button
        {
            Content = "❌ Отмена",
            Padding = new Thickness(18, 7, 18, 7),
            IsCancel = true,
            Cursor = Cursors.Hand,
            Background = ParseBrush("#E0E0E0"),
            Foreground = ParseBrush("#555"),
            BorderThickness = new Thickness(0),
            FontWeight = FontWeights.SemiBold
        };
        cancelBtn.Click += (s, e) => { DialogResult = false; Close(); };

        btnPanel.Children.Add(okBtn);
        btnPanel.Children.Add(cancelBtn);
        contentPanel.Children.Add(btnPanel);

        panel.Child = contentPanel;
        Grid.SetRow(panel, 0);
        Grid.SetRowSpan(panel, 3);
        mainGrid.Children.Add(panel);

        Content = mainGrid;
    }

    private static Brush ParseBrush(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return Brushes.White; }
    }
}
