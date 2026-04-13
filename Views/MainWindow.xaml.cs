using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AGenerator.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    // Перетаскивание окна за шапку
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            this.DragMove();
        }
    }

    // Свернуть окно
    private void Minimize_Click(object sender, RoutedEventArgs e)
    {
        this.WindowState = WindowState.Minimized;
    }

    // Развернуть/Восстановить
    private void MaximizeRestore_Click(object sender, RoutedEventArgs e)
    {
        if (this.WindowState == WindowState.Maximized)
        {
            this.WindowState = WindowState.Normal;
            MaximizeRestoreBtn.Content = "☐";
        }
        else
        {
            this.WindowState = WindowState.Maximized;
            MaximizeRestoreBtn.Content = "❐";
        }
    }

    // Закрыть окно
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new Window
        {
            Title = "⚙️ Настройки",
            Width = 800,
            Height = 560,
            MinWidth = 600,
            MinHeight = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = this,
            ResizeMode = ResizeMode.CanResize,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            Content = CreateSettingsViewContent()
        };
        settingsWindow.ShowDialog();
    }

    /// <summary>
    /// Создаёт содержимое окна настроек с современным стилем
    /// </summary>
    private System.Windows.Controls.Grid CreateSettingsViewContent()
    {
        var settingsView = new SettingsView();

        // Внешний контейнер с тенью
        var outerBorder = new System.Windows.Controls.Border
        {
            Background = System.Windows.Media.Brushes.Transparent,
            Margin = new Thickness(10)
        };
        outerBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = System.Windows.Media.Colors.Black,
            Opacity = 0.3,
            BlurRadius = 25,
            ShadowDepth = 0
        };

        // Основной контейнер с закругленными краями
        var mainBorder = new System.Windows.Controls.Border
        {
            Background = Application.Current.Resources["MainGradient"] as System.Windows.Media.Brush,
            CornerRadius = new CornerRadius(12)
        };

        var grid = new System.Windows.Controls.Grid();
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = System.Windows.GridLength.Auto });
        grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });

        // Шапка (перетаскиваемая)
        var headerBorder = new System.Windows.Controls.Border
        {
            Background = Application.Current.Resources["GlassBrush"] as System.Windows.Media.Brush,
            BorderBrush = Application.Current.Resources["GlassBorderBrush"] as System.Windows.Media.Brush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            CornerRadius = new CornerRadius(12, 12, 0, 0),
            Padding = new Thickness(16, 10, 16, 10)
        };
        headerBorder.MouseLeftButtonDown += (s, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                Window.GetWindow(headerBorder)?.DragMove();
            }
        };

        var headerGrid = new System.Windows.Controls.Grid();
        headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new System.Windows.GridLength(1, System.Windows.GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = System.Windows.GridLength.Auto });

        // Заголовок
        var titlePanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        titlePanel.Children.Add(new TextBlock
        {
            Text = "⚙️",
            FontSize = 20,
            Margin = new Thickness(0, 0, 10, 0),
            VerticalAlignment = VerticalAlignment.Center
        });
        titlePanel.Children.Add(new TextBlock
        {
            Text = "Настройки",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = Application.Current.Resources["TextPrimaryBrush"] as System.Windows.Media.Brush,
            VerticalAlignment = VerticalAlignment.Center
        });
        System.Windows.Controls.Grid.SetColumn(titlePanel, 0);
        headerGrid.Children.Add(titlePanel);

        // Кнопки управления
        var buttonsPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

        // Свернуть
        var minimizeBtn = new Button
        {
            Content = "─",
            Width = 46,
            Height = 32,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontSize = 12,
            Foreground = Application.Current.Resources["TextPrimaryBrush"] as System.Windows.Media.Brush,
            ToolTip = "Свернуть"
        };
        minimizeBtn.Click += (s, e) =>
        {
            var window = Window.GetWindow(minimizeBtn);
            if (window != null) window.WindowState = WindowState.Minimized;
        };

        // Закрыть
        var closeBtn = new Button
        {
            Content = "✕",
            Width = 46,
            Height = 32,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            FontSize = 12,
            Foreground = Application.Current.Resources["TextPrimaryBrush"] as System.Windows.Media.Brush,
            ToolTip = "Закрыть"
        };
        closeBtn.Click += (s, e) =>
        {
            var window = Window.GetWindow(closeBtn);
            if (window != null) window.Close();
        };

        buttonsPanel.Children.Add(minimizeBtn);
        buttonsPanel.Children.Add(closeBtn);
        System.Windows.Controls.Grid.SetColumn(buttonsPanel, 1);
        headerGrid.Children.Add(buttonsPanel);

        headerBorder.Child = headerGrid;
        System.Windows.Controls.Grid.SetRow(headerBorder, 0);
        grid.Children.Add(headerBorder);

        // Содержимое настроек
        System.Windows.Controls.Grid.SetRow(settingsView, 1);
        grid.Children.Add(settingsView);

        mainBorder.Child = grid;
        outerBorder.Child = mainBorder;

        return new System.Windows.Controls.Grid { Children = { outerBorder } };
    }
}
