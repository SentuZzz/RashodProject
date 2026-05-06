namespace WpfApp1.Models
{
    public class DirectoryItemModel
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Priority { get; set; }
        public bool IsDuty { get; set; }
        public string Location { get; set; }
        public int Capacity { get; set; }
        public int Duration { get; set; }

        public string PriorityText => Priority == 1 ? "Офицеры/Прапорщики" : (Priority == 2 ? "Сержанты" : "Рядовой состав");
        public string TableName { get; set; }
        public string IdColumnName { get; set; }
        public string NameColumnName { get; set; }
    }
}