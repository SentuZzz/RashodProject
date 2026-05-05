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
                // Пробуем безопасно добавить колонку Location (если она уже есть, try-catch просто проглотит ошибку)
                try { db.Execute("ALTER TABLE Duties ADD COLUMN Location TEXT DEFAULT 'Общее'"); } catch { }
            }
        }

        public List<DirectoryItemModel> GetDictionary(string tableName, string idCol, string nameCol)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                // Вытаскиваем нужную таблицу и подстраиваем её под нашу универсальную модель
                string sql = $"SELECT {idCol} AS Id, {nameCol} AS Name FROM {tableName} ORDER BY {idCol}";
                var items = db.Query<DirectoryItemModel>(sql).ToList();

                // Запоминаем, откуда мы это взяли (чтобы потом уметь удалять)
                items.ForEach(i => { i.TableName = tableName; i.IdColumnName = idCol; i.NameColumnName = nameCol; });
                return items;
            }
        }

        public void AddItem(string tableName, string nameCol, string nameValue)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = $"INSERT INTO {tableName} ({nameCol}) VALUES (@Name)";
                db.Execute(sql, new { Name = nameValue });
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
                db.Execute(sql, new { Name = newName, Id = idValue });
            }
        }

        public List<DirectoryItemModel> GetDuties()
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = "SELECT DutyID AS Id, DutyName AS Name, RolePriority AS Priority, Location FROM Duties ORDER BY Location, RolePriority";
                var items = db.Query<DirectoryItemModel>(sql).ToList();
                items.ForEach(i => { i.TableName = "Duties"; i.IdColumnName = "DutyID"; i.IsDuty = true; });
                return items;
            }
        }

        public void AddDuty(string nameValue, int priorityValue, string locationValue)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = "INSERT INTO Duties (DutyName, RolePriority, Location) VALUES (@Name, @Priority, @Location)";
                db.Execute(sql, new { Name = nameValue, Priority = priorityValue, Location = locationValue ?? "Общее" });
            }
        }

        public void UpdateDuty(int idValue, string newName, int newPriority, string newLocation)
        {
            using (var db = new SQLiteConnection(_connectionString))
            {
                string sql = "UPDATE Duties SET DutyName = @Name, RolePriority = @Priority, Location = @Location WHERE DutyID = @Id";
                db.Execute(sql, new { Name = newName, Priority = newPriority, Location = newLocation ?? "Общее", Id = idValue });
            }
        }
    }
}