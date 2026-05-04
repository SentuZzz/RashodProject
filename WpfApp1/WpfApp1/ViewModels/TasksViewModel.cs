using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Repositories;

namespace WpfApp1.ViewModels
{
    public class TaskSoldierSelectionModel : ViewModelBase
    {
        public SoldierModel Soldier { get; set; }

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }

        public bool IsGoingOnDuty { get; set; }

        private bool _isEnabled = true;
        public bool IsEnabled { get => _isEnabled; set { _isEnabled = value; OnPropertyChanged(); } }

        public string StatusIcon => IsGoingOnDuty ? "ShieldHalved" : "CheckCircle";
        public string StatusColor => IsGoingOnDuty ? "#F59E0B" : "#10B981";
        public string TooltipText => IsGoingOnDuty ? "Заступает в наряд (доступен до 15:00)" : "Свободен для задач";
    }

    public class TasksViewModel : ViewModelBase
    {
        private readonly TaskRepository _taskRepo;
        private readonly SoldierRepository _soldierRepo;
        private readonly DutyRepository _dutyRepo;

        public ObservableCollection<TaskModel> TodoTasks { get; set; }
        public ObservableCollection<TaskModel> InProgressTasks { get; set; }
        public ObservableCollection<TaskModel> DoneTasks { get; set; }

        public ObservableCollection<TaskCategoryModel> Categories { get; set; }

        // РАЗДЕЛЕНИЕ НА КОНТРАКТНИКОВ И СРОЧНИКОВ
        public ObservableCollection<TaskSoldierSelectionModel> AvailableContractors { get; set; }
        public ObservableCollection<TaskSoldierSelectionModel> AvailableConscripts { get; set; }

        private TaskSoldierSelectionModel _selectedContractor;
        public TaskSoldierSelectionModel SelectedContractor { get => _selectedContractor; set { _selectedContractor = value; OnPropertyChanged(); } }

        private string _newTaskName;
        public string NewTaskName { get => _newTaskName; set { _newTaskName = value; OnPropertyChanged(); } }

        private TaskCategoryModel _selectedCategory;
        public TaskCategoryModel SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }

        private DateTime _newTaskStartDate = DateTime.Today;
        public DateTime NewTaskStartDate { get => _newTaskStartDate; set { _newTaskStartDate = value; OnPropertyChanged(); } }

        private DateTime? _newTaskDeadline;
        public DateTime? NewTaskDeadline { get => _newTaskDeadline; set { _newTaskDeadline = value; OnPropertyChanged(); ValidateSoldiersByDeadline(); } }

        public ICommand CreateTaskCommand { get; }
        public ICommand DeleteTaskCommand { get; }

        public TasksViewModel()
        {
            _taskRepo = new TaskRepository();
            _soldierRepo = new SoldierRepository();
            _dutyRepo = new DutyRepository();

            CreateTaskCommand = new ViewModelCommand(ExecuteCreateTask, CanExecuteCreateTask);
            DeleteTaskCommand = new ViewModelCommand(ExecuteDeleteTask);

            LoadData();
        }

        public void LoadData()
        {
            var allTasks = _taskRepo.GetAllTasks();
            TodoTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "К выполнению"));
            InProgressTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "В процессе"));
            DoneTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "Выполнено"));

            Categories = new ObservableCollection<TaskCategoryModel>(_taskRepo.GetCategories());
            LoadAvailableSoldiers();

            OnPropertyChanged(nameof(TodoTasks)); OnPropertyChanged(nameof(InProgressTasks)); OnPropertyChanged(nameof(DoneTasks)); OnPropertyChanged(nameof(Categories));
        }

        private void LoadAvailableSoldiers()
        {
            var inFormation = _soldierRepo.GetAllSoldiers().Where(s => s.CurrentStatus == "В строю" && !s.IsOnActiveDuty).ToList();

            var contractors = new ObservableCollection<TaskSoldierSelectionModel>();
            var conscripts = new ObservableCollection<TaskSoldierSelectionModel>();
            DateTime nextShiftDate = DateTime.Now.Hour < 16 ? DateTime.Today : DateTime.Today.AddDays(1);

            // Пустой вариант для комбобокса (если ответственный не нужен)
            contractors.Add(new TaskSoldierSelectionModel { Soldier = new SoldierModel { LastName = "Не назначать", SoldierID = 0 }, IsEnabled = true });

            foreach (var soldier in inFormation)
            {
                bool isGoingOnDuty = _dutyRepo.GetBusyDatesWithInfoForSoldier(soldier.SoldierID).ContainsKey(nextShiftDate);
                var model = new TaskSoldierSelectionModel { Soldier = soldier, IsGoingOnDuty = isGoingOnDuty, IsEnabled = true };

                if (soldier.ServiceType == "По контракту") contractors.Add(model);
                else conscripts.Add(model);
            }

            AvailableContractors = contractors;
            AvailableConscripts = conscripts;
            SelectedContractor = contractors.First();

            OnPropertyChanged(nameof(AvailableContractors)); OnPropertyChanged(nameof(AvailableConscripts));
        }

        private void ValidateSoldiersByDeadline()
        {
            if (AvailableConscripts == null || AvailableContractors == null) return;
            bool isLate = NewTaskDeadline.HasValue && NewTaskDeadline.Value.Hour >= 15; // Правило 16:00 (с запасом)

            Action<TaskSoldierSelectionModel> validate = s => {
                if (isLate && s.IsGoingOnDuty) { s.IsEnabled = false; s.IsSelected = false; } else s.IsEnabled = true;
            };

            foreach (var s in AvailableConscripts) validate(s);
            foreach (var s in AvailableContractors) validate(s);

            if (SelectedContractor != null && !SelectedContractor.IsEnabled) SelectedContractor = AvailableContractors.First();
        }

        private bool CanExecuteCreateTask(object obj) => !string.IsNullOrWhiteSpace(NewTaskName) && SelectedCategory != null;

        private void ExecuteCreateTask(object obj)
        {
            var task = new TaskModel { TaskName = NewTaskName, CategoryID = SelectedCategory.CategoryID, CreationDate = NewTaskStartDate, DueDate = NewTaskDeadline, Status = "К выполнению" };

            // Собираем всех: и выбранных срочников, и ответственного контрактника (если ID != 0)
            var selectedIds = AvailableConscripts.Where(s => s.IsSelected).Select(s => s.Soldier.SoldierID).ToList();
            if (SelectedContractor != null && SelectedContractor.Soldier.SoldierID != 0) selectedIds.Add(SelectedContractor.Soldier.SoldierID);

            _taskRepo.CreateTask(task, selectedIds);

            NewTaskName = string.Empty; SelectedCategory = null; NewTaskDeadline = null; NewTaskStartDate = DateTime.Today;
            foreach (var s in AvailableConscripts) s.IsSelected = false;
            SelectedContractor = AvailableContractors.First();

            LoadData();
        }

        public void ChangeTaskStatus(int taskId, string newStatus) { _taskRepo.UpdateTaskStatus(taskId, newStatus); LoadData(); }

        private void ExecuteDeleteTask(object obj)
        {
            if (obj is int taskId)
            {
                if (MessageBox.Show("Удалить эту задачу навсегда?", "Удаление", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _taskRepo.DeleteTask(taskId);
                    LoadData();
                }
            }
        }
    }
}