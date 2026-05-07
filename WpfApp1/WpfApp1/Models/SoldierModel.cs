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
        public string MiddleName { get; set; }

        public DateTime JoinDate { get; set; }

        // Короткое ФИО (например: Иванов И. П.) - на случай если где-то еще используется
        public string FullName
        {
            get
            {
                string first = string.IsNullOrWhiteSpace(FirstName) ? "" : $"{FirstName[0]}.";
                string middle = string.IsNullOrWhiteSpace(MiddleName) ? "" : $"{MiddleName[0]}.";
                return $"{LastName} {first} {middle}".Trim();
            }
        }

        // ПОЛНОЕ ФИО (например: Иванов Иван Петрович) для нашей новой таблицы
        public string FullNameExpanded => $"{LastName} {FirstName} {MiddleName}".Trim();

        public string ServiceType { get; set; }
        public bool IsDismissed { get; set; }

        public string RankName { get; set; }
        public string PositionName { get; set; }
        public string UnitName { get; set; }

        public string CurrentStatus { get; set; } = "В строю";
        public bool IsOnActiveDuty { get; set; }
    }
}