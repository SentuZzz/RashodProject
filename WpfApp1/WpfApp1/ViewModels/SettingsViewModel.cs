using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Repositories;
using WpfApp1.Helpers;

namespace WpfApp1.ViewModels
{
    public class PriorityOption
    {
        public string Title { get; set; }
        public int Value { get; set; }
    }

    public class SettingsViewModel : ViewModelBase
    {
        private readonly DirectoryRepository _repo;

        public ObservableCollection<string> MenuItems { get; }
        public ObservableCollection<PriorityOption> PriorityOptions { get; }

        private string _selectedMenu;
        public string SelectedMenu
        {
            get => _selectedMenu;
            set { _selectedMenu = value; ResetForm(); LoadDictionary(); } // При смене меню сбрасываем форму
        }

        private ObservableCollection<DirectoryItemModel> _currentItems;
        public ObservableCollection<DirectoryItemModel> CurrentItems { get => _currentItems; set { _currentItems = value; OnPropertyChanged(); } }

        private string _newItemName;
        public string NewItemName { get => _newItemName; set { _newItemName = value; OnPropertyChanged(); } }

        private PriorityOption _selectedPriority;
        public PriorityOption SelectedPriority { get => _selectedPriority; set { _selectedPriority = value; OnPropertyChanged(); } }

        // РЕЖИМ РЕДАКТИРОВАНИЯ
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; OnPropertyChanged(); OnPropertyChanged(nameof(SubmitButtonText)); OnPropertyChanged(nameof(CancelButtonVisibility)); }
        }

        private int _editingItemId;
        public string SubmitButtonText => IsEditing ? "Сохранить изменения" : "Добавить запись";
        public Visibility CancelButtonVisibility => IsEditing ? Visibility.Visible : Visibility.Collapsed;

        // Видимость форм
        public Visibility SimpleFormVisibility => SelectedMenu != "Виды нарядов" ? Visibility.Visible : Visibility.Collapsed;
        public Visibility DutyFormVisibility => SelectedMenu == "Виды нарядов" ? Visibility.Visible : Visibility.Collapsed;

        public ICommand SaveItemCommand { get; }
        public ICommand DeleteItemCommand { get; }
        public ICommand EditItemCommand { get; }
        public ICommand CancelEditCommand { get; }

        public SettingsViewModel()
        {
            _repo = new DirectoryRepository();
            MenuItems = new ObservableCollection<string> { "Подразделения", "Звания", "Должности", "Категории задач", "Виды нарядов" };

            PriorityOptions = new ObservableCollection<PriorityOption>
            {
                new PriorityOption { Title = "1 - Офицеры / Прапорщики", Value = 1 },
                new PriorityOption { Title = "2 - Сержанты", Value = 2 },
                new PriorityOption { Title = "3 - Рядовой состав", Value = 3 }
            };
            SelectedPriority = PriorityOptions.First();

            SaveItemCommand = new ViewModelCommand(ExecuteSaveItem, CanExecuteSaveItem);
            DeleteItemCommand = new ViewModelCommand(ExecuteDeleteItem);
            EditItemCommand = new ViewModelCommand(ExecuteEditItem);
            CancelEditCommand = new ViewModelCommand(o => ResetForm());

            SelectedMenu = MenuItems[0];
        }

        private void LoadDictionary()
        {
            if (SelectedMenu == "Подразделения") CurrentItems = new ObservableCollection<DirectoryItemModel>(_repo.GetDictionary("Units", "UnitID", "UnitName"));
            else if (SelectedMenu == "Звания") CurrentItems = new ObservableCollection<DirectoryItemModel>(_repo.GetDictionary("Ranks", "RankID", "RankName"));
            else if (SelectedMenu == "Должности") CurrentItems = new ObservableCollection<DirectoryItemModel>(_repo.GetDictionary("Positions", "PositionID", "PositionName"));
            else if (SelectedMenu == "Категории задач") CurrentItems = new ObservableCollection<DirectoryItemModel>(_repo.GetDictionary("TaskCategories", "CategoryID", "CategoryName"));
            else if (SelectedMenu == "Виды нарядов") CurrentItems = new ObservableCollection<DirectoryItemModel>(_repo.GetDuties());

            OnPropertyChanged(nameof(SimpleFormVisibility));
            OnPropertyChanged(nameof(DutyFormVisibility));
        }

        private bool CanExecuteSaveItem(object obj) => !string.IsNullOrWhiteSpace(NewItemName);

        private void ExecuteSaveItem(object obj)
        {
            if (IsEditing)
            {
                // Сохраняем изменения
                if (SelectedMenu == "Виды нарядов")
                    _repo.UpdateDuty(_editingItemId, NewItemName, SelectedPriority.Value);
                else
                    _repo.UpdateItem(CurrentItems.First().TableName, CurrentItems.First().IdColumnName, CurrentItems.First().NameColumnName, _editingItemId, NewItemName);
            }
            else
            {
                // Создаем новую запись
                if (SelectedMenu == "Виды нарядов") _repo.AddDuty(NewItemName, SelectedPriority.Value);
                else if (SelectedMenu == "Подразделения") _repo.AddItem("Units", "UnitName", NewItemName);
                else if (SelectedMenu == "Звания") _repo.AddItem("Ranks", "RankName", NewItemName);
                else if (SelectedMenu == "Должности") _repo.AddItem("Positions", "PositionName", NewItemName);
                else if (SelectedMenu == "Категории задач") _repo.AddItem("TaskCategories", "CategoryName", NewItemName);
            }

            ResetForm();
            LoadDictionary();
            AppMessenger.BroadcastDirectoriesUpdated();
        }

        private void ExecuteEditItem(object obj)
        {
            if (obj is DirectoryItemModel item)
            {
                IsEditing = true;
                _editingItemId = item.Id;
                NewItemName = item.Name;

                if (item.IsDuty)
                {
                    SelectedPriority = PriorityOptions.FirstOrDefault(p => p.Value == item.Priority) ?? PriorityOptions.First();
                }
                AppMessenger.BroadcastDirectoriesUpdated();
            }
        }

        private void ResetForm()
        {
            IsEditing = false;
            NewItemName = string.Empty;
            SelectedPriority = PriorityOptions.First();
        }

        private void ExecuteDeleteItem(object obj)
        {
            if (obj is DirectoryItemModel item)
            {
                if (MessageBox.Show($"Удалить '{item.Name}'?\nЭто действие может повлиять на связанные данные в базе!", "Внимание", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _repo.DeleteItem(item.TableName, item.IdColumnName, item.Id);
                    if (IsEditing && _editingItemId == item.Id) ResetForm();
                    LoadDictionary();
                    AppMessenger.BroadcastDirectoriesUpdated();
                }
            }
        }
    }
}