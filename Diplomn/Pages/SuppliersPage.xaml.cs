using Diplomn.Addons;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// Страница управления поставщиками магазина
    /// </summary>
    public partial class SuppliersPage : Page
    {
        #region Поля

        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;
        private ObservableCollection<SupplierViewModel> suppliersView;
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

        public SuppliersPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Поставщики — {user.Фамилия} {user.Имя}";
            suppliersView = new ObservableCollection<SupplierViewModel>();

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
            TxtSupplierName.TextChanged += (s, e) => UpdateButtonsState();
            TxtInn.TextChanged += (s, e) => UpdateButtonsState();
            TxtAddress.TextChanged += (s, e) => UpdateButtonsState();
            TxtEmail.TextChanged += (s, e) => UpdateButtonsState();
            TxtContactLastName.TextChanged += (s, e) => UpdateButtonsState();
            TxtContactFirstName.TextChanged += (s, e) => UpdateButtonsState();
            TxtPhone.TextChanged += (s, e) => UpdateButtonsState();

            // Добавляем сброс подсветки при изменении полей
            TxtSupplierName.TextChanged += OnFieldTextChanged;
            TxtInn.TextChanged += OnFieldTextChanged;
            TxtAddress.TextChanged += OnFieldTextChanged;
            TxtEmail.TextChanged += OnFieldTextChanged;
            TxtContactLastName.TextChanged += OnFieldTextChanged;
            TxtContactFirstName.TextChanged += OnFieldTextChanged;
            TxtContactMiddleName.TextChanged += OnFieldTextChanged;
            TxtPhone.TextChanged += OnFieldTextChanged;
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

            if (rights.Suppliers.CanCreate)
            {
                var (button, overlay) = CreateButtonWithOverlay("➕ Добавить", Add_Click, 110);
                addButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(addButtonContainer);
            }

            if (rights.Suppliers.CanEdit)
            {
                var (button, overlay) = CreateButtonWithOverlay("✏️ Обновить", Update_Click, 110);
                editButtonContainer = CreateButtonContainer(button, overlay);
                actionButtonsPanel.Children.Add(editButtonContainer);
            }

            if (rights.Suppliers.CanDelete)
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
                FontSize = 19,
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

        private string GetButtonTooltip(string buttonContent, bool isActive)
        {
            if (string.IsNullOrEmpty(buttonContent)) return "";

            if (buttonContent.Contains("Добавить"))
            {
                if (!isActive)
                {
                    var missing = GetMissingRequiredFields();
                    if (missing.Any())
                        return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                }
                return "Нажмите для добавления поставщика";
            }

            if (buttonContent.Contains("Обновить"))
            {
                if (!isActive && ListViewSuppliers.SelectedItem == null)
                    return "Выберите поставщика из списка";
                if (!isActive)
                {
                    var missing = GetMissingRequiredFields();
                    if (missing.Any())
                        return $"Для активации заполните:\n• {string.Join("\n• ", missing)}";
                }
                return "Нажмите для обновления поставщика";
            }

            if (buttonContent.Contains("Удалить"))
                return "Выберите поставщика из списка для удаления";

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
            var controls = new Control[] { TxtSupplierName, TxtInn, TxtAddress, TxtEmail, TxtContactLastName, TxtContactFirstName, TxtContactMiddleName, TxtPhone, TxtSupplierId };
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

            if (string.IsNullOrWhiteSpace(GetActualText(TxtSupplierName)))
                missing.Add("Наименование поставщика");

            var inn = GetActualText(TxtInn);
            if (string.IsNullOrWhiteSpace(inn) || !Regex.IsMatch(inn, @"^\d+$") || (inn.Length != 10 && inn.Length != 12))
                missing.Add("ИНН (10 или 12 цифр)");

            var address = GetActualText(TxtAddress);
            if (string.IsNullOrWhiteSpace(address) || address.Length < 5)
                missing.Add("Адрес (мин. 5 символов)");

            var email = GetActualText(TxtEmail);
            if (string.IsNullOrWhiteSpace(email) || !Regex.IsMatch(email, @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$"))
                missing.Add("Email (корректный формат)");

            if (string.IsNullOrWhiteSpace(GetActualText(TxtContactLastName)))
                missing.Add("Фамилия контактного лица");

            if (string.IsNullOrWhiteSpace(GetActualText(TxtContactFirstName)))
                missing.Add("Имя контактного лица");

            return missing;
        }

        private bool AreRequiredFieldsFilled()
        {
            return !GetMissingRequiredFields().Any();
        }

        /// <summary>
        /// Проверяет корректность данных поставщика с подсветкой полей
        /// </summary>
        private bool ValidateSupplier(out string errorMessage, int? excludeId = null)
        {
            var errors = new List<string>();
            var errorFields = new Dictionary<Control, string>();

            var name = GetActualText(TxtSupplierName);
            var inn = GetActualText(TxtInn);
            var address = GetActualText(TxtAddress);
            var email = GetActualText(TxtEmail);
            var contactLastName = GetActualText(TxtContactLastName);
            var contactFirstName = GetActualText(TxtContactFirstName);
            var contactMiddleName = GetActualText(TxtContactMiddleName);
            var phone = GetActualText(TxtPhone);

            // Сбрасываем подсветку
            ClearAllHighlights();

            // Наименование поставщика
            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add("• Наименование поставщика не введено");
                errorFields[TxtSupplierName] = "Наименование поставщика обязательно для заполнения";
            }
            else if (name.Length < 2)
            {
                errors.Add("• Наименование должно содержать минимум 2 символа");
                errorFields[TxtSupplierName] = "Наименование должно содержать минимум 2 символа";
            }
            else if (name.Length > 200)
            {
                errors.Add("• Наименование не должно превышать 200 символов");
                errorFields[TxtSupplierName] = "Наименование не должно превышать 200 символов";
            }

            // ИНН
            if (string.IsNullOrWhiteSpace(inn))
            {
                errors.Add("• ИНН не введён");
                errorFields[TxtInn] = "ИНН обязателен для заполнения";
            }
            else if (!Regex.IsMatch(inn, @"^\d+$"))
            {
                errors.Add("• ИНН должен содержать только цифры");
                errorFields[TxtInn] = "ИНН должен содержать только цифры";
            }
            else if (inn.Length != 10 && inn.Length != 12)
            {
                errors.Add("• ИНН должен содержать 10 или 12 цифр");
                errorFields[TxtInn] = "ИНН должен содержать 10 или 12 цифр";
            }
            else
            {
                // Проверка уникальности ИНН
                bool innExists;
                if (excludeId.HasValue)
                    innExists = context.Поставщики.Any(s => s.ИНН == inn && s.Код_поставщика != excludeId.Value);
                else
                    innExists = context.Поставщики.Any(s => s.ИНН == inn);

                if (innExists)
                {
                    errors.Add("• Поставщик с таким ИНН уже существует");
                    errorFields[TxtInn] = "Поставщик с таким ИНН уже существует";
                }
            }

            // Адрес
            if (string.IsNullOrWhiteSpace(address))
            {
                errors.Add("• Адрес не введён");
                errorFields[TxtAddress] = "Адрес обязателен для заполнения";
            }
            else if (address.Length < 5)
            {
                errors.Add("• Адрес должен содержать минимум 5 символов");
                errorFields[TxtAddress] = "Адрес должен содержать минимум 5 символов";
            }
            else if (address.Length > 300)
            {
                errors.Add("• Адрес не должен превышать 300 символов");
                errorFields[TxtAddress] = "Адрес не должен превышать 300 символов";
            }

            // Email
            if (string.IsNullOrWhiteSpace(email))
            {
                errors.Add("• Email не введён");
                errorFields[TxtEmail] = "Email обязателен для заполнения";
            }
            else if (!Regex.IsMatch(email, @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$"))
            {
                errors.Add("• Неверный формат Email");
                errorFields[TxtEmail] = "Введите корректный Email (например: supplier@mail.ru)";
            }
            else if (email.Length > 100)
            {
                errors.Add("• Email не должен превышать 100 символов");
                errorFields[TxtEmail] = "Email не должен превышать 100 символов";
            }
            else
            {
                // Проверка уникальности Email
                bool emailExists;
                if (excludeId.HasValue)
                    emailExists = context.Поставщики.Any(s => s.Email_поставщика == email && s.Код_поставщика != excludeId.Value);
                else
                    emailExists = context.Поставщики.Any(s => s.Email_поставщика == email);

                if (emailExists)
                {
                    errors.Add("• Поставщик с таким Email уже существует");
                    errorFields[TxtEmail] = "Поставщик с таким Email уже существует";
                }
            }

            // Фамилия контактного лица
            if (string.IsNullOrWhiteSpace(contactLastName))
            {
                errors.Add("• Фамилия контактного лица не введена");
                errorFields[TxtContactLastName] = "Фамилия контактного лица обязательна для заполнения";
            }
            else if (!Regex.IsMatch(contactLastName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
            {
                errors.Add("• Фамилия содержит недопустимые символы");
                errorFields[TxtContactLastName] = "Фамилия может содержать только буквы и дефис";
            }
            else if (contactLastName.Length > 50)
            {
                errors.Add("• Фамилия не должна превышать 50 символов");
                errorFields[TxtContactLastName] = "Фамилия не должна превышать 50 символов";
            }

            // Имя контактного лица
            if (string.IsNullOrWhiteSpace(contactFirstName))
            {
                errors.Add("• Имя контактного лица не введено");
                errorFields[TxtContactFirstName] = "Имя контактного лица обязательно для заполнения";
            }
            else if (!Regex.IsMatch(contactFirstName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
            {
                errors.Add("• Имя содержит недопустимые символы");
                errorFields[TxtContactFirstName] = "Имя может содержать только буквы и дефис";
            }
            else if (contactFirstName.Length > 50)
            {
                errors.Add("• Имя не должно превышать 50 символов");
                errorFields[TxtContactFirstName] = "Имя не должно превышать 50 символов";
            }

            // Отчество контактного лица (опционально)
            if (!string.IsNullOrWhiteSpace(contactMiddleName))
            {
                if (!Regex.IsMatch(contactMiddleName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
                {
                    errors.Add("• Отчество содержит недопустимые символы");
                    errorFields[TxtContactMiddleName] = "Отчество может содержать только буквы и дефис";
                }
                else if (contactMiddleName.Length > 50)
                {
                    errors.Add("• Отчество не должно превышать 50 символов");
                    errorFields[TxtContactMiddleName] = "Отчество не должно превышать 50 символов";
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

        private void UpdateButtonsState()
        {
            bool isSupplierSelected = ListViewSuppliers.SelectedItem != null;
            bool requiredFieldsFilled = AreRequiredFieldsFilled();

            SetButtonState(addButtonContainer, requiredFieldsFilled);
            SetButtonState(editButtonContainer, isSupplierSelected && requiredFieldsFilled);
            SetButtonState(deleteButtonContainer, isSupplierSelected);
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

        private IQueryable<Поставщики> GetFilteredQuery()
        {
            var query = context.Поставщики.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(s => s.Наименование_поставщика.Contains(searchText) ||
                                        s.ИНН.Contains(searchText) ||
                                        s.Фамилия_контактного_лица.Contains(searchText));

            return query;
        }

        private void LoadData()
        {
            UpdateSuppliersView(context.Поставщики.ToList());
        }

        private void UpdateSuppliersView(List<Поставщики> suppliers)
        {
            suppliersView.Clear();
            foreach (var supplier in suppliers)
                suppliersView.Add(new SupplierViewModel(supplier));
            ListViewSuppliers.ItemsSource = suppliersView;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters() => UpdateSuppliersView(GetFilteredQuery().ToList());

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        { if (e.Key == Key.Enter) ApplyFilters(); }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        { TxtSearch.Text = ""; LoadData(); }

        #endregion

        #region Отчёт в PDF

        /// <summary>
        /// Сохраняет отчёт о поставщиках в PDF
        /// </summary>
        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var suppliers = GetFilteredQuery().ToList();
                if (!suppliers.Any())
                {
                    ShowSuccess("Нет данных для сохранения отчета.");
                    return;
                }

                var sfd = new SaveFileDialog { Filter = "PDF файл (*.pdf)|*.pdf", Title = "Сохранить отчет о поставщиках", FileName = $"Отчет_поставщики_{DateTime.Now:yyyy-MM-dd_HH-mm}" };
                if (sfd.ShowDialog() != true) return;

                const string sn = "Oculus+", sp = "+7 (461) 345 12-34", se = "Oculus@глаза.ру", sw = "Oculus.ру", sh = "9:00 – 17:00 ежедневно";
                var init = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
                if (!string.IsNullOrWhiteSpace(currentUser.Отчество)) init += $"{currentUser.Отчество?.Substring(0, 1)}.";

                using (var doc = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 50, 50))
                using (var w = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, new FileStream(sfd.FileName, FileMode.Create)))
                {
                    doc.Open();
                    var fp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                    var bf = iTextSharp.text.pdf.BaseFont.CreateFont(fp, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);
                    var ft = new iTextSharp.text.Font(bf, 16, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
                    var fs = new iTextSharp.text.Font(bf, 11, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                    var fth = new iTextSharp.text.Font(bf, 9, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE);
                    var ftc = new iTextSharp.text.Font(bf, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);
                    var ff = new iTextSharp.text.Font(bf, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);
                    var fsm = new iTextSharp.text.Font(bf, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                    var fsg = new iTextSharp.text.Font(bf, 10, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);

                    var t = new iTextSharp.text.Paragraph("ОТЧЁТ О ПОСТАВЩИКАХ", ft);
                    t.Alignment = iTextSharp.text.Element.ALIGN_CENTER; t.SpacingAfter = 25; doc.Add(t);

                    var tbl = new iTextSharp.text.pdf.PdfPTable(6) { WidthPercentage = 100 };
                    tbl.SetWidths(new float[] { 8, 25, 15, 22, 15, 15 });
                    foreach (var h in new[] { "Код", "Наименование", "ИНН", "Контактное лицо", "Телефон", "Email" })
                    {
                        var hc = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(h, fth)) { BackgroundColor = new iTextSharp.text.BaseColor(0, 51, 102), HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER, Padding = 5 };
                        tbl.AddCell(hc);
                    }
                    bool alt = false;
                    foreach (var s in suppliers)
                    {
                        var cp = $"{s.Фамилия_контактного_лица} {s.Имя_контактного_лица} {s.Отчество_контактного_лица ?? ""}".Trim();
                        var cs = new[] { s.Код_поставщика.ToString(), s.Наименование_поставщика, s.ИНН, cp, s.Телефон_контактного_лица ?? "-", s.Email_поставщика };
                        for (int i = 0; i < cs.Length; i++)
                        {
                            var c = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cs[i], ftc)) { Padding = 5 };
                            if (alt) c.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                            if (i == 0 || i == 2) c.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                            tbl.AddCell(c);
                        }
                        alt = !alt;
                    }
                    doc.Add(tbl);

                    var tp = new iTextSharp.text.Paragraph($"Всего поставщиков: {suppliers.Count}", fs) { Alignment = iTextSharp.text.Element.ALIGN_LEFT, SpacingAfter = 35 };
                    doc.Add(tp);

                    var st = new iTextSharp.text.pdf.PdfPTable(1) { WidthPercentage = 55, HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT };
                    var sc1 = new iTextSharp.text.pdf.PdfPCell() { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingBottom = 3 };
                    sc1.AddElement(new iTextSharp.text.Paragraph($"{currentUser.Должность?.Название ?? "Сотрудник"} {init} _______________  {DateTime.Now:dd.MM.yyyy}", fsg));
                    st.AddCell(sc1);
                    var sc2 = new iTextSharp.text.pdf.PdfPCell() { Border = iTextSharp.text.Rectangle.NO_BORDER, PaddingLeft = 145 };
                    sc2.AddElement(new iTextSharp.text.Paragraph("(Подпись)", fsm)); st.AddCell(sc2); doc.Add(st);

                    var fl = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.LIGHT_GRAY, iTextSharp.text.Element.ALIGN_CENTER, 0);
                    var flp = new iTextSharp.text.Paragraph(); flp.SpacingBefore = 40; flp.Add(fl); doc.Add(flp);
                    doc.Add(new iTextSharp.text.Paragraph($"{sn}  |  Часы работы: {sh}", ff) { Alignment = iTextSharp.text.Element.ALIGN_CENTER, SpacingBefore = 8, SpacingAfter = 2 });
                    doc.Add(new iTextSharp.text.Paragraph($"{sp}  |  {se}  |  {sw}", ff) { Alignment = iTextSharp.text.Element.ALIGN_CENTER });

                    doc.Close();
                }
                var res = MessageBox.Show($"Отчёт сохранён!\n\n{sfd.FileName}\n\nОткрыть PDF?", "Отчёт сохранён", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (res == MessageBoxResult.Yes) System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = sfd.FileName, UseShellExecute = true });
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        #endregion

        #region Выбор поставщика и логотипа

        private void ListViewSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSuppliers.SelectedItem is SupplierViewModel sv)
            {
                var s = sv.OriginalSupplier;
                TxtSupplierId.Text = s.Код_поставщика.ToString();
                TxtSupplierName.Text = s.Наименование_поставщика; TxtInn.Text = s.ИНН;
                TxtAddress.Text = s.Адрес_поставщика; TxtEmail.Text = s.Email_поставщика;
                TxtContactLastName.Text = s.Фамилия_контактного_лица; TxtContactFirstName.Text = s.Имя_контактного_лица;
                TxtContactMiddleName.Text = s.Отчество_контактного_лица; TxtPhone.Text = s.Телефон_контактного_лица;
                LoadSupplierLogo(s); selectedImageData = null;
            }
            else
            {
                ClearForm();
            }

            ClearAllHighlights();
            UpdateButtonsState();
        }

        private void LoadSupplierLogo(Поставщики s)
        {
            try { SupplierLogo.Source = (s?.Логотип != null && s.Логотип.Length > 0) ? LoadImageFromBytes(s.Логотип) : new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute)); }
            catch { SupplierLogo.Source = new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute)); }
        }

        private void SelectLogo_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var ofd = new OpenFileDialog { Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp", Title = "Выберите логотип" };
                if (ofd.ShowDialog() == true) { selectedImageData = File.ReadAllBytes(ofd.FileName); SupplierLogo.Source = LoadImageFromBytes(selectedImageData); }
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        #endregion

        #region CRUD операции

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateSupplier(out var err))
                {
                    return;
                }

                var s = new Поставщики
                {
                    Наименование_поставщика = GetActualText(TxtSupplierName),
                    ИНН = GetActualText(TxtInn),
                    Адрес_поставщика = GetActualText(TxtAddress),
                    Email_поставщика = GetActualText(TxtEmail),
                    Фамилия_контактного_лица = GetActualText(TxtContactLastName),
                    Имя_контактного_лица = GetActualText(TxtContactFirstName),
                    Отчество_контактного_лица = GetActualText(TxtContactMiddleName),
                    Телефон_контактного_лица = GetActualText(TxtPhone),
                    Логотип = selectedImageData
                };
                context.Поставщики.Add(s); context.SaveChanges();
                ShowSuccess($"Поставщик «{s.Наименование_поставщика}» добавлен!");
                LoadData(); ClearForm();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtSupplierId.Text))
                {
                    HighlightError(TxtSupplierId, "Выберите поставщика из списка!");
                    return;
                }

                var id = int.Parse(TxtSupplierId.Text); var s = context.Поставщики.Find(id);
                if (s == null)
                {
                    HighlightError(TxtSupplierId, "Поставщик не найден в базе данных!");
                    return;
                }

                if (!ValidateSupplier(out var err, id))
                {
                    return;
                }

                var oldName = s.Наименование_поставщика;
                s.Наименование_поставщика = GetActualText(TxtSupplierName); s.ИНН = GetActualText(TxtInn);
                s.Адрес_поставщика = GetActualText(TxtAddress); s.Email_поставщика = GetActualText(TxtEmail);
                s.Фамилия_контактного_лица = GetActualText(TxtContactLastName); s.Имя_контактного_лица = GetActualText(TxtContactFirstName);
                s.Отчество_контактного_лица = GetActualText(TxtContactMiddleName); s.Телефон_контактного_лица = GetActualText(TxtPhone);
                if (selectedImageData != null) s.Логотип = selectedImageData;
                context.SaveChanges();
                ShowSuccess($"Поставщик обновлён с «{oldName}» на «{s.Наименование_поставщика}»!");
                LoadData(); ClearForm();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtSupplierId.Text))
                {
                    HighlightError(TxtSupplierId, "Выберите поставщика из списка!");
                    return;
                }

                var id = int.Parse(TxtSupplierId.Text); var s = context.Поставщики.Find(id);
                if (s == null)
                {
                    HighlightError(TxtSupplierId, "Поставщик не найден в базе данных!");
                    return;
                }

                if (context.Поставка.Any(o => o.Код_поставщика == id))
                {
                    MessageBox.Show("Нельзя удалить поставщика — есть связанные поставки!\n\nСначала удалите или переназначьте связанные поставки.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (MessageBox.Show($"Удалить поставщика «{s.Наименование_поставщика}»?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    var supplierName = s.Наименование_поставщика;
                    context.Поставщики.Remove(s); context.SaveChanges();
                    ShowSuccess($"Поставщик «{supplierName}» удалён!");
                    LoadData(); ClearForm();
                }
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        #endregion

        #region Очистка и утилиты

        private void ClearForm()
        {
            TxtSupplierId.Text = ""; TxtSupplierName.Text = ""; TxtInn.Text = ""; TxtAddress.Text = ""; TxtEmail.Text = "";
            TxtContactLastName.Text = ""; TxtContactFirstName.Text = ""; TxtContactMiddleName.Text = ""; TxtPhone.Text = "";
            SupplierLogo.Source = new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute));
            selectedImageData = null; ListViewSuppliers.SelectedItem = null;

            ClearAllHighlights();
            UpdateButtonsState();
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

        /// <summary>
        /// Показывает сообщение об успехе с автоматическим скрытием
        /// </summary>
        private void ShowSuccess(string message)
        {
            SuccessText.Text = message;
                                    SuccessBorder.Visibility = Visibility.Visible;
            SuccessBorder.Focusable = true;
            SuccessBorder.Focus();
            _successTimer?.Stop();
            _successTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _successTimer.Tick += (s, e) =>
            {
                SuccessBorder.Visibility = Visibility.Collapsed;
                _successTimer.Stop();
            };
            _successTimer.Start();
        }

        private BitmapImage LoadImageFromBytes(byte[] d)
        {
            if (d == null || d.Length == 0) return null;
            try { using (var ms = new MemoryStream(d)) { var bmp = new BitmapImage(); bmp.BeginInit(); bmp.StreamSource = ms; bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.EndInit(); return bmp; } }
            catch { return null; }
        }

        private string GetActualText(TextBox tb)
        {
            if (tb == null) return string.Empty;
            var ph = Addons.PlaceholderBehavior.GetPlaceholderText(tb); var text = tb.Text?.Trim() ?? string.Empty;
            return (!string.IsNullOrEmpty(ph) && text == ph) ? string.Empty : text;
        }

        #endregion
    }

    /// <summary>
    /// ViewModel для отображения поставщика в карточке
    /// </summary>
    public class SupplierViewModel
    {
        public Поставщики OriginalSupplier { get; set; }
        public string Наименование_поставщика { get; set; }
        public string ИНН { get; set; }
        public string Фамилия_контактного_лица { get; set; }
        public string Имя_контактного_лица { get; set; }
        public string Отчество_контактного_лица { get; set; }
        public string Телефон_контактного_лица { get; set; }
        public BitmapImage LogoSource { get; set; }

        public string ContactPersonDisplay => string.Join(" ", new[] { Фамилия_контактного_лица, Имя_контактного_лица, Отчество_контактного_лица }.Where(p => !string.IsNullOrWhiteSpace(p)));
        public string PhoneDisplay => string.IsNullOrWhiteSpace(Телефон_контактного_лица) ? "☎ Не указан" : $"☎ {Телефон_контактного_лица}";

        public SupplierViewModel(Поставщики s)
        {
            OriginalSupplier = s; Наименование_поставщика = s.Наименование_поставщика; ИНН = $"ИНН: {s.ИНН}";
            Фамилия_контактного_лица = s.Фамилия_контактного_лица; Имя_контактного_лица = s.Имя_контактного_лица;
            Отчество_контактного_лица = s.Отчество_контактного_лица; Телефон_контактного_лица = s.Телефон_контактного_лица;
            LogoSource = (s.Логотип != null && s.Логотип.Length > 0) ? LoadImage(s.Логотип) : new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute));
        }

        private static BitmapImage LoadImage(byte[] d)
        {
            try { using (var ms = new MemoryStream(d)) { var bmp = new BitmapImage(); bmp.BeginInit(); bmp.StreamSource = ms; bmp.CacheOption = BitmapCacheOption.OnLoad; bmp.EndInit(); return bmp; } }
            catch { return new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute)); }
        }
    }
}