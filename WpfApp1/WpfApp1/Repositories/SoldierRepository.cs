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
        private static bool _isDbInitialized = false;

        public SoldierRepository()
        {
            if (!_isDbInitialized)
            {
                using (var connection = new SQLiteConnection(_connectionString))
                {
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

                    // ИСПРАВЛЕНИЕ: Пытаемся добавить колонку Patronymic (а не MiddleName)
                    try { connection.Execute("ALTER TABLE Soldiers ADD COLUMN Patronymic TEXT"); } catch { }
                    try { connection.Execute("ALTER TABLE Soldiers ADD COLUMN IsDismissed INTEGER DEFAULT 0"); } catch { }
                    // Пытаемся добавить JoinDate, если вдруг его не было в изначальной схеме (хотя в DataSeeder он есть)
                    try { connection.Execute("ALTER TABLE Soldiers ADD COLUMN JoinDate DATETIME DEFAULT CURRENT_DATE"); } catch { }
                }
                _isDbInitialized = true;
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

                string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
                string statusSql = @"
                    SELECT SoldierID, StatusType 
                    FROM SoldierStatuses 
                    WHERE date(StartDate) <= date(@Today) AND date(EndDate) >= date(@Today)";

                var activeStatuses = connection.Query(statusSql, new { Today = todayStr }).ToList();

                foreach (var s in soldiers)
                {
                    var activeStatus = activeStatuses.FirstOrDefault(st => (int)st.SoldierID == s.SoldierID);
                    s.CurrentStatus = activeStatus != null ? (string)activeStatus.StatusType : "В строю";

                    // Если JoinDate пустая (например, старые записи), ставим дефолтную
                    if (s.JoinDate == DateTime.MinValue) s.JoinDate = DateTime.Today;
                }

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
                if (soldier.JoinDate == DateTime.MinValue) soldier.JoinDate = DateTime.Today;

                // ИСПРАВЛЕНИЕ: Используем Patronymic вместо MiddleName
                string sql = @"INSERT INTO Soldiers (RankID, PositionID, UnitID, FirstName, LastName, Patronymic, ServiceType, JoinDate) 
                               VALUES (@RankID, @PositionID, @UnitID, @FirstName, @LastName, @Patronymic, @ServiceType, @JoinDate)";
                connection.Execute(sql, soldier);
            }
        }

        public void AddSoldiersBulk(IEnumerable<SoldierModel> soldiers)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    // ИСПРАВЛЕНИЕ: Используем Patronymic вместо MiddleName
                    string sql = @"INSERT INTO Soldiers (RankID, PositionID, UnitID, FirstName, LastName, Patronymic, ServiceType, JoinDate) 
                                   VALUES (@RankID, @PositionID, @UnitID, @FirstName, @LastName, @Patronymic, @ServiceType, @JoinDate)";

                    foreach (var soldier in soldiers)
                    {
                        if (soldier.JoinDate == DateTime.MinValue) soldier.JoinDate = DateTime.Today;
                        connection.Execute(sql, soldier, transaction);
                    }

                    transaction.Commit();
                }
            }
        }

        public void UpdateSoldier(SoldierModel soldier)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                // ИСПРАВЛЕНИЕ: Устанавливаем Patronymic = @Patronymic
                string sql = @"UPDATE Soldiers 
                               SET RankID = @RankID, 
                                   PositionID = @PositionID, 
                                   UnitID = @UnitID, 
                                   FirstName = @FirstName, 
                                   LastName = @LastName, 
                                   Patronymic = @Patronymic, 
                                   ServiceType = @ServiceType 
                               WHERE SoldierID = @SoldierID";
                connection.Execute(sql, soldier);
            }
        }

        public void UpdateSoldierStatus(int soldierId, string statusType, DateTime startDate, DateTime endDate)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
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