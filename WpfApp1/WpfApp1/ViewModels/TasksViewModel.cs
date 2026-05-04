using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Repositories;

namespace WpfApp1.ViewModels
{
    // Вспомогательный класс для списка выбора бойцов с учетом "Правила 16:00"
    public class TaskSoldierSelectionModel : ViewModelBase
    {
        public SoldierModel Soldier { get; set; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public bool IsGoingOnDuty { get; set; } // Заступает ли сегодня?

        // Блокируем выбор, если боец заступает в наряд, а дедлайн позже 15:00
        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public string StatusIcon => IsGoingOnDuty ? "ShieldHalved" : "CheckCircle";
        public string StatusColor => IsGoingOnDuty ? "#F59E0B" : "#10B981";
        public string TooltipText => IsGoingOnDuty ? "Заступает в наряд (доступен до 15:00)" : "Свободен для задач";
    }

    public class TasksViewModel : ViewModelBase
    {
        private readonly TaskRepository _taskRepo;
        private readonly SoldierRepository _soldierRepo;
        private readonly DutyRepository _dutyRepo;

        // Колонки Канбан-доски
        public ObservableCollection<TaskModel> TodoTasks { get; set; }
        public ObservableCollection<TaskModel> InProgressTasks { get; set; }
        public ObservableCollection<TaskModel> DoneTasks { get; set; }

        // Данные для создания задачи
        public ObservableCollection<TaskCategoryModel> Categories { get; set; }
        public ObservableCollection<TaskSoldierSelectionModel> AvailableSoldiers { get; set; }

        private string _newTaskName;
        public string NewTaskName { get => _newTaskName; set { _newTaskName = value; OnPropertyChanged(); } }

        private TaskCategoryModel _selectedCategory;
        public TaskCategoryModel SelectedCategory { get => _selectedCategory; set { _selectedCategory = value; OnPropertyChanged(); } }

        private DateTime? _newTaskDeadline;
        public DateTime? NewTaskDeadline
        {
            get => _newTaskDeadline;
            set
            {
                _newTaskDeadline = value;
                OnPropertyChanged();
                ValidateSoldiersByDeadline(); // Проверяем "Правило 16:00" при смене времени
            }
        }

        public ICommand CreateTaskCommand { get; }

        public TasksViewModel()
        {
            _taskRepo = new TaskRepository();
            _soldierRepo = new SoldierRepository();
            _dutyRepo = new DutyRepository();

            CreateTaskCommand = new ViewModelCommand(ExecuteCreateTask, CanExecuteCreateTask);

            LoadData();
        }

        public void LoadData()
        {
            var allTasks = _taskRepo.GetAllTasks();

            TodoTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "К выполнению"));
            InProgressTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "В процессе"));
            DoneTasks = new ObservableCollection<TaskModel>(allTasks.Where(t => t.Status == "Выполнено"));

            OnPropertyChanged(nameof(TodoTasks));
            OnPropertyChanged(nameof(InProgressTasks));
            OnPropertyChanged(nameof(DoneTasks));

            Categories = new ObservableCollection<TaskCategoryModel>(_taskRepo.GetCategories());
            OnPropertyChanged(nameof(Categories));

            LoadAvailableSoldiers();
        }

        private void LoadAvailableSoldiers()
        {
            // Берем только тех, кто "В строю" и прямо сейчас не в наряде
            var soldiersInFormation = _soldierRepo.GetAllSoldiers().Where(s => s.CurrentStatus == "В строю" && !s.IsOnActiveDuty).ToList();
            var tempList = new ObservableCollection<TaskSoldierSelectionModel>();

            // Дата следующей смены (сегодня или завтра, в зависимости от текущего времени)
            DateTime nextShiftDate = DateTime.Now.Hour < 16 ? DateTime.Today : DateTime.Today.AddDays(1);

            foreach (var soldier in soldiersInFormation)
            {
                // Проверяем, заступает ли он в следующую смену (через репозиторий нарядов)
                bool isGoingOnDuty = _dutyRepo.GetBusyDatesWithInfoForSoldier(soldier.SoldierID).ContainsKey(nextShiftDate);

                tempList.Add(new TaskSoldierSelectionModel
                {
                    Soldier = soldier,
                    IsGoingOnDuty = isGoingOnDuty,
                    IsEnabled = true
                });
            }

            AvailableSoldiers = tempList;
            OnPropertyChanged(nameof(AvailableSoldiers));
        }

        private void ValidateSoldiersByDeadline()
        {
            if (AvailableSoldiers == null) return;

            foreach (var s in AvailableSoldiers)
            {
                // Если дедлайн установлен, и он позже 15:00 сегодняшнего дня, 
                // а солдат заступает в наряд — блокируем его выбор!
                if (NewTaskDeadline.HasValue && NewTaskDeadline.Value.Hour >= 15 && s.IsGoingOnDuty)
                {
                    s.IsEnabled = false;
                    s.IsSelected = false; // Снимаем галочку, если она была
                }
                else
                {
                    s.IsEnabled = true;
                }
            }
        }

        private bool CanExecuteCreateTask(object obj)
        {
            return !string.IsNullOrWhiteSpace(NewTaskName) && SelectedCategory != null;
        }

        private void ExecuteCreateTask(object obj)
        {
            var task = new TaskModel
            {
                TaskName = NewTaskName,
                CategoryID = SelectedCategory.CategoryID,
                CreationDate = DateTime.Now,
                DueDate = NewTaskDeadline,
                Status = "К выполнению"
            };

            var selectedIds = AvailableSoldiers.Where(s => s.IsSelected).Select(s => s.Soldier.SoldierID).ToList();

            _taskRepo.CreateTask(task, selectedIds);

            // Очищаем форму
            NewTaskName = string.Empty;
            SelectedCategory = null;
            NewTaskDeadline = null;
            foreach (var s in AvailableSoldiers) s.IsSelected = false;

            LoadData(); // Обновляем доску
        }

        // Вызывается из Code-Behind при Drag-n-Drop
        public void ChangeTaskStatus(int taskId, string newStatus)
        {
            _taskRepo.UpdateTaskStatus(taskId, newStatus);
            LoadData();
        }
    }
}