using System;

namespace WpfApp1.Models
{
    public class SoldierModel
    {
        public int SoldierID { get; set; }
        public int RankID { get; set; }
        public int PositionID { get; set; }
        public int? UnitID { get; set; }

        // Настоящие поля, которые заполняет база данных
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string MiddleName { get; set; }

        // Умное поле: само собирает ФИО (например: Иванов И. И.)
        public string FullName
        {
            get
            {
                string first = string.IsNullOrWhiteSpace(FirstName) ? "" : $"{FirstName[0]}.";
                string middle = string.IsNullOrWhiteSpace(MiddleName) ? "" : $"{MiddleName[0]}.";
                return $"{LastName} {first} {middle}".Trim();
            }
        }

        public string ServiceType { get; set; }
        public bool IsDismissed { get; set; }

        // Поля для отображения
        public string RankName { get; set; }
        public string PositionName { get; set; }
        public string UnitName { get; set; }

        // Поля статусов
        public string CurrentStatus { get; set; } = "В строю";
        public bool IsOnActiveDuty { get; set; }
    }
}