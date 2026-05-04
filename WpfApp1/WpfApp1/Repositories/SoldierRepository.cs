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

        // Добавили параметр targetDate
        // Вопросительный знак делает дату необязательной, а "= null" задает значение по умолчанию
        public List<SoldierModel> GetAllSoldiers(DateTime? targetDate = null)
        {
            var soldiers = new List<SoldierModel>();

            // МАГИЯ ДАТ: 
            // Если дату передали (например, из календаря нарядов) - используем её.
            // Если ничего не передали (вызов из других вкладок) - вычисляем текущую активную смену (правило 16:00)
            DateTime queryDate;
            if (targetDate.HasValue)
            {
                queryDate = targetDate.Value;
            }
            else
            {
                DateTime now = DateTime.Now;
                queryDate = now.Hour < 16 ? now.Date.AddDays(-1) : now.Date;
            }

            using (var connection = new SQLiteConnection(_connectionString))
            {
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
            LEFT JOIN Units u ON s.UnitID = u.UnitID";

                // Передаем нашу вычисленную дату в SQL-запрос
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
    }
}