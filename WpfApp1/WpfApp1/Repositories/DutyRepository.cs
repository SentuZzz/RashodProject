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

        // ПРОВЕРКА КВОТЫ: Есть ли места на этот день?
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

        // ПРОВЕРКА ОТДЫХА: Нет ли нарядов за день до, во время и через день после?
        public bool HasRestViolation(int soldierId, DateTime startDate, int duration)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                // Захватываем сутки до и сутки после наряда для проверки отдыха
                DateTime minDate = startDate.AddDays(-1);
                DateTime maxDate = startDate.AddDays(duration);

                int conflicts = connection.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM DutyHistory WHERE SoldierID = @SoldierID AND DutyDate >= @MinDate AND DutyDate <= @MaxDate",
                    new { SoldierID = soldierId, MinDate = minDate, MaxDate = maxDate });

                return conflicts > 0;
            }
        }

        // НОВОЕ НАЗНАЧЕНИЕ: Умеет записывать многодневные наряды
        public void AssignDuty(int soldierId, int dutyId, DateTime startDate, int duration)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                // Используем транзакцию, чтобы если произойдет сбой, 3-дневный наряд не записался наполовину
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

        public List<DateTime> GetBusyDatesForSoldier(int soldierId)
        {
            // Используем HashSet, чтобы даты гарантированно не дублировались
            var busyDates = new HashSet<DateTime>();

            using (var connection = new SQLiteConnection(_connectionString))
            {
                // 1. Получаем даты существующих нарядов
                var dutyDates = connection.Query<DateTime>(
                    "SELECT DutyDate FROM DutyHistory WHERE SoldierID = @SoldierID",
                    new { SoldierID = soldierId }).ToList();

                foreach (var d in dutyDates)
                {
                    busyDates.Add(d);
                }

                // 2. Получаем периоды отсутствия (где IsAbsence = 1)
                var absences = connection.Query(
                    @"SELECT sl.StartDate, sl.EndDate 
                      FROM StatusLog sl 
                      INNER JOIN Statuses st ON sl.StatusID = st.StatusID 
                      WHERE sl.SoldierID = @SoldierID AND st.IsAbsence = 1",
                    new { SoldierID = soldierId }).ToList();

                foreach (var absence in absences)
                {
                    // Парсим даты из базы
                    DateTime start = Convert.ToDateTime(absence.StartDate);
                    DateTime end = Convert.ToDateTime(absence.EndDate);

                    // Проходимся циклом от первого до последнего дня отсутствия и заносим каждый день в список
                    for (DateTime d = start.Date; d.Date <= end.Date; d = d.AddDays(1))
                    {
                        busyDates.Add(d);
                    }
                }
            }

            return busyDates.ToList();
        }
    }
}