using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using WpfApp1.Models;

namespace WpfApp1.Repositories
{
    public class SoldierRepository
    {
        private readonly string _connectionString = "Data Source=rashod.db;Version=3;Foreign Keys=True;";

        // НОВЫЙ КОНСТРУКТОР: Автоматически обновляет структуру базы данных
        public SoldierRepository()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                // 1. Создаем таблицу статусов, если её еще нет в базе
                string createStatusesTable = @"
                    CREATE TABLE IF NOT EXISTS SoldierStatuses (
                        StatusID INTEGER PRIMARY KEY AUTOINCREMENT,
                        SoldierID INTEGER NOT NULL,
                        StatusType TEXT NOT NULL,
                        StartDate DATETIME NOT NULL,
                        EndDate DATETIME NOT NULL,
                        FOREIGN KEY(SoldierID) REFERENCES Soldiers(SoldierID) ON DELETE CASCADE
                    )";
                connection.Execute(createStatusesTable);

                // 2. Безопасно добавляем новые колонки в таблицу Soldiers (для Отчества и Увольнения)
                // Используем try-catch, потому что если колонки уже есть, SQLite выдаст ошибку, которую мы просто проигнорируем
                try { connection.Execute("ALTER TABLE Soldiers ADD COLUMN MiddleName TEXT"); } catch { }
                try { connection.Execute("ALTER TABLE Soldiers ADD COLUMN IsDismissed INTEGER DEFAULT 0"); } catch { }
            }
        }

        public List<SoldierModel> GetAllSoldiers(DateTime? dutyDate = null, bool includeDismissed = false)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                string sql = @"
                    SELECT s.*, r.RankName, p.PositionName, u.UnitName
                    FROM Soldiers s
                    LEFT JOIN Ranks r ON s.RankID = r.RankID
                    LEFT JOIN Positions p ON s.PositionID = p.PositionID
                    LEFT JOIN Units u ON s.UnitID = u.UnitID
                    WHERE 1=1 ";

                if (!includeDismissed)
                {
                    sql += " AND s.IsDismissed = 0 ";
                }

                var soldiers = connection.Query<SoldierModel>(sql).ToList();

                // Загрузка статусов (в отпуске, в госпитале и т.д.)
                var statuses = connection.Query("SELECT * FROM SoldierStatuses").ToList();
                foreach (var s in soldiers)
                {
                    var activeStatus = statuses.FirstOrDefault(st =>
                        st.SoldierID == s.SoldierID &&
                        DateTime.Parse(st.StartDate.ToString()) <= DateTime.Today &&
                        DateTime.Parse(st.EndDate.ToString()) >= DateTime.Today);

                    s.CurrentStatus = activeStatus != null ? (string)activeStatus.StatusType : "В строю";
                }

                // Проверка нарядов на выбранную дату
                if (dutyDate.HasValue)
                {
                    string dutySql = @"
                        SELECT ta.SoldierID 
                        FROM TaskAssignments ta
                        JOIN TaskHistory th ON ta.TaskHistoryID = th.TaskHistoryID
                        JOIN Duties d ON th.CategoryID = d.DutyID
                        WHERE date(th.CreationDate) = date(@DutyDate)";

                    var busyIds = connection.Query<int>(dutySql, new { DutyDate = dutyDate.Value }).ToHashSet();
                    foreach (var s in soldiers)
                    {
                        if (busyIds.Contains(s.SoldierID)) s.IsOnActiveDuty = true;
                    }
                }

                return soldiers;
            }
        }

        public void AddSoldier(SoldierModel soldier)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                string sql = @"INSERT INTO Soldiers (RankID, PositionID, UnitID, FirstName, LastName, MiddleName, ServiceType) 
                               VALUES (@RankID, @PositionID, @UnitID, @FirstName, @LastName, @MiddleName, @ServiceType)";
                connection.Execute(sql, soldier);
            }
        }

        public void UpdateSoldier(SoldierModel soldier)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                string sql = @"UPDATE Soldiers 
                               SET RankID = @RankID, 
                                   PositionID = @PositionID, 
                                   UnitID = @UnitID, 
                                   FirstName = @FirstName, 
                                   LastName = @LastName, 
                                   MiddleName = @MiddleName, 
                                   ServiceType = @ServiceType 
                               WHERE SoldierID = @SoldierID";
                connection.Execute(sql, soldier);
            }
        }

        public void UpdateSoldierStatus(int soldierId, string statusType, DateTime startDate, DateTime endDate)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Execute("DELETE FROM SoldierStatuses WHERE SoldierID = @Id", new { Id = soldierId });

                if (statusType != "В строю")
                {
                    string sql = "INSERT INTO SoldierStatuses (SoldierID, StatusType, StartDate, EndDate) VALUES (@Id, @Type, @Start, @End)";
                    connection.Execute(sql, new { Id = soldierId, Type = statusType, Start = startDate, End = endDate });
                }
            }
        }

        public void DismissSoldier(int soldierId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Execute("UPDATE Soldiers SET IsDismissed = 1 WHERE SoldierID = @Id", new { Id = soldierId });
            }
        }
    }
}