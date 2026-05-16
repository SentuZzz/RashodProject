using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using ClosedXML.Excel;
using WpfApp1.Models;
using WpfApp1.Repositories;

namespace WpfApp1.ViewModels
{
    public class ReportsViewModel : ViewModelBase
    {
        private readonly SoldierRepository _soldierRepo;
        private readonly DutyRepository _dutyRepo;

        private DateTime _selectedDate = DateTime.Today.AddDays(1);
        public DateTime SelectedDate
        {
            get => _selectedDate;
            set { _selectedDate = value; OnPropertyChanged(); }
        }

        public ICommand ExportMusterRollCommand { get; }
        public ICommand ExportDutyRosterCommand { get; }

        public ReportsViewModel()
        {
            _soldierRepo = new SoldierRepository();
            _dutyRepo = new DutyRepository();

            ExportMusterRollCommand = new ViewModelCommand(ExecuteExportMusterRoll);
            ExportDutyRosterCommand = new ViewModelCommand(ExecuteExportDutyRoster);
        }

        private void ExecuteExportMusterRoll(object obj)
        {
            try
            {
                var soldiers = _soldierRepo.GetAllSoldiers();

                var saveFileDialog = new SaveFileDialog
                {
                    FileName = $"Строевая_записка_{DateTime.Today:dd_MM_yyyy}.xlsx",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    Title = "Сохранить Строевую записку"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;

                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Личный состав");

                        ws.Cell(1, 1).Value = $"СТРОЕВАЯ ЗАПИСКА (по состоянию на {DateTime.Now:dd.MM.yyyy HH:mm})";
                        ws.Range("A1:F1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        string[] headers = { "№ п/п", "Звание", "ФИО", "Подразделение", "Должность", "Текущий статус" };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            var cell = ws.Cell(3, i + 1);
                            cell.Value = headers[i];
                            cell.Style.Font.SetBold();
                            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
                            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                        }

                        int row = 4;
                        int index = 1;
                        foreach (var s in soldiers.OrderBy(x => x.UnitName).ThenBy(x => x.LastName))
                        {
                            ws.Cell(row, 1).Value = index++;
                            ws.Cell(row, 2).Value = s.RankName;
                            ws.Cell(row, 3).Value = s.FullName;
                            ws.Cell(row, 4).Value = s.UnitName ?? "Не распределен";
                            ws.Cell(row, 5).Value = s.PositionName;

                            string status = s.CurrentStatus;
                            ws.Cell(row, 6).Value = status;

                            if (status != "В строю")
                            {
                                ws.Cell(row, 6).Style.Font.SetBold();

                                if (status == "В наряде")
                                    ws.Cell(row, 6).Style.Font.FontColor = XLColor.DarkViolet; 
                                else if (status == "На задаче")
                                    ws.Cell(row, 6).Style.Font.FontColor = XLColor.DarkOrange; 
                                else
                                    ws.Cell(row, 6).Style.Font.FontColor = XLColor.DarkRed;    
                            }

                            ws.Range(row, 1, row, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            ws.Range(row, 1, row, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                            row++;
                        }

                        ws.Columns().AdjustToContents();
                        workbook.SaveAs(filePath);
                    }

                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выгрузке Строевой записки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteExportDutyRoster(object obj)
        {
            try
            {
                var activeDuties = _dutyRepo.GetActiveDutiesForDate(SelectedDate);
                if (activeDuties == null || activeDuties.Count == 0)
                {
                    MessageBox.Show("На выбранную дату нет назначенных нарядов!", "Пусто", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    FileName = $"Книга_нарядов_{SelectedDate:dd_MM_yyyy}.xlsx",
                    DefaultExt = ".xlsx",
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    Title = "Сохранить Книгу нарядов"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;

                    using (var workbook = new XLWorkbook())
                    {
                        var ws = workbook.Worksheets.Add("Наряды");

                        ws.Cell(1, 1).Value = $"ВЕДОМОСТЬ СУТОЧНОГО НАРЯДА (на {SelectedDate:dd.MM.yyyy})";
                        ws.Range("A1:D1").Merge().Style.Font.SetBold().Font.FontSize = 14;
                        ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                        int row = 3;

                        foreach (var dutyGroup in activeDuties)
                        {
                            ws.Cell(row, 1).Value = $"МЕСТО НЕСЕНИЯ СЛУЖБЫ: {dutyGroup.GroupName.ToUpper()}";
                            ws.Range(row, 1, row, 4).Merge().Style.Font.SetBold().Fill.BackgroundColor = XLColor.LightSteelBlue;
                            ws.Range(row, 1, row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            row++;

                            string[] headers = { "Роль / Наряд", "Звание и ФИО", "Подпись о заступлении", "Примечание" };
                            for (int i = 0; i < headers.Length; i++)
                            {
                                var cell = ws.Cell(row, i + 1);
                                cell.Value = headers[i];
                                cell.Style.Font.SetBold();
                                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                            }
                            row++;

                            foreach (var role in dutyGroup.PersonnelRoles)
                            {
                                ws.Cell(row, 1).Value = role.RoleName;
                                ws.Cell(row, 2).Value = role.PersonnelName;

                                ws.Range(row, 1, row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                                ws.Range(row, 1, row, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                                row++;
                            }
                            row++;
                        }

                        ws.Columns().AdjustToContents();
                        ws.Column(3).Width = 25;
                        ws.Column(4).Width = 20;

                        workbook.SaveAs(filePath);
                    }

                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выгрузке Нарядов: {ex.Message}\n\nВозможно файл уже открыт в Excel, закройте его.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}