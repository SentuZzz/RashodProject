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

                // 1. Читаем ID званий, должностей и подразделений
                var rankIds = db.Query<int>("SELECT RankID FROM Ranks").ToList();
                var posIds = db.Query<int>("SELECT PositionID FROM Positions").ToList();
                var unitIds = db.Query<int>("SELECT UnitID FROM Units").ToList();
                var duties = db.Query<DutyDto>("SELECT DutyID, DutyName, RolePriority FROM Duties").ToList();

                if (!rankIds.Any() || !posIds.Any() || !unitIds.Any() || !duties.Any())
                    return;

                // Для логики срочников: предполагаем, что первые 4 ID в таблице Ranks - это 
                // Рядовой, Ефрейтор, Мл. сержант, Сержант. (Если у тебя иначе - поменяй логику)
                var conscriptRankIds = rankIds.Where(id => id <= 4).ToList();
                var contractRankIds = rankIds; // Контрактники могут иметь любые звания

                var random = new Random();

                string[] lastNames = { "Иванов", "Смирнов", "Кузнецов", "Попов", "Васильев", "Петров", "Соколов", "Михайлов", "Новиков", "Федоров", "Морозов", "Волков", "Алексеев", "Лебедев", "Семенов", "Егоров", "Павлов", "Козлов", "Степанов", "Николаев" };
                string[] firstNames = { "Александр", "Сергей", "Дмитрий", "Алексей", "Андрей", "Максим", "Евгений", "Иван", "Михаил", "Артем" };
                string[] patronymics = { "Александрович", "Сергеевич", "Дмитриевич", "Алексеевич", "Андреевич", "Максимович", "Евгениевич", "Иванович", "Михайлович", "Артемович" };

                // Очищаем таблицы (Важно соблюдать порядок из-за внешних ключей)
                db.Execute("DELETE FROM TaskAssignments");
                db.Execute("DELETE FROM DutyHistory");
                db.Execute("DELETE FROM StatusLog");
                db.Execute("DELETE FROM Soldiers");

                // 2. Генерируем 110 солдат
                var contractors = new List<int>();
                var conscripts = new List<int>();

                DateTime startJoinDate = DateTime.Now.AddYears(-2);

                for (int i = 0; i < 110; i++)
                {
                    string ln = lastNames[random.Next(lastNames.Length)];
                    string fn = firstNames[random.Next(firstNames.Length)];
                    string mn = patronymics[random.Next(patronymics.Length)];
                    int pId = posIds[random.Next(posIds.Count)];
                    int uId = unitIds[random.Next(unitIds.Count)];

                    bool isContract = i < 30; // 30 контрактников, 80 срочников
                    string sType = isContract ? "По контракту" : "По призыву";

                    // ВАЖНО: Разделение званий
                    int rId = isContract ?
                        contractRankIds[random.Next(contractRankIds.Count)] :
                        conscriptRankIds[random.Next(conscriptRankIds.Count)];

                    // ВАЖНО: SQLite ожидает дату в строковом формате 'yyyy-MM-dd' для поля DATE
                    int randomDays = random.Next((DateTime.Now - startJoinDate).Days);
                    string jDateStr = startJoinDate.AddDays(randomDays).ToString("yyyy-MM-dd");

                    string insertSql = @"INSERT INTO Soldiers (FirstName, LastName, Patronymic, RankID, PositionID, UnitID, ServiceType, JoinDate)
                                         VALUES (@fn, @ln, @mn, @rId, @pId, @uId, @sType, @jDate);
                                         SELECT last_insert_rowid();";

                    int newId = db.QuerySingle<int>(insertSql, new { fn, ln, mn, rId, pId, uId, sType, jDate = jDateStr });

                    if (isContract) contractors.Add(newId);
                    else conscripts.Add(newId);
                }

                // 3. Генерируем историю нарядов (Апрель 2026) С УЧЕТОМ ОТДЫХА
                DateTime startDate = new DateTime(2026, 4, 1);

                // Словари для отслеживания, когда солдат последний раз был в наряде
                var lastDutyDates = new Dictionary<int, DateTime>();

                for (int day = 0; day < 30; day++)
                {
                    DateTime currentDay = startDate.AddDays(day);

                    // Фильтруем людей: берем только тех, кто НЕ был в наряде последние 2 дня
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
                            // Назначаем, если есть отдохнувшие люди
                            if (duty.RolePriority == 1 && contIdx < restedContractors.Count)
                                sId = restedContractors[contIdx++];
                            else if (duty.RolePriority > 1 && consIdx < restedConscripts.Count)
                                sId = restedConscripts[consIdx++];

                            if (sId != 0)
                            {
                                db.Execute("INSERT INTO DutyHistory (DutyID, SoldierID, DutyDate) VALUES (@dId, @sId, @dt)",
                                           new { dId = duty.DutyID, sId = sId, dt = currentDay.ToString("yyyy-MM-dd") });

                                // Обновляем дату последнего наряда для этого бойца
                                lastDutyDates[sId] = currentDay;
                            }
                        }
                    }
                }
            }
        }
    }
}