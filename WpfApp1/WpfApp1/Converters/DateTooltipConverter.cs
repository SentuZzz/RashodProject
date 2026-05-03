using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace WpfApp1.Converters
{
    public class DateTooltipConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // Проверяем, что к нам пришла дата и наш словарь с занятыми датами
            if (values.Length == 2 && values[0] is DateTime date && values[1] is Dictionary<DateTime, string> info)
            {
                if (info.TryGetValue(date.Date, out string reason))
                {
                    return reason; // Возвращаем конкретную причину (например, "В наряде: Дежурный по роте")
                }
            }
            return "Военнослужащий уже занят в этот день"; // Запасной вариант
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}