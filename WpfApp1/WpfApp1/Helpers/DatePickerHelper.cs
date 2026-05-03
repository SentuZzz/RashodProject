using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp1.Helpers
{
    public static class DatePickerHelper
    {
        public static readonly DependencyProperty BlackoutDatesProperty =
            DependencyProperty.RegisterAttached("BlackoutDates", typeof(ObservableCollection<DateTime>), typeof(DatePickerHelper),
            new PropertyMetadata(null, OnBlackoutDatesChanged));

        public static void SetBlackoutDates(DependencyObject d, ObservableCollection<DateTime> value) => d.SetValue(BlackoutDatesProperty, value);
        public static ObservableCollection<DateTime> GetBlackoutDates(DependencyObject d) => (ObservableCollection<DateTime>)d.GetValue(BlackoutDatesProperty);

        private static void OnBlackoutDatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is DatePicker datePicker)
            {
                // Отписываемся от старой коллекции (чтобы не было утечек памяти)
                if (e.OldValue is INotifyCollectionChanged oldCollection)
                {
                    oldCollection.CollectionChanged -= Collection_Changed;
                }

                datePicker.BlackoutDates.Clear();

                // Подписываемся на новую коллекцию
                if (e.NewValue is ObservableCollection<DateTime> newCollection)
                {
                    UpdateBlackoutDates(datePicker, newCollection);
                    newCollection.CollectionChanged += Collection_Changed;
                }

                // Локальный метод: срабатывает, когда мы вызываем .Add() или .Clear()
                void Collection_Changed(object sender, NotifyCollectionChangedEventArgs args)
                {
                    UpdateBlackoutDates(datePicker, (ObservableCollection<DateTime>)sender);
                }
            }
        }

        private static void UpdateBlackoutDates(DatePicker datePicker, ObservableCollection<DateTime> dates)
        {
            datePicker.BlackoutDates.Clear();
            foreach (var date in dates)
            {
                try
                {
                    datePicker.BlackoutDates.Add(new CalendarDateRange(date));
                }
                catch { /* Игнорируем дубликаты */ }
            }
        }
    }
}