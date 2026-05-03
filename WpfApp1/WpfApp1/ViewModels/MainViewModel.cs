using System.Windows.Input;

namespace WpfApp1.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentView;
        public ViewModelBase CurrentView
        {
            get { return _currentView; }
            set
            {
                _currentView = value;
                OnPropertyChanged();
            }
        }

        // Команды для всех 7 разделов
        public ICommand ShowHomeViewCommand { get; }
        public ICommand ShowPersonnelViewCommand { get; }
        public ICommand ShowDutiesViewCommand { get; }
        public ICommand ShowTasksViewCommand { get; }
        public ICommand ShowHistoryViewCommand { get; }
        public ICommand ShowSettingsViewCommand { get; }
        public ICommand ShowReportsViewCommand { get; }

        public MainViewModel()
        {
            // Инициализация команд
            ShowHomeViewCommand = new ViewModelCommand(ExecuteShowHomeViewCommand);
            ShowPersonnelViewCommand = new ViewModelCommand(ExecuteShowPersonnelViewCommand);
            ShowDutiesViewCommand = new ViewModelCommand(ExecuteShowDutiesViewCommand);
            ShowTasksViewCommand = new ViewModelCommand(ExecuteShowTasksViewCommand);
            ShowHistoryViewCommand = new ViewModelCommand(ExecuteShowHistoryViewCommand);
            ShowSettingsViewCommand = new ViewModelCommand(ExecuteShowSettingsViewCommand);
            ShowReportsViewCommand = new ViewModelCommand(ExecuteShowReportsViewCommand);

            // По умолчанию открываем Дашборд
            ExecuteShowHomeViewCommand(null);
        }

        private void ExecuteShowHomeViewCommand(object obj)
        {
            CurrentView = new HomeViewModel();
        }

        private void ExecuteShowPersonnelViewCommand(object obj) => CurrentView = new PersonnelViewModel();
        private void ExecuteShowDutiesViewCommand(object obj) => CurrentView = new DutiesViewModel();
        private void ExecuteShowTasksViewCommand(object obj) => CurrentView = new TasksViewModel();
        private void ExecuteShowHistoryViewCommand(object obj) => CurrentView = new HistoryViewModel();
        private void ExecuteShowSettingsViewCommand(object obj) => CurrentView = new SettingsViewModel();
        private void ExecuteShowReportsViewCommand(object obj) => CurrentView = new ReportsViewModel();
    }
}