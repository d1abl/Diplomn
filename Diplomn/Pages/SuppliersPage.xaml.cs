using Microsoft.Win32;
using System;
using System.Collections.Generic;
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

        public SuppliersPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
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

            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                query = query.Where(s => s.Наименование_поставщика.Contains(term) ||
                                        s.ИНН.Contains(term) ||
                                        s.Фамилия_контактного_лица.Contains(term));
            }

            return query;
        }

        private void LoadData()
        {
            DataGridSuppliers.ItemsSource = context.Поставщики.ToList();
        }

        private void ApplyFilters()
        {
            DataGridSuppliers.ItemsSource = GetFilteredQuery().ToList();
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
                    Filter = "CSV файл (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt",
                    Title = "Сохранить отчет о поставщиках",
                    FileName = $"Отчет_поставщики_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Отчет о поставщиках от {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine($"Сформировал: {currentUser.Фамилия} {currentUser.Имя}");

                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                        sb.AppendLine($"Поиск: \"{TxtSearch.Text}\"");

                    sb.AppendLine();
                    sb.AppendLine($"Всего поставщиков: {suppliers.Count}");
                    sb.AppendLine();
                    sb.AppendLine("Код;Наименование;ИНН;Контактное лицо;Телефон;Email;Адрес");

                    foreach (var supplier in suppliers)
                    {
                        var contactPerson = $"{supplier.Фамилия_контактного_лица} {supplier.Имя_контактного_лица} {supplier.Отчество_контактного_лица ?? ""}".Trim();
                        sb.AppendLine($"{supplier.Код_поставщика};{supplier.Наименование_поставщика};{supplier.ИНН};{contactPerson};{supplier.Телефон_контактного_лица ?? "-"};{supplier.Email_поставщика};{supplier.Адрес_поставщика}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Отчет сохранен!\n{saveFileDialog.FileName}", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DataGridSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridSuppliers.SelectedItem is Поставщики supplier)
            {
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
                    using (var ms = new MemoryStream(supplier.Логотип))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        SupplierLogo.Source = bitmap;
                    }
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
                    selectedImageData = LoadAndResizeImage(openFileDialog.FileName, 200, 200);

                    using (var ms = new MemoryStream(selectedImageData))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        SupplierLogo.Source = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе логотипа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private byte[] LoadAndResizeImage(string filePath, int maxWidth, int maxHeight)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            var resizedBitmap = new BitmapImage();
            resizedBitmap.BeginInit();
            resizedBitmap.UriSource = new Uri(filePath);
            resizedBitmap.DecodePixelWidth = maxWidth;
            resizedBitmap.DecodePixelHeight = maxHeight;
            resizedBitmap.CacheOption = BitmapCacheOption.OnLoad;
            resizedBitmap.EndInit();

            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(resizedBitmap));
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        private bool ValidateSupplier(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();

            string name = TxtSupplierName.Text?.Trim();
            string inn = TxtInn.Text?.Trim();
            string email = TxtEmail.Text?.Trim();
            string address = TxtAddress.Text?.Trim();
            string contactLastName = TxtContactLastName.Text?.Trim();
            string contactFirstName = TxtContactFirstName.Text?.Trim();
            string contactMiddleName = TxtContactMiddleName.Text?.Trim();
            string phone = TxtPhone.Text?.Trim();

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
                    Наименование_поставщика = TxtSupplierName.Text?.Trim(),
                    ИНН = TxtInn.Text?.Trim(),
                    Адрес_поставщика = TxtAddress.Text?.Trim(),
                    Email_поставщика = TxtEmail.Text?.Trim(),
                    Фамилия_контактного_лица = TxtContactLastName.Text?.Trim(),
                    Имя_контактного_лица = TxtContactFirstName.Text?.Trim(),
                    Отчество_контактного_лица = TxtContactMiddleName.Text?.Trim(),
                    Телефон_контактного_лица = TxtPhone.Text?.Trim(),
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

                supplier.Наименование_поставщика = TxtSupplierName.Text?.Trim();
                supplier.ИНН = TxtInn.Text?.Trim();
                supplier.Адрес_поставщика = TxtAddress.Text?.Trim();
                supplier.Email_поставщика = TxtEmail.Text?.Trim();
                supplier.Фамилия_контактного_лица = TxtContactLastName.Text?.Trim();
                supplier.Имя_контактного_лица = TxtContactFirstName.Text?.Trim();
                supplier.Отчество_контактного_лица = TxtContactMiddleName.Text?.Trim();
                supplier.Телефон_контактного_лица = TxtPhone.Text?.Trim();

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

                var hasSupplies = context.Состав_поставки.Any(o => o.Код_поставщика == supplierId);
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
            DataGridSuppliers.SelectedItem = null;
        }
    }
}