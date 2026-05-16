using System;
using System.Collections.Generic;

namespace WpfApp1.Models
{
    public class TaskCategoryModel
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
    }

    public class TaskModel
    {
        public int TaskHistoryID { get; set; }
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public string TaskName { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime? DueDate { get; set; }
        public string Status { get; set; }
        public List<SoldierModel> AssignedSoldiers { get; set; } = new List<SoldierModel>();

        public string StartDateText => CreationDate.ToString("dd.MM HH:mm");
        public string DeadlineText => DueDate.HasValue ? DueDate.Value.ToString("dd.MM HH:mm") : "Без срока";
        public bool IsOverdue => DueDate.HasValue && DueDate.Value < DateTime.Now && Status != "Выполнено";

        public string CategoryColor
        {
            get
            {
                switch (CategoryName)
                {
                    case "ПХД": return "#3B82F6";
                    case "Уборка территории": return "#10B981";
                    case "Разгрузка/Погрузка": return "#F59E0B";
                    case "Ремонт": return "#EF4444";
                    default: return "#6B7280";
                }
            }
        }
    }
}