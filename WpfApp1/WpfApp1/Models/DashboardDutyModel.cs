using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1.Models
{
    public class DashboardDutyModel
    {
        public string DutyName { get; set; }
        public int Capacity { get; set; }   // Сколько всего нужно людей
        public int Assigned { get; set; }   // Сколько уже назначено

        // Сколько осталось назначить
        public int Missing => Capacity - Assigned;

        // Текст для интерфейса
        public string StatusText => Missing > 0 ? $"ОСТАЛОСЬ НАЗНАЧИТЬ: {Missing}" : "УКОМПЛЕКТОВАН";

        // Цвет (Красный, если не хватает людей, и Зеленый, если всё ок)
        public string StatusColor => Missing > 0 ? "#DC2626" : "#16A34A";

        // Иконка (Восклицательный знак или галочка)
        public string IconName => Missing > 0 ? "CircleExclamation" : "CheckCircle";
    }
}
