using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Data.Entity;
using System.Linq;
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

        public ProductsPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Товары — {user.Фамилия} {user.Имя}";
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
            DataGridProducts.ItemsSource = context.Товары
                .Include("Категории")
                .Include("Бренд")
                .Include("Производитель")
                .Include("Материал")
                .Include("Фасовка")
                .ToList();
        }

        private void DataGridProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridProducts.SelectedItem is Товары product)
            {
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
                    using (var ms = new MemoryStream(product.Фото))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ProductPhoto.Source = bitmap;
                    }
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
                    using (var ms = new MemoryStream(selectedImageData))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ProductPhoto.Source = bitmap;
                    }
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
                var result = GetFilteredQuery()
                    .Include("Категории")
                    .Include("Бренд")
                    .Include("Производитель")
                    .Include("Материал")
                    .Include("Фасовка")
                    .ToList();

                DataGridProducts.ItemsSource = result;
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
                    Filter = "CSV файл (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt",
                    Title = "Сохранить отчет о товарах",
                    FileName = $"Отчет_товары_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();

                    // Заголовок отчета
                    sb.AppendLine($"Отчет о товарах от {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine($"Сформировал: {currentUser.Фамилия} {currentUser.Имя}");

                    // Информация о фильтрах
                    var filters = new List<string>();
                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                        filters.Add($"Поиск: \"{TxtSearch.Text}\"");
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
                        sb.AppendLine("Примененные фильтры:");
                        filters.ForEach(f => sb.AppendLine($"  • {f}"));
                    }

                    sb.AppendLine();
                    sb.AppendLine($"Всего товаров: {products.Count}");
                    sb.AppendLine($"Общая сумма (цена × количество): {products.Sum(p => p.Цена_за_ед_продажа * p.Количество):N2} ₽");
                    sb.AppendLine();

                    // Заголовки CSV
                    sb.AppendLine("Код;Наименование;Категория;Бренд;Производитель;Материал;Фасовка;Цена;Количество;Сумма");

                    // Данные
                    foreach (var product in products)
                    {
                        var category = product.Категории?.Категория ?? "-";
                        var brand = product.Бренд?.Наименование_бредна ?? "-";
                        var manufacturer = product.Производитель?.Наименование_произваодителя ?? "-";
                        var material = product.Материал?.Наименование_материала ?? "-";
                        var packing = product.Фасовка?.Количество.ToString() ?? "-";
                        var sum = product.Цена_за_ед_продажа * product.Количество;

                        sb.AppendLine($"{product.Код_товара};{product.Наименование};{category};{brand};{manufacturer};{material};{packing};{product.Цена_за_ед_продажа:N2};{product.Количество};{sum:N2}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);

                    MessageBox.Show($"Отчет успешно сохранен!\n\nФайл: {saveFileDialog.FileName}\nТоваров: {products.Count}",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}", "Ошибка",
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
            DataGridProducts.SelectedItem = null;
        }
    }
}