using System.Collections.ObjectModel;
using WpfApp1.Models;
using WpfApp1.Repositories;

namespace WpfApp1.ViewModels
{
    public class PersonnelViewModel : ViewModelBase
    {
        private readonly SoldierRepository _repository;

        public ObservableCollection<SoldierModel> Soldiers { get; set; }

        private SoldierModel _selectedSoldier;
        public SoldierModel SelectedSoldier
        {
            get { return _selectedSoldier; }
            set { _selectedSoldier = value; OnPropertyChanged(); }
        }

        public PersonnelViewModel()
        {
            _repository = new SoldierRepository();
            LoadData();
        }

        private void LoadData()
        {
            var data = _repository.GetAllSoldiers();
            Soldiers = new ObservableCollection<SoldierModel>(data);
        }
    }
}