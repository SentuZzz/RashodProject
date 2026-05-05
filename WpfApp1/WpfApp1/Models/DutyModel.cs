namespace WpfApp1.Models
{
    public class DutyModel
    {
        public int DutyID { get; set; }
        public string DutyName { get; set; }
        public bool IsDaily { get; set; }

        // Добавленные поля из базы данных для правильной работы логики
        public int RolePriority { get; set; }
        public string Location { get; set; }
        public int Capacity { get; set; }
    }
}