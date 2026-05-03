using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
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

        private ObservableCollection<DashboardDutyModel> _tomorrowDuties;
        public ObservableCollection<DashboardDutyModel> TomorrowDuties
        {
            get => _tomorrowDuties;
            set { _tomorrowDuties = value; OnPropertyChanged(); }
        }

        public HomeViewModel()
        {
            _soldierRepository = new SoldierRepository();
            _dutyRepository = new DutyRepository();
            _statusRepository = new StatusRepository();
            StartClock();
            LoadStatistics();
        }

        private void StartClock()
        {
            // Обновляем время сразу при запуске
            UpdateDateTime();

            // Создаем таймер, который будет тикать каждую 1 секунду
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (sender, args) => UpdateDateTime();
            _timer.Start();
        }

        private void UpdateDateTime()
        {
            // Формат времени: Часы:Минуты:Секунды (например, 14:05:59)
            CurrentTime = DateTime.Now.ToString("HH:mm:ss");
            // Формат даты: День Месяц Год, День недели (например, 03 Май 2026, воскресенье)
            CurrentDate = DateTime.Now.ToString("dd MMMM yyyy, dddd");
        }

        private void LoadStatistics()
        {
            // Берем список всех бойцов. 
            // Благодаря нашему крутому SQL-запросу, у них уже проставлены актуальные статусы!
            var soldiers = _soldierRepository.GetAllSoldiers();

            TotalCount = soldiers.Count;

            // Считаем тех, у кого статус начинается на "В наряде"
            OnDutyCount = soldiers.Count(s => s.CurrentStatus != null && s.CurrentStatus.StartsWith("В наряде"));

            // Считаем тех, кто в строю
            InFormationCount = soldiers.Count(s => s.CurrentStatus == "В строю");

            // Все остальные (отпуска, госпитали, СОЧ) — это отсутствующие
            AbsentCount = TotalCount - OnDutyCount - InFormationCount;
            TomorrowDuties = new ObservableCollection<DashboardDutyModel>(_dutyRepository.GetTomorrowDutiesStatus());
            UpcomingEvents = new ObservableCollection<NotificationModel>(_statusRepository.GetUpcomingNotifications(3));
        }

    }
}