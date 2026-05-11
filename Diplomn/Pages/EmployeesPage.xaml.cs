using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Diagnostics;
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
    public partial class EmployeesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;
        private ObservableCollection<EmployeeViewModel> employeesView;

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

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private void LoadPositions()
        {
            CmbPosition.ItemsSource = context.Должность.ToList();
        }

        private IQueryable<Сотрудники> GetFilteredQuery()
        {
            var query = context.Сотрудники.AsQueryable();

            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                query = query.Where(e => e.Фамилия.Contains(term) ||
                                        e.Имя.Contains(term) ||
                                        e.Логин.Contains(term));
            }

            return query;
        }

        private void LoadData()
        {
            var employees = context.Сотрудники
                .Include("Должность")
                .ToList();
            UpdateEmployeesView(employees);
        }

        private void UpdateEmployeesView(List<Сотрудники> employees)
        {
            employeesView.Clear();
            foreach (var employee in employees)
            {
                employeesView.Add(new EmployeeViewModel(employee));
            }
            ListViewEmployees.ItemsSource = employeesView;
        }

        private BitmapImage LoadImageFromBytes(byte[] imageData)
        {
            if (imageData == null || imageData.Length == 0)
                return null;

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
            catch
            {
                return null;
            }
        }

        private void ApplyFilters()
        {
            var employees = GetFilteredQuery()
                .Include("Должность")
                .ToList();
            UpdateEmployeesView(employees);
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            LoadData();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем все записи в базе
                var allEmployees = context.Сотрудники.Include("Должность").ToList();
                Debug.WriteLine($"Всего сотрудников в БД: {allEmployees.Count}");

                // Получаем отфильтрованные данные
                var query = context.Сотрудники.Include("Должность").AsQueryable();

                string searchText = GetActualText(TxtSearch);
                if (!string.IsNullOrWhiteSpace(searchText))
                {
                    var term = searchText.Trim();
                    query = query.Where(emp => emp.Фамилия.Contains(term) ||
                                            emp.Имя.Contains(term) ||
                                            emp.Логин.Contains(term));
                }

                var employees = query.ToList();
                Debug.WriteLine($"Сотрудников после фильтрации: {employees.Count}");

                if (!employees.Any())
                {
                    // Если фильтр пустой, показываем всех
                    if (string.IsNullOrWhiteSpace(searchText))
                    {
                        MessageBox.Show("В базе данных нет сотрудников.", "Информация",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageBox.Show($"По запросу \"{searchText}\" сотрудников не найдено.", "Информация",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить отчет о сотрудниках",
                    FileName = $"Отчет_сотрудники_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() != true)
                    return;

                // Данные магазина
                const string shopName = "Oculus+";
                const string shopPhone = "+7 (461) 345 12-34";
                const string shopEmail = "Oculus@глаза.ру";
                const string shopWebsite = "Oculus.ру";
                const string shopHours = "9:00 – 17:00 ежедневно";

                // Формируем ФИО с инициалами
                string initials = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
                if (!string.IsNullOrWhiteSpace(currentUser.Отчество))
                    initials += $"{currentUser.Отчество?.Substring(0, 1)}.";
                else
                    initials += ".";

                // Статистика
                var totalEmployees = employees.Count;
                var adminCount = employees.Count(emp => emp.Должность?.Уровень_доступа == 1);
                var managerCount = employees.Count(emp => emp.Должность?.Уровень_доступа > 1 && emp.Должность?.Уровень_доступа <= 3);
                var staffCount = employees.Count(emp => emp.Должность?.Уровень_доступа > 3 || emp.Должность == null);

                // Создаём PDF
                using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 50, 50))
                {
                    using (var writer = iTextSharp.text.pdf.PdfWriter.GetInstance(document, new FileStream(saveFileDialog.FileName, FileMode.Create)))
                    {
                        document.Open();

                        // Шрифты
                        string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                        var baseFont = iTextSharp.text.pdf.BaseFont.CreateFont(fontPath, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED);

                        var fontTitle = new iTextSharp.text.Font(baseFont, 16, iTextSharp.text.Font.BOLD, new iTextSharp.text.BaseColor(0, 51, 102));
                        var fontSubtitle = new iTextSharp.text.Font(baseFont, 11, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontTableHeader = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.BOLD, iTextSharp.text.BaseColor.WHITE);
                        var fontTableCell = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);
                        var fontFooter = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);
                        var fontSmall = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontSign = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);

                        // === ЗАГОЛОВОК ===
                        var reportTitle = new iTextSharp.text.Paragraph("ОТЧЁТ О СОТРУДНИКАХ", fontTitle);
                        reportTitle.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        reportTitle.SpacingAfter = 25;
                        document.Add(reportTitle);

                        // === ИНФОРМАЦИЯ О ПОИСКЕ ===
                        if (!string.IsNullOrWhiteSpace(searchText))
                        {
                            var searchInfo = new iTextSharp.text.Paragraph($"Поиск: \"{searchText}\"", fontSmall);
                            searchInfo.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                            searchInfo.SpacingAfter = 15;
                            document.Add(searchInfo);
                        }

                        // === ТАБЛИЦА ===
                        var table = new iTextSharp.text.pdf.PdfPTable(7);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 8, 18, 14, 14, 16, 14, 16 });
                        table.SpacingBefore = 10;
                        table.SpacingAfter = 25;

                        // Заголовки таблицы
                        var headers = new[] { "Код", "Фамилия", "Имя", "Отчество", "Должность", "Телефон", "Логин" };
                        foreach (var header in headers)
                        {
                            var headerCell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(header, fontTableHeader));
                            headerCell.BackgroundColor = new iTextSharp.text.BaseColor(0, 51, 102);
                            headerCell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                            headerCell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;
                            headerCell.Padding = 5;
                            table.AddCell(headerCell);
                        }

                        // Данные
                        bool alternate = false;
                        foreach (var employee in employees)
                        {
                            var position = employee.Должность?.Название ?? "-";
                            var phone = employee.Телефон ?? "-";
                            var middleName = employee.Отчество ?? "-";

                            var cells = new[]
                            {
                        employee.Код_сотрудника.ToString(),
                        employee.Фамилия,
                        employee.Имя,
                        middleName,
                        position,
                        phone,
                        employee.Логин
                    };

                            var centerColumns = new HashSet<int> { 0, 5 }; // Код и Телефон по центру

                            for (int i = 0; i < cells.Length; i++)
                            {
                                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cells[i], fontTableCell));
                                cell.Padding = 5;
                                cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;

                                if (alternate)
                                {
                                    cell.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                                }

                                if (centerColumns.Contains(i))
                                {
                                    cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                                }

                                table.AddCell(cell);
                            }

                            alternate = !alternate;
                        }

                        document.Add(table);

                        // === ИТОГО (по левому краю) ===
                        var totalParagraph = new iTextSharp.text.Paragraph();
                        totalParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        totalParagraph.SpacingBefore = 5;
                        totalParagraph.SpacingAfter = 3;
                        totalParagraph.Add(new iTextSharp.text.Chunk($"Всего сотрудников: {totalEmployees}", fontSubtitle));
                        document.Add(totalParagraph);

                        var adminParagraph = new iTextSharp.text.Paragraph();
                        adminParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        adminParagraph.SpacingAfter = 3;
                        adminParagraph.Add(new iTextSharp.text.Chunk($"Администраторы (ур. 1-3): {adminCount}", fontSmall));
                        document.Add(adminParagraph);

                        var managerParagraph = new iTextSharp.text.Paragraph();
                        managerParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        managerParagraph.SpacingAfter = 3;
                        managerParagraph.Add(new iTextSharp.text.Chunk($"Менеджеры (ур. 4-6): {managerCount}", fontSmall));
                        document.Add(managerParagraph);

                        var staffParagraph = new iTextSharp.text.Paragraph();
                        staffParagraph.Alignment = iTextSharp.text.Element.ALIGN_LEFT;
                        staffParagraph.SpacingAfter = 35;
                        staffParagraph.Add(new iTextSharp.text.Chunk($"Младший персонал (ур. 7-10): {staffCount}", fontSmall));
                        document.Add(staffParagraph);

                        // === ПОДПИСЬ (по правому краю) ===
                        var signTable = new iTextSharp.text.pdf.PdfPTable(1);
                        signTable.WidthPercentage = 55;
                        signTable.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;

                        // Строка с должностью, ФИО, линией и датой
                        var signCell1 = new iTextSharp.text.pdf.PdfPCell();
                        signCell1.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell1.HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT;
                        signCell1.PaddingBottom = 3;

                        var signParagraph = new iTextSharp.text.Paragraph();
                        signParagraph.Add(new iTextSharp.text.Chunk(
                            $"{currentUser.Должность?.Название ?? "Сотрудник"} {initials} _______________  {DateTime.Now:dd.MM.yyyy}",
                            fontSign));
                        signCell1.AddElement(signParagraph);
                        signTable.AddCell(signCell1);

                        // Строка с надписью "(Подпись)" — выровнена под линией
                        var signCell2 = new iTextSharp.text.pdf.PdfPCell();
                        signCell2.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell2.HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT;
                        signCell2.PaddingLeft = 145;

                        var signLine = new iTextSharp.text.Paragraph();
                        signLine.Add(new iTextSharp.text.Chunk("(Подпись)", fontSmall));
                        signCell2.AddElement(signLine);

                        signTable.AddCell(signCell2);

                        document.Add(signTable);

                        // === ФУТЕР ===
                        var footerLine = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.LIGHT_GRAY, iTextSharp.text.Element.ALIGN_CENTER, 0);
                        var footerLineParagraph = new iTextSharp.text.Paragraph();
                        footerLineParagraph.SpacingBefore = 40;
                        footerLineParagraph.Add(footerLine);
                        document.Add(footerLineParagraph);

                        var footerLine1 = new iTextSharp.text.Paragraph();
                        footerLine1.Add(new iTextSharp.text.Chunk($"{shopName}  |  Часы работы: {shopHours}", fontFooter));
                        footerLine1.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        footerLine1.SpacingBefore = 8;
                        footerLine1.SpacingAfter = 2;
                        document.Add(footerLine1);

                        var footerLine2 = new iTextSharp.text.Paragraph();
                        footerLine2.Add(new iTextSharp.text.Chunk($"{shopPhone}  |  {shopEmail}  |  {shopWebsite}", fontFooter));
                        footerLine2.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        footerLine2.SpacingBefore = 2;
                        document.Add(footerLine2);

                        document.Close();
                    }
                }

                // Открываем файл
                var result = MessageBox.Show(
                    $"Отчёт о сотрудниках сохранён!\n\nФайл: {saveFileDialog.FileName}\nВсего сотрудников: {totalEmployees}\nАдминистраторы: {adminCount}\nМенеджеры: {managerCount}\nМладший персонал: {staffCount}\n\nОткрыть PDF?",
                    "Отчёт сохранён",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = saveFileDialog.FileName,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}\n\nУбедитесь, что библиотека iTextSharp установлена.",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

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

        private void LoadEmployeePhoto(Сотрудники employee)
        {
            try
            {
                if (employee?.Аватарка != null && employee.Аватарка.Length > 0)
                {
                    EmployeePhoto.Source = LoadImageFromBytes(employee.Аватарка);
                }
                else
                {
                    EmployeePhoto.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
                }
            }
            catch
            {
                EmployeePhoto.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
            }
        }

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
                MessageBox.Show($"Ошибка при выборе фото: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateEmployee(out string errorMessage, bool skipPasswordValidation = false)
        {
            var errors = new StringBuilder();
            string lastName = GetActualText(TxtLastName);
            string firstName = GetActualText(TxtFirstName);
            string middleName = GetActualText(TxtMiddleName);
            string phone = GetActualText(TxtPhone);
            string login = GetActualText(TxtLogin);
            string password = GetActualPassword();

            // Фамилия
            if (string.IsNullOrWhiteSpace(lastName))
                errors.AppendLine("• Фамилия не введена");
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(lastName))
                    errors.AppendLine("• Фамилия содержит недопустимые символы");
                else if (lastName.Length > 30)
                    errors.AppendLine("• Фамилия должна быть не длиннее 30 символов");
            }

            // Имя
            if (string.IsNullOrWhiteSpace(firstName))
                errors.AppendLine("• Имя не введено");
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(firstName))
                    errors.AppendLine("• Имя содержит недопустимые символы");
                else if (firstName.Length > 30)
                    errors.AppendLine("• Имя должно быть не длиннее 30 символов");
            }

            // Должность
            if (CmbPosition.SelectedValue == null)
                errors.AppendLine("• Должность не выбрана");

            // Логин
            if (string.IsNullOrWhiteSpace(login))
                errors.AppendLine("• Логин не введен");

            // Пароль
            if (!skipPasswordValidation)
            {
                if (string.IsNullOrWhiteSpace(password))
                    errors.AppendLine("• Пароль не введен");
                else if (password.Length < 12)
                    errors.AppendLine("• Пароль должен быть не менее 12 символов");
            }

            // Телефон
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneRegex = new Regex(@"^\+?\d{11}$");
                if (!phoneRegex.IsMatch(phone))
                    errors.AppendLine("• Неверный формат телефона (11 цифр)");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateEmployee(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string lastName = GetActualText(TxtLastName);
                string firstName = GetActualText(TxtFirstName);
                string middleName = GetActualText(TxtMiddleName);
                string phone = GetActualText(TxtPhone);
                string login = GetActualText(TxtLogin);
                string password = GetActualPassword();

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
                    Фамилия = lastName,
                    Имя = firstName,
                    Отчество = middleName,
                    Телефон = phone,
                    Код_должности = (int)CmbPosition.SelectedValue,
                    Логин = login,
                    Пароль = password,
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

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtEmployeeId.Text))
                {
                    MessageBox.Show("Выберите сотрудника!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string password = GetActualPassword();
                bool skipPasswordValidation = string.IsNullOrEmpty(password);

                if (!ValidateEmployee(out string errorMessage, skipPasswordValidation))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int employeeId = int.Parse(TxtEmployeeId.Text);
                var employee = context.Сотрудники.Find(employeeId);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string lastName = GetActualText(TxtLastName);
                string firstName = GetActualText(TxtFirstName);
                string middleName = GetActualText(TxtMiddleName);
                string phone = GetActualText(TxtPhone);
                string login = GetActualText(TxtLogin);

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

                employee.Фамилия = lastName;
                employee.Имя = firstName;
                employee.Отчество = middleName;
                employee.Телефон = phone;
                employee.Код_должности = (int)CmbPosition.SelectedValue;
                employee.Логин = login;

                if (!string.IsNullOrEmpty(password))
                    employee.Пароль = password;

                if (selectedImageData != null)
                    employee.Аватарка = selectedImageData;

                // Обновление currentUser если редактируется текущий пользователь
                if (currentUser.Код_сотрудника == employee.Код_сотрудника)
                {
                    currentUser.Фамилия = employee.Фамилия;
                    currentUser.Имя = employee.Имя;
                    currentUser.Отчество = employee.Отчество;
                    currentUser.Телефон = employee.Телефон;
                    if (!string.IsNullOrEmpty(password))
                        currentUser.Пароль = password;
                    if (selectedImageData != null)
                        currentUser.Аватарка = selectedImageData;

                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    mainWindow?.SetCurrentUser(currentUser);
                }

                context.SaveChanges();

                MessageBox.Show("Сотрудник обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Получает реальный текст из TextBox, игнорируя плейсхолдер
        /// </summary>
        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;

            var placeholderText = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;

            // Если текст совпадает с плейсхолдером, значит реальных данных нет
            if (!string.IsNullOrEmpty(placeholderText) && text == placeholderText)
                return string.Empty;

            return text;
        }

        /// <summary>
        /// Получает реальный пароль из PasswordBox, игнорируя плейсхолдер
        /// </summary>
        private string GetActualPassword()
        {
            if (PassBox == null) return string.Empty;

            var placeholderText = Addons.PlaceholderBehavior.GetPlaceholderText(PassBox);
            var password = PassBox.Password ?? string.Empty;

            // Если пароль совпадает с плейсхолдером, значит реальных данных нет
            if (!string.IsNullOrEmpty(placeholderText) && password == placeholderText)
                return string.Empty;

            return password;
        }
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtEmployeeId.Text))
                {
                    MessageBox.Show("Выберите сотрудника!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int employeeId = int.Parse(TxtEmployeeId.Text);
                var employee = context.Сотрудники.Find(employeeId);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (currentUser.Код_сотрудника == employee.Код_сотрудника)
                {
                    MessageBox.Show("Нельзя удалить свою учетную запись!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить сотрудника '{employee.Фамилия} {employee.Имя}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Сотрудники.Remove(employee);
                    context.SaveChanges();
                    MessageBox.Show("Сотрудник удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

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

        public string FullName
        {
            get
            {
                var parts = new[] { Фамилия, Имя, Отчество };
                return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
        }

        public string Position => OriginalEmployee.Должность?.Название ?? "Без должности";

        public string LoginDisplay => $"🔑 {Логин}";

        public string AccessLevelDisplay
        {
            get
            {
                var level = OriginalEmployee.Должность?.Уровень_доступа;
                if (!level.HasValue) return "🔒 Уровень доступа: не определен";
                return level.Value >= 4 ? "👑 Администратор" : $"🔒 Уровень доступа: {level}";
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