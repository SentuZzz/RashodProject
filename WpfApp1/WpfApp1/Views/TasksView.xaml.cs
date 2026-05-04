using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WpfApp1.Models;
using WpfApp1.ViewModels;

namespace WpfApp1.Views
{
    public partial class TasksView : UserControl
    {
        private Point _startPoint;

        public TasksView()
        {
            InitializeComponent();
        }

        private void TaskCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Игнорируем нажатие, если кликнули по кнопке (например, по корзине)
            if (e.OriginalSource is DependencyObject source)
            {
                var button = FindParent<Button>(source);
                if (button != null) return;
            }

            _startPoint = e.GetPosition(null);
        }

        // Вспомогательный метод для поиска родителя-кнопки
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        // Проверяем, достаточно ли далеко сдвинули мышку, чтобы начать перетаскивание
        private void TaskCard_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point mousePos = e.GetPosition(null);
                Vector diff = _startPoint - mousePos;

                if (System.Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    System.Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Находим саму карточку
                    Border border = sender as Border;
                    if (border != null)
                    {
                        TaskModel draggedTask = border.DataContext as TaskModel;
                        if (draggedTask != null)
                        {
                            // Инициируем процесс Drag-n-Drop
                            DragDrop.DoDragDrop(border, draggedTask, DragDropEffects.Move);
                        }
                    }
                }
            }
        }

        // Ловим момент "отпускания" карточки над колонкой
        private void ListView_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(typeof(TaskModel)))
            {
                TaskModel task = e.Data.GetData(typeof(TaskModel)) as TaskModel;
                ListView targetListView = sender as ListView;

                if (task != null && targetListView != null)
                {
                    // Узнаем статус колонки из её тега (К выполнению / В процессе / Выполнено)
                    string newStatus = targetListView.Tag.ToString();

                    if (task.Status != newStatus)
                    {
                        // Вызываем метод ViewModel для обновления в базе данных
                        var viewModel = this.DataContext as TasksViewModel;
                        if (viewModel != null)
                        {
                            viewModel.ChangeTaskStatus(task.TaskHistoryID, newStatus);
                        }
                    }
                }
            }
        }
    }
}