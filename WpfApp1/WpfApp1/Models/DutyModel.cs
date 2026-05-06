namespace WpfApp1.Models
{
    public class DutyModel
    {
        public int DutyID { get; set; }
        public string DutyName { get; set; }
        public bool IsDaily { get; set; }
        public int RolePriority { get; set; }
        public string Location { get; set; }
        public int Capacity { get; set; }
        public int Duration { get; set; }
    }
}