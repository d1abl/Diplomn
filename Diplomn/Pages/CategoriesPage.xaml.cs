using Diplomn.Addons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления категориями товаров
    /// </summary>
    public partial class CategoriesPage : Page
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

        // Таймер для уведомлений
        private DispatcherTimer _successTimer;

        #endregion

        #region Конструктор

        public CategoriesPage(Сотрудники user)
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
            TxtCategoryName.TextChanged += OnFieldTextChanged;
            TxtDescription.TextChanged += OnFieldTextChanged;
        }

        /// <summary>
        /// Сбрасывает подсветку ошибок при изменении текста в поле
        /// </summary>
        private void OnFieldTextChanged(object sender, EventArgs e)
        {
            if (sender is Control control)
            {
                control.BorderBrush = SystemColors.ControlDarkBrush;
                control.BorderThickness = new Thickness(1);
                control.ToolTip = null;
            }
            UpdateButtonsState();
        }

        #endregion

        #region Создание кнопок

        private void CreateActionButtons()
        {
            if (actionButtonsPanel == null) return;
            actionButtonsPanel.Children.Clear();

            if (rights.Categories.CanCreate)
            {
                var (button, overlay) = CreateButtonWithOverlay("➕ Добавить", Add_Click, 110);
                addButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(addButtonContainer);
            }

            if (rights.Categories.CanEdit)
            {
                var (button, overlay) = CreateButtonWithOverlay("✏️ Обновить", Update_Click, 110);
                editButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(editButtonContainer);
            }

            if (rights.Categories.CanDelete)
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
                return "Нажмите для добавления категории";
            }

            if (buttonContent.Contains("Обновить"))
            {
                if (DataGridCategories.SelectedItem == null)
                    return "Выберите категорию из таблицы";
                var missing = GetMissingRequiredFields();
                if (missing.Any())
                    return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                return "Нажмите для обновления категории";
            }

            if (buttonContent.Contains("Удалить"))
                return "Выберите категорию из таблицы для удаления";

            if (buttonContent.Contains("Очистить"))
                return "Очистить все поля формы";

            return "Кнопка недоступна";
        }

        #endregion

        #region Валидация полей

        /// <summary>
        /// Подсвечивает поле с ошибкой
        /// </summary>
        private void HighlightError(Control control, string errorMessage)
        {
            control.BorderBrush = Brushes.Red;
            control.BorderThickness = new Thickness(2);
            control.ToolTip = errorMessage;
        }

        /// <summary>
        /// Сбрасывает подсветку всех полей
        /// </summary>
        private void ClearAllHighlights()
        {
            var controls = new Control[] { TxtCategoryName };
            foreach (var control in controls)
            {
                if (control != null)
                {
                    control.BorderBrush = SystemColors.ControlDarkBrush;
                    control.BorderThickness = new Thickness(1);
                    control.ToolTip = null;
                }
            }
        }

        private List<string> GetMissingRequiredFields()
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(GetActualText(TxtCategoryName)))
                missing.Add("Название категории");

            return missing;
        }

        private bool AreRequiredFieldsFilled()
        {
            return !GetMissingRequiredFields().Any();
        }

        /// <summary>
        /// Проверяет корректность введённых данных
        /// </summary>
        private bool ValidateCategory(out string errorMessage, int? excludeId = null)
        {
            var errors = new List<string>();
            var errorFields = new Dictionary<Control, string>();
            var name = GetActualText(TxtCategoryName);

            // Сбрасываем подсветку
            ClearAllHighlights();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("• Введите название категории");
                errorFields[TxtCategoryName] = "Название категории обязательно для заполнения";
            }
            else if (name.Length < 2)
            {
                errors.Add("• Название должно быть не короче 2 символов");
                errorFields[TxtCategoryName] = "Название категории должно содержать минимум 2 символа";
            }
            else if (name.Length > 40)
            {
                errors.Add("• Название не должно превышать 40 символов");
                errorFields[TxtCategoryName] = "Название категории не должно превышать 40 символов";
            }
            else if (!Regex.IsMatch(name, @"^[A-Za-zА-Яа-яЁё\-\s]+$"))
            {
                errors.Add("• Название содержит недопустимые символы");
                errorFields[TxtCategoryName] = "Название категории может содержать только буквы, дефис и пробелы";
            }
            else
            {
                var exists = excludeId.HasValue
                    ? context.Категории.Any(c => c.Категория == name && c.Код_категория != excludeId.Value)
                    : context.Категории.Any(c => c.Категория == name);

                if (exists)
                {
                    errors.Add("• Категория с таким названием уже существует");
                    errorFields[TxtCategoryName] = "Категория с таким названием уже существует";
                }
            }

            // Подсвечиваем поля с ошибками
            foreach (var field in errorFields)
            {
                HighlightError(field.Key, field.Value);
            }

            errorMessage = string.Join(Environment.NewLine, errors);
            return errors.Count == 0;
        }

        #endregion

        #region Управление состоянием кнопок

        private void UpdateButtonsState()
        {
            bool isCategorySelected = DataGridCategories.SelectedItem != null;
            bool requiredFieldsFilled = AreRequiredFieldsFilled();

            SetButtonState(addButtonContainer, requiredFieldsFilled);
            SetButtonState(editButtonContainer, isCategorySelected && requiredFieldsFilled);
            SetButtonState(deleteButtonContainer, isCategorySelected);
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
            DataGridCategories.ItemsSource = context.Категории.ToList();
        }

        private IQueryable<Категории> GetFilteredQuery()
        {
            var query = context.Категории.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(c => c.Категория.Contains(searchText) ||
                                        c.Описание_категории.Contains(searchText));

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters()
        {
            DataGridCategories.ItemsSource = GetFilteredQuery().ToList();
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

        private void DataGridCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridCategories.SelectedItem is Категории category)
            {
                TxtCategoryId.Text = category.Код_категория.ToString();
                TxtCategoryName.Text = category.Категория;
                TxtDescription.Text = category.Описание_категории;
            }
            else
            {
                ClearForm();
            }

            // Сбрасываем подсветку при выборе другого элемента
            ClearAllHighlights();
            UpdateButtonsState();
        }

        #endregion

        #region CRUD операции

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateCategory(out var error))
                {
                    // Не показываем MessageBox, поля уже подсвечены
                    return;
                }

                var category = new Категории
                {
                    Категория = GetActualText(TxtCategoryName),
                    Описание_категории = GetActualText(TxtDescription)
                };

                context.Категории.Add(category);
                context.SaveChanges();

                ShowSuccess($"Категория «{category.Категория}» добавлена!");
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtCategoryId.Text))
                {
                    MessageBox.Show("Выберите категорию!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var categoryId = int.Parse(TxtCategoryId.Text);
                var category = context.Категории.Find(categoryId);

                if (category == null)
                {
                    MessageBox.Show("Категория не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateCategory(out var error, categoryId))
                {
                    return;
                }

                var newName = GetActualText(TxtCategoryName);
                var oldName = category.Категория;
                category.Категория = newName;
                category.Описание_категории = GetActualText(TxtDescription);
                context.SaveChanges();

                ShowSuccess($"Категория обновлена с «{oldName}» на «{newName}»!");
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtCategoryId.Text))
                {
                    MessageBox.Show("Выберите категорию!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var categoryId = int.Parse(TxtCategoryId.Text);
                var category = context.Категории.Find(categoryId);

                if (category == null)
                {
                    MessageBox.Show("Категория не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Товары.Any(p => p.Код_категория == categoryId))
                {
                    MessageBox.Show("Нельзя удалить категорию — есть связанные товары!\nСначала переназначьте или удалите их.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить категорию «{category.Категория}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var categoryName = category.Категория;
                    context.Категории.Remove(category);
                    context.SaveChanges();
                    ShowSuccess($"Категория «{categoryName}» удалена!");
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Очистка формы

        private void ClearForm()
        {
            TxtCategoryId.Text = "";
            TxtCategoryName.Text = "";
            TxtDescription.Text = "";
            DataGridCategories.SelectedItem = null;
            ClearAllHighlights();

            UpdateButtonsState();
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

        #endregion

        #region Уведомления

        /// <summary>
        /// Показывает сообщение об успехе с автоматическим скрытием
        /// </summary>
        private void ShowSuccess(string message)
        {
            SuccessText.Text = message;
            SuccessBorder.Visibility = Visibility.Visible;

            _successTimer?.Stop();
            _successTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _successTimer.Tick += (s, e) =>
            {
                SuccessBorder.Visibility = Visibility.Collapsed;
                _successTimer.Stop();
            };
            _successTimer.Start();
        }

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