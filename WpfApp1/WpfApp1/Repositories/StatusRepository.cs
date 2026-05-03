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
        public List<NotificationModel> GetUpcomingNotifications(int daysAhead = 3)
        {
            var notifications = new List<NotificationModel>();
            DateTime today = DateTime.Today;
            DateTime future = today.AddDays(daysAhead);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                string sql = @"
            SELECT s.LastName || ' ' || substr(s.FirstName, 1, 1) || '.' as SoldierName,
                   st.StatusName, sl.StartDate, sl.EndDate
            FROM StatusLog sl
            JOIN Soldiers s ON sl.SoldierID = s.SoldierID
            JOIN Statuses st ON sl.StatusID = st.StatusID
            WHERE st.IsAbsence = 1 AND 
                 ((date(sl.StartDate) BETWEEN date(@Today) AND date(@Future))
               OR (date(sl.EndDate) BETWEEN date(@Today) AND date(@Future)))";

                var records = connection.Query(sql, new
                {
                    Today = today.ToString("yyyy-MM-dd"),
                    Future = future.ToString("yyyy-MM-dd")
                });

                foreach (dynamic row in records)
                {
                    DateTime start = DateTime.Parse(row.StartDate.ToString()).Date;
                    DateTime end = DateTime.Parse(row.EndDate.ToString()).Date;
                    string name = row.SoldierName;
                    string status = row.StatusName;

                    // Если дата начала попадает в наши 3 дня
                    if (start >= today && start <= future)
                    {
                        notifications.Add(new NotificationModel
                        {
                            SoldierName = name,
                            StatusName = status,
                            EventDate = start,
                            IsDeparting = true
                        });
                    }
                    // Если дата конца попадает в наши 3 дня
                    if (end >= today && end <= future)
                    {
                        notifications.Add(new NotificationModel
                        {
                            SoldierName = name,
                            StatusName = status,
                            EventDate = end,
                            IsDeparting = false
                        });
                    }
                }
            }
            // Сортируем по дате, чтобы ближайшие были сверху
            return notifications.OrderBy(n => n.EventDate).ToList();
        }
    }
}