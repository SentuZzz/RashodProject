using System;

namespace WpfApp1.Models
{
    public class SoldierModel
    {
        public int SoldierID { get; set; }
        public int RankID { get; set; }
        public int PositionID { get; set; }
        public int? UnitID { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Patronymic { get; set; } // ИСПРАВЛЕНО: Вернули правильное имя из БД!

        public DateTime JoinDate { get; set; }

        public string FullName
        {
            get
            {
                string first = string.IsNullOrWhiteSpace(FirstName) ? "" : $"{FirstName[0]}.";
                string patronymic = string.IsNullOrWhiteSpace(Patronymic) ? "" : $"{Patronymic[0]}.";
                return $"{LastName} {first} {patronymic}".Trim();
            }
        }

        public string FullNameExpanded => $"{LastName} {FirstName} {Patronymic}".Trim();

        public string ServiceType { get; set; }
        public bool IsDismissed { get; set; }

        public string RankName { get; set; }
        public string PositionName { get; set; }
        public string UnitName { get; set; }

        public string CurrentStatus { get; set; } = "В строю";
        public bool IsOnActiveDuty { get; set; }
    }
}