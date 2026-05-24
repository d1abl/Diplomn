using Diplomn.Addons;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления продажами магазина
    /// </summary>
    public partial class SalesPage : Page
    {
        #region Поля

        private BDEntities context;
        private Сотрудники currentUser;
        private ObservableCollection<SaleViewModel> salesView;
        private ObservableCollection<ProductSaleViewModel> productsView;
        private ObservableCollection<SaleItemDisplay> newSaleItems;
        private AccessManager.AccessRights rights;
        private int? editingSaleCode = null;
        private const int MaxEditHours = 24;

        #region Поля Кнопок

        // Контейнеры для кнопок в режиме просмотра
        private Grid btnNewSaleContainer;
        private Grid btnEditSaleContainer;
        private Grid btnDeleteSaleContainer;
        private Grid btnPrintSaleContainer;
        private Grid btnClearContainer;

        // Контейнеры для кнопок в режиме создания/редактирования
        private Grid btnSaveNewSaleContainer;
        private Grid btnCancelNewSaleContainer;

        #endregion

        #region Поля сортировки

        private enum SortMode { None, DateAsc, DateDesc, AmountAsc, AmountDesc }
        private SortMode currentDateSort = SortMode.DateDesc; // По умолчанию - новые сначала
        private SortMode currentAmountSort = SortMode.None;

        #endregion

        #endregion

        #region Вспомогательные классы

        public class SaleItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public int OriginalQuantity { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
            public string PriceQuantityDisplay => $"{Цена:N2} ₽ × {Количество} шт.";
            public BitmapImage PhotoSource { get; set; }
        }

        #endregion

        #region Конструктор

        public SalesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Продажи — {user.Фамилия} {user.Имя}";
            salesView = new ObservableCollection<SaleViewModel>();
            productsView = new ObservableCollection<ProductSaleViewModel>();
            newSaleItems = new ObservableCollection<SaleItemDisplay>();

            rights = AccessManager.GetAccessRights(user.Должность?.Уровень_доступа ?? 10);

            ListViewSales.ItemsSource = salesView;
            ListViewNewSaleItems.ItemsSource = newSaleItems;

            CreateViewModeButtons();
            CreateNewSaleModeButtons();
            LoadEmployees();

            this.Loaded += (s, e) =>
            {
                LoadAllSales();
                LoadGrandTotal();
            };
        }

        #endregion

        #region Создание кнопок с overlay

        private void CreateViewModeButtons()
        {
            ViewModeButtons.Children.Clear();

            // Кнопка "Новая продажа"
            if (rights.Sales.CanCreate)
            {
                var (newBtn, newOverlay) = CreateButtonWithOverlay("➕ Новая продажа", NewSale_Click, 140);
                newBtn.IsEnabled = true;
                newOverlay.Visibility = Visibility.Collapsed;
                btnNewSaleContainer = CreateButtonContainer(newBtn, newOverlay);
                ViewModeButtons.Children.Add(btnNewSaleContainer);
            }

            // Кнопка "Редактировать"
            if (rights.Sales.CanEdit)
            {
                var (editBtn, editOverlay) = CreateButtonWithOverlay("✏ Редактировать", EditSale_Click, 120);
                btnEditSaleContainer = CreateButtonContainer(editBtn, editOverlay);
                ViewModeButtons.Children.Add(btnEditSaleContainer);
            }

            // Кнопка "Удалить"
            if (rights.Sales.CanDelete)
            {
                var (delBtn, delOverlay) = CreateButtonWithOverlay("🗑 Удалить", DeleteSale_Click, 100);
                btnDeleteSaleContainer = CreateButtonContainer(delBtn, delOverlay);
                ViewModeButtons.Children.Add(btnDeleteSaleContainer);
            }

            // Кнопка "Печать чека"
            var (printBtn, printOverlay) = CreateButtonWithOverlay("🖨 Печать чека", PrintSale_Click, 120);
            btnPrintSaleContainer = CreateButtonContainer(printBtn, printOverlay);
            ViewModeButtons.Children.Add(btnPrintSaleContainer);

            // Кнопка "Очистить"
            var (clearBtn, clearOverlay) = CreateButtonWithOverlay("🔄 Очистить", ClearForm_Click, 100);
            clearBtn.IsEnabled = true;
            clearOverlay.Visibility = Visibility.Collapsed;
            btnClearContainer = CreateButtonContainer(clearBtn, clearOverlay);
            ViewModeButtons.Children.Add(btnClearContainer);

            UpdateViewModeButtonsState();
        }

        private void CreateNewSaleModeButtons()
        {
            NewSaleModeButtons.Children.Clear();

            // Кнопка "Оформить продажу"
            var (saveBtn, saveOverlay) = CreateButtonWithOverlay("💾 Оформить продажу", SaveNewSale_Click, 150);
            btnSaveNewSaleContainer = CreateButtonContainer(saveBtn, saveOverlay);
            NewSaleModeButtons.Children.Add(btnSaveNewSaleContainer);

            // Кнопка "Отмена"
            var (cancelBtn, cancelOverlay) = CreateButtonWithOverlay("↩ Отмена", CancelNewSale_Click, 100);
            cancelBtn.IsEnabled = true;
            cancelOverlay.Visibility = Visibility.Collapsed;
            btnCancelNewSaleContainer = CreateButtonContainer(cancelBtn, cancelOverlay);
            NewSaleModeButtons.Children.Add(btnCancelNewSaleContainer);
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
            Grid.SetZIndex(button, 0);
            Grid.SetZIndex(overlay, 1);

            return grid;
        }

        private (Button button, Border overlay) CreateButtonWithOverlay(string text, RoutedEventHandler handler, double width = 90)
        {
            var button = new Button
            {
                Content = text,
                Width = width,
                Height = 35,
                IsEnabled = false,
                Cursor = Cursors.Hand
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

            if (buttonContent.Contains("Новая продажа"))
                return "Создать новую продажу";

            if (buttonContent.Contains("Редактировать"))
                return "Выберите чек для редактирования";

            if (buttonContent.Contains("Удалить"))
                return "Выберите чек для удаления";

            if (buttonContent.Contains("Печать чека"))
                return "Выберите чек для печати";

            if (buttonContent.Contains("Очистить"))
                return "Очистить детали чека";

            if (buttonContent.Contains("Оформить продажу"))
            {
                if (!newSaleItems.Any())
                    return "Добавьте товары в чек";
                return "Нажмите для оформления продажи";
            }

            if (buttonContent.Contains("Отмена"))
                return "Вернуться к списку продаж";

            return "Кнопка недоступна";
        }

        private void SetButtonState(Grid container, bool isEnabled)
        {
            if (container == null) return;
            var button = container.Children.OfType<Button>().FirstOrDefault();
            if (button != null) button.IsEnabled = isEnabled;
        }

        private void UpdateViewModeButtonsState()
        {
            bool isSaleSelected = ListViewSales.SelectedItem != null;

            SetButtonState(btnEditSaleContainer, isSaleSelected);
            SetButtonState(btnDeleteSaleContainer, isSaleSelected);
            SetButtonState(btnPrintSaleContainer, isSaleSelected);

            // Проверяем возможность редактирования/удаления
            if (isSaleSelected)
            {
                var selected = ListViewSales.SelectedItem as SaleViewModel;
                if (selected != null)
                {
                    UpdateEditDeleteButtonsState(selected.OriginalSale.Код_чека);
                }
            }
        }

        private void UpdateEditDeleteButtonsState(int saleCode)
        {
            // Проверка кнопки "Редактировать"
            string editError;
            bool canEdit = CanEditSale(saleCode, out editError);
            var editBtn = GetButtonFromContainer(btnEditSaleContainer);
            if (editBtn != null)
            {
                editBtn.IsEnabled = canEdit;
                if (!canEdit)
                {
                    var overlay = GetOverlayFromContainer(btnEditSaleContainer);
                    if (overlay != null) overlay.ToolTip = editError;
                }
            }

            // Проверка кнопки "Удалить"
            string deleteError;
            bool canDelete = CanEditSale(saleCode, out deleteError);
            var delBtn = GetButtonFromContainer(btnDeleteSaleContainer);
            if (delBtn != null)
            {
                delBtn.IsEnabled = canDelete;
                if (!canDelete)
                {
                    var overlay = GetOverlayFromContainer(btnDeleteSaleContainer);
                    if (overlay != null) overlay.ToolTip = deleteError;
                }
            }
        }

        private Button GetButtonFromContainer(Grid container)
        {
            if (container == null) return null;
            return container.Children.OfType<Button>().FirstOrDefault();
        }

        private Border GetOverlayFromContainer(Grid container)
        {
            if (container == null) return null;
            return container.Children.OfType<Border>().FirstOrDefault(b => b.Background == Brushes.Transparent);
        }

        private void UpdateNewSaleModeButtonsState()
        {
            bool canSave = newSaleItems.Any();
            SetButtonState(btnSaveNewSaleContainer, canSave);
        }

        #endregion

        #region Проверка возможности редактирования/удаления

        private bool CanEditSale(int saleCode, out string errorMessage)
        {
            errorMessage = string.Empty;

            var sale = context.Продажи.Find(saleCode);
            if (sale == null)
            {
                errorMessage = "Чек не найден.";
                return false;
            }

            // Проверка по времени (не старше 24 часов)
            if (sale.Дата_продажи < DateTime.Now.AddHours(-MaxEditHours))
            {
                errorMessage = $"Невозможно редактировать — прошло более {MaxEditHours} ч. с момента продажи.";
                return false;
            }

            return true;
        }

        #endregion

        #region Загрузка данных

        private void LoadEmployees()
        {
            var employees = context.Сотрудники.ToList();
            PanelEmployees.Children.Clear();

            foreach (var emp in employees)
            {
                // Получаем права ДАННОГО сотрудника (не текущего пользователя)
                var empRights = AccessManager.GetAccessRights(emp.Должность?.Уровень_доступа ?? 10);

                // Проверяем, может ли этот сотрудник создавать или редактировать продажи
                bool canCreateOrEditSale = empRights.Sales.CanCreate || empRights.Sales.CanEdit;

                // Показываем только тех, у кого есть права на создание/редактирование продаж
                if (canCreateOrEditSale)
                {
                    var cb = new CheckBox
                    {
                        Margin = new Thickness(2, 1, 2, 1),
                        Content = $"{emp.Фамилия} {emp.Имя}",
                        Tag = emp.Код_сотрудника,
                        FontSize = 11,
                        Foreground = TryFindResource("ForegroundBrush") as Brush,
                        MaxWidth = 180,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    PanelEmployees.Children.Add(cb);
                }
            }
        }


        private void LoadAllSales()
        {
            // Временно сбрасываем сортировку для загрузки всех данных
            var tempDateSort = currentDateSort;
            var tempAmountSort = currentAmountSort;

            currentDateSort = SortMode.DateDesc;
            currentAmountSort = SortMode.None;

            var sales = GetFilteredAndSortedSales();
            UpdateSalesView(sales);

            // Восстанавливаем сортировку
            currentDateSort = tempDateSort;
            currentAmountSort = tempAmountSort;
        }

        private void LoadFilteredSales()
        {
            var sales = GetFilteredAndSortedSales();
            UpdateSalesView(sales);
            LoadGrandTotal();
        }

        private void UpdateSalesView(List<Продажи> sales)
        {
            var selectedCode = (ListViewSales.SelectedItem as SaleViewModel)?.OriginalSale?.Код_чека;

            var saleIds = sales.Select(s => s.Код_чека).ToList();
            var totals = saleIds.Any()
                ? context.Состав_продажи
                    .Where(i => saleIds.Contains(i.Код_чека))
                    .GroupBy(i => i.Код_чека)
                    .Select(g => new { SaleId = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0 })
                    .ToDictionary(x => x.SaleId, x => x.Total)
                : new Dictionary<int, decimal>();

            salesView.Clear();
            foreach (var sale in sales)
            {
                var total = totals.TryGetValue(sale.Код_чека, out var t) ? t : 0;
                salesView.Add(new SaleViewModel(sale, total));
            }

            if (selectedCode.HasValue)
            {
                var item = salesView.FirstOrDefault(s => s.OriginalSale?.Код_чека == selectedCode.Value);
                if (item != null) ListViewSales.SelectedItem = item;
                else ClearSaleDetails();
            }
            else ClearSaleDetails();
        }

        #endregion

        #region Сортировка

        private void ToggleDateSort_Click(object sender, MouseButtonEventArgs e)
        {
            // Переключаем сортировку по дате: Desc -> Asc -> выкл
            if (currentDateSort == SortMode.None || currentDateSort == SortMode.DateAsc)
            {
                currentDateSort = SortMode.DateDesc;
                DateSortArrow.Text = "↑"; // Новые сначала
            }
            else if (currentDateSort == SortMode.DateDesc)
            {
                currentDateSort = SortMode.DateAsc;
                DateSortArrow.Text = "↓"; // Старые сначала
            }

            // Выключаем сортировку по сумме
            currentAmountSort = SortMode.None;
            AmountSortArrow.Text = "";

            UpdateSortButtonsVisual();
            ApplyFilters();
        }

        private void ToggleAmountSort_Click(object sender, MouseButtonEventArgs e)
        {
            // Переключаем сортировку по сумме: Desc -> Asc -> выкл
            if (currentAmountSort == SortMode.None || currentAmountSort == SortMode.AmountAsc)
            {
                currentAmountSort = SortMode.AmountDesc;
                AmountSortArrow.Text = "↑"; // По убыванию
            }
            else if (currentAmountSort == SortMode.AmountDesc)
            {
                currentAmountSort = SortMode.AmountAsc;
                AmountSortArrow.Text = "↓"; // По возрастанию
            }

            // Выключаем сортировку по дате
            currentDateSort = SortMode.None;
            DateSortArrow.Text = "";

            UpdateSortButtonsVisual();
            ApplyFilters();
        }

        private void UpdateSortButtonsVisual()
        {
            // Подсветка активной кнопки
            var activeBrush = TryFindResource("AccentBrush") as Brush ?? Brushes.Blue;
            var inactiveBrush = TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;

            BtnSortByDate.BorderBrush = currentDateSort != SortMode.None ? activeBrush : inactiveBrush;
            BtnSortByAmount.BorderBrush = currentAmountSort != SortMode.None ? activeBrush : inactiveBrush;
        }

        #endregion

        #region Фильтрация и запросы

        private void FilterEmployees_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPanelItems(PanelEmployees, sender as TextBox);
        }

        private void FilterPanelItems(Panel panel, TextBox searchTextBox)
        {
            if (panel == null) return;
            var search = GetActualText(searchTextBox)?.ToLower() ?? "";

            foreach (UIElement child in panel.Children)
            {
                if (child is CheckBox checkBox)
                {
                    if (string.IsNullOrWhiteSpace(search))
                        checkBox.Visibility = Visibility.Visible;
                    else
                    {
                        var content = checkBox.Content?.ToString()?.ToLower() ?? "";
                        checkBox.Visibility = content.Contains(search) ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
        }

        private IQueryable<Продажи> GetBaseQuery()
        {
            var query = context.Продажи
                .Include(s => s.Сотрудники)
                .AsQueryable();

            // Поиск по товару в чеке
            string searchText = GetActualText(TxtSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.ToLower();
                var matchingIds = context.Состав_продажи
                    .Include(i => i.Товары)
                    .Where(i => i.Товары.Наименование.ToLower().Contains(term))
                    .Select(i => i.Код_чека)
                    .Distinct()
                    .ToList();

                query = query.Where(s => matchingIds.Contains(s.Код_чека));
            }

            // Фильтр по дате ОТ
            if (DateFrom.SelectedDate.HasValue)
            {
                var dateFrom = DateFrom.SelectedDate.Value;
                query = query.Where(s => s.Дата_продажи >= dateFrom);
            }

            // Фильтр по дате ДО
            if (DateTo.SelectedDate.HasValue)
            {
                var dateTo = DateTo.SelectedDate.Value.AddDays(1); // Вычисляем ДО передачи в LINQ
                query = query.Where(s => s.Дата_продажи < dateTo);
            }

            // Фильтр по сотрудникам (из чекбоксов)
            var selectedEmployeeIds = PanelEmployees.Children.OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true && cb.Tag is int)
                .Select(cb => (int)cb.Tag)
                .ToList();

            if (selectedEmployeeIds.Any())
                query = query.Where(s => selectedEmployeeIds.Contains(s.Код_сотрудника));

            return query;
        }
        private List<Продажи> GetFilteredAndSortedSales()
        {
            var query = GetBaseQuery();

            // Применяем сортировку
            if (currentDateSort == SortMode.DateAsc)
                query = query.OrderBy(s => s.Дата_продажи);
            else if (currentDateSort == SortMode.DateDesc)
                query = query.OrderByDescending(s => s.Дата_продажи);
            else if (currentAmountSort != SortMode.None)
            {
                var sales = query.ToList();
                var ids = sales.Select(s => s.Код_чека).ToList();
                var totals = context.Состав_продажи
                    .Where(i => ids.Contains(i.Код_чека))
                    .GroupBy(i => i.Код_чека)
                    .Select(g => new { Id = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0 })
                    .ToDictionary(x => x.Id, x => x.Total);

                return currentAmountSort == SortMode.AmountDesc
                    ? sales.OrderByDescending(s => totals.TryGetValue(s.Код_чека, out var t) ? t : 0).ToList()
                    : sales.OrderBy(s => totals.TryGetValue(s.Код_чека, out var t) ? t : 0).ToList();
            }
            else
                query = query.OrderByDescending(s => s.Дата_продажи); // По умолчанию

            return query.ToList();
        }

        private IQueryable<Товары> GetProductFilteredQuery()
        {
            var query = context.Товары.AsQueryable();
            string searchText = GetActualText(TxtProductSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(p => p.Наименование.ToLower().Contains(searchText.ToLower()));

            return query;
        }

        private void ApplyFilters() { LoadFilteredSales(); LoadGrandTotal(); }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            // Очистка поиска
            TxtSearch.Text = "";

            // Очистка дат
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;

            // Очистка поиска сотрудников
            TxtSearchEmployee.Text = "";

            // Сброс чекбоксов сотрудников
            foreach (var child in PanelEmployees.Children)
            {
                if (child is CheckBox cb)
                {
                    cb.IsChecked = false;
                    cb.Visibility = Visibility.Visible; // Показываем все
                }
            }

            // Сброс сортировки на значение по умолчанию
            currentDateSort = SortMode.DateDesc;
            currentAmountSort = SortMode.None;
            DateSortArrow.Text = "↑"; // Новые сначала
            AmountSortArrow.Text = "";
            UpdateSortButtonsVisual();

            // Перезагрузка данных
            LoadAllSales();
            LoadGrandTotal();
            ClearSaleDetails();
            ListViewSales.SelectedItem = null;
            UpdateViewModeButtonsState();
        }

        //private void SortChanged(object sender, RoutedEventArgs e)
        //{
        //    if (this.IsLoaded) ApplyFilters();
        //}

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyFilters();
        }

        private void ApplyProductFilters()
        {
            LoadProducts();
        }

        private void ApplyProductFilters_Click(object sender, RoutedEventArgs e) => ApplyProductFilters();

        private void ClearProductFilters_Click(object sender, RoutedEventArgs e) { ClearProductFilters(); LoadProducts(); }
        private void ClearProductFilters()
        {
            TxtProductSearch.Text = "";
            TxtNewSalePriceMin.Text = "";
            TxtNewSalePriceMax.Text = "";
            ChkNewSaleInStock.IsChecked = false;

            // Сброс поиска в фильтрах
            TxtNewSaleSearchCategories.Text = "";
            TxtNewSaleSearchBrands.Text = "";
            TxtNewSaleSearchManufacturers.Text = "";
            TxtNewSaleSearchMaterials.Text = "";
            TxtNewSaleSearchPacking.Text = "";

            // Сброс чекбоксов
            foreach (var panel in new[] { PanelNewSaleCategories, PanelNewSaleBrands, PanelNewSaleManufacturers, PanelNewSaleMaterials, PanelNewSalePacking })
            {
                foreach (var child in panel.Children)
                {
                    if (child is CheckBox cb)
                    {
                        cb.IsChecked = false;
                        cb.Visibility = Visibility.Visible;
                    }
                }
            }
        }
        private void TxtProductSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyProductFilters();
        }

        #endregion

        #region Выбор продажи

        private void ListViewSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSales.SelectedItem is SaleViewModel selected)
            {
                var sale = selected.OriginalSale;
                TxtSaleId.Text = sale.Код_чека.ToString();
                TxtSaleDate.Text = sale.Дата_продажи.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = sale.Сотрудники != null ? $"{sale.Сотрудники.Фамилия} {sale.Сотрудники.Имя}" : "";

                var items = context.Состав_продажи
                    .Include(i => i.Товары)
                    .Where(i => i.Код_чека == sale.Код_чека)
                    .ToList()
                    .Select(i => new SaleItemDisplay
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена,
                        PhotoSource = LoadImageFromBytes(i.Товары?.Фото)
                    }).ToList();

                ListViewSaleItems.ItemsSource = items;
                TxtTotal.Text = $"{items.Sum(i => i.Сумма):N2} ₽";
                TotalPanel.Visibility = Visibility.Visible;
            }
            else
            {
                ClearSaleDetails();
            }

            UpdateViewModeButtonsState();
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

        #endregion

        #region Режим создания/редактирования продажи

        private void NewSale_Click(object sender, RoutedEventArgs e)
        {
            ClearProductFilters();
            var saveBtn = GetButtonFromContainer(btnSaveNewSaleContainer);
            if (saveBtn != null) saveBtn.Content = "💾 Оформить продажу";

            SalesViewGrid.Visibility = Visibility.Collapsed;
            NewSaleGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSaleModeButtons.Visibility = Visibility.Visible;
            NewSaleTotalPanel.Visibility = Visibility.Visible;
            GrandTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = "Оформление новой продажи";

            newSaleItems.Clear();
            TxtNewSaleTotal.Text = "0.00 ₽";
            TxtProductSearch.Text = "";

            LoadProducts();
            UpdateNewSaleModeButtonsState();
            LoadNewSaleLookups();
        }

        #region Загрузка и фильтрация каталога для новой продажи

        /// <summary>
        /// Загружает справочники в панели фильтров режима создания продажи
        /// </summary>
        private void LoadNewSaleLookups()
        {
            PopulateFilterPanel(PanelNewSaleCategories, context.Категории.ToList(), "Категория", "Код_категория");
            PopulateFilterPanel(PanelNewSaleBrands, context.Бренд.ToList(), "Наименование_бредна", "Код_бренда");
            PopulateFilterPanel(PanelNewSaleManufacturers, context.Производитель.ToList(), "Наименование_произваодителя", "Код_производителя");
            PopulateFilterPanel(PanelNewSaleMaterials, context.Материал.ToList(), "Наименование_материала", "Код_материала");
            PopulateFilterPanel(PanelNewSalePacking, context.Фасовка.ToList(), "Количество", "Код_фасовки");
        }

        /// <summary>
        /// Заполняет панель фильтров чекбоксами (универсальный метод)
        /// </summary>
        private void PopulateFilterPanel(Panel panel, IEnumerable<object> items, string displayProperty, string valueProperty)
        {
            panel.Children.Clear();

            foreach (var item in items)
            {
                var displayValue = item.GetType().GetProperty(displayProperty)?.GetValue(item)?.ToString() ?? "";
                var tagValue = item.GetType().GetProperty(valueProperty)?.GetValue(item);

                var cb = new CheckBox
                {
                    Margin = new Thickness(2, 1, 2, 1),
                    Content = displayValue,
                    Tag = tagValue,
                    FontSize = 11,
                    Foreground = TryFindResource("ForegroundBrush") as Brush,
                    MaxWidth = 180,
                    VerticalAlignment = VerticalAlignment.Top
                };

                panel.Children.Add(cb);
            }
        }

        /// <summary>
        /// Получает ID отмеченных чекбоксов из панели
        /// </summary>
        private List<int> GetCheckedIdsFromPanel(Panel panel)
        {
            return panel.Children.OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true && cb.Tag != null && int.TryParse(cb.Tag.ToString(), out _))
                .Select(cb => (int)cb.Tag)
                .ToList();
        }

        /// <summary>
        /// Формирует отфильтрованный запрос товаров для новой продажи
        /// </summary>
        private IQueryable<Товары> GetNewSaleFilteredQuery()
        {
            var query = context.Товары.AsQueryable();

            // Поиск по наименованию
            string searchText = GetActualText(TxtProductSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(p => p.Наименование.ToLower().Contains(searchText.ToLower()));

            // Только в наличии
            if (ChkNewSaleInStock.IsChecked == true)
                query = query.Where(p => p.Количество > 0);

            // Фильтр по цене
            var priceMinText = GetActualText(TxtNewSalePriceMin);
            if (decimal.TryParse(priceMinText, out decimal pmin))
                query = query.Where(p => p.Цена_за_ед_продажа >= pmin);

            var priceMaxText = GetActualText(TxtNewSalePriceMax);
            if (decimal.TryParse(priceMaxText, out decimal pmax))
                query = query.Where(p => p.Цена_за_ед_продажа <= pmax);

            // Фильтры по справочникам
            var catIds = GetCheckedIdsFromPanel(PanelNewSaleCategories);
            if (catIds.Any()) query = query.Where(p => catIds.Contains(p.Код_категория));

            var brandIds = GetCheckedIdsFromPanel(PanelNewSaleBrands);
            if (brandIds.Any()) query = query.Where(p => brandIds.Contains(p.Код_бренда));

            var manIds = GetCheckedIdsFromPanel(PanelNewSaleManufacturers);
            if (manIds.Any()) query = query.Where(p => manIds.Contains(p.Код_производителя));

            var matIds = GetCheckedIdsFromPanel(PanelNewSaleMaterials);
            if (matIds.Any()) query = query.Where(p => matIds.Contains(p.Код_материала));

            var packIds = GetCheckedIdsFromPanel(PanelNewSalePacking);
            if (packIds.Any()) query = query.Where(p => packIds.Contains(p.Код_фасовки));

            return query;
        }

        // Обработчики поиска для фильтров новой продажи
        private void NewSaleFilterCategories_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPanelItems(PanelNewSaleCategories, sender as TextBox);
        }

        private void NewSaleFilterBrands_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPanelItems(PanelNewSaleBrands, sender as TextBox);
        }

        private void NewSaleFilterManufacturers_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPanelItems(PanelNewSaleManufacturers, sender as TextBox);
        }

        private void NewSaleFilterMaterials_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPanelItems(PanelNewSaleMaterials, sender as TextBox);
        }

        private void NewSaleFilterPacking_TextChanged(object sender, TextChangedEventArgs e)
        {
            FilterPanelItems(PanelNewSalePacking, sender as TextBox);
        }
        #endregion

        private void EditSale_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListViewSales.SelectedItem as SaleViewModel;
            if (selected == null)
            {
                MessageBox.Show("Выберите чек для редактирования!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saleCode = selected.OriginalSale.Код_чека;
            string errorMsg;
            if (!CanEditSale(saleCode, out errorMsg))
            {
                MessageBox.Show(errorMsg, "Редактирование невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            editingSaleCode = saleCode;
            var saveBtn = GetButtonFromContainer(btnSaveNewSaleContainer);
            if (saveBtn != null) saveBtn.Content = "💾 Сохранить изменения";

            SalesViewGrid.Visibility = Visibility.Collapsed;
            NewSaleGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSaleModeButtons.Visibility = Visibility.Visible;
            NewSaleTotalPanel.Visibility = Visibility.Visible;
            GrandTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = $"Редактирование чека №{editingSaleCode}";

            newSaleItems.Clear();

            // Загружаем существующие позиции
            var items = context.Состав_продажи
                .Include(i => i.Товары)
                .Where(i => i.Код_чека == editingSaleCode.Value)
                .ToList();

            foreach (var item in items)
            {
                newSaleItems.Add(new SaleItemDisplay
                {
                    Товар = item.Товары.Наименование,
                    Количество = item.Количество,
                    OriginalQuantity = item.Количество,
                    Цена = item.Цена,
                    PhotoSource = LoadImageFromBytes(item.Товары?.Фото)
                });
            }

            UpdateNewSaleTotal();
            ListViewNewSaleItems.Items.Refresh();

            TxtProductSearch.Text = "";
            LoadProducts();
            UpdateNewSaleModeButtonsState();
            LoadNewSaleLookups();
        }

        private void CancelNewSale_Click(object sender, RoutedEventArgs e) => SwitchToViewMode();

        private void LoadProducts()
        {
            var products = GetNewSaleFilteredQuery()
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
                productsView.Add(new ProductSaleViewModel(product));

            ListViewProducts.ItemsSource = productsView;
        }

        private void ListViewProducts_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void TxtItemQuantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !int.TryParse(e.Text, out _);
        }

        private void QuantitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (sender is Slider slider)
            {
                var parent = FindParent<Border>(slider);
                if (parent != null)
                {
                    var tb = FindChild<TextBox>(parent, "TxtItemQuantity");
                    if (tb != null) tb.Text = ((int)slider.Value).ToString();
                }
            }
        }

        private void AddToSaleItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductSaleViewModel productVM)
            {
                var product = productVM.OriginalProduct;
                var parent = FindParent<Border>(button);
                if (parent == null) return;

                var tb = FindChild<TextBox>(parent, "TxtItemQuantity");
                var slider = FindChild<Slider>(parent, "QuantitySlider");

                int quantity = 0;
                if (tb != null && int.TryParse(tb.Text, out int q)) quantity = q;
                else if (slider != null) quantity = (int)slider.Value;

                if (quantity <= 0)
                {
                    MessageBox.Show("Укажите количество больше нуля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверяем доступное количество с учётом уже добавленного
                var alreadyInCart = newSaleItems
                    .Where(i => i.Товар == product.Наименование)
                    .Sum(i => i.Количество);

                if (product.Количество < quantity + alreadyInCart)
                {
                    var available = product.Количество - alreadyInCart;
                    MessageBox.Show($"Недостаточно товара на складе!\nВ наличии: {product.Количество} шт.\nУже в чеке: {alreadyInCart} шт.\nДоступно: {available} шт.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var existing = newSaleItems.FirstOrDefault(i => i.Товар == product.Наименование);
                if (existing != null)
                    existing.Количество += quantity;
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
                if (tb != null) tb.Text = "1";

                UpdateNewSaleTotal();
                ListViewNewSaleItems.Items.Refresh();
                UpdateNewSaleModeButtonsState();
            }
        }

        private void RemoveNewSaleItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SaleItemDisplay item)
            {
                newSaleItems.Remove(item);
                UpdateNewSaleTotal();
                ListViewNewSaleItems.Items.Refresh();
                UpdateNewSaleModeButtonsState();
            }
        }

        private void UpdateNewSaleTotal()
        {
            TxtNewSaleTotal.Text = $"{newSaleItems.Sum(i => i.Сумма):N2} ₽";
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
                    // Режим редактирования
                    var existingSale = context.Продажи.Find(editingSaleCode.Value);
                    if (existingSale == null)
                    {
                        MessageBox.Show("Чек не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    // Возвращаем старые товары на склад
                    var oldItems = context.Состав_продажи
                        .Where(i => i.Код_чека == editingSaleCode.Value)
                        .ToList();

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
                            context.Состав_продажи.Add(new Состав_продажи
                            {
                                Код_чека = editingSaleCode.Value,
                                Код_товара = product.Код_товара,
                                Количество = item.Количество,
                                Цена = item.Цена
                            });

                            product.Количество -= item.Количество;
                            if (product.Количество < 0) product.Количество = 0;
                        }
                    }

                    context.SaveChanges();
                    MessageBox.Show($"Чек №{editingSaleCode} обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    // Режим создания
                    var sale = new Продажи
                    {
                        Код_сотрудника = currentUser.Код_сотрудника,
                        Дата_продажи = DateTime.Now
                    };

                    context.Продажи.Add(sale);
                    context.SaveChanges();

                    foreach (var item in newSaleItems)
                    {
                        var product = context.Товары.FirstOrDefault(p => p.Наименование == item.Товар);
                        if (product != null)
                        {
                            context.Состав_продажи.Add(new Состав_продажи
                            {
                                Код_чека = sale.Код_чека,
                                Код_товара = product.Код_товара,
                                Количество = item.Количество,
                                Цена = item.Цена
                            });

                            product.Количество -= item.Количество;
                            if (product.Количество < 0) product.Количество = 0;
                        }
                    }

                    context.SaveChanges();
                    MessageBox.Show($"Продажа №{sale.Код_чека} оформлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    PrintSale(sale.Код_чека);
                }

                SwitchToViewMode();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SwitchToViewMode()
        {
            editingSaleCode = null;
            var saveBtn = GetButtonFromContainer(btnSaveNewSaleContainer);
            if (saveBtn != null) saveBtn.Content = "💾 Оформить продажу";

            SalesViewGrid.Visibility = Visibility.Visible;
            NewSaleGrid.Visibility = Visibility.Collapsed;
            ViewModeButtons.Visibility = Visibility.Visible;
            NewSaleModeButtons.Visibility = Visibility.Collapsed;
            NewSaleTotalPanel.Visibility = Visibility.Collapsed;
            GrandTotalPanel.Visibility = Visibility.Visible;
            PageModeText.Text = "Управление продажами";

            newSaleItems.Clear();
            LoadAllSales();
            LoadGrandTotal();
            ClearSaleDetails();
            ListViewSales.SelectedItem = null;
            UpdateViewModeButtonsState();
        }

        #endregion

        #region CRUD и отчёты

        private void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListViewSales.SelectedItem as SaleViewModel;
            if (selected == null)
            {
                MessageBox.Show("Выберите чек для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var code = selected.OriginalSale.Код_чека;
            string errorMsg;
            if (!CanEditSale(code, out errorMsg))
            {
                MessageBox.Show(errorMsg, "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Удалить чек №{code}?\nТовары будут возвращены на склад.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    var items = context.Состав_продажи.Where(i => i.Код_чека == code).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null) product.Количество += item.Количество;
                        context.Состав_продажи.Remove(item);
                    }

                    var sale = context.Продажи.Find(code);
                    if (sale != null) context.Продажи.Remove(sale);

                    context.SaveChanges();
                    MessageBox.Show("Чек удалён! Товары возвращены на склад.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    LoadAllSales();
                    LoadGrandTotal();
                    ClearSaleDetails();
                    ListViewSales.SelectedItem = null;
                    UpdateViewModeButtonsState();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void PrintSale_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListViewSales.SelectedItem as SaleViewModel;
            if (selected == null)
            {
                MessageBox.Show("Выберите чек для печати!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            PrintSale(selected.OriginalSale.Код_чека);
        }

        private void PrintSale(int code)
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить чек",
                    FileName = $"Чек_{code}_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (sfd.ShowDialog() != true) return;

                var sale = context.Продажи
                    .Include(s => s.Сотрудники)
                    .FirstOrDefault(s => s.Код_чека == code);

                if (sale == null) return;

                var items = context.Состав_продажи
                    .Include(i => i.Товары)
                    .Where(i => i.Код_чека == code)
                    .ToList()
                    .Select(i => new
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена,
                        Сумма = i.Количество * i.Цена
                    }).ToList();

                var total = items.Sum(i => i.Сумма);

                using (var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 50, 50))
                using (var w = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, new FileStream(sfd.FileName, FileMode.Create)))
                {
                    doc.Open();
                    var fp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                    var bf = iTextSharp.text.pdf.BaseFont.CreateFont(fp, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

                    var ft = new iTextSharp.text.Font(bf, 14, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
                    var f = new iTextSharp.text.Font(bf, 10);
                    var fBold = new iTextSharp.text.Font(bf, 10, iTextSharp.text.Font.BOLD);

                    // Шапка отчёта
                    AddReportHeader(doc, bf, "ЧЕК ПРОДАЖИ");

                    // Информация о чеке
                    var infoTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
                    infoTable.SetWidths(new float[] { 50, 50 });
                    infoTable.SpacingAfter = 20;

                    AddInfoCell(infoTable, $"Чек №: {code}", fBold, iTextSharp.text.Element.ALIGN_LEFT);
                    AddInfoCell(infoTable, $"Дата: {sale.Дата_продажи:dd.MM.yyyy HH:mm}", f, iTextSharp.text.Element.ALIGN_RIGHT);
                    AddInfoCell(infoTable, $"Сотрудник: {(sale.Сотрудники != null ? $"{sale.Сотрудники.Фамилия} {sale.Сотрудники.Имя}" : "—")}", f, iTextSharp.text.Element.ALIGN_LEFT, 2);

                    doc.Add(infoTable);

                    // Таблица товаров
                    var table = new iTextSharp.text.pdf.PdfPTable(4) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 40, 15, 20, 25 });

                    foreach (var h in new[] { "Товар", "Кол-во", "Цена", "Сумма" })
                    {
                        var hc = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(h, new iTextSharp.text.Font(bf, 9, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE)))
                        {
                            BackgroundColor = new iTextSharp.text.BaseColor(0, 51, 102),
                            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER,
                            Padding = 5
                        };
                        table.AddCell(hc);
                    }

                    bool alt = false;
                    foreach (var item in items)
                    {
                        var cells = new[] { item.Товар, item.Количество.ToString(), $"{item.Цена:N2} ₽", $"{item.Сумма:N2} ₽" };
                        for (int i = 0; i < cells.Length; i++)
                        {
                            var c = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], f)) { Padding = 5 };
                            if (alt) c.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                            if (i > 0) c.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;
                            table.AddCell(c);
                        }
                        alt = !alt;
                    }
                    doc.Add(table);

                    doc.Add(new iTextSharp.text.Paragraph($"ИТОГО: {total:N2} ₽", ft) { Alignment = iTextSharp.text.Element.ALIGN_RIGHT, SpacingBefore = 15 });

                    // Футер
                    AddReportFooter(doc, bf);

                    doc.Close();
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = sfd.FileName,
                        UseShellExecute = true
                    });
                }

                //MessageBox.Show($"Чек №{code} сохранён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sales = GetBaseQuery().OrderByDescending(s => s.Дата_продажи).ToList();
                if (!sales.Any()) { MessageBox.Show("Нет данных для отчёта.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var sfd = new SaveFileDialog { Filter = "PDF файл (*.pdf)|*.pdf", Title = "Сохранить отчёт о продажах", FileName = $"Отчёт_продажи_{DateTime.Now:yyyy-MM-dd_HH-mm}" };
                if (sfd.ShowDialog() != true) return;

                var ids = sales.Select(s => s.Код_чека).ToList();
                var totals = context.Состав_продажи
                    .Where(i => ids.Contains(i.Код_чека))
                    .GroupBy(i => i.Код_чека)
                    .Select(g => new { Id = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0 })
                    .ToDictionary(x => x.Id, x => x.Total);

                var grandTotal = totals.Values.Sum();

                using (var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 50, 50))
                using (var w = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, new FileStream(sfd.FileName, FileMode.Create)))
                {
                    doc.Open();
                    var fp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                    var bf = iTextSharp.text.pdf.BaseFont.CreateFont(fp, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

                    var fTitle = new iTextSharp.text.Font(bf, 16, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
                    var fSub = new iTextSharp.text.Font(bf, 11);
                    var fth = new iTextSharp.text.Font(bf, 9, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE);
                    var ftc = new iTextSharp.text.Font(bf, 9);
                    var fSign = new iTextSharp.text.Font(bf, 10);

                    // Заголовок отчёта
                    doc.Add(new iTextSharp.text.Paragraph("ОТЧЁТ О ПРОДАЖАХ", fTitle) { Alignment = iTextSharp.text.Element.ALIGN_CENTER, SpacingAfter = 25 });

                    // Таблица
                    var table = new iTextSharp.text.pdf.PdfPTable(5) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 12, 22, 25, 15, 26 });

                    foreach (var h in new[] { "Код", "Дата", "Сотрудник", "Товаров", "Сумма" })
                    {
                        var hc = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(h, fth))
                        {
                            BackgroundColor = new iTextSharp.text.BaseColor(0, 51, 102),
                            HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER,
                            Padding = 5
                        };
                        table.AddCell(hc);
                    }

                    bool alt = false;
                    foreach (var s in sales)
                    {
                        var total = totals.TryGetValue(s.Код_чека, out var t) ? t : 0;
                        var itemCount = context.Состав_продажи.Count(i => i.Код_чека == s.Код_чека);
                        var cells = new[]
                        {
                    s.Код_чека.ToString(),
                    s.Дата_продажи.ToString("dd.MM.yyyy HH:mm"),
                    $"{s.Сотрудники?.Фамилия} {s.Сотрудники?.Имя}",
                    itemCount.ToString(),
                    $"{total:N2} ₽"
                };

                        for (int i = 0; i < cells.Length; i++)
                        {
                            var c = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], ftc)) { Padding = 5 };
                            if (alt) c.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                            if (i == 0 || i == 3 || i == 4) c.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                            table.AddCell(c);
                        }
                        alt = !alt;
                    }
                    doc.Add(table);

                    // Итоги
                    doc.Add(new iTextSharp.text.Paragraph($"Всего чеков: {sales.Count} | Общая сумма: {grandTotal:N2} ₽", fSub) { SpacingBefore = 10, SpacingAfter = 35 });

                    // Подпись
                    string initials = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
                    if (!string.IsNullOrWhiteSpace(currentUser.Отчество)) initials += $"{currentUser.Отчество?.Substring(0, 1)}.";

                    var signTable = new iTextSharp.text.pdf.PdfPTable(1) { WidthPercentage = 55, HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT };
                    var signCell = new iTextSharp.text.pdf.PdfPCell { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 3 };
                    signCell.AddElement(new iTextSharp.text.Paragraph($"{currentUser.Должность?.Название ?? "Сотрудник"} {initials} _______________  {DateTime.Now:dd.MM.yyyy}", fSign));
                    signTable.AddCell(signCell);

                    var signLineCell = new iTextSharp.text.pdf.PdfPCell { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingLeft = 145 };
                    signLineCell.AddElement(new iTextSharp.text.Paragraph("(Подпись)", new iTextSharp.text.Font(bf, 9)));
                    signTable.AddCell(signLineCell);
                    doc.Add(signTable);

                    // Футер
                    AddReportFooter(doc, bf);

                    doc.Close();
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = sfd.FileName,
                        UseShellExecute = true
                    });
                }

                //MessageBox.Show($"Отчёт сохранён!\n{sales.Count} чеков\nОбщая сумма: {grandTotal:N2} ₽", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearSaleDetails();
            ListViewSales.SelectedItem = null;
            UpdateViewModeButtonsState();
        }

        private void LoadGrandTotal()
        {
            try
            {
                var query = context.Состав_продажи.AsQueryable();

                if (DateFrom.SelectedDate.HasValue)
                {
                    var dateFrom = DateFrom.SelectedDate.Value;
                    query = query.Where(i => i.Продажи.Дата_продажи >= dateFrom);
                }

                if (DateTo.SelectedDate.HasValue)
                {
                    var dateTo = DateTo.SelectedDate.Value.AddDays(1);
                    query = query.Where(i => i.Продажи.Дата_продажи < dateTo);
                }

                // Фильтр по сотрудникам (из чекбоксов)
                var selectedEmployeeIds = PanelEmployees.Children.OfType<CheckBox>()
                    .Where(cb => cb.IsChecked == true && cb.Tag is int)
                    .Select(cb => (int)cb.Tag)
                    .ToList();

                if (selectedEmployeeIds.Any())
                    query = query.Where(i => selectedEmployeeIds.Contains(i.Продажи.Код_сотрудника));

                var total = query.Sum(i => (decimal?)i.Количество * i.Цена) ?? 0;
                TxtGrandTotal.Text = $"{total:N2} ₽";
            }
            catch { TxtGrandTotal.Text = "0.00 ₽"; }
        }
        #endregion

        #region Вспомогательные методы

        private BitmapImage LoadImageFromBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
                return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));

            try
            {
                using (var ms = new MemoryStream(data))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute)); }
        }

        private string GetActualText(TextBox tb)
        {
            if (tb == null) return string.Empty;
            var ph = PlaceholderBehavior.GetPlaceholderText(tb);
            var text = tb.Text?.Trim() ?? string.Empty;
            return (!string.IsNullOrEmpty(ph) && text == ph) ? string.Empty : text;
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
                parent = VisualTreeHelper.GetParent(parent);
            return parent as T;
        }

        private T FindChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is FrameworkElement fe && fe.Name == name && child is T t) return t;
                var found = FindChild<T>(child, name);
                if (found != null) return found;
            }
            return null;
        }

        // Вспомогательные методы для построения отчётов
        private void AddReportHeader(iTextSharp.text.Document doc, iTextSharp.text.pdf.BaseFont bf, string title)
        {
            var fontShopName = new iTextSharp.text.Font(bf, 22, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
            var fontTitle = new iTextSharp.text.Font(bf, 14, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));

            doc.Add(new iTextSharp.text.Paragraph("Oculus+", fontShopName) { Alignment = iTextSharp.text.Element.ALIGN_CENTER, SpacingAfter = 15 });
            doc.Add(new iTextSharp.text.Paragraph(title, fontTitle) { Alignment = iTextSharp.text.Element.ALIGN_CENTER, SpacingAfter = 20 });
        }

        private void AddReportFooter(iTextSharp.text.Document doc, iTextSharp.text.pdf.BaseFont bf)
        {
            const string shopName = "Oculus+";
            const string shopPhone = "+7 (461) 345 12-34";
            const string shopEmail = "Oculus@глаза.ру";
            const string shopWebsite = "Oculus.ру";
            const string shopHours = "9:00 – 17:00 ежедневно";
            var fontFooter = new iTextSharp.text.Font(bf, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);

            // Добавляем линию-разделитель
            var lineSeparator = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.LIGHT_GRAY, iTextSharp.text.Element.ALIGN_CENTER, 0);
            var lineParagraph = new iTextSharp.text.Paragraph();
            lineParagraph.SpacingBefore = 40;
            lineParagraph.Add(new iTextSharp.text.Chunk(lineSeparator));
            doc.Add(lineParagraph);

            // Текст футера
            doc.Add(new iTextSharp.text.Paragraph($"{shopName}  |  Часы работы: {shopHours}", fontFooter) { Alignment = iTextSharp.text.Element.ALIGN_CENTER, SpacingBefore = 8, SpacingAfter = 2 });
            doc.Add(new iTextSharp.text.Paragraph($"{shopPhone}  |  {shopEmail}  |  {shopWebsite}", fontFooter) { Alignment = iTextSharp.text.Element.ALIGN_CENTER, SpacingBefore = 2, SpacingAfter = 2 });
            doc.Add(new iTextSharp.text.Paragraph($"Отчёт сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}", fontFooter) { Alignment = iTextSharp.text.Element.ALIGN_CENTER, SpacingBefore = 2 });
        }

        private void AddInfoCell(iTextSharp.text.pdf.PdfPTable table, string text, iTextSharp.text.Font font, int alignment, int colSpan = 1)
        {
            var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(text, font))
            {
                Border = iTextSharp.text.Rectangle.NO_BORDER,
                HorizontalAlignment = alignment,
                Colspan = colSpan,
                Padding = 3
            };
            table.AddCell(cell);
        }
        #endregion
    }

    #region ViewModels

    public class SaleViewModel
    {
        public Продажи OriginalSale { get; set; }
        public decimal Total { get; set; }

        public string SaleDisplay => $"Чек №{OriginalSale.Код_чека}";
        public string DateDisplay => OriginalSale.Дата_продажи.ToString("dd.MM.yyyy HH:mm");
        public string EmployeeDisplay => OriginalSale.Сотрудники != null ? $"👤 {OriginalSale.Сотрудники.Фамилия} {OriginalSale.Сотрудники.Имя}" : "👤 Не указан";
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
        public decimal Цена { get; set; }
        public int Количество { get; set; }
        public string CategoryName { get; set; }
        public BitmapImage PhotoSource { get; set; }

        public string PriceDisplay => $"{Цена:N2} ₽";
        public string StockStatus => Количество > 0 ? $"✅ В наличии: {Количество} шт." : "❌ Нет в наличии";

        public ProductSaleViewModel(Товары product)
        {
            OriginalProduct = product;
            Наименование = product.Наименование;
            Цена = product.Цена_за_ед_продажа;
            Количество = product.Количество;
            CategoryName = product.Категории?.Категория != null ? $"📂 {product.Категории.Категория}" : "📂 Без категории";
            PhotoSource = LoadImage(product.Фото);
        }

        private static BitmapImage LoadImage(byte[] data)
        {
            if (data == null || data.Length == 0)
                return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            try
            {
                using (var ms = new MemoryStream(data))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = ms;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    return bmp;
                }
            }
            catch { return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute)); }
        }
    }

    #endregion
}