using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Xceed.Document.NET;
using Xceed.Words.NET;
using WpfApp1.Models;

namespace WpfApp1.Helpers
{
    public static class ReportHelper
    {
        public static void GenerateLeaveReport(SoldierModel soldier)
        {
            if (soldier == null) return;

            var saveFileDialog = new SaveFileDialog
            {
                FileName = $"Рапорт_на_отпуск_{soldier.LastName}.docx",
                DefaultExt = ".docx",
                Filter = "Word Documents (*.docx)|*.docx",
                Title = "Сохранить рапорт"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var doc = DocX.Create(saveFileDialog.FileName))
                    {
                        var defaultFont = new Font("Times New Roman");
                        int defaultSize = 14;

                        var header = doc.InsertParagraph("Командиру войсковой части 00000\n", false);
                        header.Font(defaultFont).FontSize(defaultSize);
                        header.Alignment = Alignment.right;
                        doc.InsertParagraph("\n", false); 

                        var title = doc.InsertParagraph("РАПОРТ", false);
                        title.Font(defaultFont).FontSize(16).Bold();
                        title.Alignment = Alignment.center;
                        doc.InsertParagraph("\n", false);


                        string bodyText = $"Прошу Вашего ходатайства перед вышестоящим командованием о предоставлении мне, {soldier.RankName} {soldier.FullName}, основного отпуска за {DateTime.Now.Year} год сроком на 30 суток, с «___» _________ {DateTime.Now.Year} года.\n" +
                                          $"Отпуск буду проводить по адресу: ____________________________________________________________________.\n" +
                                          $"Обязуюсь прибыть в часть без опозданий.";

                        var body = doc.InsertParagraph(bodyText, false);
                        body.Font(defaultFont).FontSize(defaultSize);
                        body.Alignment = Alignment.both;
                        body.IndentationFirstLine = 35.0f; 

                        doc.InsertParagraph("\n\n", false);

                        var footerTable = doc.AddTable(1, 2);
                        footerTable.Design = TableDesign.None;
                        footerTable.AutoFit = AutoFit.Window;

                        footerTable.Rows[0].Cells[0].Paragraphs[0].Append($"«___» ________ {DateTime.Now.Year} г.")
                            .Font(defaultFont).FontSize(defaultSize);

                        string initials = string.IsNullOrWhiteSpace(soldier.FirstName) ? "" : soldier.FirstName.Substring(0, 1) + ".";

                        string signature = $"{soldier.PositionName ?? "Военнослужащий"}\n" +
                                           $"{soldier.RankName}\n" +
                                           $"______________ / {soldier.LastName} {initials} /";

                        footerTable.Rows[0].Cells[1].Paragraphs[0].Append(signature)
                            .Font(defaultFont).FontSize(defaultSize).Alignment = Alignment.right;

                        doc.InsertTable(footerTable);

                        doc.Save();
                    }

                    Process.Start(new ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при создании рапорта: {ex.Message}\nВозможно, файл открыт в другой программе.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}