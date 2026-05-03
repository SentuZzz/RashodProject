using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Repositories;

namespace WpfApp1.ViewModels
{
    public class DutiesViewModel : ViewModelBase
    {
        private readonly SoldierRepository _repository;
        private readonly DutyRepository _dutyRepository;

        private ObservableCollection<DateTime> _busyDates = new ObservableCollection<DateTime>();
        public ObservableCollection<DateTime> BusyDates
        {
            get { return _busyDates; }
            set { _busyDates = value; OnPropertyChanged(); }
        }

        public ObservableCollection<SoldierModel> Soldiers { get; set; }
        public ObservableCollection<DutyModel> AvailableDuties { get; set; }
        private List<DutyModel> _allDuties;

        private SoldierModel _selectedSoldier;
        public SoldierModel SelectedSoldier
        {
            get { return _selectedSoldier; }
            set { _selectedSoldier = value; OnPropertyChanged(); FilterDuties(); UpdateBusyDates(); }
        }

        private DutyModel _selectedDuty;
        public DutyModel SelectedDuty
        {
            get { return _selectedDuty; }
            set { _selectedDuty = value; OnPropertyChanged(); }
        }

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get { return _selectedDate; }
            set { _selectedDate = value; OnPropertyChanged(); }
        }

        public ICommand AssignDutyCommand { get; }

        public DutiesViewModel()
        {
            _repository = new SoldierRepository();
            _dutyRepository = new DutyRepository();

            _allDuties = _dutyRepository.GetDuties();
            AvailableDuties = new ObservableCollection<DutyModel>();

            AssignDutyCommand = new ViewModelCommand(ExecuteAssignDuty, CanExecuteAssignDuty);
            LoadData();
        }

        private void LoadData()
        {
            var data = _repository.GetAllSoldiers();
            Soldiers = new ObservableCollection<SoldierModel>(data);
        }

        private bool CanExecuteAssignDuty(object obj)
        {
            return SelectedSoldier != null && SelectedDuty != null;
        }

        private void ExecuteAssignDuty(object obj)
        {
            try
            {
                var rule = _dutyRepository.DutyRules[SelectedDuty.DutyID];
                int capacity = rule.Capacity;
                int duration = rule.Duration;

                for (int i = 0; i < duration; i++)
                {
                    DateTime checkDate = SelectedDate.AddDays(i);
                    if (_dutyRepository.IsCapacityFull(SelectedDuty.DutyID, checkDate, capacity))
                    {
                        MessageBox.Show($"На {checkDate.ToShortDateString()} наряд «{SelectedDuty.DutyName}» уже полностью укомплектован ({capacity} чел.).", "Нет мест", MessageBoxButton.OK, MessageBoxImage.Warning);
                        UpdateBusyDates();
                        return;
                    }
                }

                if (_dutyRepository.HasRestViolation(SelectedSoldier.SoldierID, SelectedDate, duration))
                {
                    MessageBox.Show($"Нарушение режима!\nУ военнослужащего возникает накладка на другие наряды, либо не соблюден интервал отдыха (1 сутки до и после наряда).", "Отказ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UpdateBusyDates();
                    return;
                }

                _dutyRepository.AssignDuty(SelectedSoldier.SoldierID, SelectedDuty.DutyID, SelectedDate, duration);

                string daysText = duration > 1 ? $"на {duration} суток" : "на 1 сутки";
                MessageBox.Show($"Военнослужащий {SelectedSoldier.LastName} успешно назначен в {SelectedDuty.DutyName} {daysText} (с {SelectedDate.ToShortDateString()})!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при назначении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                UpdateBusyDates();
            }
        }

        private void FilterDuties()
        {
            AvailableDuties.Clear();
            SelectedDuty = null;

            if (_selectedSoldier == null) return;

            foreach (var duty in _allDuties)
            {
                if (_selectedSoldier.ServiceType == "По контракту" && duty.DutyID <= 5)
                {
                    AvailableDuties.Add(duty);
                }
                else if (_selectedSoldier.ServiceType == "По призыву" && duty.DutyID >= 6)
                {
                    AvailableDuties.Add(duty);
                }
            }
        }

        private void UpdateBusyDates()
        {
            if (_selectedSoldier == null)
            {
                BusyDates = new ObservableCollection<DateTime>();
                return;
            }

            var dates = _dutyRepository.GetBusyDatesForSoldier(_selectedSoldier.SoldierID);
            BusyDates = new ObservableCollection<DateTime>(dates);
        }

        private void RefreshView()
        {
            SelectedSoldier = null;
            SelectedDuty = null;
            SelectedDate = DateTime.Today;
            LoadData();
        }
    }
}