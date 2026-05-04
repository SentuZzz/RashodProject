using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;

namespace WpfApp1.Helpers
{
    // Вспомогательный класс для чтения нарядов
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

                // 1. Проверяем справочники (чтобы было из чего генерировать)
                var rankIds = db.Query<int>("SELECT RankID FROM Ranks").ToList();
                var posIds = db.Query<int>("SELECT PositionID FROM Positions").ToList();
                var unitIds = db.Query<int>("SELECT UnitID FROM Units").ToList();
                var duties = db.Query<DutyDto>("SELECT DutyID, DutyName, RolePriority FROM Duties").ToList();

                if (!rankIds.Any() || !posIds.Any() || !unitIds.Any() || !duties.Any())
                    return; // В базе нет базовых настроек

                var random = new Random();

                // База русских имен и фамилий
                string[] lastNames = { "Иванов", "Смирнов", "Кузнецов", "Попов", "Васильев", "Петров", "Соколов", "Михайлов", "Новиков", "Федоров", "Морозов", "Волков", "Алексеев", "Лебедев", "Семенов", "Егоров", "Павлов", "Козлов", "Степанов", "Николаев", "Орлов", "Андреев", "Макаров", "Никитин", "Захаров", "Зайцев", "Соловьев", "Борисов", "Яковлев", "Григорьев", "Романов", "Воробьев", "Сергеев", "Кузьмин", "Фролов", "Александров", "Комаров", "Ильин", "Гусев", "Титов", "Куликов", "Алексеев", "Степанов", "Яковлев", "Тарасов", "Белов", "Котов", "Медведев", "Ершов", "Антонов" };
                string[] firstNames = { "Александр", "Сергей", "Дмитрий", "Алексей", "Андрей", "Максим", "Евгений", "Иван", "Михаил", "Артем", "Павел", "Игорь", "Владимир", "Антон", "Илья", "Денис", "Роман", "Николай", "Олег", "Вячеслав" };
                string[] patronymics = { "Александрович", "Сергеевич", "Дмитриевич", "Алексеевич", "Андреевич", "Максимович", "Евгениевич", "Иванович", "Михайлович", "Артемович", "Павлович", "Игоревич", "Владимирович", "Антонович", "Ильич", "Денисович", "Романович", "Николаевич", "Олегович", "Вячеславович" };

                // ОЧИСТКА БАЗЫ (ВНИМАНИЕ: удалит твоих старых тестовых бойцов и их историю!)
                db.Execute("DELETE FROM DutyHistory");
                db.Execute("DELETE FROM StatusLog");
                db.Execute("DELETE FROM Soldiers");

                // 2. Генерируем 110 солдат (30 контрактов, 80 призывов)
                var contractors = new List<int>();
                var conscripts = new List<int>();

                for (int i = 0; i < 110; i++)
                {
                    string ln = lastNames[random.Next(lastNames.Length)];
                    string fn = firstNames[random.Next(firstNames.Length)];
                    string mn = patronymics[random.Next(patronymics.Length)];
                    int rId = rankIds[random.Next(rankIds.Count)];
                    int pId = posIds[random.Next(posIds.Count)];
                    int uId = unitIds[random.Next(unitIds.Count)];

                    bool isContract = i < 30;
                    string sType = isContract ? "По контракту" : "По призыву";

                    string insertSql = @"INSERT INTO Soldiers (FirstName, LastName, Patronymic, RankID, PositionID, UnitID, ServiceType)
                                         VALUES (@fn, @ln, @mn, @rId, @pId, @uId, @sType);
                                         SELECT last_insert_rowid();";

                    int newId = db.QuerySingle<int>(insertSql, new { fn, ln, mn, rId, pId, uId, sType });

                    if (isContract) contractors.Add(newId);
                    else conscripts.Add(newId);
                }

                // 3. Генерируем историю нарядов на ВЕСЬ АПРЕЛЬ 2026 (30 дней)
                DateTime startDate = new DateTime(2026, 4, 1);
                for (int day = 0; day < 30; day++)
                {
                    DateTime currentDay = startDate.AddDays(day);

                    // Каждый день перемешиваем списки, чтобы наряды брали случайные отдохнувшие люди
                    var availableContractors = contractors.OrderBy(x => random.Next()).ToList();
                    var availableConscripts = conscripts.OrderBy(x => random.Next()).ToList();

                    int contIdx = 0;
                    int consIdx = 0;

                    foreach (var duty in duties)
                    {
                        // Определяем вместимость наряда по его названию
                        int count = 1;
                        if (duty.DutyName.Contains("Группа Антитеррора")) count = 10;
                        else if (duty.DutyName.Contains("Дневальный по роте") || duty.DutyName.Contains("Караульный")) count = 3;
                        else if (duty.DutyName.Contains("Дневальный по парку") || duty.DutyName.Contains("Дневальный по КПП")) count = 2;

                        for (int k = 0; k < count; k++)
                        {
                            int sId = 0;
                            // Начкар и Дежурные (Приоритет 1) - строго контрактники
                            if (duty.RolePriority == 1) sId = availableContractors[contIdx++];
                            // Подчиненные - строго срочники
                            else sId = availableConscripts[consIdx++];

                            db.Execute("INSERT INTO DutyHistory (DutyID, SoldierID, DutyDate) VALUES (@dId, @sId, @dt)",
                                       new { dId = duty.DutyID, sId = sId, dt = currentDay.ToString("yyyy-MM-dd") });
                        }
                    }
                }
            }
        }
    }
}