using System.Collections.Generic;

namespace WpfApp1.Models
{
    // Модель для одной строчки внутри карточки (Должность + ФИО)
    public class DutyRoleItem
    {
        public string RoleName { get; set; }     // Например: "Дежурный" или "К/С"
        public string PersonnelName { get; set; } // Например: "С-т Иванов И.И."
    }

    // Модель для самой карточки (Группа наряда)
    public class ActiveDutyCardModel
    {
        public string GroupName { get; set; } // Например: "Наряд по роте"
        public string ShiftPeriod { get; set; }

        // Список должностей в этом наряде
        public List<DutyRoleItem> PersonnelRoles { get; set; }
    }
}