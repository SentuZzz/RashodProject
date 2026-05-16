using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using WpfApp1.Helpers;
using WpfApp1.Models;
using WpfApp1.Repositories;

namespace WpfApp1.ViewModels
{
    public class HomeViewModel : ViewModelBase
    {
        private readonly StatusRepository _statusRepository;
        private readonly SoldierRepository _soldierRepository;
        private DispatcherTimer _timer;
        private readonly DutyRepository _dutyRepository;

        private ObservableCollection<NotificationModel> _upcomingEvents;
        public ObservableCollection<NotificationModel> UpcomingEvents
        {
            get => _upcomingEvents;
            set { _upcomingEvents = value; OnPropertyChanged(); }
        }

        private string _currentTime;
        public string CurrentTime
        {
            get => _currentTime;
            set { _currentTime = value; OnPropertyChanged(); }
        }

        private ObservableCollection<ActiveDutyCardModel> _activeDutyCards;
        public ObservableCollection<ActiveDutyCardModel> ActiveDutyCards
        {
            get => _activeDutyCards;
            set { _activeDutyCards = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DashboardDutyModel> _planningDuties;
        public ObservableCollection<DashboardDutyModel> PlanningDuties
        {
            get => _planningDuties;
            set
            {
                _planningDuties = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VisiblePlanningDuties));
            }
        }

        private bool _isChecklistExpanded;
        public bool IsChecklistExpanded
        {
            get => _isChecklistExpanded;
            set
            {
                _isChecklistExpanded = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(VisiblePlanningDuties));
                OnPropertyChanged(nameof(ToggleChecklistText));
            }
        }

        public string ToggleChecklistText => IsChecklistExpanded ? "Свернуть" : "Развернуть";
        public IEnumerable<DashboardDutyModel> VisiblePlanningDuties => IsChecklistExpanded ? PlanningDuties : PlanningDuties?.Where(d => d.Missing > 0).Take(2);

        public ICommand ToggleChecklistCommand { get; }

        private string _currentDate;
        public string CurrentDate
        {
            get => _currentDate;
            set { _currentDate = value; OnPropertyChanged(); }
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set { _totalCount = value; OnPropertyChanged(); }
        }

        private int _inFormationCount;
        public int InFormationCount
        {
            get => _inFormationCount;
            set { _inFormationCount = value; OnPropertyChanged(); }
        }

        private int _absentCount;
        public int AbsentCount
        {
            get => _absentCount;
            set { _absentCount = value; OnPropertyChanged(); }
        }

        private int _onDutyCount;
        public int OnDutyCount
        {
            get => _onDutyCount;
            set { _onDutyCount = value; OnPropertyChanged(); }
        }

        private int _vmpCount;
        public int VmpCount
        {
            get => _vmpCount;
            set
            {
                _vmpCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsVmpVisible));
                OnPropertyChanged(nameof(GridColumnsCount));
            }
        }

        public Visibility IsVmpVisible => VmpCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        public int GridColumnsCount => VmpCount > 0 ? 5 : 4;

        private ObservableCollection<DashboardDutyModel> _tomorrowDuties;
        public ObservableCollection<DashboardDutyModel> TomorrowDuties
        {
            get => _tomorrowDuties;
            set { _tomorrowDuties = value; OnPropertyChanged(); }
        }

        public HomeViewModel()
        {
            ToggleChecklistCommand = new ViewModelCommand(o => IsChecklistExpanded = !IsChecklistExpanded);

            _soldierRepository = new SoldierRepository();
            _dutyRepository = new DutyRepository();
            _statusRepository = new StatusRepository();

            StartClock();
            LoadStatistics();

            AppMessenger.DirectoriesUpdated += () => LoadStatistics();
            AppMessenger.DutiesUpdated += () => LoadStatistics();
        }

        private void StartClock()
        {
            UpdateDateTime();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (sender, args) => UpdateDateTime();
            _timer.Start();
        }

        private void UpdateDateTime()
        {
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            CurrentDate = DateTime.Now.ToString("dd MMMM yyyy, dddd");
        }

        private void LoadStatistics()
        {
            var soldiers = _soldierRepository.GetAllSoldiers();

            TotalCount = soldiers.Count;

            OnDutyCount = soldiers.Count(s => s.CurrentStatus == "В наряде");
            AbsentCount = soldiers.Count(s => s.CurrentStatus != "В строю" && s.CurrentStatus != "В наряде");
            VmpCount = soldiers.Count(s => s.CurrentStatus == "В строю" && s.UnitName != null &&
                                          (s.UnitName.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           s.UnitName.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                           s.UnitName.IndexOf("КМБ", StringComparison.OrdinalIgnoreCase) >= 0));

            InFormationCount = TotalCount - OnDutyCount - AbsentCount - VmpCount;

            DateTime now = DateTime.Now;
            DateTime activeShiftDate = now.Hour < 16 ? now.Date.AddDays(-1) : now.Date;
            DateTime planningDate = activeShiftDate.AddDays(1);

            TomorrowDuties = new ObservableCollection<DashboardDutyModel>(_dutyRepository.GetTomorrowDutiesStatus());
            UpcomingEvents = new ObservableCollection<NotificationModel>(_statusRepository.GetUpcomingNotifications(3));
            ActiveDutyCards = new ObservableCollection<ActiveDutyCardModel>(_dutyRepository.GetActiveDutiesForDate(activeShiftDate));
            PlanningDuties = new ObservableCollection<DashboardDutyModel>(_dutyRepository.GetDutiesStatusForDate(planningDate));
        }
    }
}