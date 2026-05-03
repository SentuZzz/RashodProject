using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.Repositories;

namespace WpfApp1.ViewModels
{
    public class PersonnelViewModel : ViewModelBase
    {
        private readonly SoldierRepository _repository;
        private readonly StatusRepository _statusRepository;
        private readonly DutyRepository _dutyRepository;

        private ObservableCollection<DateTime> _busyDates = new ObservableCollection<DateTime>();
        public ObservableCollection<DateTime> BusyDates
        {
            get => _busyDates;
            set { _busyDates = value; OnPropertyChanged(); }
        }

        private Dictionary<DateTime, string> _busyDatesInfo = new Dictionary<DateTime, string>();
        public Dictionary<DateTime, string> BusyDatesInfo
        {
            get => _busyDatesInfo;
            set { _busyDatesInfo = value; OnPropertyChanged(); }
        }

        public ObservableCollection<SoldierModel> Soldiers { get; set; }
        public ObservableCollection<StatusModel> Statuses { get; set; }

        private SoldierModel _selectedSoldier;
        public SoldierModel SelectedSoldier
        {
            get => _selectedSoldier;
            set
            {
                _selectedSoldier = value;
                OnPropertyChanged();
                UpdateBusyDates();
            }
        }

        private StatusModel _selectedStatus;
        public StatusModel SelectedStatus
        {
            get => _selectedStatus;
            set { _selectedStatus = value; OnPropertyChanged(); }
        }

        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate
        {
            get => _startDate;
            set { _startDate = value; OnPropertyChanged(); }
        }

        private DateTime _endDate = DateTime.Today.AddDays(1);
        public DateTime EndDate
        {
            get => _endDate;
            set { _endDate = value; OnPropertyChanged(); }
        }

        private string _documentInfo;
        public string DocumentInfo
        {
            get => _documentInfo;
            set { _documentInfo = value; OnPropertyChanged(); }
        }

        public ICommand ChangeStatusCommand { get; }

        public PersonnelViewModel()
        {
            _repository = new SoldierRepository();
            _statusRepository = new StatusRepository();
            _dutyRepository = new DutyRepository();

            Statuses = new ObservableCollection<StatusModel>(_statusRepository.GetAllStatuses());
            ChangeStatusCommand = new ViewModelCommand(ExecuteChangeStatus, CanExecuteChangeStatus);

            LoadData();
        }

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
            return SelectedSoldier != null &&
                   SelectedStatus != null &&
                   StartDate.Date <= EndDate.Date;
        }

        private void ExecuteChangeStatus(object obj)
        {
            try
            {
                for (DateTime d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
                {
                    if (BusyDatesInfo.ContainsKey(d))
                    {
                        MessageBox.Show($"Дата {d.ToShortDateString()} занята!\nПричина: {BusyDatesInfo[d]}",
                                        "Конфликт дат", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                _statusRepository.AddStatusLog(SelectedSoldier.SoldierID, SelectedStatus.StatusID, StartDate, EndDate, DocumentInfo);

                MessageBox.Show($"Статус для {SelectedSoldier.LastName} успешно сохранен.",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshView()
        {
            int? currentSoldierId = SelectedSoldier?.SoldierID;

            SelectedStatus = null;
            StartDate = DateTime.Today;
            EndDate = DateTime.Today.AddDays(1);
            DocumentInfo = string.Empty;

            LoadData();

            if (currentSoldierId.HasValue)
            {
                SelectedSoldier = Soldiers.FirstOrDefault(s => s.SoldierID == currentSoldierId.Value);
            }
            UpdateBusyDates();
        }

        private void LoadData()
        {
            var data = _repository.GetAllSoldiers();

            if (Soldiers == null)
            {
                Soldiers = new ObservableCollection<SoldierModel>(data);
            }
            else
            {
                Soldiers.Clear();
                foreach (var soldier in data)
                {
                    Soldiers.Add(soldier);
                }
            }
        }
    }
}