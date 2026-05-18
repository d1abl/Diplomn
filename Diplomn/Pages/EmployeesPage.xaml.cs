using Diplomn.Addons;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления сотрудниками магазина
    /// </summary>
    public partial class EmployeesPage : Page
    {
        #region Поля

        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;
        private ObservableCollection<EmployeeViewModel> employeesView;
        private AccessManager.AccessRights rights;
        private WrapPanel actionButtonsPanel;

        // Ссылки на кнопки
        private Button addButton;
        private Button editButton;
        private Button deleteButton;
        private Button clearButton;

        // Ссылки на overlay-элементы для tooltip
        private Border addButtonOverlay;
        private Border editButtonOverlay;
        private Border deleteButtonOverlay;
        private Border clearButtonOverlay;

        // Таймер для уведомлений
        private DispatcherTimer _successTimer;

        #endregion

        #region Конструктор

        public EmployeesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Сотрудники — {user.Фамилия} {user.Имя}";
            employeesView = new ObservableCollection<EmployeeViewModel>();

            rights = AccessManager.GetAccessRights(user.Должность?.Уровень_доступа ?? 10);
            actionButtonsPanel = FindName("ActionButtonsPanel") as WrapPanel;

            CreateActionButtons();
            SubscribeToFieldChanges();

            LoadPositions();
            LoadData();

            UpdateButtonsState();
        }

        #endregion

        #region Загрузка данных

        /// <summary>
        /// Загружает список должностей в выпадающий список
        /// </summary>
        private void LoadPositions()
        {
            CmbPosition.ItemsSource = context.Должность.ToList();
        }

        /// <summary>
        /// Формирует запрос с учётом поискового фильтра
        /// </summary>
        private IQueryable<Сотрудники> GetFilteredQuery()
        {
            var query = context.Сотрудники.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(e => e.Фамилия.Contains(searchText) ||
                                        e.Имя.Contains(searchText) ||
                                        e.Логин.Contains(searchText));
            }

            return query;
        }

        /// <summary>
        /// Загружает всех сотрудников без фильтрации
        /// </summary>
        private void LoadData()
        {
            var employees = context.Сотрудники.Include("Должность").ToList();
            UpdateEmployeesView(employees);
        }

        /// <summary>
        /// Обновляет коллекцию ViewModel для отображения в карточках
        /// </summary>
        private void UpdateEmployeesView(List<Сотрудники> employees)
        {
            employeesView.Clear();
            foreach (var employee in employees)
                employeesView.Add(new EmployeeViewModel(employee));

            ListViewEmployees.ItemsSource = employeesView;
        }

        #endregion

        #region Фильтрация и отчёты

        private void ApplyFilters()
        {
            var employees = GetFilteredQuery().Include("Должность").ToList();
            UpdateEmployeesView(employees);
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyFilters();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            LoadData();
        }

        /// <summary>
        /// Сохраняет отчёт о сотрудниках в PDF
        /// </summary>
        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var employees = GetFilteredQuery().Include("Должность").ToList();

                if (!employees.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить отчет о сотрудниках",
                    FileName = $"Отчет_сотрудники_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() != true) return;

                // Данные магазина
                const string shopName = "Oculus+";
                const string shopPhone = "+7 (461) 345 12-34";
                const string shopEmail = "Oculus@глаза.ру";
                const string shopWebsite = "Oculus.ру";
                const string shopHours = "9:00 – 17:00 ежедневно";

                var initials = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
                if (!string.IsNullOrWhiteSpace(currentUser.Отчество))
                    initials += $"{currentUser.Отчество?.Substring(0, 1)}.";

                var totalEmployees = employees.Count;
                var adminCount = employees.Count(emp => emp.Должность?.Уровень_доступа == 1);
                var managerCount = employees.Count(emp => emp.Должность?.Уровень_доступа > 1 && emp.Должность?.Уровень_доступа <= 3);
                var staffCount = employees.Count(emp => emp.Должность?.Уровень_доступа > 3 || emp.Должность == null);

                using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 50, 50))
                {
                    using (var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, new FileStream(saveFileDialog.FileName, FileMode.Create)))
                    {
                        document.Open();

                        var fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                        var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

                        var fontTitle = new iTextSharp.text.Font(baseFont, 16, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
                        var fontSubtitle = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontTableHeader = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE);
                        var fontTableCell = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);
                        var fontFooter = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);
                        var fontSmall = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontSign = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);

                        var reportTitle = new iTextSharp.text.Paragraph("ОТЧЁТ О СОТРУДНИКАХ", fontTitle);
                        reportTitle.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        reportTitle.SpacingAfter = 25;
                        document.Add(reportTitle);

                        var table = new iTextSharp.text.pdf.PdfPTable(7);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 8, 18, 14, 14, 16, 14, 16 });
                        table.SpacingBefore = 10;
                        table.SpacingAfter = 25;

                        var headers = new[] { "Код", "Фамилия", "Имя", "Отчество", "Должность", "Телефон", "Логин" };
                        foreach (var header in headers)
                        {
                            var headerCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(header, fontTableHeader));
                            headerCell.BackgroundColor = new iTextSharp.text.BaseColor(0, 51, 102);
                            headerCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                            headerCell.Padding = 5;
                            table.AddCell(headerCell);
                        }

                        bool alternate = false;
                        foreach (var employee in employees)
                        {
                            var cells = new[]
                            {
                                employee.Код_сотрудника.ToString(),
                                employee.Фамилия,
                                employee.Имя,
                                employee.Отчество ?? "-",
                                employee.Должность?.Название ?? "-",
                                employee.Телефон ?? "-",
                                employee.Логин
                            };

                            var centerColumns = new HashSet<int> { 0, 5 };

                            for (int i = 0; i < cells.Length; i++)
                            {
                                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], fontTableCell));
                                cell.Padding = 5;
                                if (alternate) cell.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                                if (centerColumns.Contains(i)) cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                                table.AddCell(cell);
                            }
                            alternate = !alternate;
                        }

                        document.Add(table);

                        // Итого
                        var totalP = new iTextSharp.text.Paragraph($"Всего сотрудников: {totalEmployees}", fontSubtitle);
                        totalP.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        totalP.SpacingAfter = 3;
                        document.Add(totalP);

                        var adminP = new iTextSharp.text.Paragraph($"Администраторы (ур. 1): {adminCount}", fontSmall);
                        adminP.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        adminP.SpacingAfter = 3;
                        document.Add(adminP);

                        var managerP = new iTextSharp.text.Paragraph($"Менеджеры (ур. 2-3): {managerCount}", fontSmall);
                        managerP.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        managerP.SpacingAfter = 3;
                        document.Add(managerP);

                        var staffP = new iTextSharp.text.Paragraph($"Младший персонал (ур. 4-10): {staffCount}", fontSmall);
                        staffP.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        staffP.SpacingAfter = 35;
                        document.Add(staffP);

                        // Подпись
                        var signTable = new iTextSharp.text.pdf.PdfPTable(1);
                        signTable.WidthPercentage = 55;
                        signTable.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;

                        var signCell1 = new iTextSharp.text.pdf.PdfPCell();
                        signCell1.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell1.PaddingBottom = 3;
                        signCell1.AddElement(new iTextSharp.text.Paragraph(
                            $"{currentUser.Должность?.Название ?? "Сотрудник"} {initials} _______________  {DateTime.Now:dd.MM.yyyy}", fontSign));
                        signTable.AddCell(signCell1);

                        var signCell2 = new iTextSharp.text.pdf.PdfPCell();
                        signCell2.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell2.PaddingLeft = 145;
                        signCell2.AddElement(new iTextSharp.text.Paragraph("(Подпись)", fontSmall));
                        signTable.AddCell(signCell2);
                        document.Add(signTable);

                        // Футер
                        var footerLine = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.LIGHT_GRAY, iTextSharp.text.Element.ALIGN_CENTER, 0);
                        var flp = new iTextSharp.text.Paragraph();
                        flp.SpacingBefore = 40;
                        flp.Add(footerLine);
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

                var result = MessageBox.Show($"Отчёт о сотрудниках сохранён!\n\n{saveFileDialog.FileName}\n\nОткрыть PDF?",
                    "Отчёт сохранён", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = saveFileDialog.FileName, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Подписка на изменения полей

        /// <summary>
        /// Подписываемся на изменения обязательных полей для валидации в реальном времени
        /// </summary>
        private void SubscribeToFieldChanges()
        {
            TxtLastName.TextChanged += (s, e) => UpdateButtonsState();
            TxtFirstName.TextChanged += (s, e) => UpdateButtonsState();
            TxtLogin.TextChanged += (s, e) => UpdateButtonsState();
            PassBox.PasswordChanged += (s, e) => UpdateButtonsState();
            CmbPosition.SelectionChanged += (s, e) => UpdateButtonsState();
            TxtPhone.TextChanged += (s, e) => UpdateButtonsState();

            // Добавляем сброс подсветки при изменении полей
            TxtLastName.TextChanged += OnFieldTextChanged;
            TxtFirstName.TextChanged += OnFieldTextChanged;
            TxtMiddleName.TextChanged += OnFieldTextChanged;
            TxtPhone.TextChanged += OnFieldTextChanged;
            TxtLogin.TextChanged += OnFieldTextChanged;
            PassBox.PasswordChanged += (s, e) => OnFieldTextChanged(s, e);
            CmbPosition.SelectionChanged += (s, e) => OnFieldTextChanged(s, e);
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

        #region Создание кнопок с overlay для tooltip

        private void CreateActionButtons()
        {
            if (actionButtonsPanel == null) return;
            actionButtonsPanel.Children.Clear();

            // Кнопка "Добавить"
            if (rights.Employees.CanCreate)
            {
                var (button, overlay) = CreateButtonWithOverlay("➕ Добавить", Add_Click, 110);
                addButton = button;
                addButtonOverlay = overlay;
                actionButtonsPanel.Children.Add(CreateButtonContainer(button, overlay));
            }

            // Кнопка "Обновить"
            if (rights.Employees.CanEdit)
            {
                var (button, overlay) = CreateButtonWithOverlay("✏️ Обновить", Update_Click, 110);
                editButton = button;
                editButtonOverlay = overlay;
                actionButtonsPanel.Children.Add(CreateButtonContainer(button, overlay));
            }

            // Кнопка "Удалить"
            if (rights.Employees.CanDelete)
            {
                var (button, overlay) = CreateButtonWithOverlay("🗑️ Удалить", Delete_Click, 110);
                deleteButton = button;
                deleteButtonOverlay = overlay;
                actionButtonsPanel.Children.Add(CreateButtonContainer(button, overlay));
            }

            // Кнопка "Очистить"
            var (clearBtn, clearOverlay) = CreateButtonWithOverlay("🔄 Очистить", ClearForm_Click, 110);
            clearButton = clearBtn;
            clearButtonOverlay = clearOverlay;
            actionButtonsPanel.Children.Add(CreateButtonContainer(clearButton, clearOverlay));
        }

        /// <summary>
        /// Создает контейнер Grid, содержащий кнопку и overlay для tooltip
        /// </summary>
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

        /// <summary>
        /// Создает кнопку и прозрачный overlay Border для tooltip
        /// </summary>
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
                ToolTip = GetButtonTooltip(text, false)
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
                        overlay.ToolTip = GetButtonTooltip(btn.Content?.ToString(), false);
                    }
                }
            };

            return (button, overlay);
        }

        /// <summary>
        /// Возвращает текст подсказки для кнопки
        /// </summary>
        private string GetButtonTooltip(string buttonContent, bool isActive)
        {
            if (string.IsNullOrEmpty(buttonContent)) return "";

            if (buttonContent.Contains("Добавить"))
            {
                if (!isActive)
                {
                    var missingFields = GetMissingRequiredFields();
                    if (missingFields.Any())
                        return $"Для активации заполните:\n• {string.Join("\n• ", missingFields)}";
                }
                return "Нажмите для добавления нового сотрудника";
            }

            if (buttonContent.Contains("Обновить"))
            {
                if (!isActive && ListViewEmployees.SelectedItem == null)
                    return "Сначала выберите сотрудника из списка";
                if (!isActive)
                {
                    var missingFields = GetMissingRequiredFieldsForUpdate();
                    if (missingFields.Any())
                        return $"Для активации заполните:\n• {string.Join("\n• ", missingFields)}";
                }
                return "Нажмите для обновления данных сотрудника";
            }

            if (buttonContent.Contains("Удалить"))
                return "Выберите сотрудника из списка для удаления";

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
            var controls = new Control[] { TxtLastName, TxtFirstName, TxtMiddleName, TxtPhone, TxtLogin, CmbPosition, PassBox };
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

        /// <summary>
        /// Проверяет обязательные поля и возвращает список незаполненных
        /// </summary>
        private List<string> GetMissingRequiredFields()
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(GetActualText(TxtLastName)))
                missing.Add("Фамилия");

            if (string.IsNullOrWhiteSpace(GetActualText(TxtFirstName)))
                missing.Add("Имя");

            if (CmbPosition.SelectedValue == null)
                missing.Add("Должность");

            if (string.IsNullOrWhiteSpace(GetActualText(TxtLogin)))
                missing.Add("Логин");

            if (string.IsNullOrWhiteSpace(GetActualPassword()))
                missing.Add("Пароль (мин. 12 символов)");

            return missing;
        }

        /// <summary>
        /// Проверяет обязательные поля для обновления (без пароля)
        /// </summary>
        private List<string> GetMissingRequiredFieldsForUpdate()
        {
            var missing = new List<string>();

            if (string.IsNullOrWhiteSpace(GetActualText(TxtLastName)))
                missing.Add("Фамилия");

            if (string.IsNullOrWhiteSpace(GetActualText(TxtFirstName)))
                missing.Add("Имя");

            if (CmbPosition.SelectedValue == null)
                missing.Add("Должность");

            if (string.IsNullOrWhiteSpace(GetActualText(TxtLogin)))
                missing.Add("Логин");

            return missing;
        }

        /// <summary>
        /// Проверяет, все ли обязательные поля заполнены
        /// </summary>
        private bool AreRequiredFieldsFilled()
        {
            return !GetMissingRequiredFields().Any();
        }

        /// <summary>
        /// Проверяет, заполнены ли обязательные поля для обновления (без пароля)
        /// </summary>
        private bool AreRequiredFieldsFilledForUpdate()
        {
            return !GetMissingRequiredFieldsForUpdate().Any();
        }

        /// <summary>
        /// Проверяет корректность данных сотрудника с подсветкой полей
        /// </summary>
        /// <summary>
        /// Проверяет корректность данных сотрудника с подсветкой полей
        /// </summary>
        private bool ValidateEmployee(out string errorMessage, bool skipPasswordValidation = false)
        {
            var errors = new List<string>();
            var errorFields = new Dictionary<Control, string>();

            var lastName = GetActualText(TxtLastName);
            var firstName = GetActualText(TxtFirstName);
            var middleName = GetActualText(TxtMiddleName);
            var phone = GetActualText(TxtPhone);
            var login = GetActualText(TxtLogin);
            var password = GetActualPassword();

            // Сбрасываем подсветку
            ClearAllHighlights();

            // Фамилия
            if (string.IsNullOrWhiteSpace(lastName))
            {
                errors.Add("• Фамилия не введена");
                errorFields[TxtLastName] = "Фамилия обязательна для заполнения";
            }
            else if (!Regex.IsMatch(lastName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
            {
                errors.Add("• Фамилия содержит недопустимые символы");
                errorFields[TxtLastName] = "Фамилия может содержать только буквы и дефис";
            }
            else if (lastName.Length > 30)
            {
                errors.Add("• Фамилия должна быть не длиннее 30 символов");
                errorFields[TxtLastName] = "Фамилия не должна превышать 30 символов";
            }
            else if (Regex.Replace(lastName, @"[^A-Za-zА-Яа-яЁё]", "").Length < 2)
            {
                errors.Add("• Фамилия должна содержать минимум 2 буквы");
                errorFields[TxtLastName] = "Фамилия должна содержать минимум 2 буквы";
            }

            // Имя
            if (string.IsNullOrWhiteSpace(firstName))
            {
                errors.Add("• Имя не введено");
                errorFields[TxtFirstName] = "Имя обязательно для заполнения";
            }
            else if (!Regex.IsMatch(firstName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
            {
                errors.Add("• Имя содержит недопустимые символы");
                errorFields[TxtFirstName] = "Имя может содержать только буквы и дефис";
            }
            else if (firstName.Length > 30)
            {
                errors.Add("• Имя должно быть не длиннее 30 символов");
                errorFields[TxtFirstName] = "Имя не должно превышать 30 символов";
            }
            else if (Regex.Replace(firstName, @"[^A-Za-zА-Яа-яЁё]", "").Length < 2)
            {
                errors.Add("• Имя должно содержать минимум 2 буквы");
                errorFields[TxtFirstName] = "Имя должно содержать минимум 2 буквы";
            }

            // Отчество (опционально)
            if (!string.IsNullOrWhiteSpace(middleName))
            {
                if (!Regex.IsMatch(middleName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
                {
                    errors.Add("• Отчество содержит недопустимые символы");
                    errorFields[TxtMiddleName] = "Отчество может содержать только буквы и дефис";
                }
                else if (middleName.Length > 30)
                {
                    errors.Add("• Отчество должно быть не длиннее 30 символов");
                    errorFields[TxtMiddleName] = "Отчество не должно превышать 30 символов";
                }
            }

            // Должность
            if (CmbPosition.SelectedValue == null)
            {
                errors.Add("• Должность не выбрана");
                errorFields[CmbPosition] = "Выберите должность сотрудника";
            }

            // Логин
            if (string.IsNullOrWhiteSpace(login))
            {
                errors.Add("• Логин не введён");
                errorFields[TxtLogin] = "Логин обязателен для заполнения";
            }
            else if (login.Length < 3)
            {
                errors.Add("• Логин должен быть не менее 3 символов");
                errorFields[TxtLogin] = "Логин должен содержать минимум 3 символа";
            }
            else if (login.Length > 50)
            {
                errors.Add("• Логин не должен превышать 50 символов");
                errorFields[TxtLogin] = "Логин не должен превышать 50 символов";
            }
            else if (!Regex.IsMatch(login, @"^[A-Za-z0-9_@.-]+$"))
            {
                errors.Add("• Логин содержит недопустимые символы");
                errorFields[TxtLogin] = "Логин может содержать только буквы, цифры и символы _ @ . -";
            }

            // Пароль (проверяется только если не пропущен)
            if (!skipPasswordValidation)
            {
                if (string.IsNullOrWhiteSpace(password))
                {
                    errors.Add("• Пароль не введён");
                    errorFields[PassBox] = "Пароль обязателен для заполнения";
                }
                else if (password.Length < 12)
                {
                    errors.Add("• Пароль должен быть не менее 12 символов");
                    errorFields[PassBox] = "Пароль должен содержать минимум 12 символов";
                }
            }

            // Телефон
            if (!string.IsNullOrWhiteSpace(phone) && !Regex.IsMatch(phone, @"^\+?\d{11}$"))
            {
                errors.Add("• Телефон должен содержать 11 цифр (например: +79001234567)");
                errorFields[TxtPhone] = "Телефон должен содержать 11 цифр (например: +79001234567)";
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

        /// <summary>
        /// Обновляет состояние всех кнопок
        /// </summary>
        private void UpdateButtonsState()
        {
            bool isEmployeeSelected = ListViewEmployees.SelectedItem != null;
            bool requiredFieldsFilled = AreRequiredFieldsFilled();
            bool requiredFieldsForUpdate = AreRequiredFieldsFilledForUpdate();

            // Кнопка "Добавить" активна только когда заполнены все обязательные поля
            if (addButton != null)
            {
                addButton.IsEnabled = requiredFieldsFilled;
                UpdateOverlayState(addButtonOverlay, addButton.IsEnabled, "Добавить");
            }

            // Кнопка "Обновить" активна когда выбран сотрудник и заполнены обязательные поля (кроме пароля)
            if (editButton != null)
            {
                editButton.IsEnabled = isEmployeeSelected && requiredFieldsForUpdate;
                UpdateOverlayState(editButtonOverlay, editButton.IsEnabled, "Обновить");
            }

            // Кнопка "Удалить" активна только когда выбран сотрудник
            if (deleteButton != null)
            {
                deleteButton.IsEnabled = isEmployeeSelected;
                UpdateOverlayState(deleteButtonOverlay, deleteButton.IsEnabled, "Удалить");
            }

            // Кнопка "Очистить" всегда активна
            if (clearButton != null)
            {
                clearButton.IsEnabled = true;
                UpdateOverlayState(clearButtonOverlay, clearButton.IsEnabled, "Очистить");
            }
        }

        /// <summary>
        /// Обновляет состояние overlay для кнопки
        /// </summary>
        private void UpdateOverlayState(Border overlay, bool isButtonEnabled, string buttonType)
        {
            if (overlay == null) return;

            if (isButtonEnabled)
            {
                overlay.Visibility = Visibility.Collapsed;
                overlay.ToolTip = null;
            }
            else
            {
                overlay.Visibility = Visibility.Visible;
                overlay.ToolTip = GetButtonTooltip(buttonType, false);
            }
        }

        #endregion

        #region Выбор сотрудника

        /// <summary>
        /// Заполняет форму данными выбранного сотрудника
        /// </summary>
        private void ListViewEmployees_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewEmployees.SelectedItem is EmployeeViewModel selectedEmployee)
            {
                var employee = selectedEmployee.OriginalEmployee;
                TxtEmployeeId.Text = employee.Код_сотрудника.ToString();
                TxtLastName.Text = employee.Фамилия;
                TxtFirstName.Text = employee.Имя;
                TxtMiddleName.Text = employee.Отчество;
                TxtPhone.Text = employee.Телефон;
                CmbPosition.SelectedValue = employee.Код_должности;
                TxtLogin.Text = employee.Логин;
                PassBox.Password = employee.Пароль;
                LoadEmployeePhoto(employee);
                selectedImageData = null;
            }
            else
            {
                ClearForm();
            }

            // Сбрасываем подсветку при выборе другого элемента
            ClearAllHighlights();
            UpdateButtonsState();
        }

        /// <summary>
        /// Загружает фото сотрудника в превью
        /// </summary>
        private void LoadEmployeePhoto(Сотрудники employee)
        {
            try
            {
                if (employee?.Аватарка != null && employee.Аватарка.Length > 0)
                    EmployeePhoto.Source = LoadImageFromBytes(employee.Аватарка);
                else
                    EmployeePhoto.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
            }
            catch
            {
                EmployeePhoto.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
            }
        }

        #endregion

        #region Выбор фото

        private void SelectPhoto_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                    Title = "Выберите фотографию сотрудника"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedImageData = File.ReadAllBytes(openFileDialog.FileName);
                    EmployeePhoto.Source = LoadImageFromBytes(selectedImageData);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе фото: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region CRUD операции

        /// <summary>
        /// Добавляет нового сотрудника
        /// </summary>
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateEmployee(out var error))
                {
                    // Не показываем MessageBox, поля уже подсвечены
                    return;
                }

                var login = GetActualText(TxtLogin);
                var phone = GetActualText(TxtPhone);

                // Проверка уникальности логина
                if (context.Сотрудники.Any(s => s.Логин == login))
                {
                    HighlightError(TxtLogin, "Этот логин уже используется другим сотрудником");
                    TxtLogin.Focus();
                    return;
                }

                // Проверка уникальности телефона
                if (!string.IsNullOrWhiteSpace(phone) && context.Сотрудники.Any(s => s.Телефон == phone))
                {
                    HighlightError(TxtPhone, "Этот телефон уже используется другим сотрудником");
                    TxtPhone.Focus();
                    return;
                }

                var employee = new Сотрудники
                {
                    Фамилия = GetActualText(TxtLastName),
                    Имя = GetActualText(TxtFirstName),
                    Отчество = GetActualText(TxtMiddleName),
                    Телефон = phone,
                    Код_должности = (int)CmbPosition.SelectedValue,
                    Логин = login,
                    Пароль = GetActualPassword(),
                    Аватарка = selectedImageData
                };

                context.Сотрудники.Add(employee);
                context.SaveChanges();

                ShowSuccess($"Сотрудник «{employee.Фамилия} {employee.Имя}» добавлен!");
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обновляет данные выбранного сотрудника
        /// </summary>
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtEmployeeId.Text))
                {
                    MessageBox.Show("Выберите сотрудника!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var employeeId = int.Parse(TxtEmployeeId.Text);
                var employee = context.Сотрудники.Find(employeeId);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Получаем введенный пароль
                var password = GetActualPassword();

                // Если пароль не введен (пустой) или совпадает с текущим - пропускаем валидацию пароля
                var skipPasswordValidation = string.IsNullOrEmpty(password) || password == employee.Пароль;

                if (!ValidateEmployee(out var error, skipPasswordValidation))
                {
                    return;
                }

                var login = GetActualText(TxtLogin);
                var phone = GetActualText(TxtPhone);

                // Проверка уникальности логина
                if (context.Сотрудники.Any(s => s.Логин == login && s.Код_сотрудника != employeeId))
                {
                    HighlightError(TxtLogin, "Этот логин уже используется другим сотрудником");
                    TxtLogin.Focus();
                    return;
                }

                // Проверка уникальности телефона
                if (!string.IsNullOrWhiteSpace(phone) && context.Сотрудники.Any(s => s.Телефон == phone && s.Код_сотрудника != employeeId))
                {
                    HighlightError(TxtPhone, "Этот телефон уже используется другим сотрудником");
                    TxtPhone.Focus();
                    return;
                }

                var oldName = $"{employee.Фамилия} {employee.Имя}";
                employee.Фамилия = GetActualText(TxtLastName);
                employee.Имя = GetActualText(TxtFirstName);
                employee.Отчество = GetActualText(TxtMiddleName);
                employee.Телефон = phone;
                employee.Код_должности = (int)CmbPosition.SelectedValue;
                employee.Логин = login;

                // Пароль меняем только если он введен и отличается от текущего
                if (!string.IsNullOrEmpty(password) && password != employee.Пароль)
                    employee.Пароль = password;

                if (selectedImageData != null)
                    employee.Аватарка = selectedImageData;

                // Если редактируется текущий пользователь — обновляем его локально
                if (currentUser.Код_сотрудника == employee.Код_сотрудника)
                {
                    currentUser.Фамилия = employee.Фамилия;
                    currentUser.Имя = employee.Имя;
                    currentUser.Отчество = employee.Отчество;
                    currentUser.Телефон = employee.Телефон;
                    if (!string.IsNullOrEmpty(password) && password != currentUser.Пароль)
                        currentUser.Пароль = password;
                    if (selectedImageData != null)
                        currentUser.Аватарка = selectedImageData;

                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    mainWindow?.SetCurrentUser(currentUser);
                }

                context.SaveChanges();

                ShowSuccess($"Сотрудник обновлён с «{oldName}» на «{employee.Фамилия} {employee.Имя}»!");
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// Удаляет выбранного сотрудника
        /// </summary>
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtEmployeeId.Text))
                {
                    MessageBox.Show("Выберите сотрудника!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var employeeId = int.Parse(TxtEmployeeId.Text);
                var employee = context.Сотрудники.Find(employeeId);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (currentUser.Код_сотрудника == employee.Код_сотрудника)
                {
                    MessageBox.Show("Нельзя удалить свою учётную запись!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить сотрудника «{employee.Фамилия} {employee.Имя}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var employeeName = $"{employee.Фамилия} {employee.Имя}";
                    context.Сотрудники.Remove(employee);
                    context.SaveChanges();
                    ShowSuccess($"Сотрудник «{employeeName}» удалён!");
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
            TxtEmployeeId.Text = "";
            TxtLastName.Text = "";
            TxtFirstName.Text = "";
            TxtMiddleName.Text = "";
            TxtPhone.Text = "";
            CmbPosition.SelectedIndex = -1;
            TxtLogin.Text = "";
            PassBox.Password = "";
            EmployeePhoto.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
            selectedImageData = null;
            ListViewEmployees.SelectedItem = null;
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

        private string GetActualPassword()
        {
            if (PassBox == null) return string.Empty;
            var placeholder = Addons.PlaceholderBehavior.GetPlaceholderText(PassBox);
            var password = PassBox.Password ?? string.Empty;
            return (!string.IsNullOrEmpty(placeholder) && password == placeholder) ? string.Empty : password;
        }

        #endregion
    }

    /// <summary>
    /// ViewModel для отображения сотрудника в карточке
    /// </summary>
    public class EmployeeViewModel
    {
        public Сотрудники OriginalEmployee { get; set; }
        public string Фамилия { get; set; }
        public string Имя { get; set; }
        public string Отчество { get; set; }
        public string Телефон { get; set; }
        public string Логин { get; set; }
        public BitmapImage AvatarSource { get; set; }

        /// <summary>
        /// Полное имя сотрудника (Фамилия Имя Отчество)
        /// </summary>
        public string FullName
        {
            get
            {
                var parts = new[] { Фамилия, Имя, Отчество };
                return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
        }

        /// <summary>
        /// Название должности сотрудника
        /// </summary>
        public string Position => OriginalEmployee.Должность?.Название ?? "Без должности";

        /// <summary>
        /// Логин с иконкой для отображения
        /// </summary>
        public string LoginDisplay => $"🔑 {Логин}";

        /// <summary>
        /// Уровень доступа с текстовым описанием
        /// </summary>
        public string AccessLevelDisplay
        {
            get
            {
                var level = OriginalEmployee.Должность?.Уровень_доступа;
                if (!level.HasValue) return "🔒 Уровень доступа: не определён";

                switch (level.Value)
                {
                    case 1: return $"👑 Администратор (ур. {level})";
                    case 2: return $"⭐ Старший менеджер (ур. {level})";
                    case 4: return $"📊 Менеджер по продажам (ур. {level})";
                    case 5: return $"📦 Менеджер по закупкам (ур. {level})";
                    case 7: return $"💬 Продавец-консультант (ур. {level})";
                    case 8: return $"💰 Кассир (ур. {level})";
                    case 9: return $"📦 Кладовщик (ур. {level})";
                    case 10: return $"📚 Стажёр (ур. {level})";
                    default: return level.Value <= 3 ? $"⭐ Менеджер (ур. {level})" : $"🔒 Сотрудник (ур. {level})";
                }
            }
        }

        public EmployeeViewModel(Сотрудники employee)
        {
            OriginalEmployee = employee;
            Фамилия = employee.Фамилия;
            Имя = employee.Имя;
            Отчество = employee.Отчество;
            Телефон = employee.Телефон;
            Логин = employee.Логин;

            // Загружаем аватар
            if (employee.Аватарка != null && employee.Аватарка.Length > 0)
            {
                try
                {
                    using (var ms = new MemoryStream(employee.Аватарка))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        AvatarSource = bitmap;
                    }
                }
                catch
                {
                    AvatarSource = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
                }
            }
            else
            {
                AvatarSource = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
            }
        }
    }
}