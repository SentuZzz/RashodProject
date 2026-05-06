using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Helpers;
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
            set { _hideUnavailable = value; OnPropertyChanged(); LoadSoldiers(); }
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

        private DateTime _customAssignDate = DateTime.Today;
        public DateTime CustomAssignDate
        {
            get { return _customAssignDate; }
            set { _customAssignDate = value; OnPropertyChanged(); }
        }

        private bool _isCustomDateVisible;
        public bool IsCustomDateVisible
        {
            get { return _isCustomDateVisible; }
            set { _isCustomDateVisible = value; OnPropertyChanged(); }
        }

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

            AppMessenger.DirectoriesUpdated += () =>
            {
                _allDuties = _dutyRepository.GetDuties();
                LoadData(); LoadSoldiers(); UpdateDailyStatus();
            };
        }

        private void LoadData()
        {
            var data = _repository.GetAllSoldiers().Where(s => s.CurrentStatus == "В строю").ToList();
            if (Soldiers == null) Soldiers = new ObservableCollection<SoldierModel>(data);
            else { Soldiers.Clear(); foreach (var soldier in data) Soldiers.Add(soldier); }
        }

        private void LoadSoldiers()
        {
            bool includeDismissed = SelectedDate.Date < DateTime.Today;
            var allSoldiers = _repository.GetAllSoldiers(SelectedDate, includeDismissed);

            if (HideUnavailable)
            {
                allSoldiers = allSoldiers.Where(s =>
                    s.CurrentStatus == "В строю" &&
                    !s.IsOnActiveDuty &&
                    (s.UnitName == null ||
                    (s.UnitName.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) < 0 &&
                     s.UnitName.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) < 0 &&
                     s.UnitName.IndexOf("КМБ", StringComparison.OrdinalIgnoreCase) < 0))
                ).ToList();
            }
            else
            {
                allSoldiers = allSoldiers.Where(s =>
                    s.UnitName == null ||
                    (s.UnitName.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) < 0 &&
                     s.UnitName.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) < 0 &&
                     s.UnitName.IndexOf("КМБ", StringComparison.OrdinalIgnoreCase) < 0)
                ).ToList();
            }

            if (Soldiers == null) Soldiers = new ObservableCollection<SoldierModel>(allSoldiers);
            else { Soldiers.Clear(); foreach (var soldier in allSoldiers) Soldiers.Add(soldier); }
        }

        private void UpdateDailyStatus()
        {
            DailyDutiesStatus = new ObservableCollection<DashboardDutyModel>(_dutyRepository.GetDutiesStatusForDate(SelectedDate));
        }

        private bool CanExecuteAssignDuty(object obj)
        {
            if (SelectedSoldier == null || SelectedDuty == null) return false;

            bool isVmp = SelectedSoldier.UnitName != null &&
                        (SelectedSoldier.UnitName.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         SelectedSoldier.UnitName.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         SelectedSoldier.UnitName.IndexOf("КМБ", StringComparison.OrdinalIgnoreCase) >= 0);

            return SelectedSoldier.CurrentStatus == "В строю" && !SelectedSoldier.IsOnActiveDuty && !isVmp;
        }

        private void ExecuteAssignDuty(object obj)
        {
            DateTime targetDate = IsCustomDateVisible ? CustomAssignDate : SelectedDate;

            if (targetDate.Date < DateTime.Today.AddDays(-1))
            {
                MessageBox.Show("Нельзя назначить наряд в прошлое!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int capacity = SelectedDuty.Capacity;
                int duration = SelectedDuty.Duration > 0 ? SelectedDuty.Duration : 1; // ИСПРАВЛЕНИЕ: Берем длительность из БД

                var allBusyDatesInfo = _dutyRepository.GetBusyDatesWithInfoForSoldier(SelectedSoldier.SoldierID);

                for (int i = 0; i < duration; i++)
                {
                    DateTime checkDate = targetDate.AddDays(i).Date;
                    if (allBusyDatesInfo.ContainsKey(checkDate))
                    {
                        string reason = allBusyDatesInfo[checkDate];
                        MessageBox.Show($"Боец не может заступить {checkDate.ToShortDateString()}. Причина: {reason}",
                                        "Конфликт", MessageBoxButton.OK, MessageBoxImage.Warning);
                        UpdateBusyDates();
                        return;
                    }

                    if (_dutyRepository.IsCapacityFull(SelectedDuty.DutyID, checkDate, capacity))
                    {
                        MessageBox.Show($"На {checkDate.ToShortDateString()} наряд {SelectedDuty.DutyName} уже укомплектован ({capacity} чел.).", "Квота заполнена", MessageBoxButton.OK, MessageBoxImage.Warning);
                        UpdateBusyDates();
                        return;
                    }
                }

                if (_dutyRepository.HasRestViolation(SelectedSoldier.SoldierID, targetDate, duration))
                {
                    MessageBox.Show($"Нарушение отдыха: Боец заступал вчера (менее 1 суток отдыха).", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                    UpdateBusyDates();
                    return;
                }

                _dutyRepository.AssignDuty(SelectedSoldier.SoldierID, SelectedDuty.DutyID, targetDate, duration);

                string daysText = duration > 1 ? $" на {duration} суток" : "";
                MessageBox.Show($"Боец {SelectedSoldier.LastName} назначен в наряд {SelectedDuty.DutyName}{daysText} (с {targetDate.ToShortDateString()})!",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                IsCustomDateVisible = false; CustomAssignDate = SelectedDate;
                UpdateDailyStatus(); RefreshView();
                AppMessenger.BroadcastDutiesUpdated();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка базы данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterDuties()
        {
            AvailableDuties.Clear(); SelectedDuty = null;
            if (_selectedSoldier == null) return;

            foreach (var duty in _allDuties)
            {
                if (_selectedSoldier.ServiceType == "По контракту" && duty.RolePriority <= 2) AvailableDuties.Add(duty);
                else if (_selectedSoldier.ServiceType == "По призыву" && duty.RolePriority >= 2) AvailableDuties.Add(duty);
            }
        }

        private void UpdateBusyDates()
        {
            if (SelectedSoldier == null)
            {
                BusyDatesInfo = new Dictionary<DateTime, string>(); BusyDates = new ObservableCollection<DateTime>();
                return;
            }
            BusyDatesInfo = _dutyRepository.GetBusyDatesWithInfoForSoldier(SelectedSoldier.SoldierID);
            BusyDates = new ObservableCollection<DateTime>(BusyDatesInfo.Keys);
        }

        private void RefreshView() { SelectedSoldier = null; SelectedDuty = null; LoadData(); }
    }
}