using System.Collections.Generic;

namespace WpfApp1.Models
{
    public class DutyRoleItem
    {
        public string RoleName { get; set; }     
        public string PersonnelName { get; set; } 
    }

    public class ActiveDutyCardModel
    {
        public string GroupName { get; set; } 
        public string ShiftPeriod { get; set; }

        public List<DutyRoleItem> PersonnelRoles { get; set; }
    }
}