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
    /// Страница управления фасовкой товаров
    /// </summary>
    public partial class PackingsPage : Page
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

        public PackingsPage(Сотрудники user)
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
            TxtPackingQuantity.TextChanged += OnFieldTextChanged;
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

            if (rights.Packings.CanCreate)
            {
                var (button, overlay) = CreateButtonWithOverlay("➕ Добавить", Add_Click, 110);
                addButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(addButtonContainer);
            }

            if (rights.Packings.CanEdit)
            {
                var (button, overlay) = CreateButtonWithOverlay("✏️ Обновить", Update_Click, 110);
                editButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(editButtonContainer);
            }

            if (rights.Packings.CanDelete)
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
                return "Нажмите для добавления фасовки";
            }

            if (buttonContent.Contains("Обновить"))
            {
                if (DataGridPackings.SelectedItem == null)
                    return "Выберите фасовку из таблицы";
                var missing = GetMissingRequiredFields();
                if (missing.Any())
                    return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                return "Нажмите для обновления фасовки";
            }

            if (buttonContent.Contains("Удалить"))
                return "Выберите фасовку из таблицы для удаления";

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
            var controls = new Control[] { TxtPackingQuantity };
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

            var quantityText = GetActualText(TxtPackingQuantity);
            if (string.IsNullOrWhiteSpace(quantityText) || !int.TryParse(quantityText, out int qty) || qty <= 0)
                missing.Add("Количество (целое число > 0)");

            return missing;
        }

        private bool AreRequiredFieldsFilled()
        {
            return !GetMissingRequiredFields().Any();
        }

        /// <summary>
        /// Проверяет корректность введённых данных
        /// </summary>
        private bool ValidatePacking(out string errorMessage, int? excludeId = null)
        {
            var errors = new List<string>();
            var errorFields = new Dictionary<Control, string>();
            var quantityText = GetActualText(TxtPackingQuantity);

            // Сбрасываем подсветку
            ClearAllHighlights();

            if (string.IsNullOrWhiteSpace(quantityText))
            {
                errors.Add("• Введите количество");
                errorFields[TxtPackingQuantity] = "Количество обязательно для заполнения";
            }
            else if (!int.TryParse(quantityText, out int qty))
            {
                errors.Add("• Количество должно быть целым числом");
                errorFields[TxtPackingQuantity] = "Количество должно быть целым числом";
            }
            else if (qty <= 0)
            {
                errors.Add("• Количество должно быть больше 0");
                errorFields[TxtPackingQuantity] = "Количество должно быть больше 0";
            }
            else if (qty > 100000)
            {
                errors.Add("• Количество не должно превышать 100 000");
                errorFields[TxtPackingQuantity] = "Количество не должно превышать 100 000";
            }
            else
            {
                var exists = excludeId.HasValue
                    ? context.Фасовка.Any(p => p.Количество == qty && p.Код_фасовки != excludeId.Value)
                    : context.Фасовка.Any(p => p.Количество == qty);

                if (exists)
                {
                    errors.Add("• Фасовка с таким количеством уже существует");
                    errorFields[TxtPackingQuantity] = "Фасовка с таким количеством уже существует";
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
            bool isPackingSelected = DataGridPackings.SelectedItem != null;
            bool requiredFieldsFilled = AreRequiredFieldsFilled();

            SetButtonState(addButtonContainer, requiredFieldsFilled);
            SetButtonState(editButtonContainer, isPackingSelected && requiredFieldsFilled);
            SetButtonState(deleteButtonContainer, isPackingSelected);
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
            DataGridPackings.ItemsSource = context.Фасовка.ToList();
        }

        private IQueryable<Фасовка> GetFilteredQuery()
        {
            var query = context.Фасовка.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText) && int.TryParse(searchText, out int qty))
                query = query.Where(p => p.Количество == qty);

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters() => DataGridPackings.ItemsSource = GetFilteredQuery().ToList();
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

        private void DataGridPackings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridPackings.SelectedItem is Фасовка packing)
            {
                TxtPackingId.Text = packing.Код_фасовки.ToString();
                TxtPackingQuantity.Text = packing.Количество.ToString();
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
                if (!ValidatePacking(out var error))
                {
                    return;
                }

                var qty = int.Parse(GetActualText(TxtPackingQuantity));
                var item = new Фасовка { Количество = qty };
                context.Фасовка.Add(item);
                context.SaveChanges();

                ShowSuccess($"Фасовка «{qty}» добавлена!");
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
                if (string.IsNullOrWhiteSpace(TxtPackingId.Text))
                {
                    MessageBox.Show("Выберите фасовку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtPackingId.Text);
                var item = context.Фасовка.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Фасовка не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidatePacking(out var error, id))
                {
                    return;
                }

                var newQty = int.Parse(GetActualText(TxtPackingQuantity));
                var oldQty = item.Количество;
                item.Количество = newQty;
                context.SaveChanges();

                ShowSuccess($"Фасовка обновлена с «{oldQty}» на «{newQty}»!");
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
                if (string.IsNullOrWhiteSpace(TxtPackingId.Text))
                {
                    MessageBox.Show("Выберите фасовку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtPackingId.Text);
                var item = context.Фасовка.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Фасовка не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Товары.Any(p => p.Код_фасовки == id))
                {
                    MessageBox.Show("Нельзя удалить фасовку — есть связанные товары!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить фасовку «{item.Количество}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var qty = item.Количество;
                    context.Фасовка.Remove(item);
                    context.SaveChanges();
                    ShowSuccess($"Фасовка «{qty}» удалена!");
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
            TxtPackingId.Text = "";
            TxtPackingQuantity.Text = "";
            DataGridPackings.SelectedItem = null;
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