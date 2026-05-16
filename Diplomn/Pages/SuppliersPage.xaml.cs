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
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

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
            ButtonHelper.CreateActionButtons(actionButtonsPanel,
                canCreate: rights.Suppliers.CanCreate,
                canEdit: rights.Suppliers.CanEdit,
                canDelete: rights.Suppliers.CanDelete,
                createHandler: Add_Click,
                editHandler: Update_Click,
                deleteHandler: Delete_Click,
                clearHandler: ClearForm_Click
            );

            LoadData();
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
                { MessageBox.Show("Нет данных для сохранения отчета.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information); return; }

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

        #region Валидация

        private bool ValidateSupplier(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            var name = GetActualText(TxtSupplierName); var inn = GetActualText(TxtInn);
            var email = GetActualText(TxtEmail); var address = GetActualText(TxtAddress);
            var cl = GetActualText(TxtContactLastName); var cf = GetActualText(TxtContactFirstName);
            var phone = GetActualText(TxtPhone);

            if (string.IsNullOrWhiteSpace(name)) errors.AppendLine("• Введите наименование");
            if (string.IsNullOrWhiteSpace(inn)) errors.AppendLine("• Введите ИНН");
            else if (!Regex.IsMatch(inn, @"^\d+$")) errors.AppendLine("• ИНН должен содержать только цифры");
            else if (inn.Length != 10 && inn.Length != 12) errors.AppendLine("• ИНН должен содержать 10 или 12 цифр");
            else
            {
                var exInn = excludeId.HasValue ? context.Поставщики.Any(s => s.ИНН == inn && s.Код_поставщика != excludeId.Value) : context.Поставщики.Any(s => s.ИНН == inn);
                if (exInn) errors.AppendLine("• Поставщик с таким ИНН уже существует");
            }
            if (string.IsNullOrWhiteSpace(address)) errors.AppendLine("• Введите адрес");
            else if (address.Length < 5) errors.AppendLine("• Адрес должен содержать минимум 5 символов");
            if (string.IsNullOrWhiteSpace(email)) errors.AppendLine("• Введите Email");
            else if (!Regex.IsMatch(email, @"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$")) errors.AppendLine("• Неверный формат Email");
            else
            {
                var exEm = excludeId.HasValue ? context.Поставщики.Any(s => s.Email_поставщика == email && s.Код_поставщика != excludeId.Value) : context.Поставщики.Any(s => s.Email_поставщика == email);
                if (exEm) errors.AppendLine("• Поставщик с таким Email уже существует");
            }
            if (string.IsNullOrWhiteSpace(cl)) errors.AppendLine("• Введите фамилию контактного лица");
            if (string.IsNullOrWhiteSpace(cf)) errors.AppendLine("• Введите имя контактного лица");
            if (!string.IsNullOrWhiteSpace(phone) && !Regex.IsMatch(phone, @"^\+?\d{11}$")) errors.AppendLine("• Телефон должен содержать 11 цифр");

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        #endregion

        #region CRUD операции

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateSupplier(out var err)) { MessageBox.Show(err, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
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
                MessageBox.Show("Поставщик добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData(); ClearForm();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtSupplierId.Text)) { MessageBox.Show("Выберите поставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                var id = int.Parse(TxtSupplierId.Text); var s = context.Поставщики.Find(id);
                if (s == null) { MessageBox.Show("Поставщик не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                if (!ValidateSupplier(out var err, id)) { MessageBox.Show(err, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }

                s.Наименование_поставщика = GetActualText(TxtSupplierName); s.ИНН = GetActualText(TxtInn);
                s.Адрес_поставщика = GetActualText(TxtAddress); s.Email_поставщика = GetActualText(TxtEmail);
                s.Фамилия_контактного_лица = GetActualText(TxtContactLastName); s.Имя_контактного_лица = GetActualText(TxtContactFirstName);
                s.Отчество_контактного_лица = GetActualText(TxtContactMiddleName); s.Телефон_контактного_лица = GetActualText(TxtPhone);
                if (selectedImageData != null) s.Логотип = selectedImageData;
                context.SaveChanges();
                MessageBox.Show("Поставщик обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData(); ClearForm();
            }
            catch (Exception ex) { MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtSupplierId.Text)) { MessageBox.Show("Выберите поставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                var id = int.Parse(TxtSupplierId.Text); var s = context.Поставщики.Find(id);
                if (s == null) { MessageBox.Show("Поставщик не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                if (context.Поставка.Any(o => o.Код_поставщика == id)) { MessageBox.Show("Нельзя удалить — есть связанные поставки!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
                if (MessageBox.Show($"Удалить «{s.Наименование_поставщика}»?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                { context.Поставщики.Remove(s); context.SaveChanges(); MessageBox.Show("Удалён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information); LoadData(); ClearForm(); }
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
        }
        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

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