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
        public DateTime MinAllowedDate => DateTime.Today; // ИСПРАВЛЕНИЕ: Теперь минимальная дата - сегодня

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

        private DateTime _selectedDate;
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

            // Инициализируем SelectedDate без вызова сеттера, чтобы избежать двойной загрузки
            _selectedDate = DateTime.Today;
            CustomAssignDate = DateTime.Today;

            UpdateDailyStatus();
            LoadSoldiers(); // Заменили дублирующийся LoadData() на LoadSoldiers()

            AppMessenger.DirectoriesUpdated += () =>
            {
                _allDuties = _dutyRepository.GetDuties();
                LoadSoldiers();
                UpdateDailyStatus();
            };
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

            // ИСПРАВЛЕНИЕ: Строгая проверка на прошлое (раньше пропускало -1 день)
            if (targetDate.Date < DateTime.Today)
            {
                MessageBox.Show("Нельзя назначить наряд в прошлое!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                int capacity = SelectedDuty.Capacity;
                int duration = SelectedDuty.Duration > 0 ? SelectedDuty.Duration : 1;

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
            AvailableDuties.Clear();
            SelectedDuty = null;
            if (_selectedSoldier == null) return;

            string rank = _selectedSoldier.RankName?.ToLower() ?? "";
            string pos = _selectedSoldier.PositionName?.ToLower() ?? "";

            // Определяем военный статус бойца
            bool isOfficerOrWarrant = rank.Contains("лейтенант") || rank.Contains("капитан") || rank.Contains("майор") || rank.Contains("прапорщик") || rank.Contains("старшина");
            bool isSergeant = rank.Contains("сержант");
            bool isCommander = pos.Contains("командир роты") || pos.Contains("командир взвода");

            foreach (var duty in _allDuties)
            {
                string dutyName = duty.DutyName.ToLower();

                // Офицеры и Командиры: Только самые ответственные наряды (Приоритет 1 или по названию)
                if (isOfficerOrWarrant || isCommander)
                {
                    if (duty.RolePriority == 1 || dutyName.Contains("ответственный") || dutyName.Contains("дежурный по роте"))
                    {
                        AvailableDuties.Add(duty);
                    }
                }
                // Сержанты: Дежурные по КПП, Начальники караула (Приоритет 2)
                else if (isSergeant)
                {
                    if (duty.RolePriority == 2 || dutyName.Contains("начальник караула") || dutyName.Contains("дежурный по кпп") || dutyName.Contains("помощник"))
                    {
                        AvailableDuties.Add(duty);
                    }
                }
                // Рядовые и Ефрейторы: Дневальные, караульные, патруль, рабочие (Приоритет 3+)
                else
                {
                    // Рядовой не может быть "Дежурным" или "Начальником"
                    if (duty.RolePriority >= 3 || (!dutyName.Contains("дежурный") && !dutyName.Contains("ответственный") && !dutyName.Contains("начальник")))
                    {
                        AvailableDuties.Add(duty);
                    }
                }
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

        private void RefreshView() { SelectedSoldier = null; SelectedDuty = null; LoadSoldiers(); }
    }
}