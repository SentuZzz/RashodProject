using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.SQLite;
using Dapper;
using WpfApp1.Models;

namespace WpfApp1.Repositories
{
    public class SoldierRepository
    {
        private readonly string _connectionString = "Data Source=rashod.db;Version=3;";

        public SoldierRepository()
        {
            // Умное обновление БД: добавляем колонку для "Мягкого удаления" (Дембель)
            using (var connection = new SQLiteConnection(_connectionString))
            {
                try { connection.Execute("ALTER TABLE Soldiers ADD COLUMN IsDismissed BOOLEAN DEFAULT 0"); } catch { }
            }
        }

        public List<SoldierModel> GetAllSoldiers(DateTime? targetDate = null)
        {
            var soldiers = new List<SoldierModel>();
            DateTime queryDate = targetDate ?? (DateTime.Now.Hour < 16 ? DateTime.Now.Date.AddDays(-1) : DateTime.Now.Date);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                // Вытаскиваем только действующих бойцов (IsDismissed = 0)
                string sql = @"
            SELECT s.*, r.RankName, p.PositionName, u.UnitName,
                   (SELECT st.StatusName
                     FROM StatusLog sl
                    JOIN Statuses st ON sl.StatusID = st.StatusID
                    WHERE sl.SoldierID = s.SoldierID
                       AND date(@TargetDate) BETWEEN date(sl.StartDate) AND date(sl.EndDate)
                    ORDER BY sl.StartDate DESC LIMIT 1) as CurrentStatus,
                   (SELECT COUNT(*)
                     FROM DutyHistory dh
                     WHERE dh.SoldierID = s.SoldierID
                       AND date(dh.DutyDate) = date(@TargetDate)) as ActiveDutyCount
            FROM Soldiers s
            LEFT JOIN Ranks r ON s.RankID = r.RankID
            LEFT JOIN Positions p ON s.PositionID = p.PositionID
            LEFT JOIN Units u ON s.UnitID = u.UnitID
            WHERE s.IsDismissed = 0";

                var records = connection.Query(sql, new { TargetDate = queryDate.ToString("yyyy-MM-dd") });

                foreach (var row in records)
                {
                    soldiers.Add(new SoldierModel
                    {
                        SoldierID = (int)row.SoldierID,
                        FirstName = row.FirstName,
                        LastName = row.LastName,
                        Patronymic = row.Patronymic,
                        RankID = (int)row.RankID,
                        PositionID = (int)row.PositionID,
                        UnitID = (int)row.UnitID,
                        RankName = row.RankName,
                        PositionName = row.PositionName,
                        UnitName = row.UnitName,
                        ServiceType = row.ServiceType,
                        CurrentStatus = row.CurrentStatus ?? "В строю",
                        IsOnActiveDuty = (long)row.ActiveDutyCount > 0
                    });
                }
            }
            return soldiers;
        }

        // --- НОВЫЕ МЕТОДЫ ---

        // 1. Добавление одного бойца
        public void AddSoldier(SoldierModel soldier)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                string sql = @"
                    INSERT INTO Soldiers (LastName, FirstName, Patronymic, RankID, PositionID, UnitID, ServiceType, JoinDate) 
                    VALUES (@LastName, @FirstName, @Patronymic, @RankID, @PositionID, @UnitID, @ServiceType, @JoinDate)";

                connection.Execute(sql, new
                {
                    LastName = soldier.LastName?.Trim(),
                    FirstName = soldier.FirstName?.Trim() ?? "",
                    Patronymic = soldier.Patronymic?.Trim() ?? "",
                    RankID = soldier.RankID,
                    PositionID = soldier.PositionID,
                    UnitID = soldier.UnitID,
                    ServiceType = soldier.ServiceType,
                    JoinDate = DateTime.Now.ToString("yyyy-MM-dd")
                });
            }
        }

        // 2. Массовое добавление списка бойцов (для ВМП)
        public void AddSoldiersMass(List<SoldierModel> soldiers)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    string sql = @"
                        INSERT INTO Soldiers (LastName, FirstName, Patronymic, RankID, PositionID, UnitID, ServiceType, JoinDate) 
                        VALUES (@LastName, @FirstName, @Patronymic, @RankID, @PositionID, @UnitID, @ServiceType, @JoinDate)";

                    foreach (var s in soldiers)
                    {
                        connection.Execute(sql, new
                        {
                            LastName = s.LastName?.Trim(),
                            FirstName = s.FirstName?.Trim() ?? "",
                            Patronymic = s.Patronymic?.Trim() ?? "",
                            RankID = s.RankID,
                            PositionID = s.PositionID,
                            UnitID = s.UnitID,
                            ServiceType = s.ServiceType,
                            JoinDate = DateTime.Now.ToString("yyyy-MM-dd")
                        }, transaction);
                    }
                    transaction.Commit();
                }
            }
        }

        // 3. "Мягкое" увольнение в запас (Дембель)
        public void DismissSoldier(int soldierId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Execute("UPDATE Soldiers SET IsDismissed = 1 WHERE SoldierID = @Id", new { Id = soldierId });
            }
        }
    }
}