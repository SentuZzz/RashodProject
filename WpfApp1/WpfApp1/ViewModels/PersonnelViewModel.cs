using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Repositories;
using WpfApp1.Helpers;

namespace WpfApp1.ViewModels
{
    // ИСПРАВЛЕНИЕ: Добавлен интерфейс IDisposable для очистки памяти
    public class PersonnelViewModel : ViewModelBase, IDisposable
    {
        private readonly SoldierRepository _soldierRepo;
        private readonly DirectoryRepository _dirRepo;

        // Коллекции
        private ObservableCollection<SoldierModel> _soldiers;
        public ObservableCollection<SoldierModel> Soldiers
        {
            get => _soldiers;
            set { _soldiers = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DirectoryItemModel> Ranks { get; set; }
        public ObservableCollection<DirectoryItemModel> Positions { get; set; }
        public ObservableCollection<DirectoryItemModel> Units { get; set; }
        public ObservableCollection<string> ServiceTypes { get; set; }

        // Поля формы
        private string _lastName;
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); } }

        private string _firstName;
        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); } }

        private string _middleName;
        public string MiddleName { get => _middleName; set { _middleName = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedRank;
        public DirectoryItemModel SelectedRank { get => _selectedRank; set { _selectedRank = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedPosition;
        public DirectoryItemModel SelectedPosition { get => _selectedPosition; set { _selectedPosition = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedUnit;
        public DirectoryItemModel SelectedUnit { get => _selectedUnit; set { _selectedUnit = value; OnPropertyChanged(); } }

        private string _selectedServiceType;
        public string SelectedServiceType { get => _selectedServiceType; set { _selectedServiceType = value; OnPropertyChanged(); } }

        // --- БЛОК ПОИСКА ---
        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
                FilterSoldiers(); // Обновляем фильтр при вводе
            }
        }

        // --- БЛОК РЕДАКТИРОВАНИЯ ---
        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                _isEditing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SubmitButtonText));
                OnPropertyChanged(nameof(CancelButtonVisibility));
                OnPropertyChanged(nameof(FormTitle));
            }
        }

        private int _editingSoldierId;

        // Динамическое изменение интерфейса в зависимости от режима
        public string SubmitButtonText => IsEditing ? "Сохранить изменения" : "Добавить военнослужащего";
        public string FormTitle => IsEditing ? "Редактирование" : "Новый военнослужащий";
        public Visibility CancelButtonVisibility => IsEditing ? Visibility.Visible : Visibility.Collapsed;

        // Команды
        public ICommand SaveSoldierCommand { get; }
        public ICommand EditSoldierCommand { get; }
        public ICommand DeleteSoldierCommand { get; }
        public ICommand CancelEditCommand { get; }

        public PersonnelViewModel()
        {
            _soldierRepo = new SoldierRepository();
            _dirRepo = new DirectoryRepository();

            ServiceTypes = new ObservableCollection<string> { "По призыву", "По контракту" };

            SaveSoldierCommand = new ViewModelCommand(ExecuteSaveSoldier, CanExecuteSaveSoldier);
            EditSoldierCommand = new ViewModelCommand(ExecuteEditSoldier);
            DeleteSoldierCommand = new ViewModelCommand(ExecuteDeleteSoldier);
            CancelEditCommand = new ViewModelCommand(o => ResetForm());

            LoadData();

            // Подписка на обновление справочников
            AppMessenger.DirectoriesUpdated += LoadData;
        }

        private void LoadData()
        {
            Ranks = new ObservableCollection<DirectoryItemModel>(_dirRepo.GetDictionary("Ranks", "RankID", "RankName"));
            Positions = new ObservableCollection<DirectoryItemModel>(_dirRepo.GetDictionary("Positions", "PositionID", "PositionName"));
            Units = new ObservableCollection<DirectoryItemModel>(_dirRepo.GetDictionary("Units", "UnitID", "UnitName"));

            // Добавляем пустую строку для "Не распределен"
            Units.Insert(0, new DirectoryItemModel { Id = 0, Name = "--- Не распределен ---" });

            OnPropertyChanged(nameof(Ranks));
            OnPropertyChanged(nameof(Positions));
            OnPropertyChanged(nameof(Units));

            if (!IsEditing) ResetForm();

            LoadSoldiers();
        }

        private void LoadSoldiers()
        {
            var rawSoldiers = _soldierRepo.GetAllSoldiers();
            Soldiers = new ObservableCollection<SoldierModel>(rawSoldiers);

            // ИСПРАВЛЕНИЕ: Настраиваем ICollectionView для плавной фильтрации без пересоздания списка
            ICollectionView view = CollectionViewSource.GetDefaultView(Soldiers);
            view.Filter = SoldierFilterPredicate;
        }

        // Логика фильтрации списка
        private void FilterSoldiers()
        {
            if (Soldiers != null)
            {
                // Просто командуем View обновить отображение (применит предикат)
                CollectionViewSource.GetDefaultView(Soldiers).Refresh();
            }
        }

        // Предикат (правило) для фильтрации элементов
        private bool SoldierFilterPredicate(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
                return true;

            if (item is SoldierModel s)
            {
                var query = SearchQuery.ToLower();
                return (s.LastName != null && s.LastName.ToLower().Contains(query)) ||
                       (s.FirstName != null && s.FirstName.ToLower().Contains(query)) ||
                       (s.RankName != null && s.RankName.ToLower().Contains(query)) ||
                       (s.PositionName != null && s.PositionName.ToLower().Contains(query));
            }
            return false;
        }

        private bool CanExecuteSaveSoldier(object obj)
        {
            return !string.IsNullOrWhiteSpace(LastName) && SelectedRank != null && SelectedPosition != null && SelectedServiceType != null;
        }

        private void ExecuteSaveSoldier(object obj)
        {
            var soldier = new SoldierModel
            {
                SoldierID = _editingSoldierId,
                LastName = LastName?.Trim(),
                FirstName = FirstName?.Trim(),
                MiddleName = MiddleName?.Trim(),
                RankID = SelectedRank.Id,
                PositionID = SelectedPosition.Id,
                UnitID = SelectedUnit != null && SelectedUnit.Id > 0 ? SelectedUnit.Id : (int?)null,
                ServiceType = SelectedServiceType
            };

            if (IsEditing)
            {
                _soldierRepo.UpdateSoldier(soldier);
            }
            else
            {
                _soldierRepo.AddSoldier(soldier);
            }

            ResetForm();
            LoadSoldiers();
        }

        // Заполнение формы данными выбранного бойца
        private void ExecuteEditSoldier(object obj)
        {
            if (obj is SoldierModel soldier)
            {
                IsEditing = true;
                _editingSoldierId = soldier.SoldierID;

                LastName = soldier.LastName;
                FirstName = soldier.FirstName;
                MiddleName = soldier.MiddleName;

                SelectedRank = Ranks.FirstOrDefault(r => r.Id == soldier.RankID) ?? Ranks.First();
                SelectedPosition = Positions.FirstOrDefault(p => p.Id == soldier.PositionID) ?? Positions.First();

                if (soldier.UnitID.HasValue)
                    SelectedUnit = Units.FirstOrDefault(u => u.Id == soldier.UnitID.Value) ?? Units.First();
                else
                    SelectedUnit = Units.First(); // "Не распределен"

                SelectedServiceType = ServiceTypes.Contains(soldier.ServiceType) ? soldier.ServiceType : ServiceTypes.First();
            }
        }

        private void ExecuteDeleteSoldier(object obj)
        {
            if (obj is int soldierId)
            {
                if (MessageBox.Show("Вы уверены, что хотите уволить этого военнослужащего в запас?\n\nЕго данные останутся в Архиве.", "Увольнение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _soldierRepo.DismissSoldier(soldierId);
                    if (IsEditing && _editingSoldierId == soldierId) ResetForm();
                    LoadSoldiers();
                }
            }
        }

        private void ResetForm()
        {
            IsEditing = false;
            _editingSoldierId = 0;
            LastName = string.Empty;
            FirstName = string.Empty;
            MiddleName = string.Empty;
            SelectedRank = Ranks?.FirstOrDefault();
            SelectedPosition = Positions?.FirstOrDefault();
            SelectedUnit = Units?.FirstOrDefault();
            SelectedServiceType = ServiceTypes?.FirstOrDefault();
        }

        // ИСПРАВЛЕНИЕ: Отписываемся от событий при уничтожении ViewModel (предотвращает утечку памяти)
        public void Dispose()
        {
            AppMessenger.DirectoriesUpdated -= LoadData;
        }
    }
}