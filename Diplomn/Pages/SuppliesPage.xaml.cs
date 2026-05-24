using Diplomn.Addons;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
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
    public partial class SuppliesPage : Page
    {
        #region Поля

        private BDEntities context;
        private Сотрудники currentUser;
        private ObservableCollection<SupplyViewModel> suppliesView;
        private ObservableCollection<ProductSupplyViewModel> productsView;
        private ObservableCollection<SupplyItemDisplay> newSupplyItems;
        private AccessManager.AccessRights rights;
        private int? editingSupplyCode = null;
        private const int MaxEditMonths = 1;

        #region Поля Кнопок

        // Контейнеры для кнопок в режиме просмотра
        private Grid btnNewSupplyContainer;
        private Grid btnEditSupplyContainer;
        private Grid btnDeleteSupplyContainer;
        private Grid btnPrintSupplyContainer;
        private Grid btnClearContainer;

        // Контейнеры для кнопок в режиме создания/редактирования
        private Grid btnSaveNewSupplyContainer;
        private Grid btnCancelNewSupplyContainer;
        #endregion

        #region Поля сортировки
        private enum SortMode { None, DateAsc, DateDesc, AmountAsc, AmountDesc }
        private SortMode currentDateSort = SortMode.DateDesc;
        private SortMode currentAmountSort = SortMode.None;

        #endregion

        #endregion

        #region Вспомогательные классы

        public class SupplyItemDisplay
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

        public SuppliesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Поставки — {user.Фамилия} {user.Имя}";
            suppliesView = new ObservableCollection<SupplyViewModel>();
            productsView = new ObservableCollection<ProductSupplyViewModel>();
            newSupplyItems = new ObservableCollection<SupplyItemDisplay>();

            rights = AccessManager.GetAccessRights(user.Должность?.Уровень_доступа ?? 10);

            ListViewSupplies.ItemsSource = suppliesView;
            ListViewNewSupplyItems.ItemsSource = newSupplyItems;

            CreateViewModeButtons();
            CreateNewSupplyModeButtons();
            LoadEmployees();
            LoadSuppliers();

            this.Loaded += (s, e) =>
            {
                LoadAllSupplies();
                LoadGrandTotal();
            };

        }

        #endregion

        #region Создание кнопок с overlay

        private void CreateViewModeButtons()
        {
            ViewModeButtons.Children.Clear();

            if (rights.Supplies.CanCreate)
            {
                var (newBtn, newOverlay) = CreateButtonWithOverlay("➕ Новая поставка", NewSupply_Click, 150);
                newBtn.IsEnabled = true;
                newOverlay.Visibility = Visibility.Collapsed;
                btnNewSupplyContainer = CreateButtonContainer(newBtn, newOverlay);
                ViewModeButtons.Children.Add(btnNewSupplyContainer);
            }

            if (rights.Supplies.CanEdit)
            {
                var (editBtn, editOverlay) = CreateButtonWithOverlay("✏ Редактировать", EditSupply_Click, 130);
                btnEditSupplyContainer = CreateButtonContainer(editBtn, editOverlay);
                ViewModeButtons.Children.Add(btnEditSupplyContainer);
            }

            if (rights.Supplies.CanDelete)
            {
                var (delBtn, delOverlay) = CreateButtonWithOverlay("🗑 Удалить", DeleteSupply_Click, 110);
                btnDeleteSupplyContainer = CreateButtonContainer(delBtn, delOverlay);
                ViewModeButtons.Children.Add(btnDeleteSupplyContainer);
            }

            var (printBtn, printOverlay) = CreateButtonWithOverlay("🖨 Печать поставки", PrintSupply_Click, 140);
            btnPrintSupplyContainer = CreateButtonContainer(printBtn, printOverlay);
            ViewModeButtons.Children.Add(btnPrintSupplyContainer);

            var (clearBtn, clearOverlay) = CreateButtonWithOverlay("🔄 Очистить", ClearForm_Click, 110);
            clearBtn.IsEnabled = true;
            clearOverlay.Visibility = Visibility.Collapsed;
            btnClearContainer = CreateButtonContainer(clearBtn, clearOverlay);
            ViewModeButtons.Children.Add(btnClearContainer);

            UpdateViewModeButtonsState();
        }

        private void CreateNewSupplyModeButtons()
        {
            NewSupplyModeButtons.Children.Clear();

            var (saveBtn, saveOverlay) = CreateButtonWithOverlay("💾 Оформить поставку", SaveNewSupply_Click, 160);
            btnSaveNewSupplyContainer = CreateButtonContainer(saveBtn, saveOverlay);
            NewSupplyModeButtons.Children.Add(btnSaveNewSupplyContainer);

            var (cancelBtn, cancelOverlay) = CreateButtonWithOverlay("↩ Отмена", CancelNewSupply_Click, 110);
            cancelBtn.IsEnabled = true;
            cancelOverlay.Visibility = Visibility.Collapsed;
            btnCancelNewSupplyContainer = CreateButtonContainer(cancelBtn, cancelOverlay);
            NewSupplyModeButtons.Children.Add(btnCancelNewSupplyContainer);
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

            if (buttonContent.Contains("Новая поставка"))
                return "Создать новую поставку";

            if (buttonContent.Contains("Редактировать"))
                return "Выберите поставку для редактирования";

            if (buttonContent.Contains("Удалить"))
                return "Выберите поставку для удаления";

            if (buttonContent.Contains("Печать поставки"))
                return "Выберите поставку для печати";

            if (buttonContent.Contains("Очистить"))
                return "Очистить детали поставки";

            if (buttonContent.Contains("Оформить поставку"))
            {
                if (!newSupplyItems.Any())
                    return "Добавьте товары в поставку";
                if (CmbNewSupplySupplier.SelectedItem == null)
                    return "Выберите поставщика";
                return "Нажмите для оформления поставки";
            }

            if (buttonContent.Contains("Отмена"))
                return "Вернуться к списку поставок";

            return "Кнопка недоступна";
        }

        private void SetButtonState(Grid container, bool isEnabled)
        {
            if (container == null) return;
            var button = container.Children.OfType<Button>().FirstOrDefault();
            if (button != null) button.IsEnabled = isEnabled;
        }

        private Button GetButtonFromContainer(Grid container)
        {
            return container?.Children.OfType<Button>().FirstOrDefault();
        }

        private Border GetOverlayFromContainer(Grid container)
        {
            return container?.Children.OfType<Border>().FirstOrDefault(b => b.Background == Brushes.Transparent);
        }

        private void UpdateViewModeButtonsState()
        {
            bool isSupplySelected = ListViewSupplies.SelectedItem != null;

            SetButtonState(btnEditSupplyContainer, isSupplySelected);
            SetButtonState(btnDeleteSupplyContainer, isSupplySelected);
            SetButtonState(btnPrintSupplyContainer, isSupplySelected);

            if (isSupplySelected)
            {
                var selected = ListViewSupplies.SelectedItem as SupplyViewModel;
                if (selected != null)
                    UpdateEditDeleteButtonsState(selected.OriginalSupply.Код_поставки);
            }
        }

        private void UpdateEditDeleteButtonsState(int supplyCode)
        {
            string editError;
            bool canEdit = CanEditSupply(supplyCode, out editError);
            var editBtn = GetButtonFromContainer(btnEditSupplyContainer);
            if (editBtn != null)
            {
                editBtn.IsEnabled = canEdit;
                if (!canEdit)
                {
                    var overlay = GetOverlayFromContainer(btnEditSupplyContainer);
                    if (overlay != null) overlay.ToolTip = editError;
                }
            }

            string deleteError;
            bool canDelete = CanDeleteSupply(supplyCode, out deleteError);
            var delBtn = GetButtonFromContainer(btnDeleteSupplyContainer);
            if (delBtn != null)
            {
                delBtn.IsEnabled = canDelete;
                if (!canDelete)
                {
                    var overlay = GetOverlayFromContainer(btnDeleteSupplyContainer);
                    if (overlay != null) overlay.ToolTip = deleteError;
                }
            }
        }

        private void UpdateNewSupplyModeButtonsState()
        {
            bool canSave = newSupplyItems.Any() && CmbNewSupplySupplier.SelectedItem != null;
            SetButtonState(btnSaveNewSupplyContainer, canSave);
        }

        #endregion

        #region Проверка возможности редактирования/удаления

        private bool CanEditSupply(int supplyCode, out string errorMessage)
        {
            errorMessage = string.Empty;

            var supply = context.Поставка.Find(supplyCode);
            if (supply == null) { errorMessage = "Поставка не найдена."; return false; }

            if (supply.Дата_оформления_постивки < DateTime.Now.AddMonths(-MaxEditMonths))
            {
                errorMessage = $"Прошло более {MaxEditMonths} мес. с даты оформления.";
                return false;
            }

            return true;
        }

        private bool CanDeleteSupply(int supplyCode, out string errorMessage)
        {
            errorMessage = string.Empty;
            var errors = new List<string>();

            var supply = context.Поставка.Find(supplyCode);
            if (supply == null) { errorMessage = "Поставка не найдена."; return false; }

            if (supply.Дата_оформления_постивки < DateTime.Now.AddMonths(-MaxEditMonths))
            {
                errorMessage = $"Прошло более {MaxEditMonths} мес. с даты оформления.";
                return false;
            }

            var items = context.Состав_поставки
                .Include(i => i.Товары)
                .Where(i => i.Код_поставки == supplyCode)
                .ToList();

            foreach (var item in items)
            {
                var product = item.Товары;
                if (product != null && product.Количество < item.Количество)
                    errors.Add($"• {product.Наименование}: на складе {product.Количество} шт., в поставке {item.Количество} шт.");
            }

            if (errors.Any())
            {
                errorMessage = "Недостаточно товаров на складе:\n" + string.Join("\n", errors);
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
                // Получаем права ДАННОГО сотрудника
                var empRights = AccessManager.GetAccessRights(emp.Должность?.Уровень_доступа ?? 10);

                // Проверяем, может ли этот сотрудник создавать или редактировать поставки
                bool canCreateOrEditSupply = empRights.Supplies.CanCreate || empRights.Supplies.CanEdit;

                // Показываем только тех, у кого есть права на создание/редактирование поставок
                if (canCreateOrEditSupply)
                {
                    var cb = new CheckBox
                    {
                        Margin = new Thickness(2, 1, 2, 1),
                        Content = $"{emp.Фамилия} {emp.Имя?.Substring(0, 1)}. {(emp.Отчество?.Length > 0 ? emp.Отчество.Substring(0, 1) + "." : "")}",
                        Tag = emp.Код_сотрудника,
                        FontSize = 11,
                        Foreground = TryFindResource("ForegroundBrush") as Brush,
                        MaxWidth = 250,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    PanelEmployees.Children.Add(cb);
                }
            }
        }

        private void LoadSuppliers()
        {
            var suppliers = context.Поставщики.ToList();
            PanelSuppliers.Children.Clear();
            foreach (var sup in suppliers)
            {
                var cb = new CheckBox
                {
                    Margin = new Thickness(2, 1, 2, 1),
                    Content = sup.Наименование_поставщика,
                    Tag = sup.Код_поставщика,
                    FontSize = 11,
                    Foreground = TryFindResource("ForegroundBrush") as Brush,
                    MaxWidth = 250,
                    VerticalAlignment = VerticalAlignment.Top
                };
                PanelSuppliers.Children.Add(cb);
            }
        }

        private void LoadAllSupplies()
        {
            var supplies = GetBaseQuery()
                .OrderByDescending(s => s.Дата_оформления_постивки)
                .ToList();
            UpdateSuppliesView(supplies);
        }

        private void LoadFilteredSupplies()
        {
            var supplies = GetFilteredAndSortedSupplies();
            UpdateSuppliesView(supplies);
            LoadGrandTotal();
        }

        private void UpdateSuppliesView(List<Поставка> supplies)
        {
            var selectedCode = (ListViewSupplies.SelectedItem as SupplyViewModel)?.OriginalSupply?.Код_поставки;

            var supplyIds = supplies.Select(s => s.Код_поставки).ToList();
            var totals = supplyIds.Any()
                ? context.Состав_поставки
                    .Where(i => supplyIds.Contains(i.Код_поставки))
                    .GroupBy(i => i.Код_поставки)
                    .Select(g => new { Id = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0 })
                    .ToDictionary(x => x.Id, x => x.Total)
                : new Dictionary<int, decimal>();

            suppliesView.Clear();
            foreach (var supply in supplies)
            {
                var total = totals.TryGetValue(supply.Код_поставки, out var t) ? t : 0;
                suppliesView.Add(new SupplyViewModel(supply, total));
            }

            if (selectedCode.HasValue)
            {
                var item = suppliesView.FirstOrDefault(s => s.OriginalSupply?.Код_поставки == selectedCode.Value);
                if (item != null) ListViewSupplies.SelectedItem = item;
                else ClearSupplyDetails();
            }
            else ClearSupplyDetails();
        }

        #endregion

        #region Фильтрация и запросы

        private void ToggleDateSort_Click(object sender, MouseButtonEventArgs e)
        {
            if (currentDateSort == SortMode.None || currentDateSort == SortMode.DateAsc)
            { currentDateSort = SortMode.DateDesc; DateSortArrow.Text = "↑"; }
            else if (currentDateSort == SortMode.DateDesc)
            { currentDateSort = SortMode.DateAsc; DateSortArrow.Text = "↓"; }
            currentAmountSort = SortMode.None; AmountSortArrow.Text = "";
            UpdateSortButtonsVisual(); ApplyFilters();
        }

        private void ToggleAmountSort_Click(object sender, MouseButtonEventArgs e)
        {
            if (currentAmountSort == SortMode.None || currentAmountSort == SortMode.AmountAsc)
            { currentAmountSort = SortMode.AmountDesc; AmountSortArrow.Text = "↑"; }
            else if (currentAmountSort == SortMode.AmountDesc)
            { currentAmountSort = SortMode.AmountAsc; AmountSortArrow.Text = "↓"; }
            currentDateSort = SortMode.None; DateSortArrow.Text = "";
            UpdateSortButtonsVisual(); ApplyFilters();
        }

        private void UpdateSortButtonsVisual()
        {
            var activeBrush = TryFindResource("AccentBrush") as Brush ?? Brushes.Blue;
            var inactiveBrush = TryFindResource("BorderBrush") as Brush ?? Brushes.Gray;
            BtnSortByDate.BorderBrush = currentDateSort != SortMode.None ? activeBrush : inactiveBrush;
            BtnSortByAmount.BorderBrush = currentAmountSort != SortMode.None ? activeBrush : inactiveBrush;
        }

        private IQueryable<Поставка> GetBaseQuery()
        {
            var query = context.Поставка
                .Include(s => s.Сотрудники)
                .Include(s => s.Поставщики)
                .AsQueryable();

            string searchText = GetActualText(TxtSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText.ToLower();
                var matchingIds = context.Состав_поставки
                    .Include(i => i.Товары)
                    .Where(i => i.Товары.Наименование.ToLower().Contains(term))
                    .Select(i => i.Код_поставки).Distinct().ToList();
                query = query.Where(s => matchingIds.Contains(s.Код_поставки));
            }

            // Фильтр по дате ОТ
            if (DateFrom.SelectedDate.HasValue)
            {
                var dateFrom = DateFrom.SelectedDate.Value;
                query = query.Where(s => s.Дата_оформления_постивки >= dateFrom);
            }

            // Фильтр по дате ДО
            if (DateTo.SelectedDate.HasValue)
            {
                var dateTo = DateTo.SelectedDate.Value.AddDays(1); // Вычисляем ДО передачи в LINQ
                query = query.Where(s => s.Дата_оформления_постивки < dateTo);
            }

            var empIds = PanelEmployees.Children.OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true && cb.Tag is int).Select(cb => (int)cb.Tag).ToList();
            if (empIds.Any()) query = query.Where(s => empIds.Contains(s.Код_сотрудника));

            var supIds = PanelSuppliers.Children.OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true && cb.Tag is int).Select(cb => (int)cb.Tag).ToList();
            if (supIds.Any()) query = query.Where(s => supIds.Contains(s.Код_поставщика));

            return query;
        }

        private List<Поставка> GetFilteredAndSortedSupplies()
        {
            var query = GetBaseQuery();
            if (currentDateSort == SortMode.DateAsc) query = query.OrderBy(s => s.Дата_оформления_постивки);
            else if (currentDateSort == SortMode.DateDesc) query = query.OrderByDescending(s => s.Дата_оформления_постивки);
            else if (currentAmountSort != SortMode.None)
            {
                var supplies = query.ToList(); var ids = supplies.Select(s => s.Код_поставки).ToList();
                var totals = context.Состав_поставки.Where(i => ids.Contains(i.Код_поставки))
                    .GroupBy(i => i.Код_поставки).Select(g => new { Id = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0 })
                    .ToDictionary(x => x.Id, x => x.Total);
                return currentAmountSort == SortMode.AmountDesc
                    ? supplies.OrderByDescending(s => totals.TryGetValue(s.Код_поставки, out var t) ? t : 0).ToList()
                    : supplies.OrderBy(s => totals.TryGetValue(s.Код_поставки, out var t) ? t : 0).ToList();
            }
            else query = query.OrderByDescending(s => s.Дата_оформления_постивки);
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

        private void ApplyFilters() => LoadFilteredSupplies();
        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void FilterEmployees_TextChanged(object sender, TextChangedEventArgs e) => FilterPanelItems(PanelEmployees, sender as TextBox);
        private void FilterSuppliers_TextChanged(object sender, TextChangedEventArgs e) => FilterPanelItems(PanelSuppliers, sender as TextBox);

        private void FilterPanelItems(Panel panel, TextBox searchTextBox)
        {
            if (panel == null) return;
            var search = GetActualText(searchTextBox)?.ToLower() ?? "";
            foreach (UIElement child in panel.Children)
            {
                if (child is CheckBox checkBox)
                    checkBox.Visibility = string.IsNullOrWhiteSpace(search) ? Visibility.Visible :
                        (checkBox.Content?.ToString()?.ToLower() ?? "").Contains(search) ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = ""; DateFrom.SelectedDate = null; DateTo.SelectedDate = null;
            TxtSearchEmployee.Text = ""; TxtSearchSupplier.Text = "";
            foreach (var child in PanelEmployees.Children) if (child is CheckBox cb) { cb.IsChecked = false; cb.Visibility = Visibility.Visible; }
            foreach (var child in PanelSuppliers.Children) if (child is CheckBox cb) { cb.IsChecked = false; cb.Visibility = Visibility.Visible; }
            currentDateSort = SortMode.DateDesc; currentAmountSort = SortMode.None;
            DateSortArrow.Text = "↑"; AmountSortArrow.Text = ""; UpdateSortButtonsVisual();
            LoadAllSupplies(); LoadGrandTotal(); ClearSupplyDetails();
            ListViewSupplies.SelectedItem = null; UpdateViewModeButtonsState();
        }

        private void SortChanged(object sender, RoutedEventArgs e) { if (this.IsLoaded) ApplyFilters(); }
        private void TxtSearch_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) ApplyFilters(); }

        private void ApplyProductFilters()
        {
            LoadProducts();
            //var products = GetProductFilteredQuery().Include("Категории").ToList();
            //UpdateProductsView(products);
        }
        private void ApplyProductFilters_Click(object sender, RoutedEventArgs e) => ApplyProductFilters();
        private void TxtProductSearch_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) ApplyProductFilters(); }

        #endregion

        #region Выбор поставки

        private void ListViewSupplies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSupplies.SelectedItem is SupplyViewModel selected)
            {
                var supply = selected.OriginalSupply;
                TxtSupplyId.Text = supply.Код_поставки.ToString();
                TxtSupplyDate.Text = supply.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = supply.Сотрудники != null ? $"{supply.Сотрудники.Фамилия} {supply.Сотрудники.Имя}" : "";
                TxtSupplier.Text = supply.Поставщики?.Наименование_поставщика ?? "";

                var items = context.Состав_поставки
                    .Include(i => i.Товары)
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
                TxtTotal.Text = $"{items.Sum(i => i.Сумма):N2} ₽";
                TotalPanel.Visibility = Visibility.Visible;
            }
            else ClearSupplyDetails();

            UpdateViewModeButtonsState();
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

        #endregion

        #region Режим создания/редактирования поставки

        private void NewSupply_Click(object sender, RoutedEventArgs e)
        {
            editingSupplyCode = null;
            var saveBtn = GetButtonFromContainer(btnSaveNewSupplyContainer);
            if (saveBtn != null) saveBtn.Content = "💾 Оформить поставку";

            SuppliesViewGrid.Visibility = Visibility.Collapsed;
            NewSupplyGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSupplyModeButtons.Visibility = Visibility.Visible;
            NewSupplyTotalPanel.Visibility = Visibility.Visible;
            GrandTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = "Оформление новой поставки";

            newSupplyItems.Clear();
            TxtNewSupplyTotal.Text = "0.00 ₽";
            TxtProductSearch.Text = "";

            LoadNewSupplySuppliers();
            LoadNewSupplyLookups();
            LoadProducts();
            UpdateNewSupplyModeButtonsState();
        }

        private void EditSupply_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListViewSupplies.SelectedItem as SupplyViewModel;
            if (selected == null) { MessageBox.Show("Выберите поставку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var code = selected.OriginalSupply.Код_поставки;
            string errorMsg;
            if (!CanEditSupply(code, out errorMsg)) { MessageBox.Show(errorMsg, "Редактирование невозможно", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            editingSupplyCode = code;
            var saveBtn = GetButtonFromContainer(btnSaveNewSupplyContainer);
            if (saveBtn != null) saveBtn.Content = "💾 Оформить поставку";

            SuppliesViewGrid.Visibility = Visibility.Collapsed;
            NewSupplyGrid.Visibility = Visibility.Visible;
            ViewModeButtons.Visibility = Visibility.Collapsed;
            NewSupplyModeButtons.Visibility = Visibility.Visible;
            NewSupplyTotalPanel.Visibility = Visibility.Visible;
            GrandTotalPanel.Visibility = Visibility.Collapsed;
            PageModeText.Text = $"Редактирование поставки №{editingSupplyCode}";

            newSupplyItems.Clear();

            var items = context.Состав_поставки
                .Include(i => i.Товары)
                .Where(i => i.Код_поставки == editingSupplyCode.Value)
                .ToList();

            foreach (var item in items)
            {
                newSupplyItems.Add(new SupplyItemDisplay
                {
                    Товар = item.Товары.Наименование,
                    Количество = item.Количество,
                    OriginalQuantity = item.Количество,
                    Цена = item.Цена_за_ед_покупка,
                    PhotoSource = LoadImageFromBytes(item.Товары?.Фото)
                });
            }

            UpdateNewSupplyTotal();
            ListViewNewSupplyItems.Items.Refresh();

            LoadNewSupplySuppliers();

            var supply = context.Поставка.Find(editingSupplyCode.Value);
            if (supply != null)
            {
                for (int i = 0; i < CmbNewSupplySupplier.Items.Count; i++)
                {
                    var s = CmbNewSupplySupplier.Items[i] as Поставщики;
                    if (s != null && s.Код_поставщика == supply.Код_поставщика)
                    { CmbNewSupplySupplier.SelectedIndex = i; break; }
                }
            }

            TxtProductSearch.Text = "";
            LoadProducts();
            LoadNewSupplyLookups();
            UpdateNewSupplyModeButtonsState();
        }

        private void CancelNewSupply_Click(object sender, RoutedEventArgs e) => SwitchToViewMode();

        private void LoadNewSupplySuppliers()
        {
            var suppliers = context.Поставщики.ToList();
            CmbNewSupplySupplier.ItemsSource = suppliers;
            if (suppliers.Any()) CmbNewSupplySupplier.SelectedIndex = 0;
            CmbNewSupplySupplier.SelectionChanged += (s, e) => UpdateNewSupplyModeButtonsState();
        }

        private void LoadNewSupplyLookups()
        {
            PopulateFilterPanel(PanelNewSupplyCategories, context.Категории.ToList(), "Категория", "Код_категория");
            PopulateFilterPanel(PanelNewSupplyBrands, context.Бренд.ToList(), "Наименование_бредна", "Код_бренда");
            PopulateFilterPanel(PanelNewSupplyManufacturers, context.Производитель.ToList(), "Наименование_произваодителя", "Код_производителя");
            PopulateFilterPanel(PanelNewSupplyMaterials, context.Материал.ToList(), "Наименование_материала", "Код_материала");
            PopulateFilterPanel(PanelNewSupplyPacking, context.Фасовка.ToList(), "Количество", "Код_фасовки");
        }

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

        private List<int> GetCheckedIdsFromPanel(Panel panel)
        {
            return panel.Children.OfType<CheckBox>()
                .Where(cb => cb.IsChecked == true && cb.Tag != null && int.TryParse(cb.Tag.ToString(), out _))
                .Select(cb => (int)cb.Tag)
                .ToList();
        }

        private IQueryable<Товары> GetNewSupplyFilteredQuery()
        {
            var query = context.Товары.AsQueryable();

            string st = GetActualText(TxtProductSearch);
            if (!string.IsNullOrWhiteSpace(st))
                query = query.Where(p => p.Наименование.ToLower().Contains(st.ToLower()));

            if (ChkNewSupplyInStock.IsChecked == true)
                query = query.Where(p => p.Количество > 0);

            if (decimal.TryParse(GetActualText(TxtNewSupplyPriceMin), out decimal pmin))
                query = query.Where(p => p.Цена_за_ед_продажа >= pmin);
            if (decimal.TryParse(GetActualText(TxtNewSupplyPriceMax), out decimal pmax))
                query = query.Where(p => p.Цена_за_ед_продажа <= pmax);

            var catIds = GetCheckedIdsFromPanel(PanelNewSupplyCategories);
            var brandIds = GetCheckedIdsFromPanel(PanelNewSupplyBrands);
            var manIds = GetCheckedIdsFromPanel(PanelNewSupplyManufacturers);
            var matIds = GetCheckedIdsFromPanel(PanelNewSupplyMaterials);
            var packIds = GetCheckedIdsFromPanel(PanelNewSupplyPacking);

            if (catIds.Any()) query = query.Where(p => catIds.Contains(p.Код_категория));
            if (brandIds.Any()) query = query.Where(p => brandIds.Contains(p.Код_бренда));
            if (manIds.Any()) query = query.Where(p => manIds.Contains(p.Код_производителя));
            if (matIds.Any()) query = query.Where(p => matIds.Contains(p.Код_материала));
            if (packIds.Any()) query = query.Where(p => packIds.Contains(p.Код_фасовки));

            return query;
        }
        private void LoadProducts()
        {
            var products = GetNewSupplyFilteredQuery()
                .Include("Категории")
                .Include("Бренд")
                .Include("Производитель")
                .Include("Материал")
                .Include("Фасовка")
                .ToList();
            UpdateProductsView(products);


        }
        private void ClearProductFilters_Click(object sender, RoutedEventArgs e) { ClearProductFilters(); LoadProducts(); }
        private void ClearProductFilters()
        {
            TxtProductSearch.Text = ""; TxtNewSupplyPriceMin.Text = ""; TxtNewSupplyPriceMax.Text = ""; ChkNewSupplyInStock.IsChecked = false;
            TxtNewSupplySearchCategories.Text = ""; TxtNewSupplySearchBrands.Text = ""; TxtNewSupplySearchManufacturers.Text = ""; TxtNewSupplySearchMaterials.Text = ""; TxtNewSupplySearchPacking.Text = "";
            foreach (var p in new[] { PanelNewSupplyCategories, PanelNewSupplyBrands, PanelNewSupplyManufacturers, PanelNewSupplyMaterials, PanelNewSupplyPacking })
                foreach (var c in p.Children) if (c is CheckBox cb) { cb.IsChecked = false; cb.Visibility = Visibility.Visible; }
        }

        private void NewSupplyFilterCategories_TextChanged(object sender, TextChangedEventArgs e) => FilterPanelItems(PanelNewSupplyCategories, sender as TextBox);
        private void NewSupplyFilterBrands_TextChanged(object sender, TextChangedEventArgs e) => FilterPanelItems(PanelNewSupplyBrands, sender as TextBox);
        private void NewSupplyFilterManufacturers_TextChanged(object sender, TextChangedEventArgs e) => FilterPanelItems(PanelNewSupplyManufacturers, sender as TextBox);
        private void NewSupplyFilterMaterials_TextChanged(object sender, TextChangedEventArgs e) => FilterPanelItems(PanelNewSupplyMaterials, sender as TextBox);
        private void NewSupplyFilterPacking_TextChanged(object sender, TextChangedEventArgs e) => FilterPanelItems(PanelNewSupplyPacking, sender as TextBox);

        private void UpdateProductsView(List<Товары> products)
        {
            productsView.Clear();
            foreach (var product in products)
            {
                productsView.Add(new ProductSupplyViewModel(product));
            }
                

            ListViewProducts.ItemsSource = productsView;
        }

        private void ListViewProducts_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void TxtItemQuantity_PreviewTextInput(object sender, TextCompositionEventArgs e)
        { e.Handled = !int.TryParse(e.Text, out _); }

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

        private void AddToSupplyItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is ProductSupplyViewModel productVM)
            {
                var product = productVM.OriginalProduct;
                var parent = FindParent<Border>(button);
                if (parent == null) return;

                var tb = FindChild<TextBox>(parent, "TxtItemQuantity");
                var slider = FindChild<Slider>(parent, "QuantitySlider");

                int quantity = 0;
                if (tb != null && int.TryParse(tb.Text, out int q)) quantity = q;
                else if (slider != null) quantity = (int)slider.Value;

                if (quantity <= 0) { MessageBox.Show("Укажите количество больше нуля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                var existing = newSupplyItems.FirstOrDefault(i => i.Товар == product.Наименование);
                if (existing != null) existing.Количество += quantity;
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
                if (tb != null) tb.Text = "1";

                UpdateNewSupplyTotal();
                ListViewNewSupplyItems.Items.Refresh();
                UpdateNewSupplyModeButtonsState();
            }
        }

        private void RemoveNewSupplyItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SupplyItemDisplay item)
            {
                newSupplyItems.Remove(item);
                UpdateNewSupplyTotal();
                ListViewNewSupplyItems.Items.Refresh();
                UpdateNewSupplyModeButtonsState();
            }
        }

        private void UpdateNewSupplyTotal() => TxtNewSupplyTotal.Text = $"{newSupplyItems.Sum(i => i.Сумма):N2} ₽";

        private void SaveNewSupply_Click(object sender, RoutedEventArgs e)
        {
            if (!newSupplyItems.Any()) { MessageBox.Show("Добавьте товары!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            if (CmbNewSupplySupplier.SelectedItem == null) { MessageBox.Show("Выберите поставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            try
            {
                var supplier = CmbNewSupplySupplier.SelectedItem as Поставщики;

                if (editingSupplyCode.HasValue)
                {
                    var existing = context.Поставка.Find(editingSupplyCode.Value);
                    if (existing != null)
                    {
                        existing.Код_поставщика = supplier.Код_поставщика;

                        var oldItems = context.Состав_поставки.Where(i => i.Код_поставки == editingSupplyCode.Value).ToList();
                        foreach (var old in oldItems)
                        {
                            var p = context.Товары.Find(old.Код_товара);
                            if (p != null) { p.Количество -= old.Количество; if (p.Количество < 0) p.Количество = 0; }
                            context.Состав_поставки.Remove(old);
                        }

                        foreach (var item in newSupplyItems)
                        {
                            var p = context.Товары.FirstOrDefault(pr => pr.Наименование == item.Товар);
                            if (p != null)
                            {
                                context.Состав_поставки.Add(new Состав_поставки { Код_поставки = editingSupplyCode.Value, Код_товара = p.Код_товара, Количество = item.Количество, Цена_за_ед_покупка = item.Цена });
                                p.Количество += item.Количество;
                            }
                        }
                        context.SaveChanges();
                        MessageBox.Show($"Поставка №{editingSupplyCode} обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                else
                {
                    var supply = new Поставка { Код_сотрудника = currentUser.Код_сотрудника, Код_поставщика = supplier.Код_поставщика, Дата_оформления_постивки = DateTime.Now };
                    context.Поставка.Add(supply);
                    context.SaveChanges();

                    foreach (var item in newSupplyItems)
                    {
                        var p = context.Товары.FirstOrDefault(pr => pr.Наименование == item.Товар);
                        if (p != null)
                        {
                            context.Состав_поставки.Add(new Состав_поставки { Код_поставки = supply.Код_поставки, Код_товара = p.Код_товара, Количество = item.Количество, Цена_за_ед_покупка = item.Цена });
                            p.Количество += item.Количество;
                        }
                    }
                    context.SaveChanges();
                    MessageBox.Show($"Поставка №{supply.Код_поставки} оформлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    PrintSupply(supply.Код_поставки);
                }
                
                SwitchToViewMode();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SwitchToViewMode()
        {
            editingSupplyCode = null;
            var saveBtn = GetButtonFromContainer(btnSaveNewSupplyContainer);
            if (saveBtn != null) saveBtn.Content = "💾 Оформить поставку";

            SuppliesViewGrid.Visibility = Visibility.Visible;
            NewSupplyGrid.Visibility = Visibility.Collapsed;
            ViewModeButtons.Visibility = Visibility.Visible;
            NewSupplyModeButtons.Visibility = Visibility.Collapsed;
            NewSupplyTotalPanel.Visibility = Visibility.Collapsed;
            GrandTotalPanel.Visibility = Visibility.Visible;
            PageModeText.Text = "Управление поставками";

            newSupplyItems.Clear();
            LoadAllSupplies();
            LoadGrandTotal();
            ClearSupplyDetails();
            ListViewSupplies.SelectedItem = null;
            UpdateViewModeButtonsState();
        }

        #endregion

        #region CRUD и отчёты

        private void DeleteSupply_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListViewSupplies.SelectedItem as SupplyViewModel;
            if (selected == null) { MessageBox.Show("Выберите поставку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            var code = selected.OriginalSupply.Код_поставки;
            string errorMsg;
            if (!CanDeleteSupply(code, out errorMsg)) { MessageBox.Show(errorMsg, "Удаление невозможно", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

            if (MessageBox.Show($"Удалить поставку №{code}?\nТовары будут списаны со склада.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                try
                {
                    var items = context.Состав_поставки.Where(i => i.Код_поставки == code).ToList();
                    foreach (var item in items)
                    {
                        var p = context.Товары.Find(item.Код_товара);
                        if (p != null) { p.Количество -= item.Количество; if (p.Количество < 0) p.Количество = 0; }
                        context.Состав_поставки.Remove(item);
                    }
                    var supply = context.Поставка.Find(code);
                    if (supply != null) context.Поставка.Remove(supply);
                    context.SaveChanges();
                    MessageBox.Show("Поставка удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    LoadAllSupplies();
                    LoadGrandTotal();
                    ClearSupplyDetails();
                    ListViewSupplies.SelectedItem = null;
                    UpdateViewModeButtonsState();
                }
                catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
            }
        }

        private void PrintSupply_Click(object sender, RoutedEventArgs e)
        {
            var selected = ListViewSupplies.SelectedItem as SupplyViewModel;
            if (selected == null) { MessageBox.Show("Выберите поставку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            PrintSupply(selected.OriginalSupply.Код_поставки);
        }

        private void PrintSupply(int code)
        {
            try
            {
                var sfd = new SaveFileDialog
                {
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить поставку",
                    FileName = $"Поставка_{code}_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };
                if (sfd.ShowDialog() != true) return;

                var supply = context.Поставка
                    .Include(s => s.Сотрудники)
                    .Include(s => s.Поставщики)
                    .FirstOrDefault(s => s.Код_поставки == code);
                if (supply == null) return;

                var items = context.Состав_поставки
                    .Include(i => i.Товары)
                    .Where(i => i.Код_поставки == code)
                    .ToList()
                    .Select(i => new {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена_за_ед_покупка,
                        Сумма = i.Количество * i.Цена_за_ед_покупка
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

                    // Шапка отчёта (как в улучшенном варианте)
                    AddReportHeader(doc, bf, "ПОСТАВКА");

                    // Информация о поставке
                    var infoTable = new iTextSharp.text.pdf.PdfPTable(2) { WidthPercentage = 100 };
                    infoTable.SetWidths(new float[] { 50, 50 });
                    infoTable.SpacingAfter = 20;

                    AddInfoCell(infoTable, $"Поставка №: {code}", fBold, iTextSharp.text.Element.ALIGN_LEFT);
                    AddInfoCell(infoTable, $"Дата: {supply.Дата_оформления_постивки:dd.MM.yyyy HH:mm}", f, iTextSharp.text.Element.ALIGN_RIGHT);
                    AddInfoCell(infoTable, $"Сотрудник: {(supply.Сотрудники != null ? $"{supply.Сотрудники.Фамилия} {supply.Сотрудники.Имя}" : "—")}", f, iTextSharp.text.Element.ALIGN_LEFT, 2);
                    AddInfoCell(infoTable, $"Поставщик: {supply.Поставщики?.Наименование_поставщика ?? "—"}", f, iTextSharp.text.Element.ALIGN_LEFT, 2);

                    doc.Add(infoTable);

                    // Таблица товаров
                    var table = new iTextSharp.text.pdf.PdfPTable(4) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 40, 15, 20, 25 });

                    // Заголовки таблицы
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
                //MessageBox.Show($"Поставка №{code} сохранена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supplies = GetBaseQuery().OrderByDescending(s => s.Дата_оформления_постивки).ToList();
                if (!supplies.Any()) { MessageBox.Show("Нет данных.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information); return; }

                var sfd = new SaveFileDialog { Filter = "PDF файл (*.pdf)|*.pdf", Title = "Сохранить отчёт", FileName = $"Отчёт_поставки_{DateTime.Now:yyyy-MM-dd_HH-mm}" };
                if (sfd.ShowDialog() != true) return;

                var ids = supplies.Select(s => s.Код_поставки).ToList();
                var totals = context.Состав_поставки
                    .Where(i => ids.Contains(i.Код_поставки))
                    .GroupBy(i => i.Код_поставки)
                    .Select(g => new { Id = g.Key, Total = g.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0 })
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
                    doc.Add(new iTextSharp.text.Paragraph("ОТЧЁТ О ПОСТАВКАХ", fTitle) { Alignment = iTextSharp.text.Element.ALIGN_CENTER, SpacingAfter = 25 });

                    // Таблица
                    var table = new iTextSharp.text.pdf.PdfPTable(5) { WidthPercentage = 100 };
                    table.SetWidths(new float[] { 12, 22, 25, 25, 16 });
                    foreach (var h in new[] { "Код", "Дата", "Сотрудник", "Поставщик", "Сумма" })
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
                    foreach (var s in supplies)
                    {
                        var total = totals.TryGetValue(s.Код_поставки, out var t) ? t : 0;
                        var cells = new[] {
                    s.Код_поставки.ToString(),
                    s.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm"),
                    $"{s.Сотрудники?.Фамилия} {s.Сотрудники?.Имя}",
                    s.Поставщики?.Наименование_поставщика ?? "-",
                    $"{total:N2} ₽"
                };
                        for (int i = 0; i < cells.Length; i++)
                        {
                            var c = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], ftc)) { Padding = 5 };
                            if (alt) c.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                            if (i == 0 || i == 4) c.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                            table.AddCell(c);
                        }
                        alt = !alt;
                    }
                    doc.Add(table);

                    // Итоги
                    doc.Add(new iTextSharp.text.Paragraph($"Всего поставок: {supplies.Count} | Общая сумма: {grandTotal:N2} ₽", fSub) { SpacingBefore = 10, SpacingAfter = 35 });

                    // Подпись (как в улучшенном варианте)
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
                //MessageBox.Show($"Отчёт сохранён!\n{supplies.Count} поставок\nОбщая сумма: {grandTotal:N2} ₽", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }


        private void ClearForm_Click(object sender, RoutedEventArgs e) { ClearSupplyDetails(); ListViewSupplies.SelectedItem = null; UpdateViewModeButtonsState(); }

        private void LoadGrandTotal()
        {
            try
            {
                var query = context.Состав_поставки.AsQueryable();
                if (DateFrom.SelectedDate.HasValue)
                {
                    var dateFrom = DateFrom.SelectedDate.Value;
                    query = query.Where(i => i.Поставка.Дата_оформления_постивки >= dateFrom);
                }

                if (DateTo.SelectedDate.HasValue)
                {
                    var dateTo = DateTo.SelectedDate.Value.AddDays(1);
                    query = query.Where(i => i.Поставка.Дата_оформления_постивки < dateTo);
                }
                var empIds = PanelEmployees.Children.OfType<CheckBox>().Where(cb => cb.IsChecked == true && cb.Tag is int).Select(cb => (int)cb.Tag).ToList();
                if (empIds.Any()) query = query.Where(i => empIds.Contains(i.Поставка.Код_сотрудника));
                var supIds = PanelSuppliers.Children.OfType<CheckBox>().Where(cb => cb.IsChecked == true && cb.Tag is int).Select(cb => (int)cb.Tag).ToList();
                if (supIds.Any()) query = query.Where(i => supIds.Contains(i.Поставка.Код_поставщика));
                TxtGrandTotal.Text = $"{query.Sum(i => (decimal?)i.Количество * i.Цена_за_ед_покупка) ?? 0:N2} ₽";
            }
            catch { TxtGrandTotal.Text = "0.00 ₽"; }
        }
        #endregion

        #region Вспомогательные методы

        private BitmapImage LoadImageFromBytes(byte[] data)
        {
            if (data == null || data.Length == 0) return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            try
            {
                using (var ms = new MemoryStream(data)) { var bmp = new BitmapImage(); bmp.BeginInit(); bmp.StreamSource = ms; bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.EndInit(); bmp.Freeze(); return bmp; }
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

        private T FindParent<T>(DependencyObject child) where T : DependencyObject { var p = VisualTreeHelper.GetParent(child); while (p != null && !(p is T)) p = VisualTreeHelper.GetParent(p); return p as T; }

        private T FindChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            if (parent == null) return null;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var c = VisualTreeHelper.GetChild(parent, i); if (c is FrameworkElement fe && fe.Name == name && c is T t) return t; var f = FindChild<T>(c, name); if (f != null) return f; }
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

    public class SupplyViewModel
    {
        public Поставка OriginalSupply { get; set; }
        public decimal Total { get; set; }
        public string SupplyDisplay => $"Поставка №{OriginalSupply.Код_поставки}";
        public string DateDisplay => OriginalSupply.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm");
        public string EmployeeDisplay => OriginalSupply.Сотрудники != null ? $"👤 {OriginalSupply.Сотрудники.Фамилия} {OriginalSupply.Сотрудники.Имя}" : "👤 Не указан";
        public string SupplierDisplay => OriginalSupply.Поставщики != null ? $"🏭 {OriginalSupply.Поставщики.Наименование_поставщика}" : "🏭 Не указан";
        public string TotalDisplay => $"💰 {Total:N2} ₽";
        public SupplyViewModel(Поставка supply, decimal total) { OriginalSupply = supply; Total = total; }
    }

    public class ProductSupplyViewModel
    {
        public Товары OriginalProduct { get; set; }
        public string Наименование { get; set; }
        public decimal Цена_за_ед_продажа { get; set; }
        public int Количество { get; set; }
        public string CategoryName { get; set; }
        public BitmapImage PhotoSource { get; set; }
        public string PriceDisplay => $"{Цена_за_ед_продажа:N2} ₽ (закуп)";
        public string QuantityDisplay => $"📦 На складе: {Количество} шт.";

        public ProductSupplyViewModel(Товары product)
        {
            OriginalProduct = product; 
            Наименование = product.Наименование; 
            Цена_за_ед_продажа = product.Цена_за_ед_продажа; 
            Количество = product.Количество;
            CategoryName = product.Категории?.Категория != null ? $"📂 {product.Категории.Категория}" : "📂 Без категории";
            PhotoSource = LoadImage(product.Фото);
        }

        private static BitmapImage LoadImage(byte[] data)
        {
            if (data == null || data.Length == 0) return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            try { using (var ms = new MemoryStream(data)) { var bmp = new BitmapImage(); bmp.BeginInit(); bmp.StreamSource = ms; bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.EndInit(); bmp.Freeze(); return bmp; } }
            catch { return new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute)); }
        }
    }

    #endregion
}