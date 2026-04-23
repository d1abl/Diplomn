using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
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

        private void LoadData()
        {
            DataGridSuppliers.ItemsSource = context.Поставщики.ToList();
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
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки логотипа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                SupplierLogo.Source = new BitmapImage(new Uri("/Photos/istocklogo.png", UriKind.RelativeOrAbsolute));
            }
        }

        private void SelectLogo_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Все файлы (*.*)|*.*",
                    Title = "Выберите логотип поставщика"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    // Загружаем и обрезаем изображение
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
            // Загружаем изображение
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(filePath);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();

            // Создаем обрезанную/масштабированную версию
            var resizedBitmap = new BitmapImage();
            resizedBitmap.BeginInit();
            resizedBitmap.UriSource = new Uri(filePath);
            resizedBitmap.DecodePixelWidth = maxWidth;
            resizedBitmap.DecodePixelHeight = maxHeight;
            resizedBitmap.CacheOption = BitmapCacheOption.OnLoad;
            resizedBitmap.EndInit();

            // Конвертируем в массив байтов
            using (var ms = new MemoryStream())
            {
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(resizedBitmap));
                encoder.Save(ms);
                return ms.ToArray();
            }
        }

        private bool ValidateSupplier(out string errorMessage)
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
                errors.AppendLine("❌ Поле 'Наименование поставщика' обязательно для заполнения.");
            else
            {
                var lettersOnly = Regex.Replace(name, @"[^A-Za-zА-Яа-яЁё]", "");
                if (lettersOnly.Length < 2)
                    errors.AppendLine("❌ Наименование должно содержать минимум 2 буквы.");
                else
                {
                    var vowel = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    var consonant = new Regex(@"[B-DF-HJ-NP-TV-Zb-df-hj-np-tv-zБ-ЖЗЙ-НП-РСТ-Яб-жзй-нп-рст-я]");
                    if (!vowel.IsMatch(lettersOnly) || !consonant.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Наименование должно содержать хотя бы одну гласную и одну согласную.");
                }
            }

            // ИНН
            if (string.IsNullOrWhiteSpace(inn))
                errors.AppendLine("❌ Поле 'ИНН' обязательно для заполнения.");
            else if (!Regex.IsMatch(inn, "^\\d+$"))
                errors.AppendLine("❌ ИНН должен содержать только цифры.");
            else if (inn.Length != 10 && inn.Length != 12)
                errors.AppendLine("❌ ИНН должен содержать 10 или 12 цифр.");

            // Адрес
            if (string.IsNullOrWhiteSpace(address))
                errors.AppendLine("❌ Поле 'Адрес поставщика' обязательно для заполнения.");
            else if (address.Length < 5)
                errors.AppendLine("❌ Адрес должен содержать минимум 5 символов.");
            else
            {
                var addressRegex = new Regex(@"^[A-Za-zА-Яа-яЁё0-9\s\.,\-/]+$");
                if (!addressRegex.IsMatch(address))
                    errors.AppendLine("❌ Адрес содержит недопустимые символы.");
            }

            // Email
            if (string.IsNullOrWhiteSpace(email))
                errors.AppendLine("❌ Поле 'Email поставщика' обязательно для заполнения.");
            else
            {
                var emailRegex = new Regex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$");
                if (!emailRegex.IsMatch(email))
                    errors.AppendLine("❌ Неверный формат Email.");
            }

            // Фамилия контактного лица
            if (string.IsNullOrWhiteSpace(contactLastName))
                errors.AppendLine("❌ Поле 'Фамилия контактного лица' обязательно для заполнения.");
            else
            {
                var lettersOnly = Regex.Replace(contactLastName, @"[^A-Za-zА-Яа-яЁё]", "");
                if (lettersOnly.Length < 2)
                    errors.AppendLine("❌ Фамилия должна содержать минимум 2 буквы.");
                else
                {
                    var vowel = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    var consonant = new Regex(@"[B-DF-HJ-NP-TV-Zb-df-hj-np-tv-zБ-ЖЗЙ-НП-РСТ-Яб-жзй-нп-рст-я]");
                    if (!vowel.IsMatch(lettersOnly) || !consonant.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Фамилия должна содержать хотя бы одну гласную и одну согласную.");
                }
            }

            // Имя контактного лица
            if (string.IsNullOrWhiteSpace(contactFirstName))
                errors.AppendLine("❌ Поле 'Имя контактного лица' обязательно для заполнения.");
            else
            {
                var lettersOnly = Regex.Replace(contactFirstName, @"[^A-Za-zА-Яа-яЁё]", "");
                if (lettersOnly.Length < 2)
                    errors.AppendLine("❌ Имя должно содержать минимум 2 буквы.");
                else
                {
                    var vowel = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    var consonant = new Regex(@"[B-DF-HJ-NP-TV-Zb-df-hj-np-tv-zБ-ЖЗЙ-НП-РСТ-Яб-жзй-нп-рст-я]");
                    if (!vowel.IsMatch(lettersOnly) || !consonant.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Имя должно содержать хотя бы одну гласную и одну согласную.");
                }
            }

            // Отчество (опционально)
            if (!string.IsNullOrWhiteSpace(contactMiddleName))
            {
                var lettersOnly = Regex.Replace(contactMiddleName, @"[^A-Za-zА-Яа-яЁё]", "");
                if (lettersOnly.Length < 2)
                    errors.AppendLine("❌ Отчество должно содержать минимум 2 буквы.");
                else
                {
                    var vowel = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    var consonant = new Regex(@"[B-DF-HJ-NP-TV-Zb-df-hj-np-tv-zБ-ЖЗЙ-НП-РСТ-Яб-жзй-нп-рст-я]");
                    if (!vowel.IsMatch(lettersOnly) || !consonant.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Отчество должно содержать хотя бы одну гласную и одну согласную.");
                }
            }

            // Телефон (опционально)
            if (!string.IsNullOrWhiteSpace(phone))
            {
                if (!Regex.IsMatch(phone, @"^\+?\d{11}$"))
                    errors.AppendLine("❌ Телефон должен содержать 11 цифр, можно с '+' в начале.");
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

                string inn = TxtInn.Text?.Trim();
                string email = TxtEmail.Text?.Trim();

                // Проверка уникальности ИНН
                bool innExists = context.Поставщики.Any(s => s.ИНН == inn);
                if (innExists)
                {
                    MessageBox.Show("Поставщик с таким ИНН уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка уникальности Email
                bool emailExists = context.Поставщики.Any(s => s.Email_поставщика == email);
                if (emailExists)
                {
                    MessageBox.Show("Поставщик с таким Email уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var supplier = new Поставщики
                {
                    Наименование_поставщика = TxtSupplierName.Text?.Trim(),
                    ИНН = inn,
                    Адрес_поставщика = TxtAddress.Text?.Trim(),
                    Email_поставщика = email,
                    Фамилия_контактного_лица = TxtContactLastName.Text?.Trim(),
                    Имя_контактного_лица = TxtContactFirstName.Text?.Trim(),
                    Отчество_контактного_лица = TxtContactMiddleName.Text?.Trim(),
                    Телефон_контактного_лица = TxtPhone.Text?.Trim(),
                    Логотип = selectedImageData  // Сохраняем логотип
                };

                context.Поставщики.Add(supplier);
                context.SaveChanges();

                MessageBox.Show("Поставщик успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении поставщика: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                if (!ValidateSupplier(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int supplierId = int.Parse(TxtSupplierId.Text);
                var supplier = context.Поставщики.Find(supplierId);

                if (supplier == null)
                {
                    MessageBox.Show("Поставщик не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string inn = TxtInn.Text?.Trim();
                string email = TxtEmail.Text?.Trim();

                // Проверка уникальности ИНН (исключая текущего поставщика)
                bool innExists = context.Поставщики.Any(s => s.ИНН == inn && s.Код_поставщика != supplierId);
                if (innExists)
                {
                    MessageBox.Show("Поставщик с таким ИНН уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка уникальности Email (исключая текущего поставщика)
                bool emailExists = context.Поставщики.Any(s => s.Email_поставщика == email && s.Код_поставщика != supplierId);
                if (emailExists)
                {
                    MessageBox.Show("Поставщик с таким Email уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                supplier.Наименование_поставщика = TxtSupplierName.Text?.Trim();
                supplier.ИНН = inn;
                supplier.Адрес_поставщика = TxtAddress.Text?.Trim();
                supplier.Email_поставщика = email;
                supplier.Фамилия_контактного_лица = TxtContactLastName.Text?.Trim();
                supplier.Имя_контактного_лица = TxtContactFirstName.Text?.Trim();
                supplier.Отчество_контактного_лица = TxtContactMiddleName.Text?.Trim();
                supplier.Телефон_контактного_лица = TxtPhone.Text?.Trim();

                // Обновляем логотип только если был выбран новый
                if (selectedImageData != null)
                {
                    supplier.Логотип = selectedImageData;
                }

                context.SaveChanges();

                MessageBox.Show("Поставщик успешно обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении поставщика: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                var hasOrders = context.Состав_заказа.Any(o => o.Код_поставщика == supplierId);
                if (hasOrders)
                {
                    MessageBox.Show("Нельзя удалить поставщика, так как он используется в заказах!",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите удалить поставщика '{supplier.Наименование_поставщика}'?",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Поставщики.Remove(supplier);
                    context.SaveChanges();
                    MessageBox.Show("Поставщик успешно удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении поставщика: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

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

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}