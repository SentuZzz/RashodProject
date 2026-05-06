using System;

namespace WpfApp1.Models
{
    public class SoldierModel
    {
        public int SoldierID { get; set; }
        public int RankID { get; set; }
        public int PositionID { get; set; }

        // ИСПРАВЛЕНИЕ: Теперь UnitID может быть пустым (null), если солдат не в подразделении
        public int? UnitID { get; set; }

        public string FirstName { get; set; }
        public string LastName { get; set; }

        // ИСПРАВЛЕНИЕ: Добавлено Отчество, из-за которого ругался компилятор
        public string MiddleName { get; set; }

        public string ServiceType { get; set; }
        public bool IsDismissed { get; set; }

        // --- Поля для отображения в интерфейсе (подтягиваются из других таблиц) ---
        public string RankName { get; set; }
        public string PositionName { get; set; }
        public string UnitName { get; set; }

        // Вычисляемое свойство для красивого вывода ФИО (Иванов И.И.)
        public string FullName
        {
            get
            {
                string firstInitial = string.IsNullOrWhiteSpace(FirstName) ? "" : $"{FirstName[0]}.";
                string middleInitial = string.IsNullOrWhiteSpace(MiddleName) ? "" : $"{MiddleName[0]}.";
                return $"{LastName} {firstInitial}{middleInitial}".Trim();
            }
        }

        // --- Поля статусов ---
        public string CurrentStatus { get; set; } = "В строю";
        public bool IsOnActiveDuty { get; set; }
    }
}