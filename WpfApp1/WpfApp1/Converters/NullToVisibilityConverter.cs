using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WpfApp1.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInverse = parameter != null && parameter.ToString().Equals("Inverse", StringComparison.OrdinalIgnoreCase);

            if (value == null)
            {
                return isInverse ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                return isInverse ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}