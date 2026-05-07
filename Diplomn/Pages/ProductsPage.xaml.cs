using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Data.Entity;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Diplomn.Pages
{
    public partial class ProductsPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;
        private ObservableCollection<ProductViewModel> productsView;

        public ProductsPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Товары — {user.Фамилия} {user.Имя}";
            productsView = new ObservableCollection<ProductViewModel>();
            LoadLookups();
            LoadData();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private void LoadLookups()
        {
            CmbCategory.ItemsSource = context.Категории.ToList();
            CmbBrand.ItemsSource = context.Бренд.ToList();
            CmbManufacturer.ItemsSource = context.Производитель.ToList();
            CmbMaterial.ItemsSource = context.Материал.ToList();
            CmbPacking.ItemsSource = context.Фасовка.ToList();

            PopulateFilterPanel(PanelCategories, context.Категории.Select(c => new { c.Код_категория, c.Категория }).ToList());
            PopulateFilterPanel(PanelBrands, context.Бренд.Select(b => new { b.Код_бренда, b.Наименование_бредна }).ToList());
            PopulateFilterPanel(PanelManufacturers, context.Производитель.Select(m => new { m.Код_производителя, m.Наименование_произваодителя }).ToList());
            PopulateFilterPanel(PanelMaterials, context.Материал.Select(m => new { m.Код_материала, m.Наименование_материала }).ToList());
            PopulateFilterPanel(PanelPacking, context.Фасовка.Select(f => new { f.Код_фасовки, f.Количество }).ToList());
        }

        private void PopulateFilterPanel(Panel panel, IEnumerable<dynamic> items)
        {
            panel.Children.Clear();
            foreach (var item in items)
            {
                var cb = new CheckBox
                {
                    Margin = new Thickness(2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Content = item.GetType().GetProperties()[1].GetValue(item)?.ToString(),
                    Tag = item.GetType().GetProperties()[0].GetValue(item)
                };
                panel.Children.Add(cb);
            }
        }

        private void LoadData()
        {
            var products = context.Товары
                .Include("Категории")
                .Include("Бренд")
                .Include("Производитель")
                .Include("Материал")
                .Include("Фасовка")
                .ToList();


            UpdateProductsView(products);
        }

        private void UpdateProductsView(List<Товары> products)
        {
            productsView.Clear();
            foreach (var product in products)
            {
                productsView.Add(new ProductViewModel(product));
            }
            ListViewProducts.ItemsSource = productsView;
        }

        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return null;

            try
            {
                using (var ms = new MemoryStream(imageData))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = ms;
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    return bitmap;
                }
            }
            catch
            {
                return null;
            }
        }

        private void ListViewProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewProducts.SelectedItem is ProductViewModel selectedProduct)
            {
                var product = selectedProduct.OriginalProduct;
                TxtProductId.Text = product.Код_товара.ToString();
                TxtProductName.Text = product.Наименование;
                TxtPrice.Text = product.Цена_за_ед_продажа.ToString();
                CmbCategory.SelectedValue = product.Код_категория;
                CmbBrand.SelectedValue = product.Код_бренда;
                CmbManufacturer.SelectedValue = product.Код_производителя;
                CmbMaterial.SelectedValue = product.Код_материала;
                CmbPacking.SelectedValue = product.Код_фасовки;
                TxtQuantity.Text = product.Количество.ToString();
                LoadProductPhoto(product);
                selectedImageData = null;
            }
        }

        private void LoadProductPhoto(Товары product)
        {
            try
            {
                if (product?.Фото != null && product.Фото.Length > 0)
                {
                    ProductPhoto.Source = LoadImageFromBytes(product.Фото);
                }
                else
                {
                    ProductPhoto.Source = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
                }
            }
            catch
            {
                ProductPhoto.Source = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            }
        }

        private void SelectPhoto_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                    Title = "Выберите фотографию товара"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedImageData = File.ReadAllBytes(openFileDialog.FileName);
                    ProductPhoto.Source = LoadImageFromBytes(selectedImageData);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе фото: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateProduct(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            string productName = TxtProductName.Text?.Trim();

            if (string.IsNullOrWhiteSpace(productName))
                errors.AppendLine("• Введите наименование товара");

            if (!decimal.TryParse(TxtPrice.Text, out decimal price))
                errors.AppendLine("• Введите корректную цену");
            else if (price < 0)
                errors.AppendLine("• Цена не может быть отрицательной");

            if (CmbCategory.SelectedValue == null)
                errors.AppendLine("• Выберите категорию");

            if (CmbBrand.SelectedValue == null)
                errors.AppendLine("• Выберите бренд");

            if (CmbManufacturer.SelectedValue == null)
                errors.AppendLine("• Выберите производителя");

            if (CmbMaterial.SelectedValue == null)
                errors.AppendLine("• Выберите материал");

            if (CmbPacking.SelectedValue == null)
                errors.AppendLine("• Выберите фасовку");

            if (!string.IsNullOrWhiteSpace(productName))
            {
                bool exists = excludeId.HasValue
                    ? context.Товары.Any(p => p.Наименование == productName && p.Код_товара != excludeId.Value)
                    : context.Товары.Any(p => p.Наименование == productName);

                if (exists)
                    errors.AppendLine("• Товар с таким наименованием уже существует");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        private List<int> GetCheckedIdsFromPanel(Panel panel)
        {
            var ids = new List<int>();
            foreach (var child in panel.Children)
            {
                if (child is CheckBox cb && cb.IsChecked == true && cb.Tag != null)
                {
                    if (int.TryParse(cb.Tag.ToString(), out int id))
                        ids.Add(id);
                }
            }
            return ids;
        }

        private IQueryable<Товары> GetFilteredQuery()
        {
            var query = context.Товары.AsQueryable();

            // Поиск по наименованию
            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                query = query.Where(p => p.Наименование.Contains(term));
            }

            // Только в наличии
            if (ChkInStock.IsChecked == true)
                query = query.Where(p => p.Количество > 0);

            // Цена
            if (decimal.TryParse(TxtPriceMin.Text, out decimal pmin))
                query = query.Where(p => p.Цена_за_ед_продажа >= pmin);
            if (decimal.TryParse(TxtPriceMax.Text, out decimal pmax))
                query = query.Where(p => p.Цена_за_ед_продажа <= pmax);

            // Количество
            if (int.TryParse(TxtQtyMin.Text, out int qmin))
                query = query.Where(p => p.Количество >= qmin);
            if (int.TryParse(TxtQtyMax.Text, out int qmax))
                query = query.Where(p => p.Количество <= qmax);

            // Категории
            var catIds = GetCheckedIdsFromPanel(PanelCategories);
            if (catIds.Any()) query = query.Where(p => catIds.Contains(p.Код_категория));

            // Бренды
            var brandIds = GetCheckedIdsFromPanel(PanelBrands);
            if (brandIds.Any()) query = query.Where(p => brandIds.Contains(p.Код_бренда));

            // Производители
            var manIds = GetCheckedIdsFromPanel(PanelManufacturers);
            if (manIds.Any()) query = query.Where(p => manIds.Contains(p.Код_производителя));

            // Материалы
            var matIds = GetCheckedIdsFromPanel(PanelMaterials);
            if (matIds.Any()) query = query.Where(p => matIds.Contains(p.Код_материала));

            // Фасовка
            var packIds = GetCheckedIdsFromPanel(PanelPacking);
            if (packIds.Any()) query = query.Where(p => packIds.Contains(p.Код_фасовки));

            return query;
        }

        private void ApplyFilters()
        {
            try
            {
                var products = GetFilteredQuery()
                    .Include("Категории")
                    .Include("Бренд")
                    .Include("Производитель")
                    .Include("Материал")
                    .Include("Фасовка")
                    .ToList();

                UpdateProductsView(products);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при применении фильтров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            TxtPriceMin.Text = "";
            TxtPriceMax.Text = "";
            TxtQtyMin.Text = "";
            TxtQtyMax.Text = "";
            ChkInStock.IsChecked = false;

            foreach (var panel in new Panel[] { PanelCategories, PanelBrands, PanelManufacturers, PanelMaterials, PanelPacking })
            {
                foreach (var child in panel.Children)
                    if (child is CheckBox cb) cb.IsChecked = false;
            }

            LoadData();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var products = GetFilteredQuery()
                    .Include("Категории")
                    .Include("Бренд")
                    .Include("Производитель")
                    .Include("Материал")
                    .Include("Фасовка")
                    .ToList();

                if (!products.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "HTML файл (*.html)|*.html",
                    Title = "Сохранить каталог товаров",
                    FileName = $"Каталог_товаров_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var html = new StringBuilder();

                    // HTML заголовок
                    html.AppendLine("<!DOCTYPE html>");
                    html.AppendLine("<html lang='ru'>");
                    html.AppendLine("<head>");
                    html.AppendLine("<meta charset='UTF-8'>");
                    html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
                    html.AppendLine($"<title>Каталог товаров - {DateTime.Now:dd.MM.yyyy}</title>");
                    html.AppendLine("<style>");
                    html.AppendLine(@"
                * { margin: 0; padding: 0; box-sizing: border-box; }
                body { 
                    font-family: 'Times New Roman', serif; 
                    background: #1a1a1a; 
                    color: #e0e0e0; 
                    padding: 20px; 
                }
                .header { 
                    text-align: center; 
                    margin-bottom: 30px; 
                    padding: 20px;
                    background: #2d2d2d;
                    border-radius: 8px;
                    border: 1px solid #404040;
                }
                .header h1 { 
                    color: #2196F3; 
                    margin-bottom: 10px; 
                    font-size: 24px; 
                }
                .header p { 
                    color: #999; 
                    font-size: 14px; 
                }
                .filters {
                    background: #2d2d2d;
                    padding: 15px;
                    border-radius: 8px;
                    margin-bottom: 20px;
                    border: 1px solid #404040;
                }
                .filters h3 { color: #2196F3; margin-bottom: 10px; font-size: 16px; }
                .filters ul { list-style: none; }
                .filters li { 
                    color: #999; 
                    padding: 3px 0; 
                    font-size: 13px; 
                }
                .filters li:before { 
                    content: '• '; 
                    color: #2196F3; 
                }
                .stats {
                    display: flex;
                    justify-content: space-around;
                    background: #2d2d2d;
                    padding: 15px;
                    border-radius: 8px;
                    margin-bottom: 20px;
                    border: 1px solid #404040;
                }
                .stat-item { text-align: center; }
                .stat-value { 
                    color: #2196F3; 
                    font-size: 24px; 
                    font-weight: bold; 
                }
                .stat-label { 
                    color: #999; 
                    font-size: 12px; 
                    margin-top: 5px; 
                }
                .catalog {
                    display: grid;
                    grid-template-columns: repeat(auto-fill, minmax(250px, 1fr));
                    gap: 20px;
                }
                .product-card {
                    background: #2d2d2d;
                    border: 1px solid #404040;
                    border-radius: 8px;
                    padding: 15px;
                    transition: transform 0.2s, border-color 0.2s;
                }
                .product-card:hover {
                    transform: translateY(-3px);
                    border-color: #2196F3;
                }
                .product-photo {
                    width: 200px;
                    height: 200px;
                    margin: 0 auto 15px;
                    border-radius: 8px;
                    overflow: hidden;
                    border: 2px solid #404040;
                    background: #1a1a1a;
                    display: flex;
                    align-items: center;
                    justify-content: center;
                }
                .product-photo img {
                    width: 100%;
                    height: 100%;
                    object-fit: cover;
                }
                .product-photo .no-photo {
                    color: #666;
                    font-size: 14px;
                    text-align: center;
                }
                .product-name {
                    font-size: 16px;
                    font-weight: bold;
                    color: #fff;
                    margin-bottom: 10px;
                    min-height: 40px;
                    display: -webkit-box;
                    -webkit-line-clamp: 2;
                    -webkit-box-orient: vertical;
                    overflow: hidden;
                }
                .product-price {
                    color: #2196F3;
                    font-size: 18px;
                    font-weight: bold;
                    margin-bottom: 8px;
                }
                .product-info {
                    color: #999;
                    font-size: 13px;
                    margin-bottom: 5px;
                }
                .product-category {
                    color: #666;
                    font-size: 11px;
                    margin-top: 10px;
                    padding-top: 10px;
                    border-top: 1px solid #404040;
                }
                .in-stock {
                    color: #4CAF50;
                    font-weight: bold;
                }
                .out-of-stock {
                    color: #F44336;
                    font-weight: bold;
                }
                .footer {
                    text-align: center;
                    margin-top: 30px;
                    padding: 20px;
                    color: #666;
                    font-size: 12px;
                    border-top: 1px solid #404040;
                }
            ");
                    html.AppendLine("</style>");
                    html.AppendLine("</head>");
                    html.AppendLine("<body>");

                    // Заголовок
                    html.AppendLine("<div class='header'>");
                    html.AppendLine($"<h1>📦 Каталог товаров</h1>");
                    html.AppendLine($"<p>Сформирован: {DateTime.Now:dd.MM.yyyy HH:mm} | Сотрудник: {currentUser.Фамилия} {currentUser.Имя}</p>");
                    html.AppendLine("</div>");

                    // Фильтры
                    var filters = new List<string>();
                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                        filters.Add($"Поиск: \"{TxtSearch.Text}\"");
                    if (ChkInStock.IsChecked == true)
                        filters.Add("Только в наличии");
                    if (decimal.TryParse(TxtPriceMin.Text, out decimal pmin))
                        filters.Add($"Цена от: {pmin:N2} ₽");
                    if (decimal.TryParse(TxtPriceMax.Text, out decimal pmax))
                        filters.Add($"Цена до: {pmax:N2} ₽");
                    if (int.TryParse(TxtQtyMin.Text, out int qmin))
                        filters.Add($"Количество от: {qmin}");
                    if (int.TryParse(TxtQtyMax.Text, out int qmax))
                        filters.Add($"Количество до: {qmax}");

                    if (filters.Any())
                    {
                        html.AppendLine("<div class='filters'>");
                        html.AppendLine("<h3>🔍 Примененные фильтры</h3>");
                        html.AppendLine("<ul>");
                        filters.ForEach(f => html.AppendLine($"<li>{f}</li>"));
                        html.AppendLine("</ul>");
                        html.AppendLine("</div>");
                    }

                    // Статистика
                    var totalSum = products.Sum(p => p.Цена_за_ед_продажа * p.Количество);
                    var inStock = products.Count(p => p.Количество > 0);
                    var outOfStock = products.Count - inStock;

                    html.AppendLine("<div class='stats'>");
                    html.AppendLine("<div class='stat-item'>");
                    html.AppendLine($"<div class='stat-value'>{products.Count}</div>");
                    html.AppendLine("<div class='stat-label'>Всего товаров</div>");
                    html.AppendLine("</div>");
                    html.AppendLine("<div class='stat-item'>");
                    html.AppendLine($"<div class='stat-value' style='color: #4CAF50;'>{inStock}</div>");
                    html.AppendLine("<div class='stat-label'>В наличии</div>");
                    html.AppendLine("</div>");
                    html.AppendLine("<div class='stat-item'>");
                    html.AppendLine($"<div class='stat-value' style='color: #F44336;'>{outOfStock}</div>");
                    html.AppendLine("<div class='stat-label'>Нет в наличии</div>");
                    html.AppendLine("</div>");
                    html.AppendLine("<div class='stat-item'>");
                    html.AppendLine($"<div class='stat-value'>{totalSum:N0} ₽</div>");
                    html.AppendLine("<div class='stat-label'>Общая сумма</div>");
                    html.AppendLine("</div>");
                    html.AppendLine("</div>");

                    // Карточки товаров
                    html.AppendLine("<div class='catalog'>");

                    foreach (var product in products)
                    {
                        var category = product.Категории?.Категория ?? "Без категории";
                        var brand = product.Бренд?.Наименование_бредна ?? "";
                        var manufacturer = product.Производитель?.Наименование_произваодителя ?? "";
                        var stockStatus = product.Количество > 0 ?
                            "<span class='in-stock'>✅ В наличии</span>" :
                            "<span class='out-of-stock'>❌ Нет в наличии</span>";
                        var sum = product.Цена_за_ед_продажа * product.Количество;

                        html.AppendLine("<div class='product-card'>");

                        // Фото
                        html.AppendLine("<div class='product-photo'>");
                        if (product.Фото != null && product.Фото.Length > 0)
                        {
                            var base64Image = Convert.ToBase64String(product.Фото);
                            var imageType = "image/jpeg"; // По умолчанию JPEG

                            // Определяем тип изображения по сигнатуре
                            if (product.Фото.Length > 4)
                            {
                                if (product.Фото[0] == 0x89 && product.Фото[1] == 0x50) // PNG
                                    imageType = "image/png";
                                else if (product.Фото[0] == 0x47 && product.Фото[1] == 0x49) // GIF
                                    imageType = "image/gif";
                                else if (product.Фото[0] == 0x42 && product.Фото[1] == 0x4D) // BMP
                                    imageType = "image/bmp";
                            }

                            html.AppendLine($"<img src='data:{imageType};base64,{base64Image}' alt='{product.Наименование}'>");
                        }
                        else
                        {
                            html.AppendLine("<div class='no-photo'>📷<br>Нет фото</div>");
                        }
                        html.AppendLine("</div>");

                        // Название
                        html.AppendLine($"<div class='product-name' title='{product.Наименование}'>{product.Наименование}</div>");

                        // Цена
                        html.AppendLine($"<div class='product-price'>💰 {product.Цена_за_ед_продажа:N2} ₽ / шт</div>");

                        // Информация
                        html.AppendLine($"<div class='product-info'>📦 На складе: {product.Количество} шт.</div>");
                        html.AppendLine($"<div class='product-info'>💵 Сумма: {sum:N2} ₽</div>");
                        if (!string.IsNullOrWhiteSpace(brand))
                            html.AppendLine($"<div class='product-info'>🏷 Бренд: {brand}</div>");
                        if (!string.IsNullOrWhiteSpace(manufacturer))
                            html.AppendLine($"<div class='product-info'>🏭 Производитель: {manufacturer}</div>");

                        // Статус
                        html.AppendLine($"<div class='product-info'>{stockStatus}</div>");

                        // Категория
                        html.AppendLine($"<div class='product-category'>📂 {category}</div>");

                        html.AppendLine("</div>");
                    }

                    html.AppendLine("</div>");

                    // Футер
                    html.AppendLine("<div class='footer'>");
                    html.AppendLine($"© Каталог товаров | Сформирован: {DateTime.Now:dd.MM.yyyy HH:mm} | Сотрудник: {currentUser.Фамилия} {currentUser.Имя}");
                    html.AppendLine("</div>");

                    html.AppendLine("</body>");
                    html.AppendLine("</html>");

                    File.WriteAllText(saveFileDialog.FileName, html.ToString(), Encoding.UTF8);

                    var result = MessageBox.Show(
                        $"Каталог товаров успешно сохранен!\n\n" +
                        $"📁 Файл: {saveFileDialog.FileName}\n" +
                        $"📦 Всего товаров: {products.Count}\n" +
                        $"✅ В наличии: {inStock}\n" +
                        $"❌ Нет в наличии: {outOfStock}\n\n" +
                        $"Открыть каталог в браузере?",
                        "Каталог сохранен",
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
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении каталога: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateProduct(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var product = new Товары
                {
                    Наименование = TxtProductName.Text?.Trim(),
                    Цена_за_ед_продажа = decimal.Parse(TxtPrice.Text),
                    Код_категория = (int)CmbCategory.SelectedValue,
                    Код_бренда = (int)CmbBrand.SelectedValue,
                    Код_производителя = (int)CmbManufacturer.SelectedValue,
                    Код_материала = (int)CmbMaterial.SelectedValue,
                    Код_фасовки = (int)CmbPacking.SelectedValue,
                    Количество = 0,
                    Фото = selectedImageData
                };

                context.Товары.Add(product);
                context.SaveChanges();

                MessageBox.Show("Товар успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtProductId.Text))
                {
                    MessageBox.Show("Выберите товар для обновления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int productId = int.Parse(TxtProductId.Text);
                var product = context.Товары.Find(productId);

                if (product == null)
                {
                    MessageBox.Show("Товар не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateProduct(out string errorMessage, productId))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                product.Наименование = TxtProductName.Text?.Trim();
                product.Цена_за_ед_продажа = decimal.Parse(TxtPrice.Text);
                product.Код_категория = (int)CmbCategory.SelectedValue;
                product.Код_бренда = (int)CmbBrand.SelectedValue;
                product.Код_производителя = (int)CmbManufacturer.SelectedValue;
                product.Код_материала = (int)CmbMaterial.SelectedValue;
                product.Код_фасовки = (int)CmbPacking.SelectedValue;
                product.Количество = int.Parse(TxtQuantity.Text);

                if (selectedImageData != null)
                    product.Фото = selectedImageData;

                context.SaveChanges();

                MessageBox.Show("Товар успешно обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtProductId.Text))
                {
                    MessageBox.Show("Выберите товар для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int productId = int.Parse(TxtProductId.Text);
                var product = context.Товары.Find(productId);

                if (product == null)
                {
                    MessageBox.Show("Товар не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Состав_продажи.Any(s => s.Код_товара == productId) ||
                    context.Состав_поставки.Any(o => o.Код_товара == productId))
                {
                    MessageBox.Show("Нельзя удалить товар — он используется в продажах или поставках!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить товар \"{product.Наименование}\"?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Товары.Remove(product);
                    context.SaveChanges();
                    MessageBox.Show("Товар удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            TxtProductId.Text = "";
            TxtProductName.Text = "";
            TxtPrice.Text = "";
            CmbCategory.SelectedIndex = -1;
            CmbBrand.SelectedIndex = -1;
            CmbManufacturer.SelectedIndex = -1;
            CmbMaterial.SelectedIndex = -1;
            CmbPacking.SelectedIndex = -1;
            TxtQuantity.Text = "0";
            ProductPhoto.Source = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            selectedImageData = null;
            ListViewProducts.SelectedItem = null;
        }
    }


    /// <summary>
    /// ViewModel для отображения товара в карточке
    /// </summary>
    public class ProductViewModel
    {
        public Товары OriginalProduct { get; set; }
        public string Наименование { get; set; }
        public decimal Цена_за_ед_продажа { get; set; }
        public int Количество { get; set; }
        public string КатегорияНазвание { get; set; }
        public string БрендНазвание { get; set; }
        public string ПроизводительНазвание { get; set; }
        public string МатериалНазвание { get; set; }
        public string ФасовкаНазвание { get; set; }
        public BitmapImage PhotoSource { get; set; }

        // Готовые строки для отображения
        public string PriceDisplay => $"{Цена_за_ед_продажа:N2} ₽";
        public string QuantityDisplay => $"📦 На складе: {Количество} шт.";
        public string StockStatus => Количество > 0 ? "✅ В наличии" : "❌ Нет в наличии";
        

        public ProductViewModel(Товары product)
        {
            OriginalProduct = product;
            Наименование = product.Наименование;
            Цена_за_ед_продажа = product.Цена_за_ед_продажа;
            Количество = product.Количество;
            КатегорияНазвание = product.Категории?.Категория != null ? $"📂 {product.Категории.Категория}" : "📂 Без категории";
            БрендНазвание = product.Бренд?.Наименование_бредна != null ? $"🏷 {product.Бренд.Наименование_бредна}" : "";
            ПроизводительНазвание = product.Производитель?.Наименование_произваодителя != null ? $"🏭 {product.Производитель.Наименование_произваодителя}" : "";
            МатериалНазвание = product.Материал?.Наименование_материала != null ? $"🔧 {product.Материал.Наименование_материала}" : "";
            ФасовкаНазвание = product.Фасовка?.Количество != null ? $"📏 {product.Фасовка.Количество}" : "";

            // Загружаем фото
            if (product.Фото != null && product.Фото.Length > 0)
            {
                try
                {
                    using (var ms = new MemoryStream(product.Фото))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        PhotoSource = bitmap;
                    }
                }
                catch
                {
                    PhotoSource = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
                }
            }
            else
            {
                PhotoSource = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            }
        }
    }
}