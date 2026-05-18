using Diplomn.Addons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления должностями и уровнями доступа
    /// </summary>
    public partial class RolePage : Page
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

        public RolePage(Сотрудники user)
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
            TxtRoleName.TextChanged += OnFieldTextChanged;
            TxtAccessLevel.TextChanged += OnFieldTextChanged;
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

            if (rights.Roles.CanCreate)
            {
                var (button, overlay) = CreateButtonWithOverlay("➕ Добавить", Add_Click, 110);
                addButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(addButtonContainer);
            }

            if (rights.Roles.CanEdit)
            {
                var (button, overlay) = CreateButtonWithOverlay("✏️ Обновить", Update_Click, 110);
                editButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(editButtonContainer);
            }

            if (rights.Roles.CanDelete)
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
                return "Нажмите для добавления должности";
            }

            if (buttonContent.Contains("Обновить"))
            {
                if (DataGridRoles.SelectedItem == null)
                    return "Выберите должность из таблицы";
                var missing = GetMissingRequiredFields();
                if (missing.Any())
                    return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                return "Нажмите для обновления должности";
            }

            if (buttonContent.Contains("Удалить"))
                return "Выберите должность из таблицы для удаления";

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
            var controls = new Control[] { TxtRoleName, TxtAccessLevel };
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

            if (string.IsNullOrWhiteSpace(GetActualText(TxtRoleName)))
                missing.Add("Название должности");

            var levelText = GetActualText(TxtAccessLevel);
            if (string.IsNullOrWhiteSpace(levelText) || !int.TryParse(levelText, out int level) || level < 1 || level > 10)
                missing.Add("Уровень доступа (1-10)");

            return missing;
        }

        private bool AreRequiredFieldsFilled()
        {
            return !GetMissingRequiredFields().Any();
        }

        /// <summary>
        /// Проверяет корректность введённых данных
        /// </summary>
        private bool ValidateRole(out string errorMessage, int? excludeId = null)
        {
            var errors = new List<string>();
            var errorFields = new Dictionary<Control, string>();
            var name = GetActualText(TxtRoleName);
            var levelText = GetActualText(TxtAccessLevel);

            // Сбрасываем подсветку
            ClearAllHighlights();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("• Введите название должности");
                errorFields[TxtRoleName] = "Название должности обязательно для заполнения";
            }
            else if (name.Length < 2)
            {
                errors.Add("• Название должно быть не короче 2 символов");
                errorFields[TxtRoleName] = "Название должности должно содержать минимум 2 символа";
            }
            else if (name.Length > 50)
            {
                errors.Add("• Название не должно превышать 50 символов");
                errorFields[TxtRoleName] = "Название должности не должно превышать 50 символов";
            }
            else
            {
                var exists = excludeId.HasValue
                    ? context.Должность.Any(r => r.Название == name && r.Код_должности != excludeId.Value)
                    : context.Должность.Any(r => r.Название == name);

                if (exists)
                {
                    errors.Add("• Должность с таким названием уже существует");
                    errorFields[TxtRoleName] = "Должность с таким названием уже существует";
                }
            }

            if (string.IsNullOrWhiteSpace(levelText))
            {
                errors.Add("• Введите уровень доступа");
                errorFields[TxtAccessLevel] = "Уровень доступа обязателен для заполнения";
            }
            else if (!int.TryParse(levelText, out int level))
            {
                errors.Add("• Уровень доступа должен быть числом");
                errorFields[TxtAccessLevel] = "Уровень доступа должен быть целым числом";
            }
            else if (level < 1 || level > 10)
            {
                errors.Add("• Уровень доступа должен быть от 1 до 10");
                errorFields[TxtAccessLevel] = "Уровень доступа должен быть в диапазоне от 1 до 10";
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
            bool isRoleSelected = DataGridRoles.SelectedItem != null;
            bool requiredFieldsFilled = AreRequiredFieldsFilled();

            SetButtonState(addButtonContainer, requiredFieldsFilled);
            SetButtonState(editButtonContainer, isRoleSelected && requiredFieldsFilled);
            SetButtonState(deleteButtonContainer, isRoleSelected);
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
            DataGridRoles.ItemsSource = context.Должность.ToList();
        }

        private IQueryable<Должность> GetFilteredQuery()
        {
            var query = context.Должность.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(r => r.Название.Contains(searchText));

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters() => DataGridRoles.ItemsSource = GetFilteredQuery().ToList();
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

        private void DataGridRoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridRoles.SelectedItem is Должность role)
            {
                TxtRoleId.Text = role.Код_должности.ToString();
                TxtRoleName.Text = role.Название;
                TxtAccessLevel.Text = role.Уровень_доступа.ToString();
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
                if (!ValidateRole(out var error))
                {
                    return;
                }

                var role = new Должность
                {
                    Название = GetActualText(TxtRoleName),
                    Уровень_доступа = int.Parse(GetActualText(TxtAccessLevel))
                };

                context.Должность.Add(role);
                context.SaveChanges();

                ShowSuccess($"Должность «{role.Название}» добавлена!");
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
                if (string.IsNullOrWhiteSpace(TxtRoleId.Text))
                {
                    MessageBox.Show("Выберите должность!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtRoleId.Text);
                var role = context.Должность.Find(id);

                if (role == null)
                {
                    MessageBox.Show("Должность не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateRole(out var error, id))
                {
                    return;
                }

                var newName = GetActualText(TxtRoleName);
                var oldName = role.Название;
                role.Название = newName;
                role.Уровень_доступа = int.Parse(GetActualText(TxtAccessLevel));
                context.SaveChanges();

                ShowSuccess($"Должность обновлена с «{oldName}» на «{newName}»!");
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
                if (string.IsNullOrWhiteSpace(TxtRoleId.Text))
                {
                    MessageBox.Show("Выберите должность!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtRoleId.Text);
                var role = context.Должность.Find(id);

                if (role == null)
                {
                    MessageBox.Show("Должность не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Сотрудники.Any(s => s.Код_должности == id))
                {
                    MessageBox.Show("Нельзя удалить должность — есть сотрудники с этой должностью!\nСначала переназначьте их.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить должность «{role.Название}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var roleName = role.Название;
                    context.Должность.Remove(role);
                    context.SaveChanges();
                    ShowSuccess($"Должность «{roleName}» удалена!");
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
            TxtRoleId.Text = "";
            TxtRoleName.Text = "";
            TxtAccessLevel.Text = "";
            DataGridRoles.SelectedItem = null;
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