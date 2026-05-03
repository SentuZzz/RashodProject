using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using WpfApp1.Models;

namespace WpfApp1.Repositories
{
    public class DutyRepository
    {
        private readonly string _connectionString = "Data Source=rashod.db;Version=3;";

        // СЛОВАРЬ ПРАВИЛ: ID наряда -> (Вместимость чел., Длительность суток)
        public readonly Dictionary<int, (int Capacity, int Duration)> DutyRules = new Dictionary<int, (int, int)>
        {
            { 1, (1, 1) },   // Дежурный по роте
            { 2, (1, 1) },   // Дежурный по парку
            { 3, (1, 1) },   // Дежурный по КПП
            { 4, (1, 1) },   // Начальник караула
            { 5, (1, 1) },   // Командир подразд. Антитеррора
            { 6, (1, 1) },   // Помощник дежурного по роте
            { 7, (3, 1) },   // Дневальный по роте
            { 8, (2, 1) },   // Дневальный по парку
            { 9, (2, 1) },   // Дневальный по КПП
            { 10, (6, 3) },  // Караульный (6 человек, 3 суток)
            { 11, (10, 1) }  // Группа Антитеррора
        };

        public List<DutyModel> GetDuties()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                return connection.Query<DutyModel>("SELECT * FROM Duties").ToList();
            }
        }

        public bool IsCapacityFull(int dutyId, DateTime date, int maxCapacity)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                int currentAssigned = connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM DutyHistory WHERE DutyID = @DutyID AND DutyDate = @DutyDate",
                    new { DutyID = dutyId, DutyDate = date });
                return currentAssigned >= maxCapacity;
            }
        }

        public bool HasRestViolation(int soldierId, DateTime startDate, int duration)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                DateTime minDate = startDate.AddDays(-1);
                DateTime maxDate = startDate.AddDays(duration);

                int conflicts = connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM DutyHistory WHERE SoldierID = @SoldierID AND DutyDate >= @MinDate AND DutyDate <= @MaxDate",
                    new { SoldierID = soldierId, MinDate = minDate, MaxDate = maxDate });

                return conflicts > 0;
            }
        }

        public void AssignDuty(int soldierId, int dutyId, DateTime startDate, int duration)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    string sql = "INSERT INTO DutyHistory (SoldierID, DutyID, DutyDate) VALUES (@SoldierID, @DutyID, @DutyDate)";

                    for (int i = 0; i < duration; i++)
                    {
                        connection.Execute(sql, new { SoldierID = soldierId, DutyID = dutyId, DutyDate = startDate.AddDays(i) }, transaction);
                    }

                    transaction.Commit();
                }
            }
        }

        public Dictionary<DateTime, string> GetBusyDatesWithInfoForSoldier(int soldierId)
        {
            var busyDates = new Dictionary<DateTime, string>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                var absences = connection.Query(
                    @"SELECT sl.StartDate, sl.EndDate, st.StatusName 
              FROM StatusLog sl 
              INNER JOIN Statuses st ON sl.StatusID = st.StatusID 
              WHERE sl.SoldierID = @SoldierID AND st.IsAbsence = 1",
                    new { SoldierID = soldierId }).ToList();

                foreach (var absence in absences)
                {
                    DateTime start = DateTime.Parse(absence.StartDate.ToString()).Date;
                    DateTime end = DateTime.Parse(absence.EndDate.ToString()).Date;

                    for (DateTime d = start; d <= end; d = d.AddDays(1))
                    {
                        busyDates[d] = absence.StatusName;
                    }
                }

                var dutyDates = connection.Query(
                    @"SELECT dh.DutyDate, d.DutyName 
              FROM DutyHistory dh 
              INNER JOIN Duties d ON dh.DutyID = d.DutyID 
              WHERE dh.SoldierID = @SoldierID",
                    new { SoldierID = soldierId }).ToList();

                foreach (var d in dutyDates)
                {
                    DateTime date = DateTime.Parse(d.DutyDate.ToString()).Date;
                    busyDates[date] = $"В наряде: {d.DutyName}";
                }
            }
            return busyDates;
        }
        public List<DashboardDutyModel> GetTomorrowDutiesStatus()
        {
            var result = new List<DashboardDutyModel>();
            DateTime tomorrow = DateTime.Today.AddDays(1);

            using (var connection = new SQLiteConnection(_connectionString))
            {
                // Получаем все виды нарядов из БД
                var allDuties = GetDuties();

                // Считаем, сколько человек УЖЕ назначено на завтра по каждому наряду
                string sql = @"SELECT DutyID, COUNT(*) as AssignedCount 
                       FROM DutyHistory 
                       WHERE date(DutyDate) = date(@Tomorrow) 
                       GROUP BY DutyID";

                var assignedCounts = connection.Query(sql, new { Tomorrow = tomorrow.ToString("yyyy-MM-dd") })
                                               .ToDictionary(row => (int)row.DutyID, row => (int)row.AssignedCount);

                foreach (var duty in allDuties)
                {
                    // Берем правило вместимости из нашего словаря (например, Дневальный = 3 человека)
                    if (DutyRules.TryGetValue(duty.DutyID, out var rule))
                    {
                        int assigned = assignedCounts.ContainsKey(duty.DutyID) ? assignedCounts[duty.DutyID] : 0;

                        result.Add(new DashboardDutyModel
                        {
                            DutyName = duty.DutyName,
                            Capacity = rule.Capacity,
                            Assigned = assigned
                        });
                    }
                }
            }

            // Сортируем: сначала те наряды, где НЕ ХВАТАЕТ людей (они важнее)
            return result.OrderByDescending(d => d.Missing > 0).ThenBy(d => d.DutyName).ToList();
        }
    }
}