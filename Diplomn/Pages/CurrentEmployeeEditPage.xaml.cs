using Diplomn.Addons;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Diplomn.Pages
{
    public partial class CurrentEmployeeEditPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;

        public CurrentEmployeeEditPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadData();
        }

        private void LoadData()
        {
            if (currentUser == null) return;

            TxtLastName.Text = currentUser.Фамилия;
            TxtFirstName.Text = currentUser.Имя;
            TxtMiddleName.Text = currentUser?.Отчество;
            TxtPhone.Text = currentUser?.Телефон;
            TxtEmployeePosition.Text = currentUser.Должность.Название;
            TxtLogin.Text = currentUser.Логин;
            PassBox.Password = currentUser.Пароль;

            // Загрузка фото пользователя
            LoadUserPhoto();
        }

        private void LoadUserPhoto()
        {
            try
            {
                if (currentUser?.Аватарка != null && currentUser.Аватарка.Length > 0)
                {
                    using (var ms = new MemoryStream(currentUser.Аватарка))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        EmployeeImage.Source = bitmap;
                    }
                }
                else
                {
                    // Установка изображения по умолчанию
                    EmployeeImage.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.Relative));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фото: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                EmployeeImage.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.Relative));
            }
        }

        private void SelectPhoto_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Все файлы (*.*)|*.*",
                    Title = "Выберите фотографию сотрудника"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedImageData = File.ReadAllBytes(openFileDialog.FileName);

                    using (var ms = new MemoryStream(selectedImageData))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        EmployeeImage.Source = bitmap;
                    }
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
            string password = GetActualPassword();

            // Фамилия
            if (string.IsNullOrWhiteSpace(lastName))
                errors.AppendLine("❌ Фамилия не введена!");
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(lastName))
                    errors.AppendLine("❌ Фамилия содержит недопустимые символы!");
                else if (lastName.Length > 30)
                    errors.AppendLine("❌ Фамилия должна быть не длиннее 30 символов!");
                else
                {
                    var lettersOnly = Regex.Replace(lastName, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errors.AppendLine("❌ Фамилия должна содержать минимум 2 буквы!");
                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Фамилия должна содержать хотя бы одну гласную!");
                }
            }

            // Имя
            if (string.IsNullOrWhiteSpace(firstName))
                errors.AppendLine("❌ Имя не введено!");
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(firstName))
                    errors.AppendLine("❌ Имя содержит недопустимые символы!");
                else if (firstName.Length > 30)
                    errors.AppendLine("❌ Имя должно быть не длиннее 30 символов!");
                else
                {
                    var lettersOnly = Regex.Replace(firstName, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errors.AppendLine("❌ Имя должно содержать минимум 2 буквы!");
                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Имя должно содержать хотя бы одну гласную!");
                }
            }

            // Отчество (опционально)
            if (!string.IsNullOrWhiteSpace(middleName))
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(middleName))
                    errors.AppendLine("❌ Отчество содержит недопустимые символы!");
                else if (middleName.Length > 30)
                    errors.AppendLine("❌ Отчество должно быть не длиннее 30 символов!");
                else
                {
                    var lettersOnly = Regex.Replace(middleName, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errors.AppendLine("❌ Отчество должно содержать минимум 2 буквы!");
                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Отчество должно содержать хотя бы одну гласную!");
                }
            }

            // Пароль
            if (!skipPasswordValidation)
            {
                if (string.IsNullOrWhiteSpace(password))
                    errors.AppendLine("❌ Пароль не введен!");
                else
                {
                    if (password.Length < 12)
                        errors.AppendLine("❌ Пароль должен быть не менее 12 символов!");
                    if (!Regex.IsMatch(password, "[A-ZА-ЯЁ]"))
                        errors.AppendLine("❌ Пароль должен содержать хотя бы одну заглавную букву!");
                    if (!Regex.IsMatch(password, "[a-zа-яё]"))
                        errors.AppendLine("❌ Пароль должен содержать хотя бы одну строчную букву!");
                    if (!Regex.IsMatch(password, "\\d"))
                        errors.AppendLine("❌ Пароль должен содержать хотя бы одну цифру!");
                    if (!Regex.IsMatch(password, "[^A-Za-zА-Яа-яЁё0-9]"))
                        errors.AppendLine("❌ Пароль должен содержать хотя бы один специальный символ!");
                }
            }

            // Телефон
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneRegex = new Regex(@"^\+?\d{11}$");
                if (!phoneRegex.IsMatch(phone))
                    errors.AppendLine("❌ Неверный формат телефона. Ожидается 11 цифр, можно с '+' в начале.");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // При обновлении пароль может оставаться пустым (не изменяется)
                string password = GetActualPassword();
                bool skipPasswordValidation = string.IsNullOrEmpty(password);

                if (!ValidateEmployee(out string errorMessage, skipPasswordValidation))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string phone = GetActualText(TxtPhone);

                // Находим сотрудника в базе данных
                var employee = context.Сотрудники.Find(currentUser.Код_сотрудника);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверка уникальности телефона (если указан, исключая текущего сотрудника)
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    bool phoneExists = context.Сотрудники.Any(s => s.Телефон == phone && s.Код_сотрудника != currentUser.Код_сотрудника);
                    if (phoneExists)
                    {
                        MessageBox.Show("Телефон уже используется другим сотрудником!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Обновление данных
                employee.Фамилия = GetActualText(TxtLastName);
                employee.Имя = GetActualText(TxtFirstName);
                employee.Отчество = GetActualText(TxtMiddleName);
                employee.Телефон = phone;

                // Обновляем пароль только если он был введен
                if (!string.IsNullOrEmpty(password))
                {
                    employee.Пароль = password;
                }

                // Обновляем аватар если был выбран новый
                if (selectedImageData != null)
                {
                    employee.Аватарка = selectedImageData;
                }

                context.SaveChanges();

                // Обновляем локальный объект
                currentUser.Фамилия = employee.Фамилия;
                currentUser.Имя = employee.Имя;
                currentUser.Отчество = employee.Отчество;
                currentUser.Телефон = employee.Телефон;
                if (!string.IsNullOrEmpty(password))
                {
                    currentUser.Пароль = employee.Пароль;
                }
                if (selectedImageData != null)
                {
                    currentUser.Аватарка = selectedImageData;
                }

                // Обновляем отображение в MainWindow
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow != null)
                {
                    mainWindow.SetCurrentUser(currentUser);
                }

                MessageBox.Show("Данные успешно обновлены!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Возвращаемся назад
                if (NavigationService.CanGoBack)
                {
                    NavigationService.GoBack();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении данных: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

            if (!string.IsNullOrEmpty(placeholderText) && password == placeholderText)
                return string.Empty;

            return password;
        }
    }
}