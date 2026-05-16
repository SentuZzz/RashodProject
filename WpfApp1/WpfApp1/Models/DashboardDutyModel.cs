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
        public int Capacity { get; set; }  
        public int Assigned { get; set; }   

        public int Missing => Capacity - Assigned;

        public string StatusText => Missing > 0 ? $"ОСТАЛОСЬ НАЗНАЧИТЬ: {Missing}" : "УКОМПЛЕКТОВАН";

        public string StatusColor => Missing > 0 ? "#DC2626" : "#16A34A";

        public string IconName => Missing > 0 ? "CircleExclamation" : "CheckCircle";
    }
}
