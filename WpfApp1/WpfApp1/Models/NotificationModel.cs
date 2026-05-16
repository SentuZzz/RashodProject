using System;

namespace WpfApp1.Models
{
    public class NotificationModel
    {
        public string SoldierName { get; set; }
        public string StatusName { get; set; }
        public DateTime EventDate { get; set; }
        public bool IsDeparting { get; set; } 

        public string ActionText => IsDeparting ? "Убывает:" : "Возвращается:";
        public string IconName => IsDeparting ? "ArrowRightFromBracket" : "ArrowRightToBracket";
        public string IconColor => IsDeparting ? "#F59E0B" : "#10B981";

        public string DateString
        {
            get
            {
                var diff = (EventDate.Date - DateTime.Today).Days;
                if (diff == 0) return "Сегодня";
                if (diff == 1) return "Завтра";
                if (diff == 2) return "Послезавтра";
                return EventDate.ToString("dd.MM");
            }
        }
    }
}