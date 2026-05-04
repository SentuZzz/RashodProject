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
        public DateTime MinAllowedDate => DateTime.Today.AddDays(-1);
        private Dictionary<DateTime, string> _busyDatesInfo = new Dictionary<DateTime, string>();
        public Dictionary<DateTime, string> BusyDatesInfo
        {
            get { return _busyDatesInfo; }
            set { _busyDatesInfo = value; OnPropertyChanged(); }
        }
        private readonly SoldierRepository _repository;
        private readonly DutyRepository _dutyRepository;

        private bool _hideUnavailable;
        public bool HideUnavailable
        {
            get => _hideUnavailable;
            set
            {
                _hideUnavailable = value;
                OnPropertyChanged();
                LoadSoldiers();
            }
        }

        private ObservableCollection<DateTime> _busyDates = new ObservableCollection<DateTime>();
        public ObservableCollection<DateTime> BusyDates
        {
            get { return _busyDates; }
            set { _busyDates = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DashboardDutyModel> _dailyDutiesStatus;
        public ObservableCollection<DashboardDutyModel> DailyDutiesStatus
        {
            get => _dailyDutiesStatus;
            set { _dailyDutiesStatus = value; OnPropertyChanged(); }
        }

        // Дата для внеочередного наряда
        private DateTime _customAssignDate = DateTime.Today;
        public DateTime CustomAssignDate
        {
            get { return _customAssignDate; }
            set { _customAssignDate = value; OnPropertyChanged(); }
        }

        // Видимость дополнительного календарика
        private bool _isCustomDateVisible;
        public bool IsCustomDateVisible
        {
            get { return _isCustomDateVisible; }
            set { _isCustomDateVisible = value; OnPropertyChanged(); }
        }

        // Команда для нажатия на кнопку "Плюс"
        public ICommand ToggleCustomDateCommand { get; }
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
            set
            {
                _selectedDate = value;
                CustomAssignDate = value;
                OnPropertyChanged();
                UpdateDailyStatus();
                LoadSoldiers();
            }
        }

        public ICommand AssignDutyCommand { get; }

        public DutiesViewModel()
        {
            _repository = new SoldierRepository();
            _dutyRepository = new DutyRepository();

            ToggleCustomDateCommand = new ViewModelCommand(o => IsCustomDateVisible = !IsCustomDateVisible);

            _allDuties = _dutyRepository.GetDuties();
            AvailableDuties = new ObservableCollection<DutyModel>();

            AssignDutyCommand = new ViewModelCommand(ExecuteAssignDuty, CanExecuteAssignDuty);
            UpdateDailyStatus();
            LoadData();
        }

        private void LoadData()
        {
            var data = _repository.GetAllSoldiers().Where(s => s.CurrentStatus == "В строю").ToList();

            if (Soldiers == null)
            {
                Soldiers = new ObservableCollection<SoldierModel>(data);
            }
            else
            {
                Soldiers.Clear();
                foreach (var soldier in data)
                {
                    Soldiers.Add(soldier);
                }
            }
        }
        private void LoadSoldiers()
        {
            // Передаем глобальную выбранную дату из календаря!
            var allSoldiers = _repository.GetAllSoldiers(SelectedDate);

            // Фильтруем, если нажата галочка
            if (HideUnavailable)
            {
                allSoldiers = allSoldiers.Where(s => s.CurrentStatus == "В строю" && !s.IsOnActiveDuty).ToList();
            }

            if (Soldiers == null)
            {
                Soldiers = new ObservableCollection<SoldierModel>(allSoldiers);
            }
            else
            {
                Soldiers.Clear();
                foreach (var soldier in allSoldiers)
                {
                    Soldiers.Add(soldier);
                }
            }
        }
        private void UpdateDailyStatus()
        {
            DailyDutiesStatus = new ObservableCollection<DashboardDutyModel>(
                _dutyRepository.GetDutiesStatusForDate(SelectedDate));
        }
        private bool CanExecuteAssignDuty(object obj)
        {
            return SelectedSoldier != null && SelectedDuty != null;
        }

        private void ExecuteAssignDuty(object obj)
        {
            DateTime targetDate = IsCustomDateVisible ? CustomAssignDate : SelectedDate;
            if (targetDate.Date < DateTime.Today.AddDays(-1))
            {
                MessageBox.Show("Нельзя назначать наряды задним числом! Разрешены только текущая и предыдущая смены.",
                                "Отмена операции", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var rule = _dutyRepository.DutyRules[SelectedDuty.DutyID];
                int capacity = rule.Capacity;
                int duration = rule.Duration;

                var allBusyDatesInfo = _dutyRepository.GetBusyDatesWithInfoForSoldier(SelectedSoldier.SoldierID);

                for (int i = 0; i < duration; i++)
                {
                    DateTime checkDate = targetDate.AddDays(i).Date;

                    if (allBusyDatesInfo.ContainsKey(checkDate))
                    {
                        string reason = allBusyDatesInfo[checkDate];
                        MessageBox.Show($"Дата {checkDate.ToShortDateString()} недоступна!\nПричина: {reason}",
                                        "Отказ", MessageBoxButton.OK, MessageBoxImage.Warning);
                        UpdateBusyDates();
                        return;
                    }

                    if (_dutyRepository.IsCapacityFull(SelectedDuty.DutyID, checkDate, capacity))
                    {
                        MessageBox.Show($"На {checkDate.ToShortDateString()} наряд «{SelectedDuty.DutyName}» уже полностью укомплектован ({capacity} чел.).", "Нет мест", MessageBoxButton.OK, MessageBoxImage.Warning);
                        UpdateBusyDates();
                        return;
                    }
                }

                if (_dutyRepository.HasRestViolation(SelectedSoldier.SoldierID, targetDate, duration))
                {
                    MessageBox.Show($"Нарушение режима!\nУ военнослужащего возникает накладка на другие наряды, либо не соблюден интервал отдыха (1 сутки до и после наряда).", "Отказ", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UpdateBusyDates();
                    return;
                }

                _dutyRepository.AssignDuty(SelectedSoldier.SoldierID, SelectedDuty.DutyID, targetDate, duration); // <-- Заменили

                string daysText = duration > 1 ? $"на {duration} суток" : "";
                MessageBox.Show($"Боец {SelectedSoldier.LastName} назначен в наряд {SelectedDuty.DutyName} {daysText} (дата заступления {targetDate.ToShortDateString()})!",
                                "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);

                IsCustomDateVisible = false;
                CustomAssignDate = SelectedDate;

                UpdateDailyStatus();
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
            if (SelectedSoldier == null)
            {
                BusyDatesInfo = new Dictionary<DateTime, string>();
                BusyDates = new ObservableCollection<DateTime>();
                return;
            }
            BusyDatesInfo = _dutyRepository.GetBusyDatesWithInfoForSoldier(SelectedSoldier.SoldierID);
            BusyDates = new ObservableCollection<DateTime>(BusyDatesInfo.Keys);
        }

        private void RefreshView()
        {
            SelectedSoldier = null;
            SelectedDuty = null;
            //SelectedDate = DateTime.Today;
            LoadData();
        }
    }
}