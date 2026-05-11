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
using System.Windows.Media.Imaging;

namespace Diplomn.Pages
{
    public partial class SuppliersPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;
        private ObservableCollection<SupplierViewModel> suppliersView;

        public SuppliersPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Поставщики — {user.Фамилия} {user.Имя}";
            suppliersView = new ObservableCollection<SupplierViewModel>();
            LoadData();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private IQueryable<Поставщики> GetFilteredQuery()
        {
            var query = context.Поставщики.AsQueryable();

            string searchText = GetActualText(TxtSearch);
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                var term = searchText;
                query = query.Where(s => s.Наименование_поставщика.Contains(term) ||
                                        s.ИНН.Contains(term) ||
                                        s.Фамилия_контактного_лица.Contains(term));
            }

            return query;
        }

        private void LoadData()
        {
            var suppliers = context.Поставщики.ToList();
            UpdateSuppliersView(suppliers);
        }

        private void UpdateSuppliersView(List<Поставщики> suppliers)
        {
            suppliersView.Clear();
            foreach (var supplier in suppliers)
            {
                suppliersView.Add(new SupplierViewModel(supplier));
            }
            ListViewSuppliers.ItemsSource = suppliersView;
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
            var suppliers = GetFilteredQuery().ToList();
            UpdateSuppliersView(suppliers);
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
                var suppliers = GetFilteredQuery().ToList();

                if (!suppliers.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF файл (*.pdf)|*.pdf",
                    Title = "Сохранить отчет о поставщиках",
                    FileName = $"Отчет_поставщики_{DateTime.Now:yyyy-MM-dd_HH-mm}"
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

                // Создаём PDF
                using (var document = new iTextSharp.text.Document(iTextSharp.text.PageSize.A4, 40, 40, 40, 40))
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
                        var fontFooter = new iTextSharp.text.Font(baseFont, 8, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.GRAY);
                        var fontSmall = new iTextSharp.text.Font(baseFont, 9, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.DARK_GRAY);
                        var fontSign = new iTextSharp.text.Font(baseFont, 10, iTextSharp.text.Font.NORMAL, iTextSharp.text.BaseColor.BLACK);

                        // === ЗАГОЛОВОК ===
                        var reportTitle = new iTextSharp.text.Paragraph("ОТЧЁТ О ПОСТАВЩИКАХ", fontTitle);
                        reportTitle.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        reportTitle.SpacingAfter = 20;
                        document.Add(reportTitle);

                        // === ТАБЛИЦА ===
                        var table = new iTextSharp.text.pdf.PdfPTable(6);
                        table.WidthPercentage = 100;
                        table.SetWidths(new float[] { 8, 25, 15, 22, 15, 15 });
                        table.SpacingBefore = 10;
                        table.SpacingAfter = 20;

                        // Заголовки таблицы
                        var headers = new[] { "Код", "Наименование", "ИНН", "Контактное лицо", "Телефон", "Email" };
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
                        foreach (var supplier in suppliers)
                        {
                            var contactPerson = new StringBuilder();
                            contactPerson.Append(supplier.Фамилия_контактного_лица);
                            contactPerson.Append($" {supplier.Имя_контактного_лица}");
                            if (!string.IsNullOrWhiteSpace(supplier.Отчество_контактного_лица))
                                contactPerson.Append($" {supplier.Отчество_контактного_лица}");

                            var phone = supplier.Телефон_контактного_лица ?? "-";
                            var email = supplier.Email_поставщика;

                            var cells = new[]
                            {
                        supplier.Код_поставщика.ToString(),
                        supplier.Наименование_поставщика,
                        supplier.ИНН,
                        contactPerson.ToString(),
                        phone,
                        email
                    };

                            foreach (var cellText in cells)
                            {
                                var cell = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(cellText, fontTableCell));
                                cell.Padding = 5;
                                cell.VerticalAlignment = iTextSharp.text.Element.ALIGN_MIDDLE;

                                if (alternate)
                                {
                                    cell.BackgroundColor = new iTextSharp.text.BaseColor(240, 245, 250);
                                }

                                // Выравнивание для числовых колонок
                                if (cellText == cells[0] || cellText == cells[2])
                                {
                                    cell.HorizontalAlignment = iTextSharp.text.Element.ALIGN_CENTER;
                                }

                                table.AddCell(cell);
                            }

                            alternate = !alternate;
                        }

                        document.Add(table);

                        // === ИТОГО ===
                        var countParagraph = new iTextSharp.text.Paragraph($"Всего поставщиков: {suppliers.Count}", fontSubtitle);
                        countParagraph.Alignment = iTextSharp.text.Element.ALIGN_RIGHT;
                        countParagraph.SpacingBefore = 5;
                        countParagraph.SpacingAfter = 30;
                        document.Add(countParagraph);

                        // === ПОДПИСЬ ===
                        var signParagraph = new iTextSharp.text.Paragraph();
                        signParagraph.Alignment = iTextSharp.text.Element.ALIGN_RIGHT;
                        signParagraph.SpacingAfter = 5;

                        var signTable = new iTextSharp.text.pdf.PdfPTable(1);
                        signTable.WidthPercentage = 50;
                        signTable.HorizontalAlignment = iTextSharp.text.Element.ALIGN_RIGHT;

                        // Строка с должностью и ФИО
                        var signCell1 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(
                            $"{currentUser.Должность?.Название ?? "Сотрудник"} {initials} _______________", fontSign));
                        signCell1.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell1.HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT;
                        signCell1.PaddingBottom = 15;
                        signTable.AddCell(signCell1);

                        // Строка с датой
                        var signCell2 = new iTextSharp.text.pdf.PdfPCell(new iTextSharp.text.Paragraph(
                            $"Дата: {DateTime.Now:dd.MM.yyyy}", fontSmall));
                        signCell2.Border = iTextSharp.text.Rectangle.NO_BORDER;
                        signCell2.HorizontalAlignment = iTextSharp.text.Element.ALIGN_LEFT;
                        signTable.AddCell(signCell2);

                        document.Add(signTable);

                        // === ФУТЕР ===
                        var footerLine = new iTextSharp.text.pdf.draw.LineSeparator(1f, 100f, iTextSharp.text.BaseColor.LIGHT_GRAY, iTextSharp.text.Element.ALIGN_CENTER, 0);
                        var footerLineParagraph = new iTextSharp.text.Paragraph();
                        footerLineParagraph.SpacingBefore = 20;
                        footerLineParagraph.Add(footerLine);
                        document.Add(footerLineParagraph);

                        var footer = new iTextSharp.text.Paragraph();
                        footer.Add(new iTextSharp.text.Chunk($"{shopName} | {shopPhone} | {shopEmail} | {shopWebsite}", fontFooter));
                        footer.Add(new iTextSharp.text.Chunk($"\nЧасы работы: {shopHours} | Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}", fontFooter));
                        footer.Alignment = iTextSharp.text.Element.ALIGN_CENTER;
                        footer.SpacingBefore = 5;
                        document.Add(footer);

                        document.Close();
                    }
                }

                // Открываем файл
                var result = MessageBox.Show(
                    $"Отчёт о поставщиках сохранён!\n\nФайл: {saveFileDialog.FileName}\nПоставщиков: {suppliers.Count}\n\nОткрыть PDF?",
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
        private void ListViewSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSuppliers.SelectedItem is SupplierViewModel selectedSupplier)
            {
                var supplier = selectedSupplier.OriginalSupplier;
                TxtSupplierId.Text = supplier.Код_поставщика.ToString();
                TxtSupplierName.Text = supplier.Наименование_поставщика;
                TxtInn.Text = supplier.ИНН;
                TxtAddress.Text = supplier.Адрес_поставщика;
                TxtEmail.Text = supplier.Email_поставщика;
                TxtContactLastName.Text = supplier.Фамилия_контактного_лица;
                TxtContactFirstName.Text = supplier.Имя_контактного_лица;
                TxtContactMiddleName.Text = supplier.Отчество_контактного_лица;
                TxtPhone.Text = supplier.Телефон_контактного_лица;

                LoadSupplierLogo(supplier);
                selectedImageData = null;
            }
        }

        private void LoadSupplierLogo(Поставщики supplier)
        {
            try
            {
                if (supplier?.Логотип != null && supplier.Логотип.Length > 0)
                {
                    SupplierLogo.Source = LoadImageFromBytes(supplier.Логотип);
                }
                else
                {
                    SupplierLogo.Source = new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute));
                }
            }
            catch
            {
                SupplierLogo.Source = new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute));
            }
        }

        private void SelectLogo_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                    Title = "Выберите логотип поставщика"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedImageData = File.ReadAllBytes(openFileDialog.FileName);
                    SupplierLogo.Source = LoadImageFromBytes(selectedImageData);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе логотипа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateSupplier(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();

            string name = GetActualText(TxtSupplierName);
            string inn = GetActualText(TxtInn);
            string email = GetActualText(TxtEmail);
            string address = GetActualText(TxtAddress);
            string contactLastName = GetActualText(TxtContactLastName);
            string contactFirstName = GetActualText(TxtContactFirstName);
            string contactMiddleName = GetActualText(TxtContactMiddleName);
            string phone = GetActualText(TxtPhone);

            // Наименование
            if (string.IsNullOrWhiteSpace(name))
                errors.AppendLine("• Введите наименование поставщика");

            // ИНН
            if (string.IsNullOrWhiteSpace(inn))
                errors.AppendLine("• Введите ИНН");
            else if (!Regex.IsMatch(inn, @"^\d+$"))
                errors.AppendLine("• ИНН должен содержать только цифры");
            else if (inn.Length != 10 && inn.Length != 12)
                errors.AppendLine("• ИНН должен содержать 10 или 12 цифр");
            else
            {
                bool innExists = excludeId.HasValue
                    ? context.Поставщики.Any(s => s.ИНН == inn && s.Код_поставщика != excludeId.Value)
                    : context.Поставщики.Any(s => s.ИНН == inn);

                if (innExists)
                    errors.AppendLine("• Поставщик с таким ИНН уже существует");
            }

            // Адрес
            if (string.IsNullOrWhiteSpace(address))
                errors.AppendLine("• Введите адрес");
            else if (address.Length < 5)
                errors.AppendLine("• Адрес должен содержать минимум 5 символов");

            // Email
            if (string.IsNullOrWhiteSpace(email))
                errors.AppendLine("• Введите Email");
            else
            {
                var emailRegex = new Regex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$");
                if (!emailRegex.IsMatch(email))
                    errors.AppendLine("• Неверный формат Email");
                else
                {
                    bool emailExists = excludeId.HasValue
                        ? context.Поставщики.Any(s => s.Email_поставщика == email && s.Код_поставщика != excludeId.Value)
                        : context.Поставщики.Any(s => s.Email_поставщика == email);

                    if (emailExists)
                        errors.AppendLine("• Поставщик с таким Email уже существует");
                }
            }

            // Контактное лицо
            if (string.IsNullOrWhiteSpace(contactLastName))
                errors.AppendLine("• Введите фамилию контактного лица");

            if (string.IsNullOrWhiteSpace(contactFirstName))
                errors.AppendLine("• Введите имя контактного лица");

            // Телефон (опционально)
            if (!string.IsNullOrWhiteSpace(phone))
            {
                if (!Regex.IsMatch(phone, @"^\+?\d{11}$"))
                    errors.AppendLine("• Телефон должен содержать 11 цифр");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateSupplier(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var supplier = new Поставщики
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

                context.Поставщики.Add(supplier);
                context.SaveChanges();

                MessageBox.Show("Поставщик успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtSupplierId.Text))
                {
                    MessageBox.Show("Выберите поставщика для обновления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int supplierId = int.Parse(TxtSupplierId.Text);
                var supplier = context.Поставщики.Find(supplierId);

                if (supplier == null)
                {
                    MessageBox.Show("Поставщик не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateSupplier(out string errorMessage, supplierId))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                supplier.Наименование_поставщика = GetActualText(TxtSupplierName);
                supplier.ИНН = GetActualText(TxtInn);
                supplier.Адрес_поставщика = GetActualText(TxtAddress);
                supplier.Email_поставщика = GetActualText(TxtEmail);
                supplier.Фамилия_контактного_лица = GetActualText(TxtContactLastName);
                supplier.Имя_контактного_лица = GetActualText(TxtContactFirstName);
                supplier.Отчество_контактного_лица = GetActualText(TxtContactMiddleName);
                supplier.Телефон_контактного_лица = GetActualText(TxtPhone);

                if (selectedImageData != null)
                    supplier.Логотип = selectedImageData;

                context.SaveChanges();

                MessageBox.Show("Поставщик успешно обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtSupplierId.Text))
                {
                    MessageBox.Show("Выберите поставщика для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int supplierId = int.Parse(TxtSupplierId.Text);
                var supplier = context.Поставщики.Find(supplierId);

                if (supplier == null)
                {
                    MessageBox.Show("Поставщик не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var hasSupplies = context.Поставка.Any(o => o.Код_поставщика == supplierId);
                if (hasSupplies)
                {
                    MessageBox.Show("Нельзя удалить поставщика — он используется в поставках!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить поставщика '{supplier.Наименование_поставщика}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Поставщики.Remove(supplier);
                    context.SaveChanges();
                    MessageBox.Show("Поставщик удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            TxtSupplierId.Text = "";
            TxtSupplierName.Text = "";
            TxtInn.Text = "";
            TxtAddress.Text = "";
            TxtEmail.Text = "";
            TxtContactLastName.Text = "";
            TxtContactFirstName.Text = "";
            TxtContactMiddleName.Text = "";
            TxtPhone.Text = "";
            SupplierLogo.Source = new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute));
            selectedImageData = null;
            ListViewSuppliers.SelectedItem = null;
        }

        /// <summary>
        /// Получает реальный текст из TextBox, игнорируя плейсхолдер
        /// </summary>
        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;

            var placeholderText = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(placeholderText) && text == placeholderText)
                return string.Empty;

            return text;
        }
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

        public string ContactPersonDisplay
        {
            get
            {
                var parts = new[] { Фамилия_контактного_лица, Имя_контактного_лица, Отчество_контактного_лица };
                return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
            }
        }

        public string PhoneDisplay => string.IsNullOrWhiteSpace(Телефон_контактного_лица) ? "☎ Телефон не указан" : $"☎ {Телефон_контактного_лица}";

        public SupplierViewModel(Поставщики supplier)
        {
            OriginalSupplier = supplier;
            Наименование_поставщика = supplier.Наименование_поставщика;
            ИНН = $"ИНН: {supplier.ИНН}";
            Фамилия_контактного_лица = supplier.Фамилия_контактного_лица;
            Имя_контактного_лица = supplier.Имя_контактного_лица;
            Отчество_контактного_лица = supplier.Отчество_контактного_лица;
            Телефон_контактного_лица = supplier.Телефон_контактного_лица;

            // Загружаем логотип
            if (supplier.Логотип != null && supplier.Логотип.Length > 0)
            {
                try
                {
                    using (var ms = new MemoryStream(supplier.Логотип))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        LogoSource = bitmap;
                    }
                }
                catch
                {
                    LogoSource = new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute));
                }
            }
            else
            {
                LogoSource = new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute));
            }
        }
    }
}