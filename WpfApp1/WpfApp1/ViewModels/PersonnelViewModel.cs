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

        private List<DirectoryItemModel> _allPositions = new List<DirectoryItemModel>();
        private List<DirectoryItemModel> _allUnits = new List<DirectoryItemModel>();

        private ObservableCollection<SoldierModel> _soldiers = new ObservableCollection<SoldierModel>();
        public ObservableCollection<SoldierModel> Soldiers
        {
            get => _soldiers;
            set { _soldiers = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DirectoryItemModel> Ranks { get; set; } = new ObservableCollection<DirectoryItemModel>();

        private ObservableCollection<DirectoryItemModel> _positions = new ObservableCollection<DirectoryItemModel>();
        public ObservableCollection<DirectoryItemModel> Positions
        {
            get => _positions;
            set { _positions = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DirectoryItemModel> _units = new ObservableCollection<DirectoryItemModel>();
        public ObservableCollection<DirectoryItemModel> Units
        {
            get => _units;
            set { _units = value; OnPropertyChanged(); }
        }

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
        public DirectoryItemModel SelectedRank
        {
            get => _selectedRank;
            set
            {
                if (_selectedRank != value)
                {
                    _selectedRank = value;
                    OnPropertyChanged();
                    ApplyBusinessRules();
                }
            }
        }

        private DirectoryItemModel _selectedPosition;
        public DirectoryItemModel SelectedPosition { get => _selectedPosition; set { _selectedPosition = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedUnit;
        public DirectoryItemModel SelectedUnit { get => _selectedUnit; set { _selectedUnit = value; OnPropertyChanged(); } }

        private string _selectedServiceType;
        public string SelectedServiceType
        {
            get => _selectedServiceType;
            set
            {
                if (_selectedServiceType != value)
                {
                    _selectedServiceType = value;
                    OnPropertyChanged();
                    ApplyBusinessRules();
                }
            }
        }

        private string _searchQuery;
        public string SearchQuery
        {
            get => _searchQuery;
            set { _searchQuery = value; OnPropertyChanged(); FilterSoldiers(); }
        }

        private bool _isFormOpen;
        public bool IsFormOpen { get => _isFormOpen; set { _isFormOpen = value; OnPropertyChanged(); } }

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set { _isEditing = value; if (value) IsBulkInsertMode = false; UpdateUIProperties(); }
        }

        private bool _isBulkInsertMode;
        public bool IsBulkInsertMode
        {
            get => _isBulkInsertMode;
            set
            {
                if (_isBulkInsertMode != value)
                {
                    _isBulkInsertMode = value;
                    if (value)
                    {
                        IsEditing = false;
                        ApplyBusinessRules();
                    }
                    UpdateUIProperties();
                }
            }
        }

        public bool IsSingleInsertMode => !IsBulkInsertMode;

        private string _bulkInsertText;
        public string BulkInsertText { get => _bulkInsertText; set { _bulkInsertText = value; OnPropertyChanged(); } }

        private int _editingSoldierId;
        private int? _editingOriginalUnitId;
        private bool _isUpdatingForm = false;

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

            OpenAddFormCommand = new ViewModelCommand(o => { ResetForm(); IsFormOpen = true; });
            SaveSoldierCommand = new ViewModelCommand(ExecuteSaveSoldier, CanExecuteSaveSoldier);
            EditSoldierCommand = new ViewModelCommand(ExecuteEditSoldier);
            DeleteSoldierCommand = new ViewModelCommand(ExecuteDeleteSoldier);
            CancelEditCommand = new ViewModelCommand(o => { ResetForm(); IsFormOpen = false; });
            ToggleBulkModeCommand = new ViewModelCommand(o => { IsBulkInsertMode = !IsBulkInsertMode; ResetForm(keepMode: true); });

            LoadData();
            AppMessenger.DirectoriesUpdated += LoadData;
        }

        private void LoadData()
        {
            _isUpdatingForm = true;

            var ranksData = _dirRepo.GetDictionary("Ranks", "RankID", "RankName");
            Ranks = new ObservableCollection<DirectoryItemModel>(ranksData ?? new List<DirectoryItemModel>());

            _allPositions = _dirRepo.GetDictionary("Positions", "PositionID", "PositionName") ?? new List<DirectoryItemModel>();

            _allUnits = _dirRepo.GetDictionary("Units", "UnitID", "UnitName") ?? new List<DirectoryItemModel>();
            _allUnits.Insert(0, new DirectoryItemModel { Id = 0, Name = "--- Не распределен ---" });

            _isUpdatingForm = false;

            if (!IsEditing && !IsBulkInsertMode) ResetForm();
            LoadSoldiers();
        }

        private void ApplyBusinessRules()
        {
            if (_allPositions == null || _allUnits == null || Ranks == null || _isUpdatingForm) return;

            _isUpdatingForm = true;

            try
            {
                // 1. ФИЛЬТРАЦИЯ ПОДРАЗДЕЛЕНИЙ (ВОЗВРАЩЕНО!)
                var currentUnitId = SelectedUnit?.Id;
                var allowedUnits = new List<DirectoryItemModel>();

                if (IsBulkInsertMode)
                {
                    allowedUnits = _allUnits.Where(u => u.Name != null && (u.Name.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 || u.Name.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
                }
                else
                {
                    foreach (var u in _allUnits)
                    {
                        bool isVmp = u.Name != null && (u.Name.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 || u.Name.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0);
                        if (!isVmp || (IsEditing && _editingOriginalUnitId == u.Id))
                        {
                            allowedUnits.Add(u);
                        }
                    }
                }

                Units = new ObservableCollection<DirectoryItemModel>(allowedUnits);
                SelectedUnit = Units.FirstOrDefault(u => u.Id == currentUnitId) ?? Units.FirstOrDefault();

                // 2. ФИЛЬТРАЦИЯ ЗВАНИЙ
                var currentRankId = SelectedRank?.Id;
                var allowedRanks = new List<DirectoryItemModel>();

                foreach (var r in Ranks)
                {
                    string rankName = r.Name?.ToLower() ?? "";
                    bool isOfficerOrWarrant = rankName.Contains("лейтенант") || rankName.Contains("капитан") || rankName.Contains("майор") || rankName.Contains("прапорщик") || rankName.Contains("полковник") || rankName.Contains("генерал");

                    if (SelectedServiceType == "По призыву" && isOfficerOrWarrant) continue;
                    allowedRanks.Add(r);
                }

                if (!allowedRanks.Any(r => r.Id == currentRankId))
                {
                    SelectedRank = allowedRanks.FirstOrDefault(r => (r.Name?.Equals("Рядовой", StringComparison.OrdinalIgnoreCase) ?? false)) ?? allowedRanks.FirstOrDefault();
                }

                // 3. ФИЛЬТРАЦИЯ ДОЛЖНОСТЕЙ В ЗАВИСИМОСТИ ОТ ЗВАНИЯ
                // 3. ФИЛЬТРАЦИЯ ДОЛЖНОСТЕЙ В ЗАВИСИМОСТИ ОТ ЗВАНИЯ (ПО УСТАВУ)
                var currentPosId = SelectedPosition?.Id;
                var allowedPositions = new List<DirectoryItemModel>();

                if (SelectedRank != null && !string.IsNullOrEmpty(SelectedRank.Name))
                {
                    string rank = SelectedRank.Name.ToLower();
                    bool isPrivate = rank == "рядовой";
                    bool isCorporal = rank == "ефрейтор";
                    bool isSergeant = rank.Contains("сержант") || (rank.Contains("старшина") && !rank.Contains("прапорщик"));
                    bool isWarrant = rank.Contains("прапорщик");
                    bool isOfficer = rank.Contains("лейтенант") || rank.Contains("капитан") || rank.Contains("майор");

                    foreach (var pos in _allPositions)
                    {
                        string p = pos.Name?.ToLower() ?? "";

                        // Категории должностей по штату
                        bool isSoldierPos = p.Contains("стрелок") || p.Contains("пулеметчик") || p.Contains("гранатометчик") || p.Contains("наводчик") || p.Contains("водитель");
                        bool isSeniorSoldierPos = p.Contains("старший стрелок");
                        bool isSquadLeader = p.Contains("командир отделения");
                        bool isDepPlatoonLeader = p.Contains("заместитель командира взвода");
                        bool isCompanySergeant = p.Contains("старшина роты");
                        bool isWarehouseChief = p.Contains("начальник склада");
                        bool isPlatoonLeader = p.Contains("командир взвода");
                        bool isDepCompanyLeader = p.Contains("заместитель командира роты") || p.Contains("замполит");
                        bool isCompanyLeader = p.Equals("командир роты");

                        // Матрица допуска
                        if (isPrivate && isSoldierPos) allowedPositions.Add(pos);
                        else if (isCorporal && (isSoldierPos || isSeniorSoldierPos)) allowedPositions.Add(pos);
                        // Сержантам разрешаем быть сержантами, но также временно стоять на солдатских должностях (как указано в доке)
                        else if (isSergeant && (isSquadLeader || isDepPlatoonLeader || isSoldierPos)) allowedPositions.Add(pos);
                        else if (isWarrant && (isCompanySergeant || isWarehouseChief || isPlatoonLeader)) allowedPositions.Add(pos);
                        else if (isOfficer && (isPlatoonLeader || isDepCompanyLeader || isCompanyLeader)) allowedPositions.Add(pos);

                        // Если должность нестандартная (не описана в документе), разрешаем ее всем, чтобы не заблокировать систему
                        else if (!isSoldierPos && !isSeniorSoldierPos && !isSquadLeader && !isDepPlatoonLeader && !isCompanySergeant && !isWarehouseChief && !isPlatoonLeader && !isDepCompanyLeader && !isCompanyLeader)
                        {
                            allowedPositions.Add(pos);
                        }
                    }
                }

                if (!allowedPositions.Any()) allowedPositions = _allPositions.ToList();

                Positions = new ObservableCollection<DirectoryItemModel>(allowedPositions);
                SelectedPosition = Positions.FirstOrDefault(p => p.Id == currentPosId) ?? Positions.FirstOrDefault();
            }
            finally
            {
                _isUpdatingForm = false;
            }
        }

        private void LoadSoldiers()
        {
            var soldiersData = _soldierRepo.GetAllSoldiers();
            Soldiers = new ObservableCollection<SoldierModel>(soldiersData ?? new List<SoldierModel>());

            if (Soldiers != null)
            {
                ICollectionView view = CollectionViewSource.GetDefaultView(Soldiers);
                if (view != null) view.Filter = SoldierFilterPredicate;
            }
        }

        private void FilterSoldiers()
        {
            if (Soldiers != null) CollectionViewSource.GetDefaultView(Soldiers)?.Refresh();
        }

        private bool SoldierFilterPredicate(object item)
        {
            if (string.IsNullOrWhiteSpace(SearchQuery)) return true;
            if (item is SoldierModel s)
            {
                var q = SearchQuery.ToLower();
                return (s.LastName != null && s.LastName.ToLower().Contains(q)) ||
                       (s.FirstName != null && s.FirstName.ToLower().Contains(q)) ||
                       (s.Patronymic != null && s.Patronymic.ToLower().Contains(q)) ||
                       (s.RankName != null && s.RankName.ToLower().Contains(q)) ||
                       (s.PositionName != null && s.PositionName.ToLower().Contains(q));
            }
            return false;
        }

        private bool CanExecuteSaveSoldier(object obj)
        {
            if (SelectedRank == null || SelectedPosition == null || SelectedServiceType == null) return false;
            if (IsBulkInsertMode) return !string.IsNullOrWhiteSpace(BulkInsertText);
            return !string.IsNullOrWhiteSpace(LastName);
        }

        private void ExecuteSaveSoldier(object obj)
        {
            if (JoinDate.Date > DateTime.Today)
            {
                MessageBox.Show("Дата зачисления не может быть позже сегодняшнего дня!", "Ошибка ввода", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int safeUnitId = SelectedUnit != null && SelectedUnit.Id > 0 ? SelectedUnit.Id : (Units?.FirstOrDefault(u => u.Id > 0)?.Id ?? 1);

            if (IsBulkInsertMode)
            {
                var lines = BulkInsertText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var newSoldiers = new List<SoldierModel>();

                var vmpUnit = _allUnits?.FirstOrDefault(u => (u.Name?.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0) || (u.Name?.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0));
                int targetBulkUnitId = vmpUnit != null ? vmpUnit.Id : safeUnitId;

                int targetRankId = Ranks?.FirstOrDefault(r => (r.Name?.Equals("Рядовой", StringComparison.OrdinalIgnoreCase) ?? false))?.Id ?? SelectedRank?.Id ?? 1;
                int targetPosId = _allPositions?.FirstOrDefault(p => (p.Name?.Equals("Стрелок", StringComparison.OrdinalIgnoreCase) ?? false))?.Id ?? SelectedPosition?.Id ?? 1;
                string targetServiceType = "По призыву";

                foreach (var line in lines)
                {
                    var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length == 0) continue;

                    newSoldiers.Add(new SoldierModel
                    {
                        LastName = parts[0],
                        FirstName = parts.Length > 1 ? parts[1] : "",
                        Patronymic = parts.Length > 2 ? parts[2] : "",
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
                    MessageBox.Show($"Зачислено: {newSoldiers.Count} чел.\nНаправлены в подразделение пополнения.", "Пополнение", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                string inputLast = LastName?.Trim().ToLower() ?? "";
                string inputFirst = FirstName?.Trim().ToLower() ?? "";
                string inputMiddle = MiddleName?.Trim().ToLower() ?? "";

                bool isDuplicate = Soldiers.Any(s =>
                    (s.LastName?.ToLower() ?? "") == inputLast &&
                    (s.FirstName?.ToLower() ?? "") == inputFirst &&
                    (s.Patronymic?.ToLower() ?? "") == inputMiddle &&
                    s.SoldierID != _editingSoldierId);

                if (isDuplicate)
                {
                    if (MessageBox.Show($"Военнослужащий с таким ФИО уже числится в базе.\n\nВы уверены, что хотите добавить/сохранить полного тезку?",
                                        "Возможное дублирование", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                    {
                        return;
                    }
                }

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
                _isUpdatingForm = true;

                IsEditing = true;
                _editingSoldierId = soldier.SoldierID;
                _editingOriginalUnitId = soldier.UnitID;

                LastName = soldier.LastName;
                FirstName = soldier.FirstName;
                MiddleName = soldier.Patronymic;
                JoinDate = soldier.JoinDate == DateTime.MinValue ? DateTime.Today : soldier.JoinDate;

                SelectedRank = Ranks.FirstOrDefault(r => r.Id == soldier.RankID) ?? Ranks.FirstOrDefault();
                SelectedServiceType = ServiceTypes.Contains(soldier.ServiceType) ? soldier.ServiceType : ServiceTypes.FirstOrDefault();

                _isUpdatingForm = false;
                ApplyBusinessRules();

                SelectedPosition = Positions.FirstOrDefault(p => p.Id == soldier.PositionID) ?? Positions.FirstOrDefault();
                if (soldier.UnitID.HasValue) SelectedUnit = Units.FirstOrDefault(u => u.Id == soldier.UnitID.Value) ?? Units.FirstOrDefault();
                else SelectedUnit = Units.FirstOrDefault();

                IsFormOpen = true;
            }
        }

        private void ExecuteDeleteSoldier(object obj)
        {
            if (obj is int id && MessageBox.Show("Уволить в запас?", "Увольнение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                _soldierRepo.DismissSoldier(id);
                if (IsEditing && _editingSoldierId == id) { ResetForm(); IsFormOpen = false; }
                LoadSoldiers();
            }
        }

        private void ResetForm(bool keepMode = false)
        {
            _isUpdatingForm = true;

            if (!keepMode) { IsEditing = false; IsBulkInsertMode = false; }
            _editingSoldierId = 0;
            _editingOriginalUnitId = null;
            LastName = string.Empty; FirstName = string.Empty; MiddleName = string.Empty; BulkInsertText = string.Empty;
            JoinDate = DateTime.Today;

            _isUpdatingForm = false;
            ApplyBusinessRules();

            if (!IsBulkInsertMode)
            {
                SelectedRank = Ranks?.FirstOrDefault();
                SelectedServiceType = ServiceTypes?.FirstOrDefault();
            }
        }

        public void Dispose() => AppMessenger.DirectoriesUpdated -= LoadData;
    }
}