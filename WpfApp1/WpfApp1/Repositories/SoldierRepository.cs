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

                    try { connection.Execute("ALTER TABLE Soldiers ADD COLUMN Patronymic TEXT"); } catch { }
                    try { connection.Execute("ALTER TABLE Soldiers ADD COLUMN IsDismissed INTEGER DEFAULT 0"); } catch { }
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

                if (!includeDismissed) sql += " AND s.IsDismissed = 0 ";

                var soldiers = connection.Query<SoldierModel>(sql).ToList();

                // 1. БАЗОВЫЕ СТАТУСЫ (Госпиталь, Отпуск)
                string todayStr = DateTime.Today.ToString("yyyy-MM-dd");
                string statusSql = @"SELECT SoldierID, StatusType FROM SoldierStatuses 
                                     WHERE date(StartDate) <= date(@Today) AND date(EndDate) >= date(@Today)";
                var activeStatuses = connection.Query(statusSql, new { Today = todayStr }).ToList();

                // 2. АКТИВНЫЕ ЗАДАЧИ: Кто прямо сейчас выполняет задачу
                string activeTasksSql = @"
                    SELECT ta.SoldierID 
                    FROM TaskAssignments ta
                    JOIN TaskHistory th ON ta.TaskHistoryID = th.TaskHistoryID
                    WHERE th.Status = 'В процессе'";
                var onTaskIds = connection.Query<int>(activeTasksSql).ToHashSet();

                // 3. РЕАЛЬНОЕ ВРЕМЯ НАРЯДОВ (Правило 16:00 - 16:00)
                DateTime now = DateTime.Now;
                DateTime currentShiftDate = now.Hour < 16 ? now.Date.AddDays(-1) : now.Date;

                string currentDutySql = @"SELECT SoldierID FROM DutyHistory WHERE date(DutyDate) = date(@CurrentShift)";
                var currentlyOnDutyIds = connection.Query<int>(currentDutySql, new { CurrentShift = currentShiftDate }).ToHashSet();

                foreach (var s in soldiers)
                {
                    // Назначаем базовый статус
                    var activeStatus = activeStatuses.FirstOrDefault(st => (int)st.SoldierID == s.SoldierID);
                    s.CurrentStatus = activeStatus != null ? (string)activeStatus.StatusType : "В строю";

                    // Перекрываем статусом задачи
                    if (s.CurrentStatus == "В строю" && onTaskIds.Contains(s.SoldierID))
                    {
                        s.CurrentStatus = "На задаче";
                    }

                    // Перекрываем статусом наряда (Наивысший приоритет)
                    if ((s.CurrentStatus == "В строю" || s.CurrentStatus == "На задаче") && currentlyOnDutyIds.Contains(s.SoldierID))
                    {
                        s.CurrentStatus = "В наряде";
                    }

                    if (s.JoinDate == DateTime.MinValue) s.JoinDate = DateTime.Today;
                }

                // 4. ПЛАНИРОВАНИЕ (Для календаря)
                if (dutyDate.HasValue)
                {
                    string plannedDutySql = @"SELECT SoldierID FROM DutyHistory WHERE date(DutyDate) = date(@DutyDate)";
                    var plannedDutyIds = connection.Query<int>(plannedDutySql, new { DutyDate = dutyDate.Value }).ToHashSet();

                    foreach (var s in soldiers)
                    {
                        if (plannedDutyIds.Contains(s.SoldierID)) s.IsOnActiveDuty = true;
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
                string sql = @"UPDATE Soldiers 
                               SET RankID = @RankID, PositionID = @PositionID, UnitID = @UnitID, 
                                   FirstName = @FirstName, LastName = @LastName, Patronymic = @Patronymic, ServiceType = @ServiceType 
                               WHERE SoldierID = @SoldierID";
                connection.Execute(sql, soldier);
            }
        }

        public void UpdateSoldierStatus(int soldierId, string statusType, DateTime startDate, DateTime endDate)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                // 1. Сначала "закрываем" любой текущий активный статус (ставим дату окончания на вчерашний день), 
                // чтобы статусы не наслаивались друг на друга
                string closeOldSql = "UPDATE SoldierStatuses SET EndDate = date('now', '-1 day') WHERE SoldierID = @Id AND date(EndDate) >= date('now')";
                connection.Execute(closeOldSql, new { Id = soldierId });

                // 2. Если командир выбрал не "В строю", а какой-то реальный статус отсутствия - создаем новую запись
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

        public string GetFutureObligationsInfo(int soldierId, DateTime startDate, DateTime endDate)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                var obligations = new List<string>();

                // 1. Проверяем будущие наряды
                string dutySql = @"
                    SELECT d.DutyName, dh.DutyDate 
                    FROM DutyHistory dh 
                    JOIN Duties d ON dh.DutyID = d.DutyID 
                    WHERE dh.SoldierID = @Id 
                      AND date(dh.DutyDate) >= date(@Start) 
                      AND date(dh.DutyDate) <= date(@End)";

                var duties = connection.Query(dutySql, new { Id = soldierId, Start = startDate, End = endDate });
                foreach (var d in duties)
                {
                    DateTime dt = DateTime.Parse(d.DutyDate.ToString());
                    obligations.Add($"• Наряд «{d.DutyName}» ({dt:dd.MM.yyyy})");
                }

                // 2. Проверяем невыполненные задачи, которые попадают на эти даты
                string taskSql = @"
                    SELECT th.TaskName, th.DueDate 
                    FROM TaskAssignments ta 
                    JOIN TaskHistory th ON ta.TaskHistoryID = th.TaskHistoryID 
                    WHERE ta.SoldierID = @Id 
                      AND th.Status != 'Выполнено'
                      AND date(th.CreationDate) <= date(@End) 
                      AND (th.DueDate IS NULL OR date(th.DueDate) >= date(@Start))";

                var tasks = connection.Query(taskSql, new { Id = soldierId, Start = startDate, End = endDate });
                foreach (var t in tasks)
                {
                    string deadlineInfo = t.DueDate != null ? $"до {DateTime.Parse(t.DueDate.ToString()):dd.MM.yyyy}" : "без срока";
                    obligations.Add($"• Задача «{t.TaskName}» ({deadlineInfo})");
                }

                // Если нашли пересечения - возвращаем единый текст
                if (obligations.Any())
                {
                    return string.Join("\n", obligations);
                }

                return null; // Всё чисто
            }
        }
    }
}