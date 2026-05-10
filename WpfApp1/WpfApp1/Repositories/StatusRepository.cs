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
        private readonly string _connectionString = "Data Source=rashod.db;Version=3;Foreign Keys=True;";

        public List<StatusModel> GetAllStatuses()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                try { return connection.Query<StatusModel>("SELECT * FROM Statuses").ToList(); } catch { return new List<StatusModel>(); }
            }
        }

        public void AddStatusLog(int soldierId, int statusId, DateTime startDate, DateTime endDate, string documentInfo)
        {
            // Оставили пустым для обратной совместимости, если старые методы где-то вызываются
        }

        // ИСПРАВЛЕНИЕ: Теперь берем окно в днях (по умолчанию 2) и ищем и в прошлом, и в будущем
        public List<NotificationModel> GetUpcomingNotifications(int daysWindow = 2)
        {
            var notifications = new List<NotificationModel>();
            DateTime today = DateTime.Today;
            DateTime past = today.AddDays(-daysWindow);   // 2 дня назад
            DateTime future = today.AddDays(daysWindow);  // 2 дня вперед

            using (var connection = new SQLiteConnection(_connectionString))
            {
                // ИСПРАВЛЕНИЕ: Запрашиваем данные из НОВОЙ таблицы SoldierStatuses!
                string sql = @"
                    SELECT s.LastName || ' ' || substr(s.FirstName, 1, 1) || '.' as SoldierName,
                           ss.StatusType as StatusName, 
                           ss.StartDate, 
                           ss.EndDate
                    FROM SoldierStatuses ss
                    JOIN Soldiers s ON ss.SoldierID = s.SoldierID
                    WHERE ss.StatusType != 'В строю' AND 
                         ((date(ss.StartDate) BETWEEN date(@Past) AND date(@Future))
                       OR (date(ss.EndDate) BETWEEN date(@Past) AND date(@Future)))";

                var records = connection.Query(sql, new
                {
                    Past = past.ToString("yyyy-MM-dd"),
                    Future = future.ToString("yyyy-MM-dd")
                });

                foreach (dynamic row in records)
                {
                    DateTime start = DateTime.Parse(row.StartDate.ToString()).Date;
                    DateTime end = DateTime.Parse(row.EndDate.ToString()).Date;
                    string name = row.SoldierName;
                    string status = row.StatusName;

                    // Если дата начала попадает в наше окно (Боец убывает)
                    if (start >= past && start <= future)
                    {
                        notifications.Add(new NotificationModel
                        {
                            SoldierName = name,
                            StatusName = status,
                            EventDate = start,
                            IsDeparting = true
                        });
                    }

                    // Если дата конца попадает в наше окно (Боец возвращается)
                    if (end >= past && end <= future)
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

            // Сортируем по дате, чтобы хронология событий была правильной (сначала прошедшие, потом будущие)
            return notifications.OrderBy(n => n.EventDate).ToList();
        }
    }
}