using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using WpfApp1.Models;

namespace WpfApp1.Repositories
{
    public class StatusRepository
    {
        private readonly string _connectionString = "Data Source=rashod.db;Version=3;";

        public List<StatusModel> GetAllStatuses()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                return connection.Query<StatusModel>("SELECT * FROM Statuses").ToList();
            }
        }

        public void AddStatusLog(int soldierId, int statusId, DateTime startDate, DateTime endDate, string documentInfo)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                string sql = @"
                    INSERT INTO StatusLog (SoldierID, StatusID, StartDate, EndDate, DocumentInfo) 
                    VALUES (@SoldierID, @StatusID, @StartDate, @EndDate, @DocumentInfo)";

                connection.Execute(sql, new
                {
                    SoldierID = soldierId,
                    StatusID = statusId,
                    StartDate = startDate,
                    EndDate = endDate,
                    DocumentInfo = documentInfo
                });
            }
        }
    }
}