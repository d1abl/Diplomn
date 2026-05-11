using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Diplomn.Pages
{
    public partial class SalesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private ObservableCollection<SaleViewModel> salesView;
        private ObservableCollection<ProductSaleViewModel> productsView;
        private ObservableCollection<SaleItemDisplay> newSaleItems;
        private bool isLoading = false;
        private int? editingSaleCode = null; // Код редактируемого чека
        private FilterState savedFilterState = null;

        public class SaleItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
            public string PriceQuantityDisplay => $"{Цена:N2} ₽ × {Количество} шт.";
            public BitmapImage PhotoSource { get; set; }
        }
        private class FilterState
        {
            public string SearchText { get; set; }
            public DateTime? DateFrom { get; set; }
            public DateTime? DateTo { get; set; }
            public int EmployeeIndex { get; set; }
            public string SortMode { get; set; } // "date", "date_asc", "amount", "amount_asc", "code", "code_asc"
        }
        private void SaveFilterState()
        {
            savedFilterState = new FilterState
            {
                SearchText = TxtSearch.Text,
                DateFrom = DateFrom.SelectedDate,
                DateTo = DateTo.SelectedDate,
                EmployeeIndex = CmbEmployee.SelectedIndex,
                SortMode = RbSortByDate.IsChecked == true ? "date" :
                           RbSortByDateAsc.IsChecked == true ? "date_asc" :
                           RbSortByAmount.IsChecked == true ? "amount" :
                           RbSortByAmountAsc.IsChecked == true ? "amount_asc" :
                           RbSortByCode.IsChecked == true ? "code" :
                           RbSortByCodeAsc.IsChecked == true ? "code_asc" : "date"
            };
        }

        private void RestoreFilterState()
        {
            if (savedFilterState == null) return;

            TxtSearch.Text = savedFilterState.SearchText;
            DateFrom.SelectedDate = savedFilterState.DateFrom;
            DateTo.SelectedDate = savedFilterState.DateTo;

            // Временно отписываемся от события, чтобы не вызвать ApplyFilters до восстановления всех фильтров
            if (CmbEmployee != null)
            {
                CmbEmployee.SelectedIndex = savedFilterState.EmployeeIndex >= 0 ? savedFilterState.EmployeeIndex : 0;
            }

            // Восстанавливаем сортировку (отписываемся от событий чтобы избежать множественных вызовов)
            RbSortByDate.Checked -= SortChanged;
            RbSortByDateAsc.Checked -= SortChanged;
            RbSortByAmount.Checked -= SortChanged;
            RbSortByAmountAsc.Checked -= SortChanged;
            RbSortByCode.Checked -= SortChanged;
            RbSortByCodeAsc.Checked -= SortChanged;

            switch (savedFilterState.SortMode)
            {
                case "date": RbSortByDate.IsChecked = true; break;
                case "date_asc": RbSortByDateAsc.IsChecked = true; break;
                case "amount": RbSortByAmount.IsChecked = true; break;
                case "amount_asc": RbSortByAmountAsc.IsChecked = true; break;
                case "code": RbSortByCode.IsChecked = true; break;
                case "code_asc": RbSortByCodeAsc.IsChecked = true; break;
                default: RbSortByDate.IsChecked = true; break;
            }

            // Подписываемся обратно
            RbSortByDate.Checked += SortChanged;
            RbSortByDateAsc.Checked += SortChanged;
            RbSortByAmount.Checked += SortChanged;
            RbSortByAmountAsc.Checked += SortChanged;
            RbSortByCode.Checked += SortChanged;
            RbSortByCodeAsc.Checked += SortChanged;
        }
        public SalesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Продажи — {user.Фамилия} {user.Имя}";
            salesView = new ObservableCollection<SaleViewModel>();
            productsView = new ObservableCollection<ProductSaleViewModel>();
            newSaleItems = new ObservableCollection<SaleItemDisplay>();
            ListViewSales.ItemsSource = salesView;
            ListViewNewSaleItems.ItemsSource = newSaleItems;

            // Отключаем кэширование для получения свежих данных
            context.Configuration.LazyLoadingEnabled = false;
            context.Configuration.ProxyCreationEnabled = false;

            // Подписываемся на событие выгрузки страницы для освобождения ресурсов
            this.Unloaded += SalesPage_Unloaded;

            LoadEmployees();

            // Загружаем данные после полной загрузки страницы
            this.Loaded += (s, e) =>
            {
                LoadAllSalesWithoutFilters();
                LoadGrandTotal();
            };
        }
        private void LoadGrandTotal()
        {
            try
            {
                var query = context.Состав_продажи.AsQueryable();

                // Применяем фильтр по дате
                if (DateFrom.SelectedDate.HasValue)
                {
                    var dateFrom = DateFrom.SelectedDate.Value.Date;
                    query = query.Where(i => DbFunctions.TruncateTime(i.Продажи.Дата_продажи) >= dateFrom);
                }

                if (DateTo.SelectedDate.HasValue)
                {
                    var dateTo = DateTo.SelectedDate.Value.Date.AddDays(1);
                    query = query.Where(i => DbFunctions.TruncateTime(i.Продажи.Дата_продажи) < dateTo);
                }

                // Фильтр по сотруднику
                if (CmbEmployee.SelectedValue != null)
                {
                    int employeeId;
                    if (int.TryParse(CmbEmployee.SelectedValue.ToString(), out employeeId) && employeeId > 0)
                    {
                        query = query.Where(i => i.Продажи.Код_сотрудника == employeeId);
                    }
                }

                var grandTotal = query.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0;

                TxtGrandTotal.Text = $"{grandTotal:N2} ₽";
            }
            catch (Exception ex)
            {
                TxtGrandTotal.Text = "0.00 ₽";
                Debug.WriteLine($"Ошибка загрузки общей суммы: {ex.Message}");
            }
        }

        private void SalesPage_Unloaded(object sender, RoutedEventArgs e)
        {
            if (context != null)
            {
                context.Dispose();
                context = null;
            }
        }

        #region Вспомогательные методы

        private void RefreshContext()
        {
            if (context != null)
            {
                context.Dispose();
            }
            context = new BDEntities();
            context.Configuration.LazyLoadingEnabled = false;
            context.Configuration.ProxyCreationEnabled = false;

            if (currentUser != null)
            {
                currentUser = context.Сотрудники.Find(currentUser.Код_сотрудника);
            }
        }

        private void LoadEmployees()
        {
            var employees = context.Сотрудники.ToList();

            var employeeList = new List<dynamic>();
            employeeList.Add(new { Код_сотрудника = 0, FullName = "Все сотрудники" });

            foreach (var emp in employees)
            {
                employeeList.Add(new { Код_сотрудника = emp.Код_сотрудника, FullName = $"{emp.Фамилия} {emp.Имя}" });
            }

            CmbEmployee.ItemsSource = employeeList;
            CmbEmployee.SelectedValuePath = "Код_сотрудника";
            CmbEmployee.DisplayMemberPath = "FullName";
            CmbEmployee.SelectedIndex = 0;
        }

        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                try
                {
                    return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
                }
                catch
                {
                    return new BitmapImage();
                }
            }

            try
            {
                using (var ms = new MemoryStream(imageData))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                try
                {
                    return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
                }
                catch
                {
                    return new BitmapImage();
                }
            }
        }

        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;

            var placeholderText = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(placeholderText) && text == placeholderText)
                return string.Empty;

            return text;
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }

        private T FindChild<T>(DependencyObject parent, string childName) where T : DependencyObject
        {
            if (parent == null) return null;

            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childrenCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName && child is T typedChild)
                    return typedChild;

                var foundChild = FindChild<T>(child, childName);
                if (foundChild != null)
                    return foundChild;
            }
            return null;
        }

        #endregion

        #region Режим просмотра продаж

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private void SortChanged(object sender, RoutedEventArgs e)
        {
            if (context != null && this.IsLoaded)
            {
                ApplyFilters();
            }
        }

        private IQueryable<Продажи> GetBaseQuery()
        {
            var query = context.Продажи
                .Include("Сотрудники")
                .AsQueryable();

            // Поиск по товару в чеке
            string searchText = GetActualText(TxtSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.ToLower();
                var matchingSaleIds = context.Состав_продажи
                    .Include("Товары")
                    .Where(i => i.Товары.Наименование.ToLower().Contains(term))
                    .Select(i => i.Код_чека)
                    .Distinct()
                    .ToList();

                query = query.Where(s => matchingSaleIds.Contains(s.Код_чека));
            }

            // Фильтр по дате ОТ
            if (DateFrom.SelectedDate.HasValue)
            {
                var dateFrom = DateFrom.SelectedDate.Value.Date;
                query = query.Where(s => DbFunctions.TruncateTime(s.Дата_продажи) >= dateFrom);
            }

            // Фильтр по дате ДО
            if (DateTo.SelectedDate.HasValue)
            {
                var dateTo = DateTo.SelectedDate.Value.Date.AddDays(1);
                query = query.Where(s => DbFunctions.TruncateTime(s.Дата_продажи) < dateTo);
            }

            // Фильтр по сотруднику
            if (CmbEmployee.SelectedValue != null)
            {
                int employeeId;
                if (int.TryParse(CmbEmployee.SelectedValue.ToString(), out employeeId) && employeeId > 0)
                {
                    query = query.Where(s => s.Код_сотрудника == employeeId);
                }
            }

            return query;
        }

        private List<Продажи> GetFilteredAndSortedSales()
        {
            var query = GetBaseQuery();

            // Сортировка
            if (RbSortByDate.IsChecked == true)
                query = query.OrderByDescending(s => s.Дата_продажи);
            else if (RbSortByDateAsc.IsChecked == true)
                query = query.OrderBy(s => s.Дата_продажи);
            else if (RbSortByCode.IsChecked == true)
                query = query.OrderByDescending(s => s.Код_чека);
            else if (RbSortByCodeAsc.IsChecked == true)
                query = query.OrderBy(s => s.Код_чека);
            else if (RbSortByAmount.IsChecked == true || RbSortByAmountAsc.IsChecked == true)
            {
                // Для сортировки по сумме - загружаем и сортируем в памяти
                var sales = query.AsNoTracking().ToList();
                var saleIds = sales.Select(s => s.Код_чека).ToList();
                var totals = context.Состав_продажи
                    .Where(i => saleIds.Contains(i.Код_чека))
                    .GroupBy(i => i.Код_чека)
                    .Select(g => new { SaleId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0 })
                    .ToDictionary(x => x.SaleId, x => x.Total);

                if (RbSortByAmount.IsChecked == true)
                    return sales.OrderByDescending(s => totals.TryGetValue(s.Код_чека, out var t) ? t : 0).ToList();
                else
                    return sales.OrderBy(s => totals.TryGetValue(s.Код_чека, out var t) ? t : 0).ToList();
            }
            else
                query = query.OrderByDescending(s => s.Дата_продажи); // По умолчанию

            return query.AsNoTracking().ToList();
        }

        private void LoadAllSalesWithoutFilters()
        {
            if (isLoading) return;

            try
            {
                isLoading = true;

                var sales = GetBaseQuery()
                    .OrderByDescending(s => s.Дата_продажи)
                    .AsNoTracking()
                    .ToList();

                UpdateSalesView(sales);
                LoadGrandTotal();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
            }
        }

        private void LoadFilteredSales()
        {
            if (isLoading) return;

            try
            {
                isLoading = true;

                var sales = GetFilteredAndSortedSales();
                UpdateSalesView(sales);
                LoadGrandTotal(); // ← добавить
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                isLoading = false;
            }
        }

        private void UpdateSalesView(List<Продажи> sales)
        {
            if (sales == null)
            {
                sales = new List<Продажи>();
            }

            var selectedItem = ListViewSales.SelectedItem as SaleViewModel;
            int? selectedReceiptCode = selectedItem?.OriginalSale?.Код_чека;

            var saleIds = sales.Select(s => s.Код_чека).ToList();
            Dictionary<int, decimal> totals;

            if (saleIds.Any())
            {
                totals = context.Состав_продажи
                    .Where(i => saleIds.Contains(i.Код_чека))
                    .GroupBy(i => i.Код_чека)
                    .Select(g => new { SaleId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0 })
                    .ToDictionary(x => x.SaleId, x => x.Total);
            }
            else
            {
                totals = new Dictionary<int, decimal>();
            }

            salesView.Clear();
            foreach (var sale in sales)
            {
                var total = totals.TryGetValue(sale.Код_чека, out var t) ? t : 0;
                salesView.Add(new SaleViewModel(sale, total));
            }

            if (selectedReceiptCode.HasValue)
            {
                var itemToSelect = salesView.FirstOrDefault(s => s.OriginalSale?.Код_чека == selectedReceiptCode.Value);
                if (itemToSelect != null)
                {
                    ListViewSales.SelectedItem = itemToSelect;
                }
                else
                {
                    ClearSaleDetails();
                }
            }
            else
            {
                ClearSaleDetails();
            }

            if (ListViewSales.ItemsSource != salesView)
            {
                ListViewSales.ItemsSource = salesView;
            }
        }

        private void ApplyFilters()
        {
            LoadFilteredSales();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            CmbEmployee.SelectedIndex = 0;
            RbSortByDate.IsChecked = true;
            LoadAllSalesWithoutFilters();
            LoadGrandTotal();
            ClearSaleDetails();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sales = GetBaseQuery()
                    .OrderByDescending(s => s.Дата_продажи)
                    .AsNoTracking()
                    .ToList();

                if (!sales.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить отчет о продажах",
                    FileName = $"Отчет_продажи_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                const string shopName = "Oculus+";
                const string shopPhone = "+7 (461) 345 12-34";
                const string shopEmail = "Oculus@глаза.ру";
                const string shopWebsite = "Oculus.ру";
                const string shopHours = "9:00 – 17:00 ежедневно";

                string initials = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
                if (!string.IsNullOrWhiteSpace(currentUser.Отчество))
                    initials += $"{currentUser.Отчество?.Substring(0, 1)}.";
                else
                    initials += ".";

                var saleIds = sales.Select(s => s.Код_чека).ToList();
                var totals = context.Состав_продажи
                    .Where(i => saleIds.Contains(i.Код_чека))
                    .GroupBy(i => i.Код_чека)
                    .Select(g => new { SaleId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0 })
                    .ToDictionary(x => x.SaleId, x => x.Total);

                var totalSales = sales.Count;
                var grandTotal = totals.Values.Sum();

                using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 50, 50))
                {
                    using (var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, new FileStream(saveFileDialog.FileName, FileMode.Create)))
                    {
                        document.Open();

                        string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                        var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

                        var fontTitle = new iTextSharp.text.Font(baseFont, 16, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
                        var fontSubtitle = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontTableHeader = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE);
                        var fontTableCell = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);
                        var fontFooter = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);
                        var fontSmall = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontSign = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);

                        var reportTitle = new iTextSharp.text.Paragraph("ОТЧЁТ О ПРОДАЖАХ", fontTitle);
                        reportTitle.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        reportTitle.SpacingAfter = 25;
                        document.Add(reportTitle);

                        var table = new iTextSharp.text.pdf.PdfPTable(4);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 15, 25, 35, 25 });
                        table.SpacingBefore = 10;
                        table.SpacingAfter = 25;

                        var headers = new[] { "Код чека", "Дата продажи", "Сотрудник", "Сумма" };
                        foreach (var header in headers)
                        {
                            var headerCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(header, fontTableHeader));
                            headerCell.BackgroundColor = new iTextSharp.text.BaseColor(0, 51, 102);
                            headerCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                            headerCell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
                            headerCell.Padding = 5;
                            table.AddCell(headerCell);
                        }

                        bool alternate = false;
                        foreach (var sale in sales)
                        {
                            var total = totals.TryGetValue(sale.Код_чека, out var t) ? t : 0;
                            var employee = sale.Сотрудники != null
                                ? $"{sale.Сотрудники.Фамилия} {sale.Сотрудники.Имя}"
                                : "—";

                            var cells = new[]
                            {
                                sale.Код_чека.ToString(),
                                sale.Дата_продажи.ToString("dd.MM.yyyy HH:mm"),
                                employee,
                                $"{total:N2} ₽"
                            };

                            var centerColumns = new HashSet<int> { 0, 3 };

                            for (int i = 0; i < cells.Length; i++)
                            {
                                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], fontTableCell));
                                cell.Padding = 5;
                                cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;

                                if (alternate)
                                {
                                    cell.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                                }

                                if (centerColumns.Contains(i))
                                {
                                    cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                                }

                                table.AddCell(cell);
                            }

                            alternate = !alternate;
                        }

                        document.Add(table);

                        var totalParagraph = new iTextSharp.text.Paragraph();
                        totalParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        totalParagraph.SpacingBefore = 5;
                        totalParagraph.SpacingAfter = 3;
                        totalParagraph.Add(new iTextSharp.text.Chunk($"Всего продаж: {totalSales}", fontSubtitle));
                        document.Add(totalParagraph);

                        var totalSumParagraph = new iTextSharp.text.Paragraph();
                        totalSumParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        totalSumParagraph.SpacingAfter = 35;
                        totalSumParagraph.Add(new iTextSharp.text.Chunk($"Общая выручка: {grandTotal:N2} ₽", fontSubtitle));
                        document.Add(totalSumParagraph);

                        var signTable = new iTextSharp.text.pdf.PdfPTable(1);
                        signTable.WidthPercentage = 55;
                        signTable.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;

                        var signCell1 = new iTextSharp.text.pdf.PdfPCell();
                        signCell1.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell1.HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT;
                        signCell1.PaddingBottom = 3;

                        var signParagraph = new iTextSharp.text.Paragraph();
                        signParagraph.Add(new iTextSharp.text.Chunk(
                            $"{currentUser.Должность?.Название ?? "Сотрудник"} {initials} _______________  {DateTime.Now:dd.MM.yyyy}",
                            fontSign));
                        signCell1.AddElement(signParagraph);
                        signTable.AddCell(signCell1);

                        var signCell2 = new iTextSharp.text.pdf.PdfPCell();
                        signCell2.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell2.HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT;
                        signCell2.PaddingLeft = 145;

                        var signLine = new iTextSharp.text.Paragraph();
                        signLine.Add(new iTextSharp.text.Chunk("(Подпись)", fontSmall));
                        signCell2.AddElement(signLine);

                        signTable.AddCell(signCell2);
                        document.Add(signTable);

                        var footerLine = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.LIGHT_GRAY, iTextSharp.text.Element.ALIGN_CENTER, 0);
                        var footerLineParagraph = new iTextSharp.text.Paragraph();
                        footerLineParagraph.SpacingBefore = 40;
                        footerLineParagraph.Add(footerLine);
                        document.Add(footerLineParagraph);

                        var footerLine1 = new iTextSharp.text.Paragraph();
                        footerLine1.Add(new iTextSharp.text.Chunk($"{shopName}  |  Часы работы: {shopHours}", fontFooter));
                        footerLine1.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        footerLine1.SpacingBefore = 8;
                        footerLine1.SpacingAfter = 2;
                        document.Add(footerLine1);

                        var footerLine2 = new iTextSharp.text.Paragraph();
                        footerLine2.Add(new iTextSharp.text.Chunk($"{shopPhone}  |  {shopEmail}  |  {shopWebsite}", fontFooter));
                        footerLine2.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        footerLine2.SpacingBefore = 2;
                        document.Add(footerLine2);

                        document.Close();
                    }
                }

                var result = MessageBox.Show(
                    $"Отчёт о продажах сохранён!\n\nФайл: {saveFileDialog.FileName}\nВсего продаж: {totalSales}\nОбщая выручка: {grandTotal:N2} ₽\n\nОткрыть PDF?",
                    "Отчёт сохранён",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveFileDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}\n\nУбедитесь, что библиотека iTextSharp установлена.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListViewSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSales.SelectedItem is SaleViewModel selectedSale)
            {
                EnableEditDeleteButtons(true);

                var sale = selectedSale.OriginalSale;
                TxtSaleId.Text = sale.Код_чека.ToString();
                TxtSaleDate.Text = sale.Дата_продажи.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = sale.Сотрудники != null ? $"{sale.Сотрудники.Фамилия} {sale.Сотрудники.Имя}" : "";

                var items = context.Состав_продажи
                    .Include("Товары")
                    .Where(i => i.Код_чека == sale.Код_чека)
                    .ToList()
                    .Select(i => new SaleItemDisplay
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена,
                        PhotoSource = LoadImageFromBytes(i.Товары?.Фото)
                    })
                    .ToList();

                ListViewSaleItems.ItemsSource = items;
                decimal total = items.Sum(i => i.Сумма);
                TxtTotal.Text = $"{total:N2} ₽";
                TotalPanel.Visibility = Visibility.Visible;
            }
            else
            {
                EnableEditDeleteButtons(false);
                ClearSaleDetails();
            }
        }

        private void EnableEditDeleteButtons(bool enable)
        {
            foreach (var child in ViewModeButtons.Children)
            {
                if (child is Button button)
                {
                    var content = button.Content?.ToString();
                    if (content == "🗑 Удалить" || content == "✏ Редактировать")
                    {
                        button.IsEnabled = enable;
                    }
                }
            }
        }

        private void EditSale_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ListViewSales.SelectedItem as SaleViewModel;
            if (selectedItem == null)
            {
                MessageBox.Show("Выберите чек для редактирования!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFilterState(); // Сохраняем фильтры
            editingSaleCode = selectedItem.OriginalSale.Код_чека;

            SalesViewGrid.Visibility = Visibility.Collapsed;
            NewSaleGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSaleModeButtons.Visibility = Visibility.Visible;
            NewSaleTotalPanel.Visibility = Visibility.Visible;
            GrandTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = $"Редактирование чека №{editingSaleCode}";

            newSaleItems.Clear();

            var items = context.Состав_продажи
                .Include("Товары")
                .Where(i => i.Код_чека == editingSaleCode.Value)
                .ToList();

            foreach (var item in items)
            {
                newSaleItems.Add(new SaleItemDisplay
                {
                    Товар = item.Товары.Наименование,
                    Количество = item.Количество,
                    Цена = item.Цена,
                    PhotoSource = LoadImageFromBytes(item.Товары?.Фото)
                });
            }

            UpdateNewSaleTotal();
            ListViewNewSaleItems.Items.Refresh();
            BtnSaveNewSale.Content = "💾 Сохранить изменения";

            ClearProductFilterFields();
            RefreshContext();
            LoadAllProducts();
        }
        private void ClearSaleDetails()
        {
            TxtSaleId.Text = "";
            TxtSaleDate.Text = "";
            TxtEmployee.Text = "";
            ListViewSaleItems.ItemsSource = null;
            TxtTotal.Text = "";
            TotalPanel.Visibility = Visibility.Collapsed;
        }

        private void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListViewSales.SelectedItem as SaleViewModel;
                if (selectedItem == null)
                {
                    MessageBox.Show("Выберите продажу для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var saleCode = selectedItem.OriginalSale.Код_чека;

                var result = MessageBox.Show($"Вы уверены, что хотите удалить чек №{saleCode}?\nВместе с чеком будет удален его состав!",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveFilterState();

                    var items = context.Состав_продажи.Where(i => i.Код_чека == saleCode).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null)
                        {
                            product.Количество += item.Количество;
                        }
                        context.Состав_продажи.Remove(item);
                    }

                    var saleToDelete = context.Продажи.Find(saleCode);
                    if (saleToDelete != null)
                    {
                        context.Продажи.Remove(saleToDelete);
                    }

                    context.SaveChanges();

                    MessageBox.Show("Продажа успешно удалена! Товары возвращены на склад.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshContext();
                    RestoreFilterState();

                    // Применяем фильтры
                    if (HasActiveFilters())
                    {
                        LoadFilteredSales();
                    }
                    else
                    {
                        LoadAllSalesWithoutFilters();
                    }

                    LoadGrandTotal();
                    ClearSaleDetails();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении продажи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshContext();
            LoadAllSalesWithoutFilters();
            ClearSaleDetails();
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearSaleDetails();
        }

        #endregion

        #region Режим создания новой продажи

        private void NewSale_Click(object sender, RoutedEventArgs e)
        {
            SaveFilterState();

            editingSaleCode = null;
            BtnSaveNewSale.Content = "💾 Оформить продажу";

            SalesViewGrid.Visibility = Visibility.Collapsed;
            NewSaleGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSaleModeButtons.Visibility = Visibility.Visible;
            NewSaleTotalPanel.Visibility = Visibility.Visible;
            GrandTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = "Оформление новой продажи";

            newSaleItems.Clear();
            TxtNewSaleTotal.Text = "0.00 ₽";
            BtnSaveNewSale.IsEnabled = false;

            ClearProductFilterFields();
            RefreshContext();
            LoadAllProducts();
        }

        private void ClearProductFilterFields()
        {
            TxtProductSearch.Text = "";
            TxtPriceMin.Text = "";
            TxtPriceMax.Text = "";
            TxtQtyMin.Text = "";
            TxtQtyMax.Text = "";
            ChkInStock.IsChecked = false;
        }

        private void LoadAllProducts()
        {
            try
            {
                var products = context.Товары
                    .Include("Категории")
                    .AsNoTracking()
                    .ToList();

                UpdateProductsView(products);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelNewSale_Click(object sender, RoutedEventArgs e)
        {
            SwitchToViewMode();
        }
        private void LoadProducts()
        {
            try
            {
                var products = GetProductFilteredQuery()
                    .Include("Категории")
                    .AsNoTracking()
                    .ToList();
                UpdateProductsView(products);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateProductsView(List<Товары> products)
        {
            if (products == null || products.Count == 0)
            {
                productsView.Clear();
                return;
            }

            var selectedItem = ListViewProducts.SelectedItem as ProductSaleViewModel;
            int? selectedProductCode = selectedItem?.OriginalProduct?.Код_товара;

            productsView.Clear();
            foreach (var product in products)
            {
                productsView.Add(new ProductSaleViewModel(product));
            }

            if (ListViewProducts.ItemsSource != productsView)
            {
                ListViewProducts.ItemsSource = productsView;
            }
            else
            {
                ListViewProducts.Items.Refresh();
            }

            if (selectedProductCode.HasValue)
            {
                var itemToSelect = productsView.FirstOrDefault(p => p.OriginalProduct?.Код_товара == selectedProductCode.Value);
                if (itemToSelect != null)
                {
                    ListViewProducts.SelectedItem = itemToSelect;
                }
            }
        }

        private IQueryable<Товары> GetProductFilteredQuery()
        {
            var query = context.Товары.AsQueryable();

            string searchText = GetActualText(TxtProductSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.ToLower();
                query = query.Where(p => p.Наименование.ToLower().Contains(term));
            }

            if (ChkInStock.IsChecked == true)
            {
                query = query.Where(p => p.Количество > 0);
            }

            string priceMinText = GetActualText(TxtPriceMin);
            if (decimal.TryParse(priceMinText, out decimal priceMin))
            {
                query = query.Where(p => p.Цена_за_ед_продажа >= priceMin);
            }

            string priceMaxText = GetActualText(TxtPriceMax);
            if (decimal.TryParse(priceMaxText, out decimal priceMax))
            {
                query = query.Where(p => p.Цена_за_ед_продажа <= priceMax);
            }

            string qtyMinText = GetActualText(TxtQtyMin);
            if (int.TryParse(qtyMinText, out int qtyMin))
            {
                query = query.Where(p => p.Количество >= qtyMin);
            }

            string qtyMaxText = GetActualText(TxtQtyMax);
            if (int.TryParse(qtyMaxText, out int qtyMax))
            {
                query = query.Where(p => p.Количество <= qtyMax);
            }

            return query;
        }

        private void ApplyProductFilters()
        {
            LoadProducts();
        }

        private void ApplyProductFilters_Click(object sender, RoutedEventArgs e) => ApplyProductFilters();

        private void ClearProductFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtProductSearch.Text = "";
            TxtPriceMin.Text = "";
            TxtPriceMax.Text = "";
            TxtQtyMin.Text = "";
            TxtQtyMax.Text = "";
            ChkInStock.IsChecked = false;
            LoadProducts();
        }

        private void TxtProductSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyProductFilters();
        }

        private void TxtItemQuantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void TxtItemQuantity_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                var parentBorder = FindParent<Border>(textBox);
                if (parentBorder != null)
                {
                    var slider = FindChild<Slider>(parentBorder, "QuantitySlider");
                    var dataContext = parentBorder.DataContext as ProductSaleViewModel;

                    if (dataContext != null && int.TryParse(textBox.Text, out int value))
                    {
                        if (value > dataContext.Количество)
                        {
                            value = dataContext.Количество;
                            textBox.Text = value.ToString();
                        }
                        if (value < 0)
                        {
                            value = 0;
                            textBox.Text = "0";
                        }
                        if (slider != null)
                        {
                            slider.Value = value;
                        }
                    }
                }
            }
        }

        private void QuantitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                var parentBorder = FindParent<Border>(slider);
                if (parentBorder != null)
                {
                    var textBox = FindChild<TextBox>(parentBorder, "TxtItemQuantity");
                    if (textBox != null)
                    {
                        textBox.Text = ((int)slider.Value).ToString();
                    }
                }
            }
        }

        private void ListViewProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
        }

        private void AddToSaleItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductSaleViewModel productVM)
            {
                var product = productVM.OriginalProduct;

                var parentBorder = FindParent<Border>(button);
                if (parentBorder == null) return;

                var textBox = FindChild<TextBox>(parentBorder, "TxtItemQuantity");
                var slider = FindChild<Slider>(parentBorder, "QuantitySlider");

                int quantity = 0;

                if (textBox != null && int.TryParse(textBox.Text, out int textQty))
                {
                    quantity = textQty;
                }
                else if (slider != null)
                {
                    quantity = (int)slider.Value;
                }

                if (quantity <= 0)
                {
                    MessageBox.Show("Укажите количество больше нуля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (quantity > product.Количество)
                {
                    quantity = product.Количество;
                    MessageBox.Show($"Недостаточно товара на складе! Будет добавлено максимальное доступное количество: {quantity} шт.",
                        "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                var existingItem = newSaleItems.FirstOrDefault(i => i.Товар == product.Наименование);
                if (existingItem != null)
                {
                    int newTotal = existingItem.Количество + quantity;
                    if (newTotal > product.Количество)
                    {
                        quantity = product.Количество - existingItem.Количество;
                        if (quantity <= 0)
                        {
                            MessageBox.Show("Товар уже добавлен в максимальном количестве!", "Предупреждение",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                            return;
                        }
                        MessageBox.Show($"Будет добавлено только {quantity} шт. (доступный остаток)", "Предупреждение",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    existingItem.Количество += quantity;
                }
                else
                {
                    newSaleItems.Add(new SaleItemDisplay
                    {
                        Товар = product.Наименование,
                        Количество = quantity,
                        Цена = product.Цена_за_ед_продажа,
                        PhotoSource = productVM.PhotoSource
                    });
                }

                if (slider != null) slider.Value = 0;
                if (textBox != null) textBox.Text = "1";

                UpdateNewSaleTotal();
                ListViewNewSaleItems.Items.Refresh();
            }
        }

        private void RemoveNewSaleItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SaleItemDisplay item)
            {
                newSaleItems.Remove(item);
                UpdateNewSaleTotal();
                ListViewNewSaleItems.Items.Refresh();
            }
        }

        private void UpdateNewSaleTotal()
        {
            decimal total = newSaleItems.Sum(i => i.Сумма);
            TxtNewSaleTotal.Text = $"{total:N2} ₽";
            BtnSaveNewSale.IsEnabled = newSaleItems.Any();
        }

        private void SaveNewSale_Click(object sender, RoutedEventArgs e)
        {
            if (!newSaleItems.Any())
            {
                MessageBox.Show("Добавьте товары в чек!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (editingSaleCode.HasValue)
                {
                    // Режим редактирования — заменяем состав существующего чека
                    var existingSale = context.Продажи.Find(editingSaleCode.Value);
                    if (existingSale == null)
                    {
                        MessageBox.Show("Редактируемый чек не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Возвращаем старые товары на склад
                    var oldItems = context.Состав_продажи.Where(i => i.Код_чека == editingSaleCode.Value).ToList();
                    foreach (var oldItem in oldItems)
                    {
                        var product = context.Товары.Find(oldItem.Код_товара);
                        if (product != null)
                        {
                            product.Количество += oldItem.Количество;
                        }
                        context.Состав_продажи.Remove(oldItem);
                    }

                    // Добавляем новые товары
                    foreach (var item in newSaleItems)
                    {
                        var product = context.Товары.FirstOrDefault(p => p.Наименование == item.Товар);
                        if (product != null)
                        {
                            var saleComposition = new Состав_продажи
                            {
                                Код_чека = editingSaleCode.Value,
                                Код_товара = product.Код_товара,
                                Количество = item.Количество,
                                Цена = item.Цена
                            };
                            context.Состав_продажи.Add(saleComposition);
                            product.Количество -= item.Количество;
                        }
                    }

                    context.SaveChanges();
                    MessageBox.Show($"Чек №{editingSaleCode} успешно обновлён!", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Режим создания нового чека
                    var sale = new Продажи
                    {
                        Код_сотрудника = currentUser.Код_сотрудника,
                        Дата_продажи = DateTime.Now
                    };

                    context.Продажи.Add(sale);
                    context.SaveChanges();

                    var saleCode = sale.Код_чека;

                    foreach (var item in newSaleItems)
                    {
                        var product = context.Товары.FirstOrDefault(p => p.Наименование == item.Товар);
                        if (product != null)
                        {
                            var saleComposition = new Состав_продажи
                            {
                                Код_чека = saleCode,
                                Код_товара = product.Код_товара,
                                Количество = item.Количество,
                                Цена = item.Цена
                            };
                            context.Состав_продажи.Add(saleComposition);
                            product.Количество -= item.Количество;
                        }
                    }

                    context.SaveChanges();

                    var printResult = MessageBox.Show(
                        $"Продажа оформлена! Чек №{saleCode}\n\nЖелаете распечатать чек?",
                        "Успех",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information);

                    if (printResult == MessageBoxResult.Yes)
                    {
                        PrintReceipt(saleCode);
                    }
                }

                SwitchToViewMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void SwitchToViewMode()
        {
            editingSaleCode = null;
            BtnSaveNewSale.Content = "💾 Оформить продажу";

            SalesViewGrid.Visibility = Visibility.Visible;
            NewSaleGrid.Visibility = Visibility.Collapsed;
            ViewModeButtons.Visibility = Visibility.Visible;
            NewSaleModeButtons.Visibility = Visibility.Collapsed;
            NewSaleTotalPanel.Visibility = Visibility.Collapsed;
            GrandTotalPanel.Visibility = Visibility.Visible;
            PageModeText.Text = "Управление продажами";

            newSaleItems.Clear();
            RefreshContext();
            RestoreFilterState(); // Восстанавливаем фильтры

            // Применяем фильтры вместо загрузки без фильтров
            if (HasActiveFilters())
            {
                LoadFilteredSales();
            }
            else
            {
                LoadAllSalesWithoutFilters();
            }

            LoadGrandTotal();
            ClearSaleDetails();
        }
        private bool HasActiveFilters()
        {
            return !string.IsNullOrWhiteSpace(GetActualText(TxtSearch)) ||
                   DateFrom.SelectedDate.HasValue ||
                   DateTo.SelectedDate.HasValue ||
                   (CmbEmployee.SelectedValue != null && int.TryParse(CmbEmployee.SelectedValue.ToString(), out int empId) && empId > 0) ||
                   RbSortByDateAsc.IsChecked == true ||
                   RbSortByAmount.IsChecked == true ||
                   RbSortByAmountAsc.IsChecked == true ||
                   RbSortByCode.IsChecked == true ||
                   RbSortByCodeAsc.IsChecked == true;
        }
        private void PrintReceipt(int saleCode)
        {
            try
            {
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить чек",
                    FileName = $"Чек_{saleCode}_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sale = context.Продажи
                        .Include("Сотрудники")
                        .FirstOrDefault(s => s.Код_чека == saleCode);

                    if (sale == null)
                    {
                        MessageBox.Show("Продажа не найдена в базе данных.", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    var items = context.Состав_продажи
                        .Include("Товары")
                        .Where(i => i.Код_чека == saleCode)
                        .ToList()
                        .Select(i => new
                        {
                            Товар = i.Товары.Наименование,
                            Количество = i.Количество,
                            Цена = i.Цена,
                            Сумма = i.Количество * i.Цена
                        })
                        .ToList();

                    var total = items.Sum(i => i.Сумма);

                    using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4))
                    {
                        using (var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, new FileStream(saveFileDialog.FileName, FileMode.Create)))
                        {
                            document.Open();

                            string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                            var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(
                                fontPath,
                                iTextSharp.text.pdf.BaseFont.IDENTITY_H,
                                iTextSharp.text.pdf.BaseFont.EMBEDDED);
                            var font = new iTextSharp.text.Font(baseFont, 10);
                            var fontBold = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.BOLD);
                            var fontTitle = new iTextSharp.text.Font(baseFont, 14, iTextSharp.text.Font.BOLD);

                            var title = new iTextSharp.text.Paragraph("ЧЕК ПРОДАЖИ", fontTitle);
                            title.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                            document.Add(title);
                            document.Add(new iTextSharp.text.Paragraph(" "));

                            document.Add(new iTextSharp.text.Paragraph($"Чек №: {saleCode}", font));
                            document.Add(new iTextSharp.text.Paragraph($"Дата: {sale.Дата_продажи:dd.MM.yyyy HH:mm}", font));
                            document.Add(new iTextSharp.text.Paragraph($"Сотрудник: {sale.Сотрудники?.Фамилия} {sale.Сотрудники?.Имя}", font));
                            document.Add(new iTextSharp.text.Paragraph(" "));

                            var table = new iTextSharp.text.pdf.PdfPTable(4);
                            table.WidthPercentage = 100;
                            table.SetWidths(new float[] { 40, 15, 20, 25 });

                            var headerCells = new[] { "Товар", "Кол-во", "Цена", "Сумма" };
                            foreach (var cellText in headerCells)
                            {
                                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cellText, fontBold));
                                cell.BackgroundColor = new iTextSharp.text.BaseColor(200, 200, 200);
                                table.AddCell(cell);
                            }

                            foreach (var item in items)
                            {
                                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(item.Товар, font)));
                                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(item.Количество.ToString(), font)) { HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT });
                                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph($"{item.Цена:N2} ₽", font)) { HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT });
                                table.AddCell(new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph($"{item.Сумма:N2} ₽", font)) { HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT });
                            }

                            document.Add(table);
                            document.Add(new iTextSharp.text.Paragraph(" "));

                            var totalParagraph = new iTextSharp.text.Paragraph($"ИТОГО: {total:N2} ₽", fontBold);
                            totalParagraph.Alignment = iTextSharp.text.Element.ALIGN_RIGHT;
                            document.Add(totalParagraph);

                            document.Add(new iTextSharp.text.Paragraph(" "));

                            var signature = new iTextSharp.text.Paragraph("Спасибо за покупку! :)", font);
                            signature.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                            document.Add(signature);

                            document.Close();
                        }
                    }

                    MessageBox.Show($"Чек успешно сохранен в PDF:\n{saveFileDialog.FileName}",
                        "Печать чека", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати чека: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

    }

    public class SaleViewModel
    {
        public Продажи OriginalSale { get; set; }
        public decimal Total { get; set; }

        public string ReceiptDisplay => $"Чек №{OriginalSale.Код_чека}";
        public string DateDisplay => OriginalSale.Дата_продажи.ToString("dd.MM.yyyy HH:mm");
        public string EmployeeDisplay => OriginalSale.Сотрудники != null ?
            $"👤 {OriginalSale.Сотрудники.Фамилия} {OriginalSale.Сотрудники.Имя}" : "👤 Не указан";
        public string TotalDisplay => $"💰 {Total:N2} ₽";

        public SaleViewModel(Продажи sale, decimal total)
        {
            OriginalSale = sale;
            Total = total;
        }
    }


    public class ProductSaleViewModel
    {
        public Товары OriginalProduct { get; set; }
        public string Наименование { get; set; }
        public decimal Цена_за_ед_продажа { get; set; }
        public int Количество { get; set; }
        public string CategoryName { get; set; }
        public BitmapImage PhotoSource { get; set; }

        public bool HasStock => Количество > 0;

        public string PriceDisplay => $"{Цена_за_ед_продажа:N2} ₽";
        public string QuantityDisplay => $"📦 В наличии: {Количество} шт.";

        public ProductSaleViewModel(Товары product)
        {
            OriginalProduct = product;
            Наименование = product.Наименование;
            Цена_за_ед_продажа = product.Цена_за_ед_продажа;
            Количество = product.Количество;
            CategoryName = product.Категории?.Категория != null ? $"📂 {product.Категории.Категория}" : "📂 Без категории";
            PhotoSource = LoadImageFromBytes(product.Фото);
        }

        private static BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                try
                {
                    return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
                }
                catch
                {
                    return new BitmapImage();
                }
            }

            try
            {
                using (var ms = new MemoryStream(imageData))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                }
            }
            catch
            {
                try
                {
                    return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
                }
                catch
                {
                    return new BitmapImage();
                }
            }
        }
    }
}