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
using System.Windows.Media.Imaging;

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

        #endregion

        #region Конструктор

        public EmployeesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Сотрудники — {user.Фамилия} {user.Имя}";
            employeesView = new ObservableCollection<EmployeeViewModel>();
            LoadPositions();
            LoadData();
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

        #region Валидация

        /// <summary>
        /// Проверяет корректность данных сотрудника
        /// </summary>
        private bool ValidateEmployee(out string errorMessage, bool skipPasswordValidation = false)
        {
            var errors = new StringBuilder();
            var lastName = GetActualText(TxtLastName);
            var firstName = GetActualText(TxtFirstName);
            var phone = GetActualText(TxtPhone);
            var login = GetActualText(TxtLogin);
            var password = GetActualPassword();

            if (string.IsNullOrWhiteSpace(lastName))
                errors.AppendLine("• Фамилия не введена");
            else if (!Regex.IsMatch(lastName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
                errors.AppendLine("• Фамилия содержит недопустимые символы");
            else if (lastName.Length > 30)
                errors.AppendLine("• Фамилия должна быть не длиннее 30 символов");

            if (string.IsNullOrWhiteSpace(firstName))
                errors.AppendLine("• Имя не введено");
            else if (!Regex.IsMatch(firstName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
                errors.AppendLine("• Имя содержит недопустимые символы");
            else if (firstName.Length > 30)
                errors.AppendLine("• Имя должно быть не длиннее 30 символов");

            if (CmbPosition.SelectedValue == null)
                errors.AppendLine("• Должность не выбрана");

            if (string.IsNullOrWhiteSpace(login))
                errors.AppendLine("• Логин не введён");

            if (!skipPasswordValidation)
            {
                if (string.IsNullOrWhiteSpace(password))
                    errors.AppendLine("• Пароль не введён");
                else if (password.Length < 12)
                    errors.AppendLine("• Пароль должен быть не менее 12 символов");
            }

            if (!string.IsNullOrWhiteSpace(phone) && !Regex.IsMatch(phone, @"^\+?\d{11}$"))
                errors.AppendLine("• Телефон должен содержать 11 цифр");

            errorMessage = errors.ToString();
            return errors.Length == 0;
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
                    MessageBox.Show(error, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var login = GetActualText(TxtLogin);
                var phone = GetActualText(TxtPhone);

                if (context.Сотрудники.Any(s => s.Логин == login))
                {
                    MessageBox.Show("Логин уже используется!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(phone) && context.Сотрудники.Any(s => s.Телефон == phone))
                {
                    MessageBox.Show("Телефон уже используется!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
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

                MessageBox.Show("Сотрудник добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                var skipPassword = string.IsNullOrEmpty(GetActualPassword());
                if (!ValidateEmployee(out var error, skipPassword))
                {
                    MessageBox.Show(error, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var employeeId = int.Parse(TxtEmployeeId.Text);
                var employee = context.Сотрудники.Find(employeeId);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var login = GetActualText(TxtLogin);
                var phone = GetActualText(TxtPhone);

                if (context.Сотрудники.Any(s => s.Логин == login && s.Код_сотрудника != employeeId))
                {
                    MessageBox.Show("Логин уже используется!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(phone) && context.Сотрудники.Any(s => s.Телефон == phone && s.Код_сотрудника != employeeId))
                {
                    MessageBox.Show("Телефон уже используется!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                employee.Фамилия = GetActualText(TxtLastName);
                employee.Имя = GetActualText(TxtFirstName);
                employee.Отчество = GetActualText(TxtMiddleName);
                employee.Телефон = phone;
                employee.Код_должности = (int)CmbPosition.SelectedValue;
                employee.Логин = login;

                if (!string.IsNullOrEmpty(GetActualPassword()))
                    employee.Пароль = GetActualPassword();

                if (selectedImageData != null)
                    employee.Аватарка = selectedImageData;

                // Если редактируется текущий пользователь — обновляем его локально
                if (currentUser.Код_сотрудника == employee.Код_сотрудника)
                {
                    currentUser.Фамилия = employee.Фамилия;
                    currentUser.Имя = employee.Имя;
                    currentUser.Отчество = employee.Отчество;
                    currentUser.Телефон = employee.Телефон;
                    if (!string.IsNullOrEmpty(GetActualPassword()))
                        currentUser.Пароль = GetActualPassword();
                    if (selectedImageData != null)
                        currentUser.Аватарка = selectedImageData;

                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    mainWindow?.SetCurrentUser(currentUser);
                }

                context.SaveChanges();

                MessageBox.Show("Сотрудник обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    context.Сотрудники.Remove(employee);
                    context.SaveChanges();
                    MessageBox.Show("Сотрудник удалён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
        public string Position => OriginalEmployee.Должность?.Название ?? "Без должности"; //убрать?

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
                return level.Value == 1 ? $"👑 Администратор (ур. {level})" :
                       level.Value <= 3 ? $"⭐ Менеджер (ур. {level})" :
                                          $"🔒 Сотрудник (ур. {level})";
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