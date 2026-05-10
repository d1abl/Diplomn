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

        public class SupplyItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
            public string PriceQuantityDisplay => $"{Цена:N2} ₽ × {Количество} шт.";
            public BitmapImage PhotoSource { get; set; }
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

            // Отключаем кэширование для получения свежих данных
            context.Configuration.LazyLoadingEnabled = false;
            context.Configuration.ProxyCreationEnabled = false;

            // Подписываемся на событие выгрузки страницы для освобождения ресурсов
            this.Unloaded += SuppliesPage_Unloaded;

            LoadAllSuppliesWithoutFilters(); // Загружаем все поставки при старте
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

        #region Режим просмотра поставок

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private IQueryable<Поставка> GetFilteredQuery()
        {
            var query = context.Поставка
                .Include("Сотрудники")
                .Include("Поставщики")
                .AsQueryable();

            // Поиск по тексту
            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                if (int.TryParse(term, out int supplyCode))
                {
                    query = query.Where(s => s.Код_поставки == supplyCode);
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
                query = query.Where(s => DbFunctions.TruncateTime(s.Дата_оформления_постивки) >= dateFrom);
            }

            // Фильтр по дате ДО
            if (DateTo.SelectedDate.HasValue)
            {
                var dateTo = DateTo.SelectedDate.Value.Date.AddDays(1); // Включаем весь день
                query = query.Where(s => DbFunctions.TruncateTime(s.Дата_оформления_постивки) < dateTo);
            }

            return query.OrderByDescending(s => s.Дата_оформления_постивки);
        }

        private void LoadAllSuppliesWithoutFilters()
        {
            if (isLoading) return;

            try
            {
                isLoading = true;

                var supplies = context.Поставка
                    .Include("Сотрудники")
                    .Include("Поставщики")
                    .AsNoTracking()
                    .OrderByDescending(s => s.Дата_оформления_постивки)
                    .ToList();

                UpdateSuppliesView(supplies);
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

        private void LoadFilteredSupplies()
        {
            if (isLoading) return;

            try
            {
                isLoading = true;

                var supplies = GetFilteredQuery()
                    .AsNoTracking()
                    .ToList();

                UpdateSuppliesView(supplies);
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

        private void UpdateSuppliesView(List<Поставка> supplies)
        {
            if (supplies == null)
            {
                supplies = new List<Поставка>();
            }

            // Сохраняем текущий выбранный элемент
            var selectedItem = ListViewSupplies.SelectedItem as SupplyViewModel;
            int? selectedSupplyCode = selectedItem?.OriginalSupply?.Код_поставки;

            Debug.WriteLine($"UpdateSuppliesView: incoming supplies count = {supplies.Count}");

            // Эффективная загрузка всех сумм одним запросом
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
            else
            {
                totals = new Dictionary<int, decimal>();
            }

            // Очищаем и заполняем коллекцию
            suppliesView.Clear();
            foreach (var supply in supplies)
            {
                var total = totals.TryGetValue(supply.Код_поставки, out var t) ? t : 0;
                suppliesView.Add(new SupplyViewModel(supply, total));
            }

            Debug.WriteLine($"UpdateSuppliesView: final suppliesView count = {suppliesView.Count}");

            // Восстанавливаем выделение если элемент все еще существует
            if (selectedSupplyCode.HasValue)
            {
                var itemToSelect = suppliesView.FirstOrDefault(s => s.OriginalSupply?.Код_поставки == selectedSupplyCode.Value);
                if (itemToSelect != null)
                {
                    ListViewSupplies.SelectedItem = itemToSelect;
                }
                else
                {
                    ClearSupplyDetails();
                }
            }
            else
            {
                ClearSupplyDetails();
            }

            // Обновляем ItemsSource если нужно
            if (ListViewSupplies.ItemsSource != suppliesView)
            {
                ListViewSupplies.ItemsSource = suppliesView;
            }
        }

        private void ApplyFilters()
        {
            LoadFilteredSupplies();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            LoadAllSuppliesWithoutFilters();
            ClearSupplyDetails();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supplies = GetFilteredQuery()
                    .AsNoTracking()
                    .ToList();

                if (!supplies.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV файл (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt",
                    Title = "Сохранить отчет о поставках",
                    FileName = $"Отчет_поставки_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Загружаем суммы одним запросом
                    var supplyIds = supplies.Select(s => s.Код_поставки).ToList();
                    var totals = context.Состав_поставки
                        .Where(i => supplyIds.Contains(i.Код_поставки))
                        .GroupBy(i => i.Код_поставки)
                        .Select(g => new { SupplyId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0 })
                        .ToDictionary(x => x.SupplyId, x => x.Total);

                    var sb = new StringBuilder();
                    sb.AppendLine($"Отчет о поставках от {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine($"Сформировал: {currentUser.Фамилия} {currentUser.Имя}");

                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                        sb.AppendLine($"Поиск: \"{TxtSearch.Text}\"");
                    if (DateFrom.SelectedDate.HasValue)
                        sb.AppendLine($"Дата от: {DateFrom.SelectedDate.Value:dd.MM.yyyy}");
                    if (DateTo.SelectedDate.HasValue)
                        sb.AppendLine($"Дата до: {DateTo.SelectedDate.Value:dd.MM.yyyy}");

                    sb.AppendLine();
                    sb.AppendLine($"Всего поставок: {supplies.Count}");
                    sb.AppendLine();
                    sb.AppendLine("Код поставки;Дата;Сотрудник;Поставщик;Общая сумма");

                    decimal grandTotal = 0;
                    foreach (var supply in supplies)
                    {
                        var total = totals.TryGetValue(supply.Код_поставки, out var t) ? t : 0;
                        grandTotal += total;
                        var employee = $"{supply.Сотрудники?.Фамилия} {supply.Сотрудники?.Имя}".Trim();
                        var supplier = supply.Поставщики?.Наименование_поставщика ?? "Не указан";
                        sb.AppendLine($"{supply.Код_поставки};{supply.Дата_оформления_постивки:dd.MM.yyyy HH:mm};{employee};{supplier};{total:N2}");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"Общая сумма поставок: {grandTotal:N2} ₽");

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

        private void ListViewSupplies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSupplies.SelectedItem is SupplyViewModel selectedSupply)
            {
                // Включаем кнопку удаления
                EnableDeleteButton(true);
                EnableClearButton(true);

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
                    })
                    .ToList();

                ListViewSupplyItems.ItemsSource = items;
                decimal total = items.Sum(i => i.Сумма);
                TxtTotal.Text = $"{total:N2} ₽";
                TotalPanel.Visibility = Visibility.Visible;
            }
            else
            {
                EnableDeleteButton(false);
                EnableClearButton(false);
                ClearSupplyDetails();
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

        private void EnableClearButton(bool enable)
        {
            // Находим кнопку очистки в панели ViewModeButtons
            foreach (var child in ViewModeButtons.Children)
            {
                if (child is Button button && button.Content?.ToString() == "Очистить")
                {
                    button.IsEnabled = enable;
                    break;
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

                var supply = selectedItem.OriginalSupply;

                var result = MessageBox.Show($"Вы уверены, что хотите удалить поставку №{supply.Код_поставки}?\nКоличество товаров будет уменьшено!",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var items = context.Состав_поставки.Where(i => i.Код_поставки == supply.Код_поставки).ToList();
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

                    context.Поставка.Remove(supply);
                    context.SaveChanges();

                    MessageBox.Show("Поставка успешно удалена! Количество товаров обновлено.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    RefreshContext();
                    // После удаления загружаем все поставки без фильтров
                    LoadAllSuppliesWithoutFilters();
                    ClearSupplyDetails();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении поставки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            RefreshContext();
            LoadAllSuppliesWithoutFilters();
            ClearSupplyDetails();
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearSupplyDetails();
        }

        #endregion

        #region Режим создания новой поставки

        private void NewSupply_Click(object sender, RoutedEventArgs e)
        {
            // Переключаемся в режим создания
            SuppliesViewGrid.Visibility = Visibility.Collapsed;
            NewSupplyGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSupplyModeButtons.Visibility = Visibility.Visible;
            NewSupplyTotalPanel.Visibility = Visibility.Visible;
            PageModeText.Text = "Оформление новой поставки";

            newSupplyItems.Clear();
            TxtNewSupplyTotal.Text = "0.00 ₽";
            BtnSaveNewSupply.IsEnabled = false;

            // Загружаем поставщиков
            LoadSuppliers();

            // Очищаем фильтры товаров
            ClearProductFilterFields();

            // Обновляем контекст перед загрузкой товаров
            RefreshContext();

            // Загружаем ВСЕ товары без фильтров при открытии
            LoadAllProducts();
        }

        private void LoadSuppliers()
        {
            try
            {
                var suppliers = context.Поставщики.ToList();
                CmbSupplier.ItemsSource = suppliers;
                if (suppliers.Any())
                {
                    CmbSupplier.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при загрузке поставщиков: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearProductFilterFields()
        {
            TxtProductSearch.Text = "";
            TxtPriceMin.Text = "";
            TxtPriceMax.Text = "";
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

        private void CancelNewSupply_Click(object sender, RoutedEventArgs e)
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
            var selectedItem = ListViewProducts.SelectedItem as ProductSupplyViewModel;
            int? selectedProductCode = selectedItem?.OriginalProduct?.Код_товара;

            // Очищаем и заполняем коллекцию
            productsView.Clear();
            foreach (var product in products)
            {
                productsView.Add(new ProductSupplyViewModel(product));
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
                    var dataContext = parentBorder.DataContext as ProductSupplyViewModel;

                    if (dataContext != null && int.TryParse(textBox.Text, out int value))
                    {
                        // Ограничиваем значение
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

        private void AddToSupplyItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductSupplyViewModel productVM)
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

                // Проверяем, есть ли уже этот товар в поставке
                var existingItem = newSupplyItems.FirstOrDefault(i => i.Товар == product.Наименование);
                if (existingItem != null)
                {
                    existingItem.Количество += quantity;
                }
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

                // Сбрасываем контролы
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
            BtnSaveNewSupply.IsEnabled = newSupplyItems.Any() && CmbSupplier.SelectedItem != null;
        }

        private void SaveNewSupply_Click(object sender, RoutedEventArgs e)
        {
            if (!newSupplyItems.Any())
            {
                MessageBox.Show("Добавьте товары в поставку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (CmbSupplier.SelectedItem == null)
            {
                MessageBox.Show("Выберите поставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var selectedSupplier = CmbSupplier.SelectedItem as Поставщики;

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
                        var supplyComposition = new Состав_поставки
                        {
                            Код_поставки = supplyCode,
                            Код_товара = product.Код_товара,
                            Количество = item.Количество,
                            Цена_за_ед_покупка = item.Цена
                        };
                        context.Состав_поставки.Add(supplyComposition);
                        product.Количество += item.Количество;
                    }
                }

                context.SaveChanges();

                Debug.WriteLine($"SaveNewSupply_Click: Supply created - Поставка №{supplyCode}");

                MessageBox.Show(
                    $"Поставка оформлена! Номер поставки: {supplyCode}\nТовары добавлены на склад.",
                    "Успех",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

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
            SuppliesViewGrid.Visibility = Visibility.Visible;
            NewSupplyGrid.Visibility = Visibility.Collapsed;
            ViewModeButtons.Visibility = Visibility.Visible;
            NewSupplyModeButtons.Visibility = Visibility.Collapsed;
            NewSupplyTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = "Управление поставками";

            newSupplyItems.Clear();

            RefreshContext();
            // Загружаем ВСЕ поставки без фильтров
            LoadAllSuppliesWithoutFilters();
            ClearSupplyDetails();
        }

        #endregion
    }

    /// <summary>
    /// ViewModel для отображения поставки в карточке
    /// </summary>
    public class SupplyViewModel
    {
        public Поставка OriginalSupply { get; set; }
        public decimal Total { get; set; }

        public string SupplyDisplay => $"Поставка №{OriginalSupply.Код_поставки}";
        public string DateDisplay => OriginalSupply.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm");
        public string EmployeeDisplay => OriginalSupply.Сотрудники != null ?
            $"👤 {OriginalSupply.Сотрудники.Фамилия} {OriginalSupply.Сотрудники.Имя}" : "👤 Не указан";
        public string SupplierDisplay => OriginalSupply.Поставщики != null ?
            $"🏭 {OriginalSupply.Поставщики.Наименование_поставщика}" : "🏭 Не указан";
        public string TotalDisplay => $"💰 {Total:N2} ₽";

        public SupplyViewModel(Поставка supply, decimal total)
        {
            OriginalSupply = supply;
            Total = total;
        }
    }

    /// <summary>
    /// ViewModel для отображения товара в каталоге при создании поставки
    /// </summary>
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