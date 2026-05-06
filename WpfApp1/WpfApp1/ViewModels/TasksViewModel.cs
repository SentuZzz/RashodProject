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
    public class TaskSoldierSelectionModel : ViewModelBase
    {
        public SoldierModel Soldier { get; set; }
        public Dictionary<DateTime, string> BusyDates { get; set; } = new Dictionary<DateTime, string>();

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        private bool _isGoingOnDuty;
        public bool IsGoingOnDuty
        {
            get => _isGoingOnDuty;
            set
            {
                _isGoingOnDuty = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(TooltipText));
            }
        }

        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }

        private string _conflictReason = "";
        public string ConflictReason
        {
            get => _conflictReason;
            set { _conflictReason = value; OnPropertyChanged(nameof(TooltipText)); }
        }

        public string StatusIcon => IsGoingOnDuty ? "ShieldHalved" : "CheckCircle";
        public string StatusColor => IsGoingOnDuty ? "#F59E0B" : "#10B981";
        public string TooltipText => IsGoingOnDuty ? $"Занят: {ConflictReason}" : "Свободен для задач";
    }

    public class TasksViewModel : ViewModelBase
    {
        private readonly TaskRepository _taskRepo;
        private readonly SoldierRepository _soldierRepo;
        private readonly DutyRepository _dutyRepo;

        private DateTime _selectedDate = DateTime.Today;
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { _selectedDate = value; OnPropertyChanged(); LoadAvailableSoldiers(); }
        }

        public ObservableCollection<TaskModel> TodoTasks { get; set; }
        public ObservableCollection<TaskModel> InProgressTasks { get; set; }
        public ObservableCollection<TaskModel> DoneTasks { get; set; }
        public ObservableCollection<TaskCategoryModel> Categories { get; set; }

        public ObservableCollection<TaskSoldierSelectionModel> AvailableContractors { get; set; }
        public ObservableCollection<TaskSoldierSelectionModel> AvailableConscripts { get; set; }

        private TaskSoldierSelectionModel _selectedContractor;
        public TaskSoldierSelectionModel SelectedContractor { get => _selectedContractor; set { _selectedContractor = value; OnPropertyChanged(); } }

        private string _newTaskName;
        public string NewTaskName { get => _newTaskName; set { _newTaskName = value; OnPropertyChanged(); } }

        private TaskCategoryModel _selectedCategory;
        public TaskCategoryModel SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }

        private DateTime _newTaskStartDate = DateTime.Today;
        public DateTime NewTaskStartDate { get => _newTaskStartDate; set { _newTaskStartDate = value; OnPropertyChanged(); ValidateSoldiersByDeadline(); } }

        private string _newTaskStartTime = "09:00";
        public string NewTaskStartTime { get => _newTaskStartTime; set { _newTaskStartTime = value; OnPropertyChanged(); } }

        private DateTime? _newTaskDeadline;
        public DateTime? NewTaskDeadline { get => _newTaskDeadline; set { _newTaskDeadline = value; OnPropertyChanged(); ValidateSoldiersByDeadline(); } }

        private string _newTaskDeadlineTime = "18:00";
        public string NewTaskDeadlineTime { get => _newTaskDeadlineTime; set { _newTaskDeadlineTime = value; OnPropertyChanged(); ValidateSoldiersByDeadline(); } }

        private bool _isEditing;
        public bool IsEditing { get => _isEditing; set { _isEditing = value; OnPropertyChanged(); OnPropertyChanged(nameof(PanelTitle)); OnPropertyChanged(nameof(SubmitButtonText)); } }

        private int _editingTaskId;
        public string PanelTitle => IsEditing ? "Редактирование задачи" : "Новая задача";
        public string SubmitButtonText => IsEditing ? "Сохранить изменения" : "Создать задачу";

        public ICommand SaveTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }
        public ICommand EditTaskCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand EndOfDayCommand { get; }

        public TasksViewModel()
        {
            _taskRepo = new TaskRepository();
            _soldierRepo = new SoldierRepository();
            _dutyRepo = new DutyRepository();

            SaveTaskCommand = new ViewModelCommand(ExecuteSaveTask, CanExecuteSaveTask);
            DeleteTaskCommand = new ViewModelCommand(ExecuteDeleteTask);
            EditTaskCommand = new ViewModelCommand(ExecuteEditTask);
            CancelEditCommand = new ViewModelCommand(o => ResetForm());
            EndOfDayCommand = new ViewModelCommand(ExecuteEndOfDay);

            LoadData();
            AppMessenger.DirectoriesUpdated += () => LoadData();
        }

        public void LoadData()
        {
            var allTasks = _taskRepo.GetAllTasks();
            TodoTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "К выполнению"));
            InProgressTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "В процессе"));
            DoneTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "Выполнено"));
            Categories = new ObservableCollection<TaskCategoryModel>(_taskRepo.GetCategories());

            OnPropertyChanged(nameof(TodoTasks)); OnPropertyChanged(nameof(InProgressTasks)); OnPropertyChanged(nameof(DoneTasks)); OnPropertyChanged(nameof(Categories));
            LoadAvailableSoldiers();
        }

        private void LoadAvailableSoldiers()
        {
            var inFormation = _soldierRepo.GetAllSoldiers().Where(s =>
                s.CurrentStatus == "В строю" &&
                !s.IsOnActiveDuty &&
                (s.UnitName == null ||
                (s.UnitName.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) < 0 &&
                 s.UnitName.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) < 0 &&
                 s.UnitName.IndexOf("КМБ", StringComparison.OrdinalIgnoreCase) < 0))
            ).ToList();

            var contractors = new ObservableCollection<TaskSoldierSelectionModel>();
            var conscripts = new ObservableCollection<TaskSoldierSelectionModel>();

            contractors.Add(new TaskSoldierSelectionModel { Soldier = new SoldierModel { RankName = "", LastName = "Не назначать ответственного", SoldierID = 0 }, IsEnabled = true });

            foreach (var soldier in inFormation)
            {
                var busyDates = _dutyRepo.GetBusyDatesWithInfoForSoldier(soldier.SoldierID);
                var model = new TaskSoldierSelectionModel { Soldier = soldier, BusyDates = busyDates, IsEnabled = true };

                if (soldier.ServiceType == "По контракту") contractors.Add(model);
                else conscripts.Add(model);
            }

            AvailableContractors = contractors;
            AvailableConscripts = conscripts;

            if (!IsEditing) SelectedContractor = contractors.First();

            OnPropertyChanged(nameof(AvailableContractors)); OnPropertyChanged(nameof(AvailableConscripts));
            ValidateSoldiersByDeadline();
        }

        private void ValidateSoldiersByDeadline()
        {
            if (AvailableConscripts == null || AvailableContractors == null) return;

            DateTime startDate = NewTaskStartDate.Date;
            DateTime endDate = NewTaskDeadline.HasValue ? NewTaskDeadline.Value.Date : startDate;

            bool isLate = false;
            string[] timeFormats = { @"h\:mm", @"hh\:mm" };

            if (NewTaskDeadline.HasValue)
            {
                if (TimeSpan.TryParseExact(NewTaskDeadlineTime, timeFormats, null, out TimeSpan parsedTime))
                {
                    isLate = parsedTime.Hours >= 15;
                }
            }

            Action<TaskSoldierSelectionModel> validate = s => {
                if (s.Soldier.SoldierID == 0) return; // Пропускаем заглушку "Не назначать"

                bool hasConflict = false;
                string reason = "";

                // Проверяем каждый день задачи на пересечение с занятостью бойца
                for (DateTime d = startDate; d <= endDate; d = d.AddDays(1))
                {
                    if (s.BusyDates.ContainsKey(d))
                    {
                        // Исключение: Если конфликт - это наряд в ПОСЛЕДНИЙ день задачи, 
                        // и задача заканчивается до 15:00, то боец успеет её выполнить.
                        if (d == endDate && s.BusyDates[d].Contains("Наряд") && !isLate)
                        {
                            continue;
                        }

                        hasConflict = true;
                        reason = s.BusyDates[d];
                        break;
                    }
                }

                if (hasConflict)
                {
                    s.IsEnabled = false;
                    s.IsSelected = false;
                    s.ConflictReason = reason;
                    s.IsGoingOnDuty = true;
                }
                else
                {
                    s.IsEnabled = true;
                    s.ConflictReason = "";
                    s.IsGoingOnDuty = false;
                }
            };

            foreach (var s in AvailableConscripts) validate(s);
            foreach (var s in AvailableContractors) validate(s);

            if (SelectedContractor != null && !SelectedContractor.IsEnabled) SelectedContractor = AvailableContractors.First();
        }

        private bool CanExecuteSaveTask(object obj) => !string.IsNullOrWhiteSpace(NewTaskName) && SelectedCategory != null;

        private void ExecuteSaveTask(object obj)
        {
            string[] timeFormats = { @"h\:mm", @"hh\:mm" };

            if (!TimeSpan.TryParseExact(NewTaskStartTime, timeFormats, null, out TimeSpan startTime))
            {
                MessageBox.Show("Некорректный формат времени начала! Используйте формат ЧЧ:ММ (например, 09:00).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TimeSpan? deadlineTime = null;
            if (NewTaskDeadline.HasValue)
            {
                if (!TimeSpan.TryParseExact(NewTaskDeadlineTime, timeFormats, null, out TimeSpan dt))
                {
                    MessageBox.Show("Некорректный формат времени дедлайна! Используйте формат ЧЧ:ММ (например, 18:00).", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                deadlineTime = dt;
            }

            DateTime start = NewTaskStartDate.Date + startTime;
            DateTime? deadline = NewTaskDeadline.HasValue ? NewTaskDeadline.Value.Date + deadlineTime.Value : (DateTime?)null;

            var task = new TaskModel
            {
                TaskHistoryID = _editingTaskId,
                TaskName = NewTaskName?.Trim(),
                CategoryID = SelectedCategory.CategoryID,
                CreationDate = start,
                DueDate = deadline,
                Status = "К выполнению"
            };

            var selectedIds = AvailableConscripts.Where(s => s.IsSelected).Select(s => s.Soldier.SoldierID).ToList();
            if (SelectedContractor != null && SelectedContractor.Soldier.SoldierID != 0) selectedIds.Add(SelectedContractor.Soldier.SoldierID);

            if (IsEditing) _taskRepo.UpdateTask(task, selectedIds);
            else _taskRepo.CreateTask(task, selectedIds);

            ResetForm();
            LoadData();
        }

        private void ExecuteEditTask(object obj)
        {
            if (obj is TaskModel task)
            {
                IsEditing = true;
                _editingTaskId = task.TaskHistoryID;
                NewTaskName = task.TaskName;
                SelectedCategory = Categories.FirstOrDefault(c => c.CategoryID == task.CategoryID);

                NewTaskStartDate = task.CreationDate.Date;
                NewTaskStartTime = task.CreationDate.ToString("HH:mm");

                if (task.DueDate.HasValue)
                {
                    NewTaskDeadline = task.DueDate.Value.Date;
                    NewTaskDeadlineTime = task.DueDate.Value.ToString("HH:mm");
                }
                else
                {
                    NewTaskDeadline = null;
                    NewTaskDeadlineTime = string.Empty;
                }

                foreach (var s in AvailableConscripts)
                    s.IsSelected = task.AssignedSoldiers.Any(x => x.SoldierID == s.Soldier.SoldierID);

                var contractor = task.AssignedSoldiers.FirstOrDefault(x => x.ServiceType == "По контракту");
                SelectedContractor = contractor != null
                    ? AvailableContractors.FirstOrDefault(c => c.Soldier.SoldierID == contractor.SoldierID) ?? AvailableContractors.First()
                    : AvailableContractors.First();
            }
        }

        private void ResetForm()
        {
            IsEditing = false; NewTaskName = string.Empty; SelectedCategory = null; NewTaskDeadline = null;
            NewTaskStartDate = DateTime.Today; NewTaskStartTime = "09:00"; NewTaskDeadlineTime = "18:00";
            foreach (var s in AvailableConscripts) s.IsSelected = false;
            SelectedContractor = AvailableContractors.First();
        }

        public void ChangeTaskStatus(int taskId, string newStatus) { _taskRepo.UpdateTaskStatus(taskId, newStatus); LoadData(); }

        private void ExecuteDeleteTask(object obj)
        {
            if (obj is int taskId)
            {
                if (MessageBox.Show("Удалить эту задачу навсегда?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _taskRepo.DeleteTask(taskId);
                    if (IsEditing && _editingTaskId == taskId) ResetForm();
                    LoadData();
                }
            }
        }

        private void ExecuteEndOfDay(object obj)
        {
            if (DateTime.Now.Hour < 20)
            {
                MessageBox.Show("Подводить итоги еще рано! Сдача дежурства и сдвиг задач доступны только после 20:00.",
                                "Рано", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                "Вы уверены, что хотите подвести итоги дня?\n\n" +
                "• Все выполненные задачи уйдут в Архив\n" +
                "• Задачи 'В процессе' будут отмечены как Выполненные\n" +
                "• Новые задачи перейдут в работу ('В процессе')",
                "Сдача дежурства", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult == MessageBoxResult.Yes)
            {
                _taskRepo.ShiftTasksForNewDay();
                LoadData();
                MessageBox.Show("Сутки успешно закрыты! Задачи сдвинуты по конвейеру.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}