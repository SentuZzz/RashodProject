using System;
using System.IO;
using System.Linq;
using System.Windows;
using Microsoft.Win32;

namespace WpfApp1.Helpers
{
    public static class BackupHelper
    {
        private static readonly string DbFileName = "rashod.db";
        private static readonly string BackupFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");

        public static void RunAutoBackup()
        {
            try
            {
                if (!Directory.Exists(BackupFolder))
                    Directory.CreateDirectory(BackupFolder);

                string sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFileName);
                if (!File.Exists(sourceFile)) return;

                string todayBackupName = $"rashod_autobackup_{DateTime.Now:yyyyMMdd}.db";
                string destFile = Path.Combine(BackupFolder, todayBackupName);
                if (!File.Exists(destFile))
                {
                    File.Copy(sourceFile, destFile);
                }

                var backupFiles = new DirectoryInfo(BackupFolder)
                    .GetFiles("rashod_autobackup_*.db")
                    .OrderByDescending(f => f.CreationTime)
                    .ToList();

                if (backupFiles.Count > 3)
                {
                    for (int i = 3; i < backupFiles.Count; i++)
                    {
                        backupFiles[i].Delete();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Ошибка автобэкапа: " + ex.Message);
            }
        }

        public static void CreateManualBackup()
        {
            try
            {
                string sourceFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFileName);
                if (!File.Exists(sourceFile))
                {
                    MessageBox.Show("База данных не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    FileName = $"Бэкап_БД_Расход_{DateTime.Now:dd_MM_yyyy}.db",
                    DefaultExt = ".db",
                    Filter = "SQLite Database (*.db)|*.db",
                    Title = "Сохранить резервную копию базы данных"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    File.Copy(sourceFile, saveFileDialog.FileName, true);
                    MessageBox.Show("Резервная копия успешно сохранена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании копии: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public static void RestoreBackup()
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "SQLite Database (*.db)|*.db",
                    Title = "Выберите файл резервной копии для восстановления"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    var result = MessageBox.Show(
                        "ВНИМАНИЕ! Текущая база данных будет ПОЛНОСТЬЮ заменена на выбранную резервную копию.\n\nВсе текущие несохраненные в бэкап данные будут потеряны. Вы уверены?",
                        "Восстановление БД", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        string targetFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DbFileName);
                        File.Copy(openFileDialog.FileName, targetFile, true);
                        MessageBox.Show("База данных успешно восстановлена! Пожалуйста, перезапустите программу, чтобы изменения вступили в силу.", "Восстановление завершено", MessageBoxButton.OK, MessageBoxImage.Information);

                        Application.Current.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при восстановлении копии: {ex.Message}\n\nВозможно, база данных сейчас занята. Попробуйте перезапустить программу и повторить.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}