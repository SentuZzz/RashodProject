using System.Collections.Generic;

namespace WpfApp1.Models
{
    public class ActiveDutyCardModel
    {
        public string DutyName { get; set; }

        // Список бойцов в формате: "С-т Иванов И.И."
        public List<string> Personnel { get; set; }

        // Строка периода: "01.05 - 02.05"
        public string ShiftPeriod { get; set; }
    }
}