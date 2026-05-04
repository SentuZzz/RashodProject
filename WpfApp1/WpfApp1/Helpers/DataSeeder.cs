using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;

namespace WpfApp1.Helpers
{
    public class DutyDto
    {
        public int DutyID { get; set; }
        public string DutyName { get; set; }
        public int RolePriority { get; set; }
    }

    public static class DataSeeder
    {
        public static void Seed(string connectionString)
        {
            using (var db = new SQLiteConnection(connectionString))
            {
                db.Open();

                var duties = db.Query<DutyDto>("SELECT DutyID, DutyName, RolePriority FROM Duties").ToList();

                var ranks = db.Query("SELECT RankID, RankName FROM Ranks").ToDictionary(r => (string)r.RankName, r => (int)r.RankID);
                var positions = db.Query("SELECT PositionID, PositionName FROM Positions").ToDictionary(p => (string)p.PositionName, p => (int)p.PositionID);
                var units = db.Query("SELECT UnitID, UnitName FROM Units").ToDictionary(u => (string)u.UnitName, u => (int)u.UnitID);

                if (!ranks.Any() || !positions.Any() || !units.Any() || !duties.Any())
                    return;

                var random = new Random();

                string[] lastNames = { "Иванов", "Смирнов", "Кузнецов", "Попов", "Васильев", "Петров", "Соколов", "Михайлов", "Новиков", "Федоров", "Морозов", "Волков", "Алексеев", "Лебедев", "Семенов", "Егоров", "Павлов", "Козлов", "Степанов", "Николаев" };
                string[] firstNames = { "Александр", "Сергей", "Дмитрий", "Алексей", "Андрей", "Максим", "Евгений", "Иван", "Михаил", "Артем" };
                string[] patronymics = { "Александрович", "Сергеевич", "Дмитриевич", "Алексеевич", "Андреевич", "Максимович", "Евгениевич", "Иванович", "Михайлович", "Артемович" };

                db.Execute("DELETE FROM TaskAssignments");
                db.Execute("DELETE FROM DutyHistory");
                db.Execute("DELETE FROM StatusLog");
                db.Execute("DELETE FROM Soldiers");

                var companyRoster = new List<(string Rank, string Position, string ServiceType, int Count)>
                {
                    // Управление
                    ("Капитан", "Командир роты", "По контракту", 1),
                    ("Старший лейтенант", "Заместитель командира роты", "По контракту", 1),
                    ("Старший лейтенант", "Замполит", "По контракту", 1),
                    ("Старший прапорщик", "Старшина роты", "По контракту", 1),
                    ("Прапорщик", "Начальник склада", "По контракту", 1),

                    // Взводное звено
                    ("Лейтенант", "Командир взвода", "По контракту", 2),
                    ("Прапорщик", "Командир взвода", "По контракту", 1),
                    ("Старший сержант", "Заместитель командира взвода", "По контракту", 2),
                    ("Прапорщик", "Заместитель командира взвода", "По контракту", 1),

                    // Командиры отделений и спецы
                    ("Сержант", "Командир отделения", "По контракту", 9),
                    ("Младший сержант", "Наводчик", "По контракту", 9),
                    ("Ефрейтор", "Водитель", "По контракту", 3),

                    // Срочники
                    ("Ефрейтор", "Старший стрелок", "По призыву", 9),
                    ("Рядовой", "Пулеметчик", "По призыву", 9),
                    ("Рядовой", "Гранатометчик", "По призыву", 9),
                    ("Рядовой", "Водитель", "По призыву", 6),
                    ("Рядовой", "Стрелок", "По призыву", 45)
                };

                int GetIdOrFallback(Dictionary<string, int> dict, string key) => dict.ContainsKey(key) ? dict[key] : dict.Values.First();

                var contractors = new List<int>();
                var conscripts = new List<int>();
                DateTime startJoinDate = DateTime.Now.AddYears(-2);

                int platoonCounter = 1;
                int squadCounter = 1;

                foreach (var role in companyRoster)
                {
                    for (int i = 0; i < role.Count; i++)
                    {
                        string ln = lastNames[random.Next(lastNames.Length)];
                        string fn = firstNames[random.Next(firstNames.Length)];
                        string mn = patronymics[random.Next(patronymics.Length)];

                        int rId = GetIdOrFallback(ranks, role.Rank);
                        int pId = GetIdOrFallback(positions, role.Position);

                        int uId;

                        // Логика распределения по подразделениям
                        var hqPositions = new[] { "Командир роты", "Заместитель командира роты", "Замполит", "Старшина роты", "Начальник склада" };
                        var platoonPositions = new[] { "Командир взвода", "Заместитель командира взвода" };

                        if (hqPositions.Contains(role.Position))
                        {
                            uId = GetIdOrFallback(units, "Управление роты");
                        }
                        else if (platoonPositions.Contains(role.Position))
                        {
                            uId = GetIdOrFallback(units, $"{platoonCounter}-й Взвод");
                            platoonCounter++;
                            if (platoonCounter > 3) platoonCounter = 1;
                        }
                        else
                        {
                            // Карусель отделений: 1-е Отделение 1В, 2-е Отделение 1В и т.д.
                            int platoonNum = ((squadCounter - 1) / 3) + 1;
                            int squadNum = ((squadCounter - 1) % 3) + 1;
                            uId = GetIdOrFallback(units, $"{squadNum}-е Отделение {platoonNum}В");

                            squadCounter++;
                            if (squadCounter > 9) squadCounter = 1;
                        }

                        int randomDays = random.Next((DateTime.Now - startJoinDate).Days);
                        string jDateStr = startJoinDate.AddDays(randomDays).ToString("yyyy-MM-dd");

                        string insertSql = @"INSERT INTO Soldiers (FirstName, LastName, Patronymic, RankID, PositionID, UnitID, ServiceType, JoinDate)
                                             VALUES (@fn, @ln, @mn, @rId, @pId, @uId, @sType, @jDate);
                                             SELECT last_insert_rowid();";

                        int newId = db.QuerySingle<int>(insertSql, new { fn, ln, mn, rId, pId, uId, sType = role.ServiceType, jDate = jDateStr });

                        if (role.ServiceType == "По контракту") contractors.Add(newId);
                        else conscripts.Add(newId);
                    }
                }

                // Генерируем историю нарядов
                DateTime startDate = new DateTime(2026, 4, 1);
                var lastDutyDates = new Dictionary<int, DateTime>();

                for (int day = 0; day < 30; day++)
                {
                    DateTime currentDay = startDate.AddDays(day);

                    var restedContractors = contractors.Where(id =>
                        !lastDutyDates.ContainsKey(id) || (currentDay - lastDutyDates[id]).TotalDays >= 2
                    ).OrderBy(x => random.Next()).ToList();

                    var restedConscripts = conscripts.Where(id =>
                        !lastDutyDates.ContainsKey(id) || (currentDay - lastDutyDates[id]).TotalDays >= 2
                    ).OrderBy(x => random.Next()).ToList();

                    int contIdx = 0;
                    int consIdx = 0;

                    foreach (var duty in duties)
                    {
                        int count = 1;
                        if (duty.DutyName.Contains("Группа Антитеррора")) count = 10;
                        else if (duty.DutyName.Contains("Дневальный по роте") || duty.DutyName.Contains("Караульный")) count = 3;
                        else if (duty.DutyName.Contains("Дневальный по парку") || duty.DutyName.Contains("Дневальный по КПП")) count = 2;

                        for (int k = 0; k < count; k++)
                        {
                            int sId = 0;
                            if (duty.RolePriority == 1 && contIdx < restedContractors.Count)
                                sId = restedContractors[contIdx++];
                            else if (duty.RolePriority > 1 && consIdx < restedConscripts.Count)
                                sId = restedConscripts[consIdx++];

                            if (sId != 0)
                            {
                                db.Execute("INSERT INTO DutyHistory (DutyID, SoldierID, DutyDate) VALUES (@dId, @sId, @dt)",
                                           new { dId = duty.DutyID, sId = sId, dt = currentDay.ToString("yyyy-MM-dd") });

                                lastDutyDates[sId] = currentDay;
                            }
                        }
                    }
                }
            }
        }
    }
}