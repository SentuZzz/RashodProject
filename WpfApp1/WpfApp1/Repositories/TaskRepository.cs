using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using Dapper;
using WpfApp1.Models;

namespace WpfApp1.Repositories
{
    public class TaskRepository
    {
        private readonly string _connectionString = "Data Source=rashod.db;Version=3;";

        public TaskRepository()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                int count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM TaskCategories");
                if (count == 0)
                {
                    connection.Execute("INSERT INTO TaskCategories (CategoryName) VALUES ('Уборка'), ('Ремонт'), ('Документы'), ('Погрузка'), ('Разное')");
                }
            }
        }

        public List<TaskCategoryModel> GetCategories()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                return connection.Query<TaskCategoryModel>("SELECT * FROM TaskCategories").ToList();
            }
        }

        public List<TaskModel> GetAllTasks()
        {
            var tasks = new List<TaskModel>();
            using (var connection = new SQLiteConnection(_connectionString))
            {
                string sqlTasks = "SELECT t.*, c.CategoryName FROM TaskHistory t JOIN TaskCategories c ON t.CategoryID = c.CategoryID";
                tasks = connection.Query<TaskModel>(sqlTasks).ToList();

                string sqlAssignments = @"
                    SELECT ta.TaskHistoryID, s.SoldierID, s.LastName, s.FirstName, r.RankName, s.ServiceType
                    FROM TaskAssignments ta
                    JOIN Soldiers s ON ta.SoldierID = s.SoldierID
                    JOIN Ranks r ON s.RankID = r.RankID";
                var assignments = connection.Query(sqlAssignments);

                foreach (var task in tasks)
                {
                    task.AssignedSoldiers = assignments
                        .Where(a => (int)a.TaskHistoryID == task.TaskHistoryID)
                        .Select(a => new SoldierModel
                        {
                            SoldierID = (int)a.SoldierID,
                            LastName = (string)a.LastName,
                            FirstName = (string)a.FirstName,
                            RankName = (string)a.RankName,
                            ServiceType = (string)a.ServiceType
                        }).ToList();
                }
            }
            return tasks;
        }

        public void CreateTask(TaskModel task, List<int> soldierIds)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    string insertTaskSql = @"
                        INSERT INTO TaskHistory (CategoryID, TaskName, CreationDate, DueDate, Status) 
                        VALUES (@CategoryID, @TaskName, @CreationDate, @DueDate, @Status);
                        SELECT last_insert_rowid();";

                    int newTaskId = connection.QuerySingle<int>(insertTaskSql, task, transaction);

                    if (soldierIds != null && soldierIds.Any())
                    {
                        string insertAssignmentSql = "INSERT INTO TaskAssignments (TaskHistoryID, SoldierID, AssignedDate) VALUES (@TaskId, @SoldierId, @AssignedDate)";
                        foreach (var sId in soldierIds)
                        {
                            connection.Execute(insertAssignmentSql, new { TaskId = newTaskId, SoldierId = sId, AssignedDate = DateTime.Now }, transaction);
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public void UpdateTask(TaskModel task, List<int> soldierIds)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var transaction = connection.BeginTransaction())
                {
                    string updateTaskSql = @"
                        UPDATE TaskHistory 
                        SET CategoryID = @CategoryID, TaskName = @TaskName, CreationDate = @CreationDate, DueDate = @DueDate
                        WHERE TaskHistoryID = @TaskHistoryID";

                    connection.Execute(updateTaskSql, task, transaction);

                    connection.Execute("DELETE FROM TaskAssignments WHERE TaskHistoryID = @Id", new { Id = task.TaskHistoryID }, transaction);

                    if (soldierIds != null && soldierIds.Any())
                    {
                        string insertAssignmentSql = "INSERT INTO TaskAssignments (TaskHistoryID, SoldierID, AssignedDate) VALUES (@TaskId, @SoldierId, @AssignedDate)";
                        foreach (var sId in soldierIds)
                        {
                            connection.Execute(insertAssignmentSql, new { TaskId = task.TaskHistoryID, SoldierId = sId, AssignedDate = DateTime.Now }, transaction);
                        }
                    }

                    transaction.Commit();
                }
            }
        }

        public void UpdateTaskStatus(int taskId, string newStatus)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Execute("UPDATE TaskHistory SET Status = @Status WHERE TaskHistoryID = @Id", new { Status = newStatus, Id = taskId });
            }
        }

        public void DeleteTask(int taskId)
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Execute("DELETE FROM TaskHistory WHERE TaskHistoryID = @Id", new { Id = taskId });
            }
        }

        // НОВЫЙ МЕТОД: Водопадный сдвиг задач в конце дня
        public void ShiftTasksForNewDay()
        {
            using (var connection = new SQLiteConnection(_connectionString))
            {
                connection.Open();
                using (var trans = connection.BeginTransaction())
                {
                    // Порядок критически важен! Идем с конца конвейера к началу, чтобы статусы не перезаписали друг друга
                    connection.Execute("UPDATE TaskHistory SET Status = 'В архиве' WHERE Status = 'Выполнено'", transaction: trans);
                    connection.Execute("UPDATE TaskHistory SET Status = 'Выполнено' WHERE Status = 'В процессе'", transaction: trans);
                    connection.Execute("UPDATE TaskHistory SET Status = 'В процессе' WHERE Status = 'К выполнению'", transaction: trans);

                    trans.Commit();
                }
            }
        }
    }
}