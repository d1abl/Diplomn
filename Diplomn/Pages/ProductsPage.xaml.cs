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
using iTextSharp.text;
using iTextSharp.text.pdf;

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

        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;

            var placeholderText = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(placeholderText) && text == placeholderText)
                return string.Empty;

            return text;
        }

        // Измените метод ValidateProduct:
        private bool ValidateProduct(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            string productName = GetActualText(TxtProductName);

            if (string.IsNullOrWhiteSpace(productName))
                errors.AppendLine("• Введите наименование товара");

            if (!decimal.TryParse(GetActualText(TxtPrice), out decimal price))
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

        // Измените метод Add_Click:
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
                    Наименование = GetActualText(TxtProductName),
                    Цена_за_ед_продажа = decimal.Parse(GetActualText(TxtPrice)),
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

        // Измените метод Update_Click:
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

                product.Наименование = GetActualText(TxtProductName);
                product.Цена_за_ед_продажа = decimal.Parse(GetActualText(TxtPrice));
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

        private IQueryable<Товары> GetFilteredQuery()
        {
            var query = context.Товары.AsQueryable();

            // Поиск по наименованию
            string searchText = GetActualText(TxtSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText;
                query = query.Where(p => p.Наименование.Contains(term));
            }

            // Только в наличии
            if (ChkInStock.IsChecked == true)
                query = query.Where(p => p.Количество > 0);

            // Цена
            string priceMinText = GetActualText(TxtPriceMin);
            string priceMaxText = GetActualText(TxtPriceMax);
            if (decimal.TryParse(priceMinText, out decimal pmin))
                query = query.Where(p => p.Цена_за_ед_продажа >= pmin);
            if (decimal.TryParse(priceMaxText, out decimal pmax))
                query = query.Where(p => p.Цена_за_ед_продажа <= pmax);

            // Количество
            string qtyMinText = GetActualText(TxtQtyMin);
            string qtyMaxText = GetActualText(TxtQtyMax);
            if (int.TryParse(qtyMinText, out int qmin))
                query = query.Where(p => p.Количество >= qmin);
            if (int.TryParse(qtyMaxText, out int qmax))
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
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить каталог товаров",
                    FileName = $"Каталог_товаров_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                // Данные магазина
                const string shopName = "Oculus+";
                const string shopPhone = "+7 (461) 345 12-34";
                const string shopEmail = "Oculus@глаза.ру";
                const string shopWebsite = "Oculus.ру";
                const string shopHours = "9:00 – 17:00 ежедневно";

                // Формируем ФИО с инициалами
                string initials = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
                if (!string.IsNullOrWhiteSpace(currentUser.Отчество))
                    initials += $"{currentUser.Отчество?.Substring(0, 1)}.";
                else
                    initials += ".";

                // Статистика
                var inStock = products.Count(p => p.Количество > 0);
                var outOfStock = products.Count - inStock;
                var totalSum = products.Sum(p => p.Цена_за_ед_продажа * p.Количество);

                // Создаём PDF
                using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 50, 50))
                {
                    using (var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, new FileStream(saveFileDialog.FileName, FileMode.Create)))
                    {
                        document.Open();

                        // Шрифты
                        string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                        var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

                        var fontTitle = new iTextSharp.text.Font(baseFont, 16, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
                        var fontSubtitle = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontTableHeader = new iTextSharp.text.Font(baseFont, 8, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE);
                        var fontTableCell = new iTextSharp.text.Font(baseFont, 8, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);
                        var fontFooter = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);
                        var fontSmall = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontSign = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);

                        // === ЗАГОЛОВОК ===
                        var reportTitle = new iTextSharp.text.Paragraph("КАТАЛОГ ТОВАРОВ", fontTitle);
                        reportTitle.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        reportTitle.SpacingAfter = 25;
                        document.Add(reportTitle);

                        // === ТАБЛИЦА ===
                        var table = new iTextSharp.text.pdf.PdfPTable(10);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 6, 18, 8, 9, 9, 9, 9, 9, 9, 10 });
                        table.SpacingBefore = 10;
                        table.SpacingAfter = 25;

                        // Заголовки таблицы
                        var headers = new[] { "Код", "Наименование", "Цена", "Категория", "Бренд", "Произв.", "Материал", "Фасовка", "Кол-во", "Сумма" };
                        foreach (var header in headers)
                        {
                            var headerCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(header, fontTableHeader));
                            headerCell.BackgroundColor = new iTextSharp.text.BaseColor(0, 51, 102);
                            headerCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                            headerCell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
                            headerCell.Padding = 5;
                            table.AddCell(headerCell);
                        }

                        // Данные
                        bool alternate = false;
                        foreach (var product in products)
                        {
                            var category = product.Категории?.Категория ?? "-";
                            var brand = product.Бренд?.Наименование_бредна ?? "-";
                            var manufacturer = product.Производитель?.Наименование_произваодителя ?? "-";
                            var material = product.Материал?.Наименование_материала ?? "-";
                            var packing = product.Фасовка?.Количество.ToString() ?? "-";
                            var sum = product.Цена_за_ед_продажа * product.Количество;

                            var cells = new[]
                            {
                        product.Код_товара.ToString(),
                        product.Наименование,
                        $"{product.Цена_за_ед_продажа:N2}",
                        category,
                        brand,
                        manufacturer,
                        material,
                        packing,
                        product.Количество.ToString(),
                        $"{sum:N2}"
                    };

                            // Колонки, которые выравниваются по центру
                            var centerColumns = new HashSet<int> { 0, 2, 7, 8, 9 }; // Код, Цена, Фасовка, Кол-во, Сумма

                            for (int i = 0; i < cells.Length; i++)
                            {
                                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], fontTableCell));
                                cell.Padding = 4;
                                cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;

                                if (alternate)
                                {
                                    cell.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                                }

                                // Выравнивание по центру для указанных колонок
                                if (centerColumns.Contains(i))
                                {
                                    cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                                }

                                table.AddCell(cell);
                            }

                            alternate = !alternate;
                        }

                        document.Add(table);

                        // === ИТОГО (по левому краю) ===
                        var totalParagraph = new iTextSharp.text.Paragraph();
                        totalParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        totalParagraph.SpacingBefore = 5;
                        totalParagraph.SpacingAfter = 3;

                        var totalInfo = new iTextSharp.text.Chunk($"Всего товаров: {products.Count}", fontSubtitle);
                        totalParagraph.Add(totalInfo);
                        document.Add(totalParagraph);

                        var inStockParagraph = new iTextSharp.text.Paragraph();
                        inStockParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        inStockParagraph.SpacingAfter = 3;
                        inStockParagraph.Add(new iTextSharp.text.Chunk($"В наличии: {inStock}  |  Нет в наличии: {outOfStock}", fontSmall));
                        document.Add(inStockParagraph);

                        var totalSumParagraph = new iTextSharp.text.Paragraph();
                        totalSumParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        totalSumParagraph.SpacingAfter = 35;
                        totalSumParagraph.Add(new iTextSharp.text.Chunk($"Общая сумма товаров: {totalSum:N2} ₽", fontSubtitle));
                        document.Add(totalSumParagraph);

                        // === ПОДПИСЬ (по правому краю) ===
                        var signTable = new iTextSharp.text.pdf.PdfPTable(1);
                        signTable.WidthPercentage = 55;
                        signTable.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;

                        // Строка с должностью, ФИО, линией и датой
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

                        // Строка с надписью "Подпись" — выровнена под линией
                        var signCell2 = new iTextSharp.text.pdf.PdfPCell();
                        signCell2.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell2.HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT;
                        signCell2.PaddingLeft = 145; // Отступ, чтобы "Подпись" была ровно под _______________

                        var signLine = new iTextSharp.text.Paragraph();
                        signLine.Add(new iTextSharp.text.Chunk("(Подпись)", fontSmall));
                        signCell2.AddElement(signLine);

                        signTable.AddCell(signCell2);

                        document.Add(signTable);

                        // === ФУТЕР ===
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

                // Открываем файл
                var result = MessageBox.Show(
                    $"Каталог товаров сохранён!\n\nФайл: {saveFileDialog.FileName}\nВсего товаров: {products.Count}\nВ наличии: {inStock}\nНет в наличии: {outOfStock}\n\nОткрыть PDF?",
                    "Каталог сохранён",
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
                MessageBox.Show($"Ошибка при сохранении каталога: {ex.Message}\n\nУбедитесь, что библиотека iTextSharp установлена.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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