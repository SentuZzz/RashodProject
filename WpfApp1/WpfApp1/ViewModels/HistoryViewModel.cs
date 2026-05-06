using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Repositories;

namespace WpfApp1.ViewModels
{
    public class HistoryViewModel : ViewModelBase
    {
        private readonly SoldierRepository _soldierRepo;
        private readonly TaskRepository _taskRepo;
        private readonly DutyRepository _dutyRepo;

        // --- Вкладка 1: Дембеля ---
        private ObservableCollection<SoldierModel> _dismissedSoldiers;
        public ObservableCollection<SoldierModel> DismissedSoldiers
        {
            get => _dismissedSoldiers;
            set { _dismissedSoldiers = value; OnPropertyChanged(); }
        }

        // --- Вкладка 2: История нарядов ---
        private DateTime _selectedArchiveDate = DateTime.Today.AddDays(-1);
        public DateTime SelectedArchiveDate
        {
            get => _selectedArchiveDate;
            set { _selectedArchiveDate = value; OnPropertyChanged(); LoadArchiveDuties(); }
        }

        private ObservableCollection<ActiveDutyCardModel> _archiveDuties;
        public ObservableCollection<ActiveDutyCardModel> ArchiveDuties
        {
            get => _archiveDuties;
            set { _archiveDuties = value; OnPropertyChanged(); }
        }

        // --- Вкладка 3: Архив задач ---
        private ObservableCollection<TaskModel> _archivedTasks;
        public ObservableCollection<TaskModel> ArchivedTasks
        {
            get => _archivedTasks;
            set { _archivedTasks = value; OnPropertyChanged(); }
        }

        public HistoryViewModel()
        {
            _soldierRepo = new SoldierRepository();
            _taskRepo = new TaskRepository();
            _dutyRepo = new DutyRepository();

            LoadData();
        }

        public void LoadData()
        {
            // 1. Вычисляем дембелей (разница между списком "Все" и списком "В строю")
            var allSoldiers = _soldierRepo.GetAllSoldiers(null, true);
            var activeSoldiers = _soldierRepo.GetAllSoldiers(null, false);
            var activeIds = activeSoldiers.Select(s => s.SoldierID).ToHashSet();

            var dismissed = allSoldiers.Where(s => !activeIds.Contains(s.SoldierID)).ToList();
            DismissedSoldiers = new ObservableCollection<SoldierModel>(dismissed);

            // 2. Загружаем архивные задачи
            var allTasks = _taskRepo.GetAllTasks();
            // Берем только "Выполнено" (если забыли сдвинуть) и "В архиве"
            var oldTasks = allTasks.Where(t => t.Status == "В архиве" || t.Status == "Выполнено")
                                   .OrderByDescending(t => t.CreationDate)
                                   .ToList();
            ArchivedTasks = new ObservableCollection<TaskModel>(oldTasks);

            // 3. Загружаем наряды на выбранную дату
            LoadArchiveDuties();
        }

        private void LoadArchiveDuties()
        {
            // Используем уже готовый метод из репозитория
            ArchiveDuties = new ObservableCollection<ActiveDutyCardModel>(_dutyRepo.GetActiveDutiesForDate(SelectedArchiveDate));
        }
    }
}