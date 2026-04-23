using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AGenerator.Models;

namespace AGenerator.Views
{
    public partial class ActsView : UserControl
    {
        private static readonly Typeface CellTypeface = new Typeface("Segoe UI");
        private const double CellFontSize = 12;
        private const double Padding = 6;

        public ActsView()
        {
            InitializeComponent();
            MyDataGrid.CellEditEnding += MyDataGrid_CellEditEnding;
        }

        private void MyDataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit && e.Row.Item is Act act)
            {
                if (DataContext is ViewModels.ActsViewModel vm)
                {
                    vm.RecalculateAndSaveAct(act);

                    Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
                    {
                        var row = MyDataGrid.ItemContainerGenerator.ContainerFromItem(act) as DataGridRow;
                        if (row != null)
                        {
                            UpdateRowHeightFromContent(row, act);
                            row.InvalidateMeasure();
                            row.UpdateLayout();
                        }
                        MyDataGrid.InvalidateMeasure();
                        MyDataGrid.UpdateLayout();
                    });
                }
            }
        }

        private void UpdateRowHeightFromContent(DataGridRow row, Act act)
        {
            double maxHeight = 40;
            var dataGrid = FindParentDataGrid(row);
            if (dataGrid == null) return;

            foreach (var column in dataGrid.Columns)
            {
                if (column is not DataGridTemplateColumn tcol) continue;

                string? text = GetCellText(tcol, act);
                if (string.IsNullOrEmpty(text)) continue;

                double availableWidth = column.ActualWidth - Padding * 2;
                if (availableWidth <= 0) continue;

                double textHeight = MeasureWrappedTextHeight(text, availableWidth);
                maxHeight = Math.Max(maxHeight, textHeight);
            }

            row.MinHeight = Math.Min(maxHeight, 300);
        }

        private double MeasureWrappedTextHeight(string text, double maxWidth)
        {
            if (maxWidth <= 10) return 40;

            double fontSize = CellFontSize;
            double lineHeight = fontSize * 1.2;

            double charWidth = fontSize * 0.6;
            int charsPerLine = (int)(maxWidth / charWidth);
            if (charsPerLine <= 5) charsPerLine = 5;

            int lines = (int)Math.Ceiling((double)text.Length / charsPerLine);

            string[] words = text.Split(' ');
            int currentLineLength = 0;
            int wrappedLines = 1;

            foreach (string word in words)
            {
                int wordLen = word.Length;
                if (currentLineLength + wordLen + 1 > charsPerLine)
                {
                    wrappedLines++;
                    currentLineLength = wordLen;
                }
                else
                {
                    currentLineLength += wordLen + 1;
                }
            }

            return lineHeight * wrappedLines + Padding * 2;
        }

        private DataGrid? FindParentDataGrid(DependencyObject obj)
        {
            while (obj != null)
            {
                if (obj is DataGrid dg) return dg;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

        private string? GetCellText(DataGridTemplateColumn column, Act act)
        {
            if (column.Header is string header)
            {
                return header switch
                {
                    "№ п/п" => act.SortOrder.ToString(),
                    "№ акта" => act.FinalActNumber,
                    "Тип" => act.Type,
                    "Номер акта" => act.ActNumber,
                    "Наименование работ" => act.WorkName,
                    "Тип интервала" => act.IntervalType,
                    "Интервал" => act.Interval,
                    "Объём" => act.Volume?.ToString(),
                    "Ед.изм." => act.UnitOfMeasure,
                    "Последующие работы" => act.SubsequentWork,
                    "Приложения вручную" => act.Appendix,
                    "Доп. сведения" => act.AdditionalInfo,
                    "% нагрузки" => act.LoadPercentage?.ToString(),
                    "Условия нагружения" => act.FullLoadConditions,
                    "Статус" => act.Status,
                    _ => null
                };
            }
            return null;
        }
    }
}
