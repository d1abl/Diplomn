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
    /// Страница управления материалами товаров
    /// </summary>
    public partial class MaterialsPage : Page
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

        public MaterialsPage(Сотрудники user)
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
            TxtMaterialName.TextChanged += OnFieldTextChanged;
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

            if (rights.Materials.CanCreate)
            {
                var (button, overlay) = CreateButtonWithOverlay("➕ Добавить", Add_Click, 110);
                addButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(addButtonContainer);
            }

            if (rights.Materials.CanEdit)
            {
                var (button, overlay) = CreateButtonWithOverlay("✏️ Обновить", Update_Click, 110);
                editButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(editButtonContainer);
            }

            if (rights.Materials.CanDelete)
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
                return "Нажмите для добавления материала";
            }

            if (buttonContent.Contains("Обновить"))
            {
                if (DataGridMaterials.SelectedItem == null)
                    return "Выберите материал из таблицы";
                var missing = GetMissingRequiredFields();
                if (missing.Any())
                    return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                return "Нажмите для обновления материала";
            }

            if (buttonContent.Contains("Удалить"))
                return "Выберите материал из таблицы для удаления";

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
            var controls = new Control[] { TxtMaterialName };
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

            if (string.IsNullOrWhiteSpace(GetActualText(TxtMaterialName)))
                missing.Add("Наименование материала");

            return missing;
        }

        private bool AreRequiredFieldsFilled()
        {
            return !GetMissingRequiredFields().Any();
        }

        /// <summary>
        /// Проверяет корректность введённых данных
        /// </summary>
        private bool ValidateMaterial(out string errorMessage, int? excludeId = null)
        {
            var errors = new List<string>();
            var errorFields = new Dictionary<Control, string>();
            var name = GetActualText(TxtMaterialName);

            // Сбрасываем подсветку
            ClearAllHighlights();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("• Введите наименование материала");
                errorFields[TxtMaterialName] = "Наименование материала обязательно для заполнения";
            }
            else if (name.Length < 2)
            {
                errors.Add("• Название должно быть не короче 2 символов");
                errorFields[TxtMaterialName] = "Название материала должно содержать минимум 2 символа";
            }
            else if (name.Length > 50)
            {
                errors.Add("• Название не должно превышать 50 символов");
                errorFields[TxtMaterialName] = "Название материала не должно превышать 50 символов";
            }
            else
            {
                var exists = excludeId.HasValue
                    ? context.Материал.Any(m => m.Наименование_материала == name && m.Код_материала != excludeId.Value)
                    : context.Материал.Any(m => m.Наименование_материала == name);

                if (exists)
                {
                    errors.Add("• Материал с таким названием уже существует");
                    errorFields[TxtMaterialName] = "Материал с таким названием уже существует";
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
            bool isMaterialSelected = DataGridMaterials.SelectedItem != null;
            bool requiredFieldsFilled = AreRequiredFieldsFilled();

            SetButtonState(addButtonContainer, requiredFieldsFilled);
            SetButtonState(editButtonContainer, isMaterialSelected && requiredFieldsFilled);
            SetButtonState(deleteButtonContainer, isMaterialSelected);
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
            DataGridMaterials.ItemsSource = context.Материал.ToList();
        }

        private IQueryable<Материал> GetFilteredQuery()
        {
            var query = context.Материал.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(m => m.Наименование_материала.Contains(searchText));

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters() => DataGridMaterials.ItemsSource = GetFilteredQuery().ToList();
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

        private void DataGridMaterials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridMaterials.SelectedItem is Материал material)
            {
                TxtMaterialId.Text = material.Код_материала.ToString();
                TxtMaterialName.Text = material.Наименование_материала;
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
                if (!ValidateMaterial(out var error))
                {
                    return;
                }

                var item = new Материал { Наименование_материала = GetActualText(TxtMaterialName) };
                context.Материал.Add(item);
                context.SaveChanges();

                ShowSuccess($"Материал «{item.Наименование_материала}» добавлен!");
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
                if (string.IsNullOrWhiteSpace(TxtMaterialId.Text))
                {
                    MessageBox.Show("Выберите материал!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtMaterialId.Text);
                var item = context.Материал.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Материал не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateMaterial(out var error, id))
                {
                    return;
                }

                var newName = GetActualText(TxtMaterialName);
                var oldName = item.Наименование_материала;
                item.Наименование_материала = newName;
                context.SaveChanges();

                ShowSuccess($"Материал обновлён с «{oldName}» на «{newName}»!");
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
                if (string.IsNullOrWhiteSpace(TxtMaterialId.Text))
                {
                    MessageBox.Show("Выберите материал!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtMaterialId.Text);
                var item = context.Материал.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Материал не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Товары.Any(p => p.Код_материала == id))
                {
                    MessageBox.Show("Нельзя удалить материал — есть связанные товары!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить материал «{item.Наименование_материала}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var name = item.Наименование_материала;
                    context.Материал.Remove(item);
                    context.SaveChanges();
                    ShowSuccess($"Материал «{name}» удалён!");
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
            TxtMaterialId.Text = "";
            TxtMaterialName.Text = "";
            DataGridMaterials.SelectedItem = null;
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