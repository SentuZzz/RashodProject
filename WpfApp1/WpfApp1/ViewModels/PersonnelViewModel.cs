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

        // Исходные полные списки из БД для работы фильтров
        private List<DirectoryItemModel> _allPositions;
        private List<DirectoryItemModel> _allUnits;

        private ObservableCollection<SoldierModel> _soldiers;
        public ObservableCollection<SoldierModel> Soldiers
        {
            get => _soldiers;
            set { _soldiers = value; OnPropertyChanged(); }
        }

        public ObservableCollection<DirectoryItemModel> Ranks { get; set; }

        private ObservableCollection<DirectoryItemModel> _positions;
        public ObservableCollection<DirectoryItemModel> Positions
        {
            get => _positions;
            set { _positions = value; OnPropertyChanged(); }
        }

        private ObservableCollection<DirectoryItemModel> _units;
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

        // ИСПРАВЛЕНИЕ: При смене звания запускаем перерасчет доступных должностей
        private DirectoryItemModel _selectedRank;
        public DirectoryItemModel SelectedRank
        {
            get => _selectedRank;
            set
            {
                _selectedRank = value;
                OnPropertyChanged();
                ApplyBusinessRules();
            }
        }

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
                _isBulkInsertMode = value;
                if (value)
                {
                    IsEditing = false;
                    ApplyBusinessRules(); // Принудительно ставим ВМП
                }
                UpdateUIProperties();
            }
        }

        public bool IsSingleInsertMode => !IsBulkInsertMode;

        private string _bulkInsertText;
        public string BulkInsertText { get => _bulkInsertText; set { _bulkInsertText = value; OnPropertyChanged(); } }

        private int _editingSoldierId;
        private int? _editingOriginalUnitId; // Запоминаем старое подразделение при редактировании
        private bool _isUpdatingForm = false; // Флаг, чтобы избежать лишних срабатываний фильтра при заполнении формы

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
            Ranks = new ObservableCollection<DirectoryItemModel>(_dirRepo.GetDictionary("Ranks", "RankID", "RankName"));

            // Сохраняем оригинальные списки из базы для работы фильтров
            _allPositions = _dirRepo.GetDictionary("Positions", "PositionID", "PositionName");
            _allUnits = _dirRepo.GetDictionary("Units", "UnitID", "UnitName");
            _allUnits.Insert(0, new DirectoryItemModel { Id = 0, Name = "--- Не распределен ---" });

            if (!IsEditing && !IsBulkInsertMode) ResetForm();
            LoadSoldiers();
        }

        // ==========================================
        // БИЗНЕС-ЛОГИКА: Матрица Званий, Должностей и ВМП
        // ==========================================
        private void ApplyBusinessRules()
        {
            if (_allPositions == null || _allUnits == null || _isUpdatingForm) return;

            // 1. ФИЛЬТРАЦИЯ ПОДРАЗДЕЛЕНИЙ (Блокировка ВМП)
            var currentUnitId = SelectedUnit?.Id;
            var allowedUnits = new List<DirectoryItemModel>();

            if (IsBulkInsertMode)
            {
                // Для массового добавления оставляем только ВМП
                allowedUnits = _allUnits.Where(u => u.Name.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 || u.Name.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            }
            else
            {
                foreach (var u in _allUnits)
                {
                    bool isVmp = u.Name.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 || u.Name.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0;

                    // Разрешаем все подразделения КРОМЕ ВМП. 
                    // ВМП разрешаем ТОЛЬКО если мы редактируем бойца, который уже там числится.
                    if (!isVmp || (IsEditing && _editingOriginalUnitId == u.Id))
                    {
                        allowedUnits.Add(u);
                    }
                }
            }

            Units = new ObservableCollection<DirectoryItemModel>(allowedUnits);
            SelectedUnit = Units.FirstOrDefault(u => u.Id == currentUnitId) ?? Units.FirstOrDefault();

            // 2. ФИЛЬТРАЦИЯ ДОЛЖНОСТЕЙ В ЗАВИСИМОСТИ ОТ ЗВАНИЯ
            if (IsBulkInsertMode)
            {
                // При массовом добавлении жестко фиксируем Рядового и Стрелка
                Positions = new ObservableCollection<DirectoryItemModel>(_allPositions);
                SelectedRank = Ranks?.FirstOrDefault(r => r.Name.Equals("Рядовой", StringComparison.OrdinalIgnoreCase));
                SelectedPosition = Positions?.FirstOrDefault(p => p.Name.Equals("Стрелок", StringComparison.OrdinalIgnoreCase));
                SelectedServiceType = "По призыву";
                return; // Остальные правила не применяем
            }

            var currentPosId = SelectedPosition?.Id;
            var allowedPositions = new List<DirectoryItemModel>();

            if (SelectedRank != null)
            {
                string rank = SelectedRank.Name.ToLower();
                bool isOfficerOrWarrant = rank.Contains("лейтенант") || rank.Contains("капитан") || rank.Contains("майор") || rank.Contains("прапорщик") || rank.Contains("старшина");
                bool isSergeant = rank.Contains("сержант");

                foreach (var pos in _allPositions)
                {
                    string p = pos.Name.ToLower();
                    bool isCommander = p.Contains("командир роты") || p.Contains("командир взвода") || p.Contains("заместитель командира роты") || p.Contains("старшина") || p.Contains("техник");
                    bool isSquadLeader = p.Contains("командир отделения") || p.Contains("заместитель командира взвода");

                    if (isOfficerOrWarrant)
                    {
                        if (isCommander || isSquadLeader) allowedPositions.Add(pos); // Офицеры и прапорщики - командуют
                    }
                    else if (isSergeant)
                    {
                        if (isSquadLeader && !isCommander) allowedPositions.Add(pos); // Сержанты - командуют отделениями
                    }
                    else // Рядовые и Ефрейторы
                    {
                        if (!isCommander && !isSquadLeader) allowedPositions.Add(pos); // Рядовые - только исполнители
                    }
                }
            }

            // Защита от пустых списков (если названия в БД нестандартные)
            if (!allowedPositions.Any()) allowedPositions = _allPositions.ToList();

            Positions = new ObservableCollection<DirectoryItemModel>(allowedPositions);

            // Пытаемся сохранить выбранную должность, если она всё еще доступна, иначе выбираем первую доступную
            SelectedPosition = Positions.FirstOrDefault(p => p.Id == currentPosId) ?? Positions.FirstOrDefault();
        }
        // ==========================================


        private void LoadSoldiers()
        {
            Soldiers = new ObservableCollection<SoldierModel>(_soldierRepo.GetAllSoldiers());
            ICollectionView view = CollectionViewSource.GetDefaultView(Soldiers);
            view.Filter = SoldierFilterPredicate;
        }

        private void FilterSoldiers()
        {
            if (Soldiers != null) CollectionViewSource.GetDefaultView(Soldiers).Refresh();
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
            int safeUnitId = SelectedUnit != null && SelectedUnit.Id > 0 ? SelectedUnit.Id : (Units?.FirstOrDefault(u => u.Id > 0)?.Id ?? 1);

            if (IsBulkInsertMode)
            {
                var lines = BulkInsertText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                var newSoldiers = new List<SoldierModel>();
                var vmpUnit = _allUnits?.FirstOrDefault(u => u.Name.IndexOf("ВМП", StringComparison.OrdinalIgnoreCase) >= 0 || u.Name.IndexOf("пополнения", StringComparison.OrdinalIgnoreCase) >= 0);
                int targetBulkUnitId = vmpUnit != null ? vmpUnit.Id : safeUnitId;

                int targetRankId = Ranks?.FirstOrDefault(r => r.Name.Equals("Рядовой", StringComparison.OrdinalIgnoreCase))?.Id ?? SelectedRank?.Id ?? 1;
                int targetPosId = _allPositions?.FirstOrDefault(p => p.Name.Equals("Стрелок", StringComparison.OrdinalIgnoreCase))?.Id ?? SelectedPosition?.Id ?? 1;
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
                var soldier = new SoldierModel
                {
                    SoldierID = _editingSoldierId,
                    LastName = LastName?.Trim(),
                    FirstName = FirstName?.Trim(),
                    Patronymic = MiddleName?.Trim(), // Тут привязка к полю формы, названному MiddleName
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
                _isUpdatingForm = true; // Отключаем правила на время загрузки данных бойца

                IsEditing = true;
                _editingSoldierId = soldier.SoldierID;
                _editingOriginalUnitId = soldier.UnitID;

                LastName = soldier.LastName;
                FirstName = soldier.FirstName;
                MiddleName = soldier.Patronymic;
                JoinDate = soldier.JoinDate == DateTime.MinValue ? DateTime.Today : soldier.JoinDate;

                SelectedRank = Ranks.FirstOrDefault(r => r.Id == soldier.RankID) ?? Ranks.First();
                SelectedServiceType = ServiceTypes.Contains(soldier.ServiceType) ? soldier.ServiceType : ServiceTypes.First();

                _isUpdatingForm = false;
                ApplyBusinessRules(); // Запускаем правила (отфильтруются доступные должности и подразделения)

                // Теперь, когда списки сформированы, выбираем должность и подразделение
                SelectedPosition = Positions.FirstOrDefault(p => p.Id == soldier.PositionID) ?? Positions.First();
                if (soldier.UnitID.HasValue) SelectedUnit = Units.FirstOrDefault(u => u.Id == soldier.UnitID.Value) ?? Units.First();
                else SelectedUnit = Units.First();

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