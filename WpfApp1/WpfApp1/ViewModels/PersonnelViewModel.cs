using System;
using System.Collections.Generic;
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
    public class PersonnelViewModel : ViewModelBase, IDisposable
    {
        private readonly SoldierRepository _soldierRepo;
        private readonly DirectoryRepository _dirRepo;

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

        private string _lastName;
        public string LastName { get => _lastName; set { _lastName = value; OnPropertyChanged(); } }

        private string _firstName;
        public string FirstName { get => _firstName; set { _firstName = value; OnPropertyChanged(); } }

        private string _middleName;
        public string MiddleName { get => _middleName; set { _middleName = value; OnPropertyChanged(); } }

        private DateTime _joinDate = DateTime.Today;
        public DateTime JoinDate { get => _joinDate; set { _joinDate = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedRank;
        public DirectoryItemModel SelectedRank { get => _selectedRank; set { _selectedRank = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedPosition;
        public DirectoryItemModel SelectedPosition { get => _selectedPosition; set { _selectedPosition = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedUnit;
        public DirectoryItemModel SelectedUnit { get => _selectedUnit; set { _selectedUnit = value; OnPropertyChanged(); } }

        private string _selectedServiceType;
        public string SelectedServiceType { get => _selectedServiceType; set { _selectedServiceType = value; OnPropertyChanged(); } }

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set
            {
                _searchQuery = value;
                OnPropertyChanged();
                FilterSoldiers();
            }
        }

        private bool _isFormOpen;
        public bool IsFormOpen
        {
            get => _isFormOpen;
            set { _isFormOpen = value; OnPropertyChanged(); }
        }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                _isEditing = value;
                if (value) IsBulkInsertMode = false;
                UpdateUIProperties();
            }
        }

        private bool _isBulkInsertMode;
        public bool IsBulkInsertMode
        {
            get => _isBulkInsertMode;
            set
            {
                _isBulkInsertMode = value;
                if (value)
                {
                    IsEditing = false;

                    // ИСПРАВЛЕНИЕ: Автоматически выбираем нужные пункты для визуального отображения (они будут заблокированы)
                    var vmpUnit = Units?.FirstOrDefault(u =>
                        u.Name.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        u.Name.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0);
                    if (vmpUnit != null) SelectedUnit = vmpUnit;

                    var rank = Ranks?.FirstOrDefault(r => r.Name.Equals("Рядовой", StringComparison.OrdinalIgnoreCase));
                    if (rank != null) SelectedRank = rank;

                    var pos = Positions?.FirstOrDefault(p => p.Name.Equals("Стрелок", StringComparison.OrdinalIgnoreCase));
                    if (pos != null) SelectedPosition = pos;

                    SelectedServiceType = "По призыву";
                }
                UpdateUIProperties();
            }
        }

        public bool IsSingleInsertMode => !IsBulkInsertMode;

        private string _bulkInsertText;
        public string BulkInsertText { get => _bulkInsertText; set { _bulkInsertText = value; OnPropertyChanged(); } }

        private int _editingSoldierId;

        public string SubmitButtonText => IsEditing ? "Сохранить изменения" : (IsBulkInsertMode ? "Добавить список" : "Добавить бойца");
        public string FormTitle => IsEditing ? "Карточка военнослужащего" : (IsBulkInsertMode ? "Массовое добавление" : "Новый военнослужащий");

        private void UpdateUIProperties()
        {
            OnPropertyChanged(nameof(IsEditing));
            OnPropertyChanged(nameof(IsBulkInsertMode));
            OnPropertyChanged(nameof(IsSingleInsertMode));
            OnPropertyChanged(nameof(SubmitButtonText));
            OnPropertyChanged(nameof(FormTitle));
        }

        public ICommand SaveSoldierCommand { get; }
        public ICommand EditSoldierCommand { get; }
        public ICommand DeleteSoldierCommand { get; }
        public ICommand CancelEditCommand { get; }
        public ICommand ToggleBulkModeCommand { get; }
        public ICommand OpenAddFormCommand { get; }

        public PersonnelViewModel()
        {
            _soldierRepo = new SoldierRepository();
            _dirRepo = new DirectoryRepository();

            ServiceTypes = new ObservableCollection<string> { "По призыву", "По контракту" };

            OpenAddFormCommand = new ViewModelCommand(o =>
            {
                ResetForm();
                IsFormOpen = true;
            });

            SaveSoldierCommand = new ViewModelCommand(ExecuteSaveSoldier, CanExecuteSaveSoldier);
            EditSoldierCommand = new ViewModelCommand(ExecuteEditSoldier);
            DeleteSoldierCommand = new ViewModelCommand(ExecuteDeleteSoldier);

            CancelEditCommand = new ViewModelCommand(o =>
            {
                ResetForm();
                IsFormOpen = false;
            });

            ToggleBulkModeCommand = new ViewModelCommand(o =>
            {
                IsBulkInsertMode = !IsBulkInsertMode;
                ResetForm(keepMode: true);
            });

            LoadData();
            AppMessenger.DirectoriesUpdated += LoadData;
        }

        private void LoadData()
        {
            Ranks = new ObservableCollection<DirectoryItemModel>(_dirRepo.GetDictionary("Ranks", "RankID", "RankName"));
            Positions = new ObservableCollection<DirectoryItemModel>(_dirRepo.GetDictionary("Positions", "PositionID", "PositionName"));
            Units = new ObservableCollection<DirectoryItemModel>(_dirRepo.GetDictionary("Units", "UnitID", "UnitName"));

            Units.Insert(0, new DirectoryItemModel { Id = 0, Name = "--- Не распределен ---" });

            OnPropertyChanged(nameof(Ranks));
            OnPropertyChanged(nameof(Positions));
            OnPropertyChanged(nameof(Units));

            if (!IsEditing && !IsBulkInsertMode) ResetForm();

            LoadSoldiers();
        }

        private void LoadSoldiers()
        {
            var rawSoldiers = _soldierRepo.GetAllSoldiers();
            Soldiers = new ObservableCollection<SoldierModel>(rawSoldiers);

            ICollectionView view = CollectionViewSource.GetDefaultView(Soldiers);
            view.Filter = SoldierFilterPredicate;
        }

        private void FilterSoldiers()
        {
            if (Soldiers != null)
                CollectionViewSource.GetDefaultView(Soldiers).Refresh();
        }

        private bool SoldierFilterPredicate(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return true;

            if (item is SoldierModel s)
            {
                var query = SearchQuery.ToLower();
                return (s.LastName != null && s.LastName.ToLower().Contains(query)) ||
                       (s.FirstName != null && s.FirstName.ToLower().Contains(query)) ||
                       (s.Patronymic != null && s.Patronymic.ToLower().Contains(query)) ||
                       (s.RankName != null && s.RankName.ToLower().Contains(query)) ||
                       (s.PositionName != null && s.PositionName.ToLower().Contains(query));
            }
            return false;
        }

        private bool CanExecuteSaveSoldier(object obj)
        {
            if (SelectedRank == null || SelectedPosition == null || SelectedServiceType == null)
                return false;

            if (IsBulkInsertMode) return !string.IsNullOrWhiteSpace(BulkInsertText);
            return !string.IsNullOrWhiteSpace(LastName);
        }

        private void ExecuteSaveSoldier(object obj)
        {
            int safeUnitId = SelectedUnit != null && SelectedUnit.Id > 0 ? SelectedUnit.Id : (Units?.FirstOrDefault(u => u.Id > 0)?.Id ?? 1);

            if (IsBulkInsertMode)
            {
                var lines = BulkInsertText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var newSoldiers = new List<SoldierModel>();

                // Жестко фиксируем параметры для пополнения (независимо от того, что в интерфейсе)
                var vmpUnit = Units?.FirstOrDefault(u =>
                    u.Name.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    u.Name.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0);
                int targetBulkUnitId = vmpUnit != null ? vmpUnit.Id : safeUnitId;

                int targetRankId = Ranks?.FirstOrDefault(r => r.Name.Equals("Рядовой", StringComparison.OrdinalIgnoreCase))?.Id ?? SelectedRank?.Id ?? 1;
                int targetPosId = Positions?.FirstOrDefault(p => p.Name.Equals("Стрелок", StringComparison.OrdinalIgnoreCase))?.Id ?? SelectedPosition?.Id ?? 1;
                string targetServiceType = "По призыву";

                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    string ln = parts[0];
                    string fn = parts.Length > 1 ? parts[1] : "";
                    string mn = parts.Length > 2 ? parts[2] : "";

                    newSoldiers.Add(new SoldierModel
                    {
                        LastName = ln,
                        FirstName = fn,
                        Patronymic = mn,
                        JoinDate = JoinDate,
                        RankID = targetRankId,
                        PositionID = targetPosId,
                        UnitID = targetBulkUnitId,
                        ServiceType = targetServiceType
                    });
                }

                if (newSoldiers.Any())
                {
                    _soldierRepo.AddSoldiersBulk(newSoldiers);
                    MessageBox.Show($"Успешно зачислено военнослужащих: {newSoldiers.Count}\n(Всем автоматически присвоено звание Рядовой, должность Стрелок и тип службы По призыву).", "Пополнение", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                var soldier = new SoldierModel
                {
                    SoldierID = _editingSoldierId,
                    LastName = LastName?.Trim(),
                    FirstName = FirstName?.Trim(),
                    Patronymic = MiddleName?.Trim(),
                    JoinDate = JoinDate,
                    RankID = SelectedRank.Id,
                    PositionID = SelectedPosition.Id,
                    UnitID = safeUnitId,
                    ServiceType = SelectedServiceType
                };

                if (IsEditing) _soldierRepo.UpdateSoldier(soldier);
                else _soldierRepo.AddSoldier(soldier);
            }

            ResetForm();
            IsFormOpen = false;
            LoadSoldiers();
        }

        private void ExecuteEditSoldier(object obj)
        {
            if (obj is SoldierModel soldier)
            {
                IsEditing = true;
                _editingSoldierId = soldier.SoldierID;

                LastName = soldier.LastName;
                FirstName = soldier.FirstName;
                MiddleName = soldier.Patronymic;
                JoinDate = soldier.JoinDate == DateTime.MinValue ? DateTime.Today : soldier.JoinDate;

                SelectedRank = Ranks.FirstOrDefault(r => r.Id == soldier.RankID) ?? Ranks.First();
                SelectedPosition = Positions.FirstOrDefault(p => p.Id == soldier.PositionID) ?? Positions.First();

                if (soldier.UnitID.HasValue)
                    SelectedUnit = Units.FirstOrDefault(u => u.Id == soldier.UnitID.Value) ?? Units.First();
                else
                    SelectedUnit = Units.First();

                SelectedServiceType = ServiceTypes.Contains(soldier.ServiceType) ? soldier.ServiceType : ServiceTypes.First();

                IsFormOpen = true;
            }
        }

        private void ExecuteDeleteSoldier(object obj)
        {
            if (obj is int soldierId)
            {
                if (MessageBox.Show("Вы уверены, что хотите уволить этого военнослужащего в запас?", "Увольнение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _soldierRepo.DismissSoldier(soldierId);
                    if (IsEditing && _editingSoldierId == soldierId)
                    {
                        ResetForm();
                        IsFormOpen = false;
                    }
                    LoadSoldiers();
                }
            }
        }

        private void ResetForm(bool keepMode = false)
        {
            if (!keepMode)
            {
                IsEditing = false;
                IsBulkInsertMode = false;
            }

            _editingSoldierId = 0;
            LastName = string.Empty;
            FirstName = string.Empty;
            MiddleName = string.Empty;
            BulkInsertText = string.Empty;
            JoinDate = DateTime.Today;

            if (IsBulkInsertMode)
            {
                SelectedUnit = Units?.FirstOrDefault(u =>
                    u.Name.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    u.Name.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0);
                SelectedRank = Ranks?.FirstOrDefault(r => r.Name.Equals("Рядовой", StringComparison.OrdinalIgnoreCase));
                SelectedPosition = Positions?.FirstOrDefault(p => p.Name.Equals("Стрелок", StringComparison.OrdinalIgnoreCase));
                SelectedServiceType = "По призыву";
            }
            else
            {
                SelectedUnit = Units?.FirstOrDefault();
                SelectedRank = Ranks?.FirstOrDefault();
                SelectedPosition = Positions?.FirstOrDefault();
                SelectedServiceType = ServiceTypes?.FirstOrDefault();
            }
        }

        public void Dispose()
        {
            AppMessenger.DirectoriesUpdated -= LoadData;
        }
    }
}