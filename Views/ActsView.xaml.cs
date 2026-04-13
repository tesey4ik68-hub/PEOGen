using System.Windows.Controls;
using AGenerator.Models;

namespace AGenerator.Views
{
    /// <summary>
    /// Interaction logic for ActsView.xaml
    /// </summary>
    public partial class ActsView : UserControl
    {
        public ActsView()
        {
            InitializeComponent();

            // Подписка на событие окончания редактирования ячейки
            // Вызывает пересчёт вычисляемых полей и сохранение в БД
            MyDataGrid.CellEditEnding += MyDataGrid_CellEditEnding;
        }

        private void MyDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            // Пересчёт и сохранение только при фактическом изменении (EditingElementLostFocus)
            if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is Act act)
            {
                if (DataContext is ViewModels.ActsViewModel vm)
                {
                    // Пересчитываем вычисляемые поля и сохраняем
                    vm.RecalculateAndSaveAct(act);
                }
            }
        }
    }
}
