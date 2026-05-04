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
        public List<DashboardDutyModel> GetDutiesStatusForDate(DateTime targetDate)
        {
            var result = new List<DashboardDutyModel>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                var allDuties = GetDuties();

                string sql = @"SELECT DutyID, COUNT(*) as AssignedCount 
                       FROM DutyHistory 
                       WHERE date(DutyDate) = date(@TargetDate) 
                       GROUP BY DutyID";

                var assignedCounts = connection.Query(sql, new { TargetDate = targetDate.ToString("yyyy-MM-dd") })
                                               .ToDictionary(row => (int)row.DutyID, row => (int)row.AssignedCount);

                foreach (var duty in allDuties)
                {
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

            return result.OrderByDescending(d => d.Missing > 0).ThenBy(d => d.DutyName).ToList();
        }
        public List<ActiveDutyCardModel> GetActiveDutiesForDate(DateTime shiftDate)
        {
            var result = new List<ActiveDutyCardModel>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                // Магия SQL: объединяем историю с Должностями и Группами, сортируем по приоритету
                string sql = @"
            SELECT dg.GroupName, d.DutyName as RoleName, r.RankName, s.LastName, s.FirstName, s.Patronymic
            FROM DutyHistory dh
            JOIN Duties d ON dh.DutyID = d.DutyID
            JOIN DutyGroups dg ON d.GroupID = dg.GroupID
            JOIN Soldiers s ON dh.SoldierID = s.SoldierID
            JOIN Ranks r ON s.RankID = r.RankID
            WHERE date(dh.DutyDate) = date(@ShiftDate)
            ORDER BY dg.GroupID, d.RolePriority, s.RankID";

                var records = connection.Query(sql, new { ShiftDate = shiftDate.ToString("yyyy-MM-dd") });

                // Группируем по названию ГРУППЫ (например, все кто в "Наряде по роте")
                var grouped = records.GroupBy(r => (string)r.GroupName);

                foreach (var group in grouped)
                {
                    var rolesList = new List<DutyRoleItem>();
                    foreach (var p in group)
                    {
                        string f = string.IsNullOrEmpty((string)p.FirstName) ? "" : ((string)p.FirstName)[0] + ".";
                        string m = string.IsNullOrEmpty((string)p.Patronymic) ? "" : ((string)p.Patronymic)[0] + ".";
                        string rank = p.RankName;

                        rolesList.Add(new DutyRoleItem
                        {
                            RoleName = p.RoleName, // "Дежурный"
                            PersonnelName = $"{rank} {p.LastName} {f}{m}" // "С-т Иванов И.И."
                        });
                    }

                    result.Add(new ActiveDutyCardModel
                    {
                        GroupName = group.Key,
                        PersonnelRoles = rolesList,
                        ShiftPeriod = $"{shiftDate:dd.MM} - {shiftDate.AddDays(1):dd.MM}"
                    });
                }
            }
            return result;
        }
    }
}