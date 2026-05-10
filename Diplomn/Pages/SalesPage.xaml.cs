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

        public class SaleItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
            public string PriceQuantityDisplay => $"{Цена:N2} ₽ × {Количество} шт.";
            public BitmapImage PhotoSource { get; set; }
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

            LoadAllSalesWithoutFilters(); // Загружаем все продажи при старте
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
                    return new BitmapImage(); // Пустое изображение если ресурс не найден
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
                    bitmap.Freeze(); // Замораживаем для использования в разных потоках
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

        private IQueryable<Продажи> GetFilteredQuery()
        {
            var query = context.Продажи
                .Include("Сотрудники")
                .AsQueryable();

            // Поиск по тексту
            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                if (int.TryParse(term, out int receiptCode))
                {
                    query = query.Where(s => s.Код_чека == receiptCode);
                }
                else
                {
                    query = query.Where(s => s.Сотрудники.Фамилия.Contains(term) ||
                                            s.Сотрудники.Имя.Contains(term));
                }
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
                var dateTo = DateTo.SelectedDate.Value.Date.AddDays(1); // Включаем весь день
                query = query.Where(s => DbFunctions.TruncateTime(s.Дата_продажи) < dateTo);
            }

            return query.OrderByDescending(s => s.Дата_продажи);
        }

        private void LoadAllSalesWithoutFilters()
        {
            if (isLoading) return;

            try
            {
                isLoading = true;

                var sales = context.Продажи
                    .Include("Сотрудники")
                    .AsNoTracking()
                    .OrderByDescending(s => s.Дата_продажи)
                    .ToList();

                UpdateSalesView(sales);
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

                var sales = GetFilteredQuery()
                    .AsNoTracking()
                    .ToList();

                UpdateSalesView(sales);
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

            // Сохраняем текущий выбранный элемент
            var selectedItem = ListViewSales.SelectedItem as SaleViewModel;
            int? selectedReceiptCode = selectedItem?.OriginalSale?.Код_чека;

            Debug.WriteLine($"UpdateSalesView: incoming sales count = {sales.Count}");

            // Эффективная загрузка всех сумм одним запросом
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

            // Очищаем и заполняем коллекцию
            salesView.Clear();
            foreach (var sale in sales)
            {
                var total = totals.TryGetValue(sale.Код_чека, out var t) ? t : 0;
                salesView.Add(new SaleViewModel(sale, total));
            }

            Debug.WriteLine($"UpdateSalesView: final salesView count = {salesView.Count}");

            // Восстанавливаем выделение если элемент все еще существует
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

            // Обновляем ItemsSource если нужно
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
            LoadAllSalesWithoutFilters();
            ClearSaleDetails();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sales = GetFilteredQuery()
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
                    Filter = "CSV файл (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt",
                    Title = "Сохранить отчет о продажах",
                    FileName = $"Отчет_продажи_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Загружаем суммы одним запросом
                    var saleIds = sales.Select(s => s.Код_чека).ToList();
                    var totals = context.Состав_продажи
                        .Where(i => saleIds.Contains(i.Код_чека))
                        .GroupBy(i => i.Код_чека)
                        .Select(g => new { SaleId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0 })
                        .ToDictionary(x => x.SaleId, x => x.Total);

                    var sb = new StringBuilder();
                    sb.AppendLine($"Отчет о продажах от {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine($"Сформировал: {currentUser.Фамилия} {currentUser.Имя}");

                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                        sb.AppendLine($"Поиск: \"{TxtSearch.Text}\"");
                    if (DateFrom.SelectedDate.HasValue)
                        sb.AppendLine($"Дата от: {DateFrom.SelectedDate.Value:dd.MM.yyyy}");
                    if (DateTo.SelectedDate.HasValue)
                        sb.AppendLine($"Дата до: {DateTo.SelectedDate.Value:dd.MM.yyyy}");

                    sb.AppendLine();
                    sb.AppendLine($"Всего продаж: {sales.Count}");
                    sb.AppendLine();
                    sb.AppendLine("Код чека;Дата продажи;Сотрудник;Общая сумма");

                    decimal grandTotal = 0;
                    foreach (var sale in sales)
                    {
                        var total = totals.TryGetValue(sale.Код_чека, out var t) ? t : 0;
                        grandTotal += total;
                        var employee = $"{sale.Сотрудники?.Фамилия} {sale.Сотрудники?.Имя}".Trim();
                        sb.AppendLine($"{sale.Код_чека};{sale.Дата_продажи:dd.MM.yyyy HH:mm};{employee};{total:N2}");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"Общая выручка: {grandTotal:N2} ₽");

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Отчет сохранен!\n{saveFileDialog.FileName}", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ListViewSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSales.SelectedItem is SaleViewModel selectedSale)
            {
                // Включаем кнопку удаления
                EnableDeleteButton(true);

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
                EnableDeleteButton(false);
                ClearSaleDetails();
            }
        }

        private void EnableDeleteButton(bool enable)
        {
            // Находим кнопку удаления в панели ViewModeButtons
            foreach (var child in ViewModeButtons.Children)
            {
                if (child is Button button && button.Content?.ToString() == "🗑 Удалить")
                {
                    button.IsEnabled = enable;
                    break;
                }
            }
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

                var sale = selectedItem.OriginalSale;

                var result = MessageBox.Show($"Вы уверены, что хотите удалить чек №{sale.Код_чека}?\nВместе с чеком будет удален его состав!",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var items = context.Состав_продажи.Where(i => i.Код_чека == sale.Код_чека).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null)
                        {
                            product.Количество += item.Количество;
                        }
                        context.Состав_продажи.Remove(item);
                    }

                    context.Продажи.Remove(sale);
                    context.SaveChanges();

                    MessageBox.Show("Продажа успешно удалена! Товары возвращены на склад.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshContext();
                    // После удаления загружаем все продажи без фильтров
                    LoadAllSalesWithoutFilters();
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
            // Переключаемся в режим создания
            SalesViewGrid.Visibility = Visibility.Collapsed;
            NewSaleGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSaleModeButtons.Visibility = Visibility.Visible;
            NewSaleTotalPanel.Visibility = Visibility.Visible;
            PageModeText.Text = "Оформление новой продажи";

            newSaleItems.Clear();
            TxtNewSaleTotal.Text = "0.00 ₽";
            BtnSaveNewSale.IsEnabled = false;

            // Очищаем фильтры товаров
            ClearProductFilterFields();

            // Обновляем контекст перед загрузкой товаров
            RefreshContext();

            // Загружаем ВСЕ товары без фильтров при открытии
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
            
        Debug.WriteLine($"LoadAllProducts: loaded {products.Count} products");
        UpdateProductsView(products);
    }
    catch (Exception ex)
    {
        MessageBox.Show($"Ошибка при загрузке товаров: {ex.Message}", "Ошибка",
            MessageBoxButton.OK, MessageBoxImage.Error);
        Debug.WriteLine($"LoadAllProducts error: {ex}");
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

            // Сохраняем текущий выбранный элемент
            var selectedItem = ListViewProducts.SelectedItem as ProductSaleViewModel;
            int? selectedProductCode = selectedItem?.OriginalProduct?.Код_товара;

            // Очищаем и заполняем коллекцию
            productsView.Clear();
            foreach (var product in products)
            {
                productsView.Add(new ProductSaleViewModel(product));
            }

            // Обновляем ItemsSource
            if (ListViewProducts.ItemsSource != productsView)
            {
                ListViewProducts.ItemsSource = productsView;
            }
            else
            {
                ListViewProducts.Items.Refresh();
            }

            // Восстанавливаем выделение если элемент все еще существует
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

            // Поиск по тексту
            if (!string.IsNullOrWhiteSpace(TxtProductSearch.Text))
            {
                var term = TxtProductSearch.Text.Trim().ToLower();
                query = query.Where(p => p.Наименование.ToLower().Contains(term));
            }

            // Только в наличии
            if (ChkInStock.IsChecked == true)
            {
                query = query.Where(p => p.Количество > 0);
            }

            // Фильтр по цене ОТ
            if (decimal.TryParse(TxtPriceMin.Text, out decimal priceMin))
            {
                query = query.Where(p => p.Цена_за_ед_продажа >= priceMin);
            }

            // Фильтр по цене ДО
            if (decimal.TryParse(TxtPriceMax.Text, out decimal priceMax))
            {
                query = query.Where(p => p.Цена_за_ед_продажа <= priceMax);
            }

            // Фильтр по количеству ОТ
            if (int.TryParse(TxtQtyMin.Text, out int qtyMin))
            {
                query = query.Where(p => p.Количество >= qtyMin);
            }

            // Фильтр по количеству ДО
            if (int.TryParse(TxtQtyMax.Text, out int qtyMax))
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
            // Разрешаем только цифры
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
                        // Ограничиваем значение
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
            // При выборе товара можно обновить слайдер
        }

        private void AddToSaleItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductSaleViewModel productVM)
            {
                var product = productVM.OriginalProduct;

                // Находим родительский Border и элементы управления
                var parentBorder = FindParent<Border>(button);
                if (parentBorder == null) return;

                var textBox = FindChild<TextBox>(parentBorder, "TxtItemQuantity");
                var slider = FindChild<Slider>(parentBorder, "QuantitySlider");

                int quantity = 0;

                // Приоритет отдаем текстовому полю
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

                // Проверяем, есть ли уже этот товар в чеке
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

                // Сбрасываем контролы
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

                Debug.WriteLine($"SaveNewSale_Click: Sale created - Чек №{saleCode}");

                var printResult = MessageBox.Show(
                    $"Продажа оформлена! Чек №{saleCode}\n\nЖелаете распечатать чек?",
                    "Успех",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (printResult == MessageBoxResult.Yes)
                {
                    PrintReceipt(saleCode);
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
            SalesViewGrid.Visibility = Visibility.Visible;
            NewSaleGrid.Visibility = Visibility.Collapsed;
            ViewModeButtons.Visibility = Visibility.Visible;
            NewSaleModeButtons.Visibility = Visibility.Collapsed;
            NewSaleTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = "Управление продажами";

            newSaleItems.Clear();

            RefreshContext();
            // Загружаем ВСЕ продажи без фильтров
            LoadAllSalesWithoutFilters();
            ClearSaleDetails();
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
                    // Загружаем данные чека из базы данных
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

                    // Используем iTextSharp для создания PDF
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

    /// <summary>
    /// ViewModel для отображения продажи в карточке
    /// </summary>
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

    /// <summary>
    /// ViewModel для отображения товара в каталоге при создании продажи
    /// </summary>
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
                    bitmap.Freeze(); // Замораживаем для использования в разных потоках
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