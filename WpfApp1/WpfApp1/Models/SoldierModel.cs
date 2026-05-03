using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Models
{
    public class SoldierModel
    {
        public int SoldierID { get; set; }
        public string LastName { get; set; }
        public string FirstName { get; set; }
        public string Patronymic { get; set; }
        public string RankName { get; set; }
        public string PositionName { get; set; }
        public string UnitName { get; set; }

        public string ServiceType { get; set; }
        public string FullName => $"{LastName} {FirstName} {Patronymic}".Trim();
        public string CurrentStatus { get; set; }
    }
}
