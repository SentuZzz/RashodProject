using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using WpfApp1.Models;

namespace WpfApp1.Repositories
{
    public class DirectoryRepository
    {
        private readonly string _connectionString = "Data Source=rashod.db;Version=3;";

        public DirectoryRepository()
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                // Пробуем безопасно добавить колонки Location и Capacity (Квота)
                try { db.Execute("ALTER TABLE Duties ADD COLUMN Location TEXT DEFAULT 'Общее'"); } catch { }
                try { db.Execute("ALTER TABLE Duties ADD COLUMN Capacity INTEGER DEFAULT 1"); } catch { }
            }
        }

        public List<DirectoryItemModel> GetDictionary(string tableName, string idCol, string nameCol)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = $"SELECT {idCol} AS Id, {nameCol} AS Name FROM {tableName} ORDER BY {idCol}";
                var items = db.Query<DirectoryItemModel>(sql).ToList();

                items.ForEach(i => { i.TableName = tableName; i.IdColumnName = idCol; i.NameColumnName = nameCol; });
                return items;
            }
        }

        public void AddItem(string tableName, string nameCol, string nameValue)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = $"INSERT INTO {tableName} ({nameCol}) VALUES (@Name)";
                db.Execute(sql, new { Name = nameValue?.Trim() }); // .Trim() убирает случайные пробелы по краям
            }
        }

        public void DeleteItem(string tableName, string idCol, int idValue)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = $"DELETE FROM {tableName} WHERE {idCol} = @Id";
                db.Execute(sql, new { Id = idValue });
            }
        }

        public void UpdateItem(string tableName, string idCol, string nameCol, int idValue, string newName)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = $"UPDATE {tableName} SET {nameCol} = @Name WHERE {idCol} = @Id";
                db.Execute(sql, new { Name = newName?.Trim(), Id = idValue });
            }
        }

        // --- МЕТОДЫ ДЛЯ НАРЯДОВ ---

        public List<DirectoryItemModel> GetDuties()
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                // Теперь мы вытаскиваем еще и Capacity из базы!
                string sql = "SELECT DutyID AS Id, DutyName AS Name, RolePriority AS Priority, Location, Capacity FROM Duties ORDER BY Location, RolePriority";
                var items = db.Query<DirectoryItemModel>(sql).ToList();

                items.ForEach(i => { i.TableName = "Duties"; i.IdColumnName = "DutyID"; i.IsDuty = true; });
                return items;
            }
        }

        public void AddDuty(string nameValue, int priorityValue, string locationValue, int capacityValue)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = "INSERT INTO Duties (DutyName, RolePriority, Location, Capacity) VALUES (@Name, @Priority, @Location, @Capacity)";
                db.Execute(sql, new
                {
                    Name = nameValue?.Trim(),
                    Priority = priorityValue,
                    Location = string.IsNullOrWhiteSpace(locationValue) ? "Общее" : locationValue.Trim(),
                    Capacity = capacityValue
                });
            }
        }

        public void UpdateDuty(int idValue, string newName, int newPriority, string newLocation, int newCapacity)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = "UPDATE Duties SET DutyName = @Name, RolePriority = @Priority, Location = @Location, Capacity = @Capacity WHERE DutyID = @Id";
                db.Execute(sql, new
                {
                    Name = newName?.Trim(),
                    Priority = newPriority,
                    Location = string.IsNullOrWhiteSpace(newLocation) ? "Общее" : newLocation.Trim(),
                    Capacity = newCapacity,
                    Id = idValue
                });
            }
        }
    }
}