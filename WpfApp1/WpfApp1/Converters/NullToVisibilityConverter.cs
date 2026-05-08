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
            // Проверяем, передали ли нам параметр "Inverse"
            bool isInverse = parameter != null && parameter.ToString().Equals("Inverse", StringComparison.OrdinalIgnoreCase);

            if (value == null)
            {
                // Если Inverse, то при null мы СКРЫВАЕМ элемент
                return isInverse ? Visibility.Collapsed : Visibility.Visible;
            }
            else
            {
                // Если Inverse, то при наличии данных мы ПОКАЗЫВАЕМ элемент
                return isInverse ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}