using Diplomn.Addons;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница каталога товаров с фильтрацией и управлением
    /// </summary>
    public partial class ProductsPage : Page
    {
        #region Поля

        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;
        private ObservableCollection<ProductViewModel> productsView;
        private AccessManager.AccessRights rights;
        private WrapPanel actionButtonsPanel;

        // Контейнеры для кнопок
        private Grid addButtonContainer;
        private Grid editButtonContainer;
        private Grid deleteButtonContainer;
        private Grid clearButtonContainer;

        #endregion

        #region Конструктор

        public ProductsPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Товары — {user.Фамилия} {user.Имя}";
            productsView = new ObservableCollection<ProductViewModel>();
            rights = AccessManager.GetAccessRights(user.Должность?.Уровень_доступа ?? 10);
            actionButtonsPanel = FindName("ActionButtonsPanel") as WrapPanel;

            CreateActionButtons();
            SubscribeToFieldChanges();

            LoadLookups();
            LoadData();
            UpdateButtonsState();
        }

        #endregion

        #region Подписка на изменения полей

        private void SubscribeToFieldChanges()
        {
            TxtProductName.TextChanged += (s, e) => UpdateButtonsState();
            TxtPrice.TextChanged += (s, e) => UpdateButtonsState();
            CmbCategory.SelectionChanged += (s, e) => UpdateButtonsState();
            CmbBrand.SelectionChanged += (s, e) => UpdateButtonsState();
            CmbManufacturer.SelectionChanged += (s, e) => UpdateButtonsState();
            CmbMaterial.SelectionChanged += (s, e) => UpdateButtonsState();
            CmbPacking.SelectionChanged += (s, e) => UpdateButtonsState();
        }

        #endregion

        #region Создание кнопок

        private void CreateActionButtons()
        {
            if (actionButtonsPanel == null) return;
            actionButtonsPanel.Children.Clear();

            if (rights.Products.CanCreate)
            {
                var (button, overlay) = CreateButtonWithOverlay("➕ Добавить", Add_Click, 110);
                addButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(addButtonContainer);
            }

            if (rights.Products.CanEdit)
            {
                var (button, overlay) = CreateButtonWithOverlay("✏️ Обновить", Update_Click, 110);
                editButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(editButtonContainer);
            }

            if (rights.Products.CanDelete)
            {
                var (button, overlay) = CreateButtonWithOverlay("🗑️ Удалить", Delete_Click, 110);
                deleteButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(deleteButtonContainer);
            }

            var (clearBtn, clearOverlay) = CreateButtonWithOverlay("🔄 Очистить", ClearForm_Click, 110);
            clearButtonContainer = CreateButtonContainer(clearBtn, clearOverlay);
            actionButtonsPanel.Children.Add(clearButtonContainer);
        }

        private Grid CreateButtonContainer(Button button, Border overlay)
        {
            var grid = new Grid
            {
                Margin = new Thickness(3),
                Width = button.Width,
                Height = button.Height
            };

            grid.Children.Add(button);
            grid.Children.Add(overlay);

            return grid;
        }

        private (Button button, Border overlay) CreateButtonWithOverlay(string text, RoutedEventHandler handler, double width = 90)
        {
            var button = new Button
            {
                Content = text,
                Width = width,
                Height = 34,
                IsEnabled = false
            };

            button.Click += handler;

            var overlay = new Border
            {
                Background = Brushes.Transparent,
                IsHitTestVisible = true,
                ToolTip = GetButtonTooltip(text)
            };

            button.IsEnabledChanged += (s, e) =>
            {
                var btn = s as Button;
                if (btn != null)
                {
                    if (btn.IsEnabled)
                    {
                        overlay.Visibility = Visibility.Collapsed;
                        overlay.ToolTip = null;
                    }
                    else
                    {
                        overlay.Visibility = Visibility.Visible;
                        overlay.ToolTip = GetButtonTooltip(btn.Content?.ToString());
                    }
                }
            };

            return (button, overlay);
        }

        private string GetButtonTooltip(string buttonContent)
        {
            if (string.IsNullOrEmpty(buttonContent)) return "";

            if (buttonContent.Contains("Добавить"))
            {
                var missing = GetMissingRequiredFields();
                if (missing.Any())
                    return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                return "Нажмите для добавления товара";
            }

            if (buttonContent.Contains("Обновить"))
            {
                if (ListViewProducts.SelectedItem == null)
                    return "Выберите товар из списка";
                var missing = GetMissingRequiredFields();
                if (missing.Any())
                    return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                return "Нажмите для обновления товара";
            }

            if (buttonContent.Contains("Удалить"))
                return "Выберите товар из списка для удаления";

            if (buttonContent.Contains("Очистить"))
                return "Очистить все поля формы";

            return "Кнопка недоступна";
        }

        #endregion

        #region Валидация полей

        private List<string> GetMissingRequiredFields()
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(GetActualText(TxtProductName)))
                missing.Add("Наименование товара");

            if (!decimal.TryParse(GetActualText(TxtPrice), out decimal price) || price < 0)
                missing.Add("Цена (корректное число)");

            if (CmbCategory.SelectedValue == null)
                missing.Add("Категория");

            if (CmbBrand.SelectedValue == null)
                missing.Add("Бренд");

            if (CmbManufacturer.SelectedValue == null)
                missing.Add("Производитель");

            if (CmbMaterial.SelectedValue == null)
                missing.Add("Материал");

            if (CmbPacking.SelectedValue == null)
                missing.Add("Фасовка");

            return missing;
        }

        private bool AreRequiredFieldsFilled()
        {
            return !GetMissingRequiredFields().Any();
        }

        #endregion

        #region Управление состоянием кнопок

        private void UpdateButtonsState()
        {
            bool isProductSelected = ListViewProducts.SelectedItem != null;
            bool requiredFieldsFilled = AreRequiredFieldsFilled();

            SetButtonState(addButtonContainer, requiredFieldsFilled);
            SetButtonState(editButtonContainer, isProductSelected && requiredFieldsFilled);
            SetButtonState(deleteButtonContainer, isProductSelected);
            SetButtonState(clearButtonContainer, true);
        }

        private void SetButtonState(Grid container, bool isEnabled)
        {
            if (container == null) return;

            var button = container.Children.OfType<Button>().FirstOrDefault();
            if (button != null)
            {
                button.IsEnabled = isEnabled;
            }
        }

        #endregion

        #region Загрузка данных

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
                var props = item.GetType().GetProperties();
                var cb = new CheckBox
                {
                    Margin = new Thickness(2),
                    Content = props[1].GetValue(item)?.ToString(),
                    Tag = props[0].GetValue(item),
                    FontSize = 11,
                    Foreground = TryFindResource("ForegroundBrush") as Brush
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
                productsView.Add(new ProductViewModel(product));

            ListViewProducts.ItemsSource = productsView;
        }

        #endregion

        #region Фильтрация

        private List<int> GetCheckedIdsFromPanel(Panel panel)
        {
            return panel.Children.OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true && cb.Tag != null && int.TryParse(cb.Tag.ToString(), out _))
                .Select(cb => (int)cb.Tag)
                .ToList();
        }

        private IQueryable<Товары> GetFilteredQuery()
        {
            var query = context.Товары.AsQueryable();

            var searchText = GetActualText(TxtSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(p => p.Наименование.Contains(searchText));

            if (ChkInStock.IsChecked == true)
                query = query.Where(p => p.Количество > 0);

            var priceMinText = GetActualText(TxtPriceMin);
            if (decimal.TryParse(priceMinText, out decimal pmin))
                query = query.Where(p => p.Цена_за_ед_продажа >= pmin);

            var priceMaxText = GetActualText(TxtPriceMax);
            if (decimal.TryParse(priceMaxText, out decimal pmax))
                query = query.Where(p => p.Цена_за_ед_продажа <= pmax);

            var qtyMinText = GetActualText(TxtQtyMin);
            if (int.TryParse(qtyMinText, out int qmin))
                query = query.Where(p => p.Количество >= qmin);

            var qtyMaxText = GetActualText(TxtQtyMax);
            if (int.TryParse(qtyMaxText, out int qmax))
                query = query.Where(p => p.Количество <= qmax);

            var catIds = GetCheckedIdsFromPanel(PanelCategories);
            if (catIds.Any()) query = query.Where(p => catIds.Contains(p.Код_категория));

            var brandIds = GetCheckedIdsFromPanel(PanelBrands);
            if (brandIds.Any()) query = query.Where(p => brandIds.Contains(p.Код_бренда));

            var manIds = GetCheckedIdsFromPanel(PanelManufacturers);
            if (manIds.Any()) query = query.Where(p => manIds.Contains(p.Код_производителя));

            var matIds = GetCheckedIdsFromPanel(PanelMaterials);
            if (matIds.Any()) query = query.Where(p => matIds.Contains(p.Код_материала));

            var packIds = GetCheckedIdsFromPanel(PanelPacking);
            if (packIds.Any()) query = query.Where(p => packIds.Contains(p.Код_фасовки));

            return query;
        }

        private void ApplyFilters()
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

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyFilters();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            TxtPriceMin.Text = "";
            TxtPriceMax.Text = "";
            TxtQtyMin.Text = "";
            TxtQtyMax.Text = "";
            ChkInStock.IsChecked = false;

            foreach (var panel in new[] { PanelCategories, PanelBrands, PanelManufacturers, PanelMaterials, PanelPacking })
                foreach (var child in panel.Children)
                    if (child is CheckBox cb) cb.IsChecked = false;

            LoadData();
        }

        #endregion

        #region Отчёт в PDF

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

                if (saveFileDialog.ShowDialog() != true) return;

                const string shopName = "Oculus+";
                const string shopPhone = "+7 (461) 345 12-34";
                const string shopEmail = "Oculus@глаза.ру";
                const string shopWebsite = "Oculus.ру";
                const string shopHours = "9:00 – 17:00 ежедневно";

                var initials = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
                if (!string.IsNullOrWhiteSpace(currentUser.Отчество))
                    initials += $"{currentUser.Отчество?.Substring(0, 1)}.";

                var inStock = products.Count(p => p.Количество > 0);
                var outOfStock = products.Count - inStock;
                var totalSum = products.Sum(p => p.Цена_за_ед_продажа * p.Количество);

                using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 50, 50))
                {
                    using (var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, new FileStream(saveFileDialog.FileName, FileMode.Create)))
                    {
                        document.Open();

                        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                        var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

                        var fontTitle = new iTextSharp.text.Font(baseFont, 16, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
                        var fontSubtitle = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontTableHeader = new iTextSharp.text.Font(baseFont, 8, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE);
                        var fontTableCell = new iTextSharp.text.Font(baseFont, 8, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);
                        var fontFooter = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);
                        var fontSmall = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontSign = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);

                        var reportTitle = new iTextSharp.text.Paragraph("КАТАЛОГ ТОВАРОВ", fontTitle);
                        reportTitle.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        reportTitle.SpacingAfter = 25;
                        document.Add(reportTitle);

                        var table = new iTextSharp.text.pdf.PdfPTable(10);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 6, 18, 8, 9, 9, 9, 9, 9, 9, 10 });
                        table.SpacingBefore = 10;
                        table.SpacingAfter = 25;

                        var headers = new[] { "Код", "Наименование", "Цена", "Категория", "Бренд", "Произв.", "Материал", "Фасовка", "Кол-во", "Сумма" };
                        foreach (var header in headers)
                        {
                            var headerCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(header, fontTableHeader));
                            headerCell.BackgroundColor = new iTextSharp.text.BaseColor(0, 51, 102);
                            headerCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                            headerCell.Padding = 4;
                            table.AddCell(headerCell);
                        }

                        bool alternate = false;
                        var centerColumns = new HashSet<int> { 0, 2, 7, 8, 9 };

                        foreach (var product in products)
                        {
                            var cells = new[]
                            {
                                product.Код_товара.ToString(),
                                product.Наименование,
                                $"{product.Цена_за_ед_продажа:N2}",
                                product.Категории?.Категория ?? "-",
                                product.Бренд?.Наименование_бредна ?? "-",
                                product.Производитель?.Наименование_произваодителя ?? "-",
                                product.Материал?.Наименование_материала ?? "-",
                                product.Фасовка?.Количество.ToString() ?? "-",
                                product.Количество.ToString(),
                                $"{(product.Цена_за_ед_продажа * product.Количество):N2}"
                            };

                            for (int i = 0; i < cells.Length; i++)
                            {
                                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], fontTableCell));
                                cell.Padding = 3;
                                if (alternate) cell.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                                if (centerColumns.Contains(i)) cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                                table.AddCell(cell);
                            }
                            alternate = !alternate;
                        }

                        document.Add(table);

                        var tp = new iTextSharp.text.Paragraph($"Всего товаров: {products.Count}", fontSubtitle);
                        tp.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        tp.SpacingAfter = 3;
                        document.Add(tp);

                        var sp = new iTextSharp.text.Paragraph($"В наличии: {inStock}  |  Нет в наличии: {outOfStock}", fontSmall);
                        sp.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        sp.SpacingAfter = 3;
                        document.Add(sp);

                        var sump = new iTextSharp.text.Paragraph($"Общая сумма товаров: {totalSum:N2} ₽", fontSubtitle);
                        sump.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        sump.SpacingAfter = 35;
                        document.Add(sump);

                        var signTable = new iTextSharp.text.pdf.PdfPTable(1);
                        signTable.WidthPercentage = 55;
                        signTable.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;

                        var sc1 = new iTextSharp.text.pdf.PdfPCell();
                        sc1.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        sc1.PaddingBottom = 3;
                        sc1.AddElement(new iTextSharp.text.Paragraph(
                            $"{currentUser.Должность?.Название ?? "Сотрудник"} {initials} _______________  {DateTime.Now:dd.MM.yyyy}", fontSign));
                        signTable.AddCell(sc1);

                        var sc2 = new iTextSharp.text.pdf.PdfPCell();
                        sc2.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        sc2.PaddingLeft = 145;
                        sc2.AddElement(new iTextSharp.text.Paragraph("(Подпись)", fontSmall));
                        signTable.AddCell(sc2);
                        document.Add(signTable);

                        var fl = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.LIGHT_GRAY, iTextSharp.text.Element.ALIGN_CENTER, 0);
                        var flp = new iTextSharp.text.Paragraph();
                        flp.SpacingBefore = 40;
                        flp.Add(fl);
                        document.Add(flp);

                        var fl1 = new iTextSharp.text.Paragraph($"{shopName}  |  Часы работы: {shopHours}", fontFooter);
                        fl1.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        fl1.SpacingBefore = 8;
                        fl1.SpacingAfter = 2;
                        document.Add(fl1);

                        var fl2 = new iTextSharp.text.Paragraph($"{shopPhone}  |  {shopEmail}  |  {shopWebsite}", fontFooter);
                        fl2.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        document.Add(fl2);

                        document.Close();
                    }
                }

                var result = MessageBox.Show($"Каталог товаров сохранён!\n\n{saveFileDialog.FileName}\nВсего: {products.Count}\nВ наличии: {inStock}\n\nОткрыть PDF?",
                    "Каталог сохранён", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = saveFileDialog.FileName, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении каталога: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Выбор товара и фото

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

            UpdateButtonsState();
        }

        private void LoadProductPhoto(Товары product)
        {
            try
            {
                ProductPhoto.Source = (product?.Фото != null && product.Фото.Length > 0)
                    ? LoadImageFromBytes(product.Фото)
                    : new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
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
                MessageBox.Show($"Ошибка при выборе фото: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Валидация

        private bool ValidateProduct(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            var name = GetActualText(TxtProductName);
            var priceText = GetActualText(TxtPrice);

            if (string.IsNullOrWhiteSpace(name))
                errors.AppendLine("• Введите наименование товара");

            if (!decimal.TryParse(priceText, out decimal price))
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

            if (!string.IsNullOrWhiteSpace(name))
            {
                var exists = excludeId.HasValue
                    ? context.Товары.Any(p => p.Наименование == name && p.Код_товара != excludeId.Value)
                    : context.Товары.Any(p => p.Наименование == name);

                if (exists)
                    errors.AppendLine("• Товар с таким наименованием уже существует");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        #endregion

        #region CRUD операции

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateProduct(out var error))
                {
                    MessageBox.Show(error, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                MessageBox.Show("Товар добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtProductId.Text))
                {
                    MessageBox.Show("Выберите товар!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var productId = int.Parse(TxtProductId.Text);
                var product = context.Товары.Find(productId);

                if (product == null)
                {
                    MessageBox.Show("Товар не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateProduct(out var error, productId))
                {
                    MessageBox.Show(error, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                MessageBox.Show("Товар обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtProductId.Text))
                {
                    MessageBox.Show("Выберите товар!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var productId = int.Parse(TxtProductId.Text);
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

                var result = MessageBox.Show($"Удалить товар «{product.Наименование}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Товары.Remove(product);
                    context.SaveChanges();
                    MessageBox.Show("Товар удалён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Очистка формы

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

            UpdateButtonsState();
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

        #endregion

        #region Вспомогательные методы

        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0) return null;
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
            catch { return null; }
        }

        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;
            var placeholder = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;
            return (!string.IsNullOrEmpty(placeholder) && text == placeholder) ? string.Empty : text;
        }

        #endregion
    }

    /// <summary>
    /// ViewModel для отображения товара в карточке каталога
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

            PhotoSource = (product.Фото != null && product.Фото.Length > 0)
                ? LoadImageFromBytes(product.Фото)
                : new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
        }

        private static BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
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
                return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            }
        }
    }
}