using Diplomn.Addons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления брендами товаров
    /// </summary>
    public partial class BrandsPage : Page
    {
        #region Поля

        private BDEntities context;
        private AccessManager.AccessRights rights;
        private WrapPanel actionButtonsPanel;

        // Контейнеры для кнопок
        private Grid addButtonContainer;
        private Grid editButtonContainer;
        private Grid deleteButtonContainer;
        private Grid clearButtonContainer;

        #endregion

        #region Конструктор

        public BrandsPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            rights = AccessManager.GetAccessRights(user.Должность?.Уровень_доступа ?? 10);
            actionButtonsPanel = FindName("ActionButtonsPanel") as WrapPanel;

            CreateActionButtons();
            SubscribeToFieldChanges();

            LoadData();
            UpdateButtonsState();
        }

        #endregion

        #region Подписка на изменения полей

        private void SubscribeToFieldChanges()
        {
            TxtBrandName.TextChanged += (s, e) => UpdateButtonsState();
        }

        #endregion

        #region Создание кнопок

        private void CreateActionButtons()
        {
            if (actionButtonsPanel == null) return;
            actionButtonsPanel.Children.Clear();

            if (rights.Brands.CanCreate)
            {
                var (button, overlay) = CreateButtonWithOverlay("➕ Добавить", Add_Click, 110);
                addButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(addButtonContainer);
            }

            if (rights.Brands.CanEdit)
            {
                var (button, overlay) = CreateButtonWithOverlay("✏️ Обновить", Update_Click, 110);
                editButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(editButtonContainer);
            }

            if (rights.Brands.CanDelete)
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
                return "Нажмите для добавления бренда";
            }

            if (buttonContent.Contains("Обновить"))
            {
                if (DataGridBrands.SelectedItem == null)
                    return "Выберите бренд из таблицы";
                var missing = GetMissingRequiredFields();
                if (missing.Any())
                    return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                return "Нажмите для обновления бренда";
            }

            if (buttonContent.Contains("Удалить"))
                return "Выберите бренд из таблицы для удаления";

            if (buttonContent.Contains("Очистить"))
                return "Очистить все поля формы";

            return "Кнопка недоступна";
        }

        #endregion

        #region Валидация полей

        private List<string> GetMissingRequiredFields()
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(GetActualText(TxtBrandName)))
                missing.Add("Наименование бренда");

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
            bool isBrandSelected = DataGridBrands.SelectedItem != null;
            bool requiredFieldsFilled = AreRequiredFieldsFilled();

            SetButtonState(addButtonContainer, requiredFieldsFilled);
            SetButtonState(editButtonContainer, isBrandSelected && requiredFieldsFilled);
            SetButtonState(deleteButtonContainer, isBrandSelected);
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

        private void LoadData()
        {
            DataGridBrands.ItemsSource = context.Бренд.ToList();
        }

        private IQueryable<Бренд> GetFilteredQuery()
        {
            var query = context.Бренд.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(b => b.Наименование_бредна.Contains(searchText));

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters()
        {
            DataGridBrands.ItemsSource = GetFilteredQuery().ToList();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            LoadData();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyFilters();
        }

        #endregion

        #region Выбор в таблице

        private void DataGridBrands_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridBrands.SelectedItem is Бренд brand)
            {
                TxtBrandId.Text = brand.Код_бренда.ToString();
                TxtBrandName.Text = brand.Наименование_бредна;
            }

            UpdateButtonsState();
        }

        #endregion

        #region Валидация

        private bool ValidateBrand(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            var name = GetActualText(TxtBrandName);

            if (string.IsNullOrWhiteSpace(name))
                errors.AppendLine("• Введите наименование бренда");
            else if (name.Length < 2)
                errors.AppendLine("• Название должно быть не короче 2 символов");
            else if (name.Length > 50)
                errors.AppendLine("• Название не должно превышать 50 символов");
            else
            {
                var exists = excludeId.HasValue
                    ? context.Бренд.Any(b => b.Наименование_бредна == name && b.Код_бренда != excludeId.Value)
                    : context.Бренд.Any(b => b.Наименование_бредна == name);

                if (exists)
                    errors.AppendLine("• Бренд с таким названием уже существует");
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
                if (!ValidateBrand(out var error))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var brand = new Бренд { Наименование_бредна = GetActualText(TxtBrandName) };
                context.Бренд.Add(brand);
                context.SaveChanges();

                MessageBox.Show("Бренд добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtBrandId.Text))
                {
                    MessageBox.Show("Выберите бренд!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var brandId = int.Parse(TxtBrandId.Text);
                var brand = context.Бренд.Find(brandId);

                if (brand == null)
                {
                    MessageBox.Show("Бренд не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateBrand(out var error, brandId))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                brand.Наименование_бредна = GetActualText(TxtBrandName);
                context.SaveChanges();

                MessageBox.Show("Бренд обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtBrandId.Text))
                {
                    MessageBox.Show("Выберите бренд!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var brandId = int.Parse(TxtBrandId.Text);
                var brand = context.Бренд.Find(brandId);

                if (brand == null)
                {
                    MessageBox.Show("Бренд не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Товары.Any(p => p.Код_бренда == brandId))
                {
                    MessageBox.Show("Нельзя удалить бренд — есть связанные товары!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить бренд «{brand.Наименование_бредна}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Бренд.Remove(brand);
                    context.SaveChanges();
                    MessageBox.Show("Бренд удалён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
            TxtBrandId.Text = "";
            TxtBrandName.Text = "";
            DataGridBrands.SelectedItem = null;

            UpdateButtonsState();
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

        #endregion

        #region Вспомогательные методы

        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;
            var placeholder = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;
            return (!string.IsNullOrEmpty(placeholder) && text == placeholder) ? string.Empty : text;
        }

        #endregion
    }
}