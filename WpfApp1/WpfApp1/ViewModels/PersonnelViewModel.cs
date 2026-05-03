using System;
using System.Collections.ObjectModel;
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

        // Коллекции для интерфейса
        public ObservableCollection<SoldierModel> Soldiers { get; set; }
        public ObservableCollection<StatusModel> Statuses { get; set; }

        // Выбранный солдат в таблице
        private SoldierModel _selectedSoldier;
        public SoldierModel SelectedSoldier
        {
            get { return _selectedSoldier; }
            set { _selectedSoldier = value; OnPropertyChanged(); }
        }

        // Выбранный статус в выпадающем списке
        private StatusModel _selectedStatus;
        public StatusModel SelectedStatus
        {
            get { return _selectedStatus; }
            set { _selectedStatus = value; OnPropertyChanged(); }
        }

        // Дата начала
        private DateTime _startDate = DateTime.Today;
        public DateTime StartDate
        {
            get { return _startDate; }
            set { _startDate = value; OnPropertyChanged(); }
        }

        // Дата окончания (по умолчанию завтра)
        private DateTime _endDate = DateTime.Today.AddDays(1);
        public DateTime EndDate
        {
            get { return _endDate; }
            set { _endDate = value; OnPropertyChanged(); }
        }

        // Основание (номер приказа)
        private string _documentInfo;
        public string DocumentInfo
        {
            get { return _documentInfo; }
            set { _documentInfo = value; OnPropertyChanged(); }
        }

        public ICommand ChangeStatusCommand { get; }

        public PersonnelViewModel()
        {
            _repository = new SoldierRepository();
            _statusRepository = new StatusRepository();

            // Загружаем список статусов для ComboBox
            Statuses = new ObservableCollection<StatusModel>(_statusRepository.GetAllStatuses());

            ChangeStatusCommand = new ViewModelCommand(ExecuteChangeStatus, CanExecuteChangeStatus);

            LoadData();
        }

        private void LoadData()
        {
            // Получаем свежие данные с вычисленными статусами
            var data = _repository.GetAllSoldiers();

            if (Soldiers == null)
            {
                // При первом запуске просто создаем коллекцию
                Soldiers = new ObservableCollection<SoldierModel>(data);
            }
            else
            {
                // При обновлении страницы — очищаем и заполняем заново. 
                // WPF мгновенно увидит это изменение и перерисует таблицу!
                Soldiers.Clear();
                foreach (var soldier in data)
                {
                    Soldiers.Add(soldier);
                }
            }
        }

        // Кнопка нажмется только если солдат и статус выбраны, а даты логически правильные
        private bool CanExecuteChangeStatus(object obj)
        {
            return SelectedSoldier != null &&
                   SelectedStatus != null &&
                   StartDate <= EndDate;
        }

        private void ExecuteChangeStatus(object obj)
        {
            try
            {
                // Записываем статус в базу
                _statusRepository.AddStatusLog(SelectedSoldier.SoldierID, SelectedStatus.StatusID, StartDate, EndDate, DocumentInfo);

                MessageBox.Show($"Статус военнослужащего {SelectedSoldier.LastName} успешно изменен на «{SelectedStatus.StatusName}».", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Делаем полный сброс экрана, чтобы обновить таблицу
                RefreshView();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении статуса: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshView()
        {
            SelectedSoldier = null;
            SelectedStatus = null;
            StartDate = DateTime.Today;
            EndDate = DateTime.Today.AddDays(1);
            DocumentInfo = string.Empty;

            // Перезапрашиваем солдат. База данных вычислит их свежий статус и вернет нам!
            LoadData();
        }
    }
}