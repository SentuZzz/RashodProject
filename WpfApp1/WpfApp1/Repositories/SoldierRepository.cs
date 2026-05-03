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
                // Собираем данные из 4-х таблиц с помощью JOIN
                string sql = @"
                    SELECT s.SoldierID, s.LastName, s.FirstName, s.Patronymic, s.ServiceType,
                    r.RankName, p.PositionName, u.UnitName
                    FROM Soldiers s
                    INNER JOIN Ranks r ON s.RankID = r.RankID
                    INNER JOIN Positions p ON s.PositionID = p.PositionID
                    INNER JOIN Units u ON s.UnitID = u.UnitID";

                // Выполняем запрос и возвращаем список
                return connection.Query<SoldierModel>(sql).ToList();
            }
        }
    }
}
