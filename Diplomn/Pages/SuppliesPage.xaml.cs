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
    public partial class SuppliesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private ObservableCollection<SupplyViewModel> suppliesView;
        private ObservableCollection<ProductSupplyViewModel> productsView;
        private ObservableCollection<SupplyItemDisplay> newSupplyItems;
        private bool isLoading = false;
        private int? editingSupplyCode = null;
        private FilterState savedFilterState = null;

        public class SupplyItemDisplay
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
            public int SupplierIndex { get; set; }
            public string SortMode { get; set; }
        }

        public SuppliesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Поставки — {user.Фамилия} {user.Имя}";
            suppliesView = new ObservableCollection<SupplyViewModel>();
            productsView = new ObservableCollection<ProductSupplyViewModel>();
            newSupplyItems = new ObservableCollection<SupplyItemDisplay>();
            ListViewSupplies.ItemsSource = suppliesView;
            ListViewNewSupplyItems.ItemsSource = newSupplyItems;

            context.Configuration.LazyLoadingEnabled = false;
            context.Configuration.ProxyCreationEnabled = false;

            this.Unloaded += SuppliesPage_Unloaded;

            LoadEmployees();
            LoadSuppliers();

            this.Loaded += (s, e) =>
            {
                LoadAllSuppliesWithoutFilters();
                LoadGrandTotal();
            };
        }

        private void SuppliesPage_Unloaded(object sender, RoutedEventArgs e)
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

        private void LoadSuppliers()
        {
            var suppliers = context.Поставщики.ToList();
            var supplierList = new List<Поставщики>();
            supplierList.Add(new Поставщики { Код_поставщика = 0, Наименование_поставщика = "Все поставщики" });
            supplierList.AddRange(suppliers);
            CmbSupplier.ItemsSource = supplierList;
            CmbSupplier.SelectedValuePath = "Код_поставщика";
            CmbSupplier.DisplayMemberPath = "Наименование_поставщика";
            CmbSupplier.SelectedIndex = 0;
        }

        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                try { return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute)); }
                catch { return new BitmapImage(); }
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
                try { return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute)); }
                catch { return new BitmapImage(); }
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
                if (foundChild != null) return foundChild;
            }
            return null;
        }

        private void SaveFilterState()
        {
            savedFilterState = new FilterState
            {
                SearchText = TxtSearch.Text,
                DateFrom = DateFrom.SelectedDate,
                DateTo = DateTo.SelectedDate,
                EmployeeIndex = CmbEmployee.SelectedIndex,
                SupplierIndex = CmbSupplier.SelectedIndex,
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
            CmbEmployee.SelectedIndex = savedFilterState.EmployeeIndex >= 0 ? savedFilterState.EmployeeIndex : 0;
            CmbSupplier.SelectedIndex = savedFilterState.SupplierIndex >= 0 ? savedFilterState.SupplierIndex : 0;

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

            RbSortByDate.Checked += SortChanged;
            RbSortByDateAsc.Checked += SortChanged;
            RbSortByAmount.Checked += SortChanged;
            RbSortByAmountAsc.Checked += SortChanged;
            RbSortByCode.Checked += SortChanged;
            RbSortByCodeAsc.Checked += SortChanged;
        }

        private bool HasActiveFilters()
        {
            return !string.IsNullOrWhiteSpace(GetActualText(TxtSearch)) ||
                   DateFrom.SelectedDate.HasValue ||
                   DateTo.SelectedDate.HasValue ||
                   (CmbEmployee.SelectedValue != null && int.TryParse(CmbEmployee.SelectedValue.ToString(), out int empId) && empId > 0) ||
                   (CmbSupplier.SelectedValue != null && int.TryParse(CmbSupplier.SelectedValue.ToString(), out int supId) && supId > 0) ||
                   RbSortByDateAsc.IsChecked == true ||
                   RbSortByAmount.IsChecked == true ||
                   RbSortByAmountAsc.IsChecked == true ||
                   RbSortByCode.IsChecked == true ||
                   RbSortByCodeAsc.IsChecked == true;
        }

        #endregion

        #region Режим просмотра поставок

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyFilters();
        }

        private void SortChanged(object sender, RoutedEventArgs e)
        {
            if (context != null && this.IsLoaded) ApplyFilters();
        }

        private IQueryable<Поставка> GetBaseQuery()
        {
            var query = context.Поставка
                .Include("Сотрудники")
                .Include("Поставщики")
                .AsQueryable();

            string searchText = GetActualText(TxtSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.ToLower();
                var matchingSupplyIds = context.Состав_поставки
                    .Include("Товары")
                    .Where(i => i.Товары.Наименование.ToLower().Contains(term))
                    .Select(i => i.Код_поставки)
                    .Distinct()
                    .ToList();
                query = query.Where(s => matchingSupplyIds.Contains(s.Код_поставки));
            }

            if (DateFrom.SelectedDate.HasValue)
            {
                var dateFrom = DateFrom.SelectedDate.Value.Date;
                query = query.Where(s => DbFunctions.TruncateTime(s.Дата_оформления_постивки) >= dateFrom);
            }

            if (DateTo.SelectedDate.HasValue)
            {
                var dateTo = DateTo.SelectedDate.Value.Date.AddDays(1);
                query = query.Where(s => DbFunctions.TruncateTime(s.Дата_оформления_постивки) < dateTo);
            }

            if (CmbEmployee.SelectedValue != null)
            {
                int employeeId;
                if (int.TryParse(CmbEmployee.SelectedValue.ToString(), out employeeId) && employeeId > 0)
                {
                    query = query.Where(s => s.Код_сотрудника == employeeId);
                }
            }

            if (CmbSupplier.SelectedValue != null)
            {
                int supplierId;
                if (int.TryParse(CmbSupplier.SelectedValue.ToString(), out supplierId) && supplierId > 0)
                {
                    query = query.Where(s => s.Код_поставщика == supplierId);
                }
            }

            return query;
        }

        private List<Поставка> GetFilteredAndSortedSupplies()
        {
            var query = GetBaseQuery();

            if (RbSortByDate.IsChecked == true)
                query = query.OrderByDescending(s => s.Дата_оформления_постивки);
            else if (RbSortByDateAsc.IsChecked == true)
                query = query.OrderBy(s => s.Дата_оформления_постивки);
            else if (RbSortByCode.IsChecked == true)
                query = query.OrderByDescending(s => s.Код_поставки);
            else if (RbSortByCodeAsc.IsChecked == true)
                query = query.OrderBy(s => s.Код_поставки);
            else if (RbSortByAmount.IsChecked == true || RbSortByAmountAsc.IsChecked == true)
            {
                var supplies = query.AsNoTracking().ToList();
                var supplyIds = supplies.Select(s => s.Код_поставки).ToList();
                var totals = context.Состав_поставки
                    .Where(i => supplyIds.Contains(i.Код_поставки))
                    .GroupBy(i => i.Код_поставки)
                    .Select(g => new { SupplyId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0 })
                    .ToDictionary(x => x.SupplyId, x => x.Total);

                if (RbSortByAmount.IsChecked == true)
                    return supplies.OrderByDescending(s => totals.TryGetValue(s.Код_поставки, out var t) ? t : 0).ToList();
                else
                    return supplies.OrderBy(s => totals.TryGetValue(s.Код_поставки, out var t) ? t : 0).ToList();
            }
            else
                query = query.OrderByDescending(s => s.Дата_оформления_постивки);

            return query.AsNoTracking().ToList();
        }

        private void LoadAllSuppliesWithoutFilters()
        {
            if (isLoading) return;
            try
            {
                isLoading = true;
                var supplies = GetBaseQuery()
                    .OrderByDescending(s => s.Дата_оформления_постивки)
                    .AsNoTracking()
                    .ToList();
                UpdateSuppliesView(supplies);
                LoadGrandTotal();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { isLoading = false; }
        }

        private void LoadFilteredSupplies()
        {
            if (isLoading) return;
            try
            {
                isLoading = true;
                var supplies = GetFilteredAndSortedSupplies();
                UpdateSuppliesView(supplies);
                LoadGrandTotal();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { isLoading = false; }
        }

        private void UpdateSuppliesView(List<Поставка> supplies)
        {
            if (supplies == null) supplies = new List<Поставка>();

            var selectedItem = ListViewSupplies.SelectedItem as SupplyViewModel;
            int? selectedSupplyCode = selectedItem?.OriginalSupply?.Код_поставки;

            var supplyIds = supplies.Select(s => s.Код_поставки).ToList();
            Dictionary<int, decimal> totals;
            if (supplyIds.Any())
            {
                totals = context.Состав_поставки
                    .Where(i => supplyIds.Contains(i.Код_поставки))
                    .GroupBy(i => i.Код_поставки)
                    .Select(g => new { SupplyId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0 })
                    .ToDictionary(x => x.SupplyId, x => x.Total);
            }
            else totals = new Dictionary<int, decimal>();

            suppliesView.Clear();
            foreach (var supply in supplies)
            {
                var total = totals.TryGetValue(supply.Код_поставки, out var t) ? t : 0;
                suppliesView.Add(new SupplyViewModel(supply, total));
            }

            if (selectedSupplyCode.HasValue)
            {
                var itemToSelect = suppliesView.FirstOrDefault(s => s.OriginalSupply?.Код_поставки == selectedSupplyCode.Value);
                if (itemToSelect != null) ListViewSupplies.SelectedItem = itemToSelect;
                else ClearSupplyDetails();
            }
            else ClearSupplyDetails();

            if (ListViewSupplies.ItemsSource != suppliesView)
                ListViewSupplies.ItemsSource = suppliesView;
        }

        private void ApplyFilters() => LoadFilteredSupplies();

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            CmbEmployee.SelectedIndex = 0;
            CmbSupplier.SelectedIndex = 0;
            RbSortByDate.IsChecked = true;
            LoadAllSuppliesWithoutFilters();
            LoadGrandTotal();
            ClearSupplyDetails();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supplies = GetBaseQuery()
                    .OrderByDescending(s => s.Дата_оформления_постивки)
                    .AsNoTracking()
                    .ToList();

                if (!supplies.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить отчет о поставках",
                    FileName = $"Отчет_поставки_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() != true) return;

                const string shopName = "Oculus+";
                const string shopPhone = "+7 (461) 345 12-34";
                const string shopEmail = "Oculus@глаза.ру";
                const string shopWebsite = "Oculus.ру";
                const string shopHours = "9:00 – 17:00 ежедневно";

                string initials = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
                if (!string.IsNullOrWhiteSpace(currentUser.Отчество))
                    initials += $"{currentUser.Отчество?.Substring(0, 1)}.";
                else initials += ".";

                var supplyIds = supplies.Select(s => s.Код_поставки).ToList();
                var totals = context.Состав_поставки
                    .Where(i => supplyIds.Contains(i.Код_поставки))
                    .GroupBy(i => i.Код_поставки)
                    .Select(g => new { SupplyId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0 })
                    .ToDictionary(x => x.SupplyId, x => x.Total);

                var totalSupplies = supplies.Count;
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

                        var reportTitle = new iTextSharp.text.Paragraph("ОТЧЁТ О ПОСТАВКАХ", fontTitle);
                        reportTitle.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        reportTitle.SpacingAfter = 25;
                        document.Add(reportTitle);

                        var table = new iTextSharp.text.pdf.PdfPTable(5);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 12, 22, 25, 25, 16 });
                        table.SpacingBefore = 10;
                        table.SpacingAfter = 25;

                        var headers = new[] { "Код пост.", "Дата", "Сотрудник", "Поставщик", "Сумма" };
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
                        foreach (var supply in supplies)
                        {
                            var total = totals.TryGetValue(supply.Код_поставки, out var t) ? t : 0;
                            var employee = supply.Сотрудники != null ? $"{supply.Сотрудники.Фамилия} {supply.Сотрудники.Имя}" : "—";
                            var supplier = supply.Поставщики?.Наименование_поставщика ?? "—";

                            var cells = new[]
                            {
                                supply.Код_поставки.ToString(),
                                supply.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm"),
                                employee,
                                supplier,
                                $"{total:N2} ₽"
                            };

                            var centerColumns = new HashSet<int> { 0, 4 };

                            for (int i = 0; i < cells.Length; i++)
                            {
                                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], fontTableCell));
                                cell.Padding = 5;
                                cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
                                if (alternate) cell.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                                if (centerColumns.Contains(i)) cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                                table.AddCell(cell);
                            }
                            alternate = !alternate;
                        }

                        document.Add(table);

                        var totalParagraph = new iTextSharp.text.Paragraph();
                        totalParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        totalParagraph.SpacingBefore = 5;
                        totalParagraph.SpacingAfter = 3;
                        totalParagraph.Add(new iTextSharp.text.Chunk($"Всего поставок: {totalSupplies}", fontSubtitle));
                        document.Add(totalParagraph);

                        var totalSumParagraph = new iTextSharp.text.Paragraph();
                        totalSumParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        totalSumParagraph.SpacingAfter = 35;
                        totalSumParagraph.Add(new iTextSharp.text.Chunk($"Общая сумма поставок: {grandTotal:N2} ₽", fontSubtitle));
                        document.Add(totalSumParagraph);

                        var signTable = new iTextSharp.text.pdf.PdfPTable(1);
                        signTable.WidthPercentage = 55;
                        signTable.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;

                        var signCell1 = new iTextSharp.text.pdf.PdfPCell();
                        signCell1.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell1.HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT;
                        signCell1.PaddingBottom = 3;
                        var signParagraph = new iTextSharp.text.Paragraph();
                        signParagraph.Add(new iTextSharp.text.Chunk($"{currentUser.Должность?.Название ?? "Сотрудник"} {initials} _______________  {DateTime.Now:dd.MM.yyyy}", fontSign));
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

                var result = MessageBox.Show($"Отчёт о поставках сохранён!\n\nФайл: {saveFileDialog.FileName}\nВсего поставок: {totalSupplies}\nОбщая сумма: {grandTotal:N2} ₽\n\nОткрыть PDF?", "Отчёт сохранён", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = saveFileDialog.FileName, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}\n\nУбедитесь, что библиотека iTextSharp установлена.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListViewSupplies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSupplies.SelectedItem is SupplyViewModel selectedSupply)
            {
                EnableEditDeleteButtons(true);
                var supply = selectedSupply.OriginalSupply;
                TxtSupplyId.Text = supply.Код_поставки.ToString();
                TxtSupplyDate.Text = supply.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = supply.Сотрудники != null ? $"{supply.Сотрудники.Фамилия} {supply.Сотрудники.Имя}" : "";
                TxtSupplier.Text = supply.Поставщики?.Наименование_поставщика ?? "Не указан";

                var items = context.Состав_поставки
                    .Include("Товары")
                    .Where(i => i.Код_поставки == supply.Код_поставки)
                    .ToList()
                    .Select(i => new SupplyItemDisplay
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена_за_ед_покупка,
                        PhotoSource = LoadImageFromBytes(i.Товары?.Фото)
                    }).ToList();

                ListViewSupplyItems.ItemsSource = items;
                decimal total = items.Sum(i => i.Сумма);
                TxtTotal.Text = $"{total:N2} ₽";
                TotalPanel.Visibility = Visibility.Visible;
            }
            else
            {
                EnableEditDeleteButtons(false);
                ClearSupplyDetails();
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
                        button.IsEnabled = enable;
                }
            }
        }

        private void ClearSupplyDetails()
        {
            TxtSupplyId.Text = "";
            TxtSupplyDate.Text = "";
            TxtEmployee.Text = "";
            TxtSupplier.Text = "";
            ListViewSupplyItems.ItemsSource = null;
            TxtTotal.Text = "";
            TotalPanel.Visibility = Visibility.Collapsed;
        }

        private void DeleteSupply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListViewSupplies.SelectedItem as SupplyViewModel;
                if (selectedItem == null)
                {
                    MessageBox.Show("Выберите поставку для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var supplyCode = selectedItem.OriginalSupply.Код_поставки;

                var result = MessageBox.Show($"Вы уверены, что хотите удалить поставку №{supplyCode}?\nКоличество товаров будет уменьшено!",
                    "Подтверждение удаления", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    SaveFilterState();

                    var items = context.Состав_поставки.Where(i => i.Код_поставки == supplyCode).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null)
                        {
                            product.Количество -= item.Количество;
                            if (product.Количество < 0) product.Количество = 0;
                        }
                        context.Состав_поставки.Remove(item);
                    }

                    var supplyToDelete = context.Поставка.Find(supplyCode);
                    if (supplyToDelete != null) context.Поставка.Remove(supplyToDelete);

                    context.SaveChanges();
                    MessageBox.Show("Поставка успешно удалена! Количество товаров обновлено.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshContext();
                    RestoreFilterState();
                    if (HasActiveFilters()) LoadFilteredSupplies();
                    else LoadAllSuppliesWithoutFilters();
                    LoadGrandTotal();
                    ClearSupplyDetails();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении поставки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearSupplyDetails();

        #endregion

        #region Режим создания/редактирования поставки

        private void NewSupply_Click(object sender, RoutedEventArgs e)
        {
            SaveFilterState();
            editingSupplyCode = null;
            BtnSaveNewSupply.Content = "💾 Оформить поставку";

            SuppliesViewGrid.Visibility = Visibility.Collapsed;
            NewSupplyGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSupplyModeButtons.Visibility = Visibility.Visible;
            NewSupplyTotalPanel.Visibility = Visibility.Visible;
            GrandTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = "Оформление новой поставки";

            newSupplyItems.Clear();
            TxtNewSupplyTotal.Text = "0.00 ₽";
            BtnSaveNewSupply.IsEnabled = false;

            LoadNewSupplySuppliers();
            ClearProductFilterFields();
            RefreshContext();
            LoadAllProducts();
        }

        private void EditSupply_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ListViewSupplies.SelectedItem as SupplyViewModel;
            if (selectedItem == null)
            {
                MessageBox.Show("Выберите поставку для редактирования!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SaveFilterState();
            editingSupplyCode = selectedItem.OriginalSupply.Код_поставки;

            SuppliesViewGrid.Visibility = Visibility.Collapsed;
            NewSupplyGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSupplyModeButtons.Visibility = Visibility.Visible;
            NewSupplyTotalPanel.Visibility = Visibility.Visible;
            GrandTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = $"Редактирование поставки №{editingSupplyCode}";

            newSupplyItems.Clear();

            var items = context.Состав_поставки
                .Include("Товары")
                .Where(i => i.Код_поставки == editingSupplyCode.Value)
                .ToList();

            foreach (var item in items)
            {
                newSupplyItems.Add(new SupplyItemDisplay
                {
                    Товар = item.Товары.Наименование,
                    Количество = item.Количество,
                    Цена = item.Цена_за_ед_покупка,
                    PhotoSource = LoadImageFromBytes(item.Товары?.Фото)
                });
            }

            UpdateNewSupplyTotal();
            ListViewNewSupplyItems.Items.Refresh();
            BtnSaveNewSupply.Content = "💾 Сохранить изменения";

            LoadNewSupplySuppliers();

            // Выбираем поставщика
            var supply = context.Поставка.Find(editingSupplyCode.Value);
            if (supply != null && CmbNewSupplySupplier.Items.Count > 0)
            {
                for (int i = 0; i < CmbNewSupplySupplier.Items.Count; i++)
                {
                    var supplier = CmbNewSupplySupplier.Items[i] as Поставщики;
                    if (supplier != null && supplier.Код_поставщика == supply.Код_поставщика)
                    {
                        CmbNewSupplySupplier.SelectedIndex = i;
                        break;
                    }
                }
            }

            ClearProductFilterFields();
            RefreshContext();
            LoadAllProducts();
        }

        private void LoadNewSupplySuppliers()
        {
            var suppliers = context.Поставщики.ToList();
            CmbNewSupplySupplier.ItemsSource = suppliers;
            if (suppliers.Any()) CmbNewSupplySupplier.SelectedIndex = 0;
        }

        private void CancelNewSupply_Click(object sender, RoutedEventArgs e) => SwitchToViewMode();

        private void LoadAllProducts()
        {
            try
            {
                var products = context.Товары.Include("Категории").AsNoTracking().ToList();
                UpdateProductsView(products);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadProducts()
        {
            try
            {
                var products = GetProductFilteredQuery().Include("Категории").AsNoTracking().ToList();
                UpdateProductsView(products);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateProductsView(List<Товары> products)
        {
            if (products == null || products.Count == 0) { productsView.Clear(); return; }

            var selectedItem = ListViewProducts.SelectedItem as ProductSupplyViewModel;
            int? selectedProductCode = selectedItem?.OriginalProduct?.Код_товара;

            productsView.Clear();
            foreach (var product in products)
                productsView.Add(new ProductSupplyViewModel(product));

            if (ListViewProducts.ItemsSource != productsView) ListViewProducts.ItemsSource = productsView;
            else ListViewProducts.Items.Refresh();

            if (selectedProductCode.HasValue)
            {
                var itemToSelect = productsView.FirstOrDefault(p => p.OriginalProduct?.Код_товара == selectedProductCode.Value);
                if (itemToSelect != null) ListViewProducts.SelectedItem = itemToSelect;
            }
        }

        private IQueryable<Товары> GetProductFilteredQuery()
        {
            var query = context.Товары.AsQueryable();

            string searchText = GetActualText(TxtProductSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(p => p.Наименование.ToLower().Contains(searchText.ToLower()));

            return query;
        }

        private void ApplyProductFilters() => LoadProducts();
        private void ApplyProductFilters_Click(object sender, RoutedEventArgs e) => ApplyProductFilters();

        private void ClearProductFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtProductSearch.Text = "";
            LoadProducts();
        }

        private void ClearProductFilterFields()
        {
            TxtProductSearch.Text = "";
        }

        private void TxtProductSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyProductFilters();
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
                    if (slider != null && int.TryParse(textBox.Text, out int value))
                    {
                        if (value < 0) { value = 0; textBox.Text = "0"; }
                        slider.Value = value;
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
                    if (textBox != null) textBox.Text = ((int)slider.Value).ToString();
                }
            }
        }

        private void ListViewProducts_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void AddToSupplyItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductSupplyViewModel productVM)
            {
                var product = productVM.OriginalProduct;
                var parentBorder = FindParent<Border>(button);
                if (parentBorder == null) return;

                var textBox = FindChild<TextBox>(parentBorder, "TxtItemQuantity");
                var slider = FindChild<Slider>(parentBorder, "QuantitySlider");

                int quantity = 0;
                if (textBox != null && int.TryParse(textBox.Text, out int textQty)) quantity = textQty;
                else if (slider != null) quantity = (int)slider.Value;

                if (quantity <= 0)
                {
                    MessageBox.Show("Укажите количество больше нуля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existingItem = newSupplyItems.FirstOrDefault(i => i.Товар == product.Наименование);
                if (existingItem != null) existingItem.Количество += quantity;
                else
                {
                    newSupplyItems.Add(new SupplyItemDisplay
                    {
                        Товар = product.Наименование,
                        Количество = quantity,
                        Цена = product.Цена_за_ед_продажа,
                        PhotoSource = productVM.PhotoSource
                    });
                }

                if (slider != null) slider.Value = 0;
                if (textBox != null) textBox.Text = "1";

                UpdateNewSupplyTotal();
                ListViewNewSupplyItems.Items.Refresh();
            }
        }

        private void RemoveNewSupplyItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SupplyItemDisplay item)
            {
                newSupplyItems.Remove(item);
                UpdateNewSupplyTotal();
                ListViewNewSupplyItems.Items.Refresh();
            }
        }

        private void UpdateNewSupplyTotal()
        {
            decimal total = newSupplyItems.Sum(i => i.Сумма);
            TxtNewSupplyTotal.Text = $"{total:N2} ₽";
            BtnSaveNewSupply.IsEnabled = newSupplyItems.Any() && CmbNewSupplySupplier.SelectedItem != null;
        }

        private void SaveNewSupply_Click(object sender, RoutedEventArgs e)
        {
            if (!newSupplyItems.Any())
            {
                MessageBox.Show("Добавьте товары в поставку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CmbNewSupplySupplier.SelectedItem == null)
            {
                MessageBox.Show("Выберите поставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var selectedSupplier = CmbNewSupplySupplier.SelectedItem as Поставщики;

                if (editingSupplyCode.HasValue)
                {
                    // Режим редактирования
                    var existingSupply = context.Поставка.Find(editingSupplyCode.Value);
                    if (existingSupply != null)
                    {
                        existingSupply.Код_поставщика = selectedSupplier.Код_поставщика;

                        // Возвращаем старые товары на склад
                        var oldItems = context.Состав_поставки.Where(i => i.Код_поставки == editingSupplyCode.Value).ToList();
                        foreach (var oldItem in oldItems)
                        {
                            var product = context.Товары.Find(oldItem.Код_товара);
                            if (product != null) product.Количество -= oldItem.Количество;
                            if (product != null && product.Количество < 0) product.Количество = 0;
                            context.Состав_поставки.Remove(oldItem);
                        }

                        // Добавляем новые товары
                        foreach (var item in newSupplyItems)
                        {
                            var product = context.Товары.FirstOrDefault(p => p.Наименование == item.Товар);
                            if (product != null)
                            {
                                context.Состав_поставки.Add(new Состав_поставки
                                {
                                    Код_поставки = editingSupplyCode.Value,
                                    Код_товара = product.Код_товара,
                                    Количество = item.Количество,
                                    Цена_за_ед_покупка = item.Цена
                                });
                                product.Количество += item.Количество;
                            }
                        }

                        context.SaveChanges();
                        MessageBox.Show($"Поставка №{editingSupplyCode} успешно обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    // Режим создания
                    var supply = new Поставка
                    {
                        Код_сотрудника = currentUser.Код_сотрудника,
                        Код_поставщика = selectedSupplier.Код_поставщика,
                        Дата_оформления_постивки = DateTime.Now
                    };

                    context.Поставка.Add(supply);
                    context.SaveChanges();

                    var supplyCode = supply.Код_поставки;

                    foreach (var item in newSupplyItems)
                    {
                        var product = context.Товары.FirstOrDefault(p => p.Наименование == item.Товар);
                        if (product != null)
                        {
                            context.Состав_поставки.Add(new Состав_поставки
                            {
                                Код_поставки = supplyCode,
                                Код_товара = product.Код_товара,
                                Количество = item.Количество,
                                Цена_за_ед_покупка = item.Цена
                            });
                            product.Количество += item.Количество;
                        }
                    }

                    context.SaveChanges();
                    MessageBox.Show($"Поставка №{supplyCode} оформлена! Товары добавлены на склад.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                SwitchToViewMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchToViewMode()
        {
            editingSupplyCode = null;
            BtnSaveNewSupply.Content = "💾 Оформить поставку";

            SuppliesViewGrid.Visibility = Visibility.Visible;
            NewSupplyGrid.Visibility = Visibility.Collapsed;
            ViewModeButtons.Visibility = Visibility.Visible;
            NewSupplyModeButtons.Visibility = Visibility.Collapsed;
            NewSupplyTotalPanel.Visibility = Visibility.Collapsed;
            GrandTotalPanel.Visibility = Visibility.Visible;
            PageModeText.Text = "Управление поставками";

            newSupplyItems.Clear();
            RefreshContext();
            RestoreFilterState();

            if (HasActiveFilters()) LoadFilteredSupplies();
            else LoadAllSuppliesWithoutFilters();

            LoadGrandTotal();
            ClearSupplyDetails();
        }

        private void LoadGrandTotal()
        {
            try
            {
                var query = context.Состав_поставки.AsQueryable();

                if (DateFrom.SelectedDate.HasValue)
                {
                    var dateFrom = DateFrom.SelectedDate.Value.Date;
                    query = query.Where(i => DbFunctions.TruncateTime(i.Поставка.Дата_оформления_постивки) >= dateFrom);
                }
                if (DateTo.SelectedDate.HasValue)
                {
                    var dateTo = DateTo.SelectedDate.Value.Date.AddDays(1);
                    query = query.Where(i => DbFunctions.TruncateTime(i.Поставка.Дата_оформления_постивки) < dateTo);
                }
                if (CmbEmployee.SelectedValue != null)
                {
                    int employeeId;
                    if (int.TryParse(CmbEmployee.SelectedValue.ToString(), out employeeId) && employeeId > 0)
                        query = query.Where(i => i.Поставка.Код_сотрудника == employeeId);
                }
                if (CmbSupplier.SelectedValue != null)
                {
                    int supplierId;
                    if (int.TryParse(CmbSupplier.SelectedValue.ToString(), out supplierId) && supplierId > 0)
                        query = query.Where(i => i.Поставка.Код_поставщика == supplierId);
                }

                var grandTotal = query.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0;
                TxtGrandTotal.Text = $"{grandTotal:N2} ₽";
            }
            catch (Exception ex)
            {
                TxtGrandTotal.Text = "0.00 ₽";
                Debug.WriteLine($"Ошибка загрузки общей суммы: {ex.Message}");
            }
        }

        #endregion
    }

    public class SupplyViewModel
    {
        public Поставка OriginalSupply { get; set; }
        public decimal Total { get; set; }

        public string SupplyDisplay => $"Поставка №{OriginalSupply.Код_поставки}";
        public string DateDisplay => OriginalSupply.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm");
        public string EmployeeDisplay => OriginalSupply.Сотрудники != null ? $"👤 {OriginalSupply.Сотрудники.Фамилия} {OriginalSupply.Сотрудники.Имя}" : "👤 Не указан";
        public string SupplierDisplay => OriginalSupply.Поставщики != null ? $"🏭 {OriginalSupply.Поставщики.Наименование_поставщика}" : "🏭 Не указан";
        public string TotalDisplay => $"💰 {Total:N2} ₽";

        public SupplyViewModel(Поставка supply, decimal total)
        {
            OriginalSupply = supply;
            Total = total;
        }
    }

    public class ProductSupplyViewModel
    {
        public Товары OriginalProduct { get; set; }
        public string Наименование { get; set; }
        public decimal Цена_за_ед_покупка { get; set; }
        public int Количество { get; set; }
        public string CategoryName { get; set; }
        public BitmapImage PhotoSource { get; set; }

        public string PriceDisplay => $"{Цена_за_ед_покупка:N2} ₽ (закуп)";
        public string QuantityDisplay => $"📦 На складе: {Количество} шт.";

        public ProductSupplyViewModel(Товары product)
        {
            OriginalProduct = product;
            Наименование = product.Наименование;
            Цена_за_ед_покупка = product.Цена_за_ед_продажа;
            Количество = product.Количество;
            CategoryName = product.Категории?.Категория != null ? $"📂 {product.Категории.Категория}" : "📂 Без категории";
            PhotoSource = LoadImageFromBytes(product.Фото);
        }

        private static BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
            {
                try { return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute)); }
                catch { return new BitmapImage(); }
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
                try { return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute)); }
                catch { return new BitmapImage(); }
            }
        }
    }
}