using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            using (var connection = new SQLiteConnection(_connectionString))
            {
                string sql = @"
                    SELECT 
                        s.SoldierID, s.LastName, s.FirstName, s.Patronymic, s.ServiceType,
                        r.RankName, p.PositionName, u.UnitName,
                        
                        COALESCE(
                            -- 1. Сначала проверяем, есть ли боец СЕГОДНЯ в наряде
                            (SELECT 'В наряде: ' || d.DutyName
                             FROM DutyHistory dh 
                             INNER JOIN Duties d ON dh.DutyID = d.DutyID 
                             WHERE dh.SoldierID = s.SoldierID AND date(dh.DutyDate) = date(@Today)
                             LIMIT 1),

                            -- 2. Если наряда нет, проверяем статусы отсутствия (отпуск/госпиталь)
                            (SELECT st.StatusName 
                             FROM StatusLog sl 
                             INNER JOIN Statuses st ON sl.StatusID = st.StatusID 
                             WHERE sl.SoldierID = s.SoldierID 
                               AND date(@Today) >= date(sl.StartDate) 
                               AND date(@Today) <= date(sl.EndDate)
                             ORDER BY sl.LogID DESC LIMIT 1), 
                             
                        'В строю') AS CurrentStatus

                    FROM Soldiers s
                    INNER JOIN Ranks r ON s.RankID = r.RankID
                    INNER JOIN Positions p ON s.PositionID = p.PositionID
                    INNER JOIN Units u ON s.UnitID = u.UnitID";

                string todayString = DateTime.Today.ToString("yyyy-MM-dd");
                return connection.Query<SoldierModel>(sql, new { Today = todayString }).ToList();
            }
        }
    }
}
