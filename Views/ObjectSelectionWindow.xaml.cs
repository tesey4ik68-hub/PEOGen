using System.Windows;
using System.Windows.Input;

namespace AGenerator.Views
{
    public partial class ObjectSelectionWindow : Window
    {
        public ObjectSelectionWindow()
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

        // Закрыть окно
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }

    /// <summary>
    /// Конвертер: true -> Collapsed, false -> Visible (инверсия BooleanToVisibility)
    /// </summary>
    public class InverseBooleanToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Visible;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            return false;
        }
    }
}
