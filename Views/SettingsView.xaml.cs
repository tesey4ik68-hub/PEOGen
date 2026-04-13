using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AGenerator.ViewModels;

namespace AGenerator.Views;

public partial class SettingsView : UserControl
{
    private readonly SettingsViewModel _viewModel;

    public SettingsView()
    {
        InitializeComponent();

        _viewModel = new SettingsViewModel();
        DataContext = _viewModel;

        // Привязываем ItemsSource для пресетов тем
        ThemesList.ItemsSource = _viewModel.ThemePresets;
    }

    /// <summary>
    /// Обработчик клика по пункту меню категории
    /// </summary>
    private void CategoryMenu_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not string category) return;

        _viewModel.SelectedCategory = category;
    }
}
