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
    public class PersonnelViewModel : ViewModelBase
    {
        private readonly SoldierRepository _repository;
        private readonly StatusRepository _statusRepository;
        private readonly DutyRepository _dutyRepository;
        private readonly DirectoryRepository _directoryRepository;

        // --- СУЩЕСТВУЮЩИЕ СВОЙСТВА ДЛЯ СТАТУСОВ (ОТПУСК, ГОСПИТАЛ) ---
        private ObservableCollection<DateTime> _busyDates = new ObservableCollection<DateTime>();
        public ObservableCollection<DateTime> BusyDates { get => _busyDates; set { _busyDates = value; OnPropertyChanged(); } }

        private Dictionary<DateTime, string> _busyDatesInfo = new Dictionary<DateTime, string>();
        public Dictionary<DateTime, string> BusyDatesInfo { get => _busyDatesInfo; set { _busyDatesInfo = value; OnPropertyChanged(); } }

        public ObservableCollection<SoldierModel> Soldiers { get; set; }
        public ObservableCollection<StatusModel> Statuses { get; set; }

        private SoldierModel _selectedSoldier;
        public SoldierModel SelectedSoldier { get => _selectedSoldier; set { _selectedSoldier = value; OnPropertyChanged(); UpdateBusyDates(); } }

        private StatusModel _selectedStatus;
        public StatusModel SelectedStatus { get => _selectedStatus; set { _selectedStatus = value; OnPropertyChanged(); } }

        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate { get => _startDate; set { _startDate = value; OnPropertyChanged(); } }

        private DateTime _endDate = DateTime.Today.AddDays(1);
        public DateTime EndDate { get => _endDate; set { _endDate = value; OnPropertyChanged(); } }

        private string _documentInfo;
        public string DocumentInfo { get => _documentInfo; set { _documentInfo = value; OnPropertyChanged(); } }

        public ICommand ChangeStatusCommand { get; }

        // --- НОВЫЕ СВОЙСТВА ДЛЯ ДОБАВЛЕНИЯ БОЙЦОВ И УВОЛЬНЕНИЯ ---
        public ObservableCollection<DirectoryItemModel> Ranks { get; set; }
        public ObservableCollection<DirectoryItemModel> Positions { get; set; }
        public ObservableCollection<DirectoryItemModel> Units { get; set; }
        public ObservableCollection<string> ServiceTypes { get; set; }

        private DirectoryItemModel _selectedRank;
        public DirectoryItemModel SelectedRank { get => _selectedRank; set { _selectedRank = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedPosition;
        public DirectoryItemModel SelectedPosition { get => _selectedPosition; set { _selectedPosition = value; OnPropertyChanged(); } }

        private DirectoryItemModel _selectedUnit;
        public DirectoryItemModel SelectedUnit { get => _selectedUnit; set { _selectedUnit = value; OnPropertyChanged(); } }

        private string _selectedServiceType;
        public string SelectedServiceType { get => _selectedServiceType; set { _selectedServiceType = value; OnPropertyChanged(); } }

        private bool _isMassAddMode;
        public bool IsMassAddMode { get => _isMassAddMode; set { _isMassAddMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsSingleAddMode)); } }
        public bool IsSingleAddMode => !IsMassAddMode;

        // Поля для одиночного добавления
        private string _newLastName;
        public string NewLastName { get => _newLastName; set { _newLastName = value; OnPropertyChanged(); } }

        private string _newFirstName;
        public string NewFirstName { get => _newFirstName; set { _newFirstName = value; OnPropertyChanged(); } }

        private string _newPatronymic;
        public string NewPatronymic { get => _newPatronymic; set { _newPatronymic = value; OnPropertyChanged(); } }

        // Поле для массового добавления
        private string _massNamesText;
        public string MassNamesText { get => _massNamesText; set { _massNamesText = value; OnPropertyChanged(); } }

        public ICommand AddSoldierCommand { get; }
        public ICommand DismissSoldierCommand { get; }
        public ICommand ToggleAddModeCommand { get; }

        public PersonnelViewModel()
        {
            _repository = new SoldierRepository();
            _statusRepository = new StatusRepository();
            _dutyRepository = new DutyRepository();
            _directoryRepository = new DirectoryRepository();

            ServiceTypes = new ObservableCollection<string> { "По призыву", "По контракту" };

            ChangeStatusCommand = new ViewModelCommand(ExecuteChangeStatus, CanExecuteChangeStatus);
            AddSoldierCommand = new ViewModelCommand(ExecuteAddSoldier);
            DismissSoldierCommand = new ViewModelCommand(ExecuteDismissSoldier);
            ToggleAddModeCommand = new ViewModelCommand(o => IsMassAddMode = !IsMassAddMode);

            LoadDirectories();
            LoadData();

            // Если в настройках изменили звания/должности - обновляем выпадающие списки здесь
            AppMessenger.DirectoriesUpdated += () => LoadDirectories();
        }

        private void LoadDirectories()
        {
            Statuses = new ObservableCollection<StatusModel>(_statusRepository.GetAllStatuses());
            Ranks = new ObservableCollection<DirectoryItemModel>(_directoryRepository.GetDictionary("Ranks", "RankID", "RankName"));
            Positions = new ObservableCollection<DirectoryItemModel>(_directoryRepository.GetDictionary("Positions", "PositionID", "PositionName"));
            Units = new ObservableCollection<DirectoryItemModel>(_directoryRepository.GetDictionary("Units", "UnitID", "UnitName"));

            OnPropertyChanged(nameof(Statuses));
            OnPropertyChanged(nameof(Ranks));
            OnPropertyChanged(nameof(Positions));
            OnPropertyChanged(nameof(Units));
        }

        private void ExecuteAddSoldier(object obj)
        {
            if (SelectedRank == null || SelectedPosition == null || SelectedUnit == null || string.IsNullOrEmpty(SelectedServiceType))
            {
                MessageBox.Show("Пожалуйста, выберите Звание, Должность, Подразделение и Тип службы!", "Ошибка заполнения", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (IsSingleAddMode)
                {
                    if (string.IsNullOrWhiteSpace(NewLastName))
                    {
                        MessageBox.Show("Введите хотя бы фамилию бойца!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var s = new SoldierModel
                    {
                        LastName = NewLastName,
                        FirstName = NewFirstName,
                        Patronymic = NewPatronymic,
                        RankID = SelectedRank.Id,
                        PositionID = SelectedPosition.Id,
                        UnitID = SelectedUnit.Id,
                        ServiceType = SelectedServiceType
                    };
                    _repository.AddSoldier(s);
                    MessageBox.Show($"Боец {NewLastName} успешно добавлен в роту!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(MassNamesText))
                    {
                        MessageBox.Show("Вставьте список ФИО в текстовое поле!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    // Разбиваем текст на строки
                    var lines = MassNamesText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var newSoldiers = new List<SoldierModel>();

                    foreach (var line in lines)
                    {
                        // Разбиваем строку на слова (по пробелу или табуляции, если скопировали из Excel)
                        var parts = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length == 0) continue;

                        newSoldiers.Add(new SoldierModel
                        {
                            LastName = parts[0],
                            FirstName = parts.Length > 1 ? parts[1] : "",
                            Patronymic = parts.Length > 2 ? parts[2] : "",
                            RankID = SelectedRank.Id,
                            PositionID = SelectedPosition.Id,
                            UnitID = SelectedUnit.Id,
                            ServiceType = SelectedServiceType
                        });
                    }

                    _repository.AddSoldiersMass(newSoldiers);
                    MessageBox.Show($"Успешно зачислено {newSoldiers.Count} чел.!", "Массовое добавление", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Очищаем форму и обновляем списки
                NewLastName = string.Empty; NewFirstName = string.Empty; NewPatronymic = string.Empty; MassNamesText = string.Empty;
                LoadData();
                AppMessenger.BroadcastDirectoriesUpdated(); // Даем сигнал Дашборду обновить счетчик "Всего по списку"
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteDismissSoldier(object obj)
        {
            if (SelectedSoldier == null) return;

            if (MessageBox.Show($"Вы уверены, что хотите уволить в запас (перевести в другой округ) бойца: {SelectedSoldier.FullName}?\n\nОн навсегда исчезнет из списков личного состава, но его история нарядов сохранится в архиве.",
                "Увольнение из рядов ВС", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _repository.DismissSoldier(SelectedSoldier.SoldierID);
                RefreshView();
                AppMessenger.BroadcastDirectoriesUpdated(); // Обновляем статистику на Главной
                MessageBox.Show("Боец исключен из списков части.", "Дембель", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // --- СТАРЫЕ МЕТОДЫ (ОСТАЮТСЯ БЕЗ ИЗМЕНЕНИЙ) ---

        private void UpdateBusyDates()
        {
            if (SelectedSoldier == null)
            {
                BusyDatesInfo = new Dictionary<DateTime, string>();
                BusyDates = new ObservableCollection<DateTime>();
                return;
            }
            BusyDatesInfo = _dutyRepository.GetBusyDatesWithInfoForSoldier(SelectedSoldier.SoldierID);
            BusyDates = new ObservableCollection<DateTime>(BusyDatesInfo.Keys);
        }

        private bool CanExecuteChangeStatus(object obj)
        {
            return SelectedSoldier != null && SelectedStatus != null && StartDate.Date <= EndDate.Date;
        }

        private void ExecuteChangeStatus(object obj)
        {
            try
            {
                for (DateTime d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
                {
                    if (BusyDatesInfo.ContainsKey(d))
                    {
                        MessageBox.Show($"На {d.ToShortDateString()} у бойца уже есть статус: {BusyDatesInfo[d]}", "Конфликт", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                _statusRepository.AddStatusLog(SelectedSoldier.SoldierID, SelectedStatus.StatusID, StartDate, EndDate, DocumentInfo);
                MessageBox.Show($"Статус успешно изменен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                RefreshView();
                AppMessenger.BroadcastDirectoriesUpdated(); // Чтобы на Дашборде обновилось количество "В строю / Отсутствуют"
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshView()
        {
            int? currentSoldierId = SelectedSoldier?.SoldierID;
            SelectedStatus = null; StartDate = DateTime.Today; EndDate = DateTime.Today.AddDays(1); DocumentInfo = string.Empty;
            LoadData();
            if (currentSoldierId.HasValue) SelectedSoldier = Soldiers.FirstOrDefault(s => s.SoldierID == currentSoldierId.Value);
            UpdateBusyDates();
        }

        private void LoadData()
        {
            var data = _repository.GetAllSoldiers();
            if (Soldiers == null) Soldiers = new ObservableCollection<SoldierModel>(data);
            else { Soldiers.Clear(); foreach (var soldier in data) Soldiers.Add(soldier); }
        }
    }
}