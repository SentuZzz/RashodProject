namespace WpfApp1.Models
{
    public class DirectoryItemModel
    {
        public int Id { get; set; }
        public string Name { get; set; }

        // Специфичные поля для нарядов
        public int Priority { get; set; }
        public bool IsDuty { get; set; }

        // НОВОЕ ПОЛЕ: Место (категория) наряда
        public string Location { get; set; }

        public string PriorityText => Priority == 1 ? "Офицеры/Прапорщики" : (Priority == 2 ? "Сержанты" : "Рядовой состав");

        public string TableName { get; set; }
        public string IdColumnName { get; set; }
        public string NameColumnName { get; set; }
    }
}