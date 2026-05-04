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

        public List<SoldierModel> GetAllSoldiers()
        {
            var soldiers = new List<SoldierModel>();

            // Вычисляем, какая смена сейчас активна (магия 16:00)
            DateTime now = DateTime.Now;
            DateTime activeShiftDate = now.Hour < 16 ? now.Date.AddDays(-1) : now.Date;

            using (var connection = new SQLiteConnection(_connectionString))
            {
                // Добавляем в SQL-запрос подзапрос (ActiveDutyCount), который проверяет наряды на текущую смену
                string sql = @"
                    SELECT s.*, r.RankName, p.PositionName, u.UnitName,
                   (SELECT st.StatusName 
                    FROM StatusLog sl
                    JOIN Statuses st ON sl.StatusID = st.StatusID
                    WHERE sl.SoldierID = s.SoldierID 
                      AND date('now') BETWEEN date(sl.StartDate) AND date(sl.EndDate)
                    ORDER BY sl.StartDate DESC LIMIT 1) as CurrentStatus,
                   
                   (SELECT COUNT(*) 
                    FROM DutyHistory dh 
                    WHERE dh.SoldierID = s.SoldierID 
                      AND date(dh.DutyDate) = date(@ActiveShift)) as ActiveDutyCount
                      FROM Soldiers s
                      LEFT JOIN Ranks r ON s.RankID = r.RankID
                      LEFT JOIN Positions p ON s.PositionID = p.PositionID
                      LEFT JOIN Units u ON s.UnitID = u.UnitID";
                var records = connection.Query(sql, new { ActiveShift = activeShiftDate.ToString("yyyy-MM-dd") });

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
                        // Если счетчик нарядов > 0, значит боец сейчас занят
                        IsOnActiveDuty = (long)row.ActiveDutyCount > 0
                    });
                }
            }
            return soldiers;
        }
    }
}