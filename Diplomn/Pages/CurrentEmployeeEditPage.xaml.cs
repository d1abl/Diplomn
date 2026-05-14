using Diplomn.Addons;
using Microsoft.Win32;
using System;
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
    /// Страница редактирования профиля текущего пользователя
    /// </summary>
    public partial class CurrentEmployeeEditPage : Page
    {
        #region Поля

        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;

        #endregion

        #region Конструктор

        public CurrentEmployeeEditPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadData();
        }

        #endregion

        #region Загрузка данных

        /// <summary>
        /// Заполняет форму данными текущего пользователя
        /// </summary>
        private void LoadData()
        {
            if (currentUser == null) return;

            TxtLastName.Text = currentUser.Фамилия;
            TxtFirstName.Text = currentUser.Имя;
            TxtMiddleName.Text = currentUser.Отчество;
            TxtPhone.Text = currentUser.Телефон;
            TxtEmployeePosition.Text = currentUser.Должность?.Название ?? "Не назначена";
            TxtLogin.Text = currentUser.Логин;
            PassBox.Password = currentUser.Пароль;

            LoadUserPhoto();
        }

        /// <summary>
        /// Загружает фотографию пользователя из базы данных
        /// </summary>
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
            }
            catch
            {
                // Оставляем изображение по умолчанию
            }
        }

        #endregion

        #region Выбор фото

        /// <summary>
        /// Открывает диалог выбора фотографии
        /// </summary>
        private void SelectPhoto_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                    Title = "Выберите фотографию"
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
                MessageBox.Show($"Ошибка при выборе фото: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Валидация

        /// <summary>
        /// Проверяет корректность введённых данных
        /// </summary>
        /// <param name="skipPasswordValidation">Пропустить проверку пароля (если не менялся)</param>
        private bool ValidateEmployee(out string errorMessage, bool skipPasswordValidation = false)
        {
            var errors = new StringBuilder();

            var lastName = GetActualText(TxtLastName);
            var firstName = GetActualText(TxtFirstName);
            var middleName = GetActualText(TxtMiddleName);
            var phone = GetActualText(TxtPhone);
            var password = GetActualPassword();

            // Фамилия
            if (string.IsNullOrWhiteSpace(lastName))
                errors.AppendLine("• Фамилия не введена");
            else
            {
                if (!Regex.IsMatch(lastName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
                    errors.AppendLine("• Фамилия содержит недопустимые символы");
                else if (lastName.Length > 30)
                    errors.AppendLine("• Фамилия должна быть не длиннее 30 символов");
                else if (Regex.Replace(lastName, @"[^A-Za-zА-Яа-яЁё]", "").Length < 2)
                    errors.AppendLine("• Фамилия должна содержать минимум 2 буквы");
            }

            // Имя
            if (string.IsNullOrWhiteSpace(firstName))
                errors.AppendLine("• Имя не введено");
            else
            {
                if (!Regex.IsMatch(firstName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
                    errors.AppendLine("• Имя содержит недопустимые символы");
                else if (firstName.Length > 30)
                    errors.AppendLine("• Имя должно быть не длиннее 30 символов");
                else if (Regex.Replace(firstName, @"[^A-Za-zА-Яа-яЁё]", "").Length < 2)
                    errors.AppendLine("• Имя должно содержать минимум 2 буквы");
            }

            // Отчество (опционально, но с проверкой если введено)
            if (!string.IsNullOrWhiteSpace(middleName))
            {
                if (!Regex.IsMatch(middleName, @"^[A-Za-zА-Яа-яЁё\-]+$"))
                    errors.AppendLine("• Отчество содержит недопустимые символы");
                else if (middleName.Length > 30)
                    errors.AppendLine("• Отчество должно быть не длиннее 30 символов");
            }

            // Пароль (проверяется только если не пропущен)
            if (!skipPasswordValidation)
            {
                if (string.IsNullOrWhiteSpace(password))
                    errors.AppendLine("• Пароль не введён");
                else if (password.Length < 12)
                    errors.AppendLine("• Пароль должен быть не менее 12 символов");
            }

            // Телефон (опционально)
            if (!string.IsNullOrWhiteSpace(phone))
            {
                if (!Regex.IsMatch(phone, @"^\+?\d{11}$"))
                    errors.AppendLine("• Телефон должен содержать 11 цифр");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        #endregion

        #region Сохранение изменений

        /// <summary>
        /// Сохраняет изменения профиля в базу данных
        /// </summary>
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Если пароль не введён — не проверяем его
                var password = GetActualPassword();
                var skipPasswordValidation = string.IsNullOrEmpty(password);

                if (!ValidateEmployee(out var errorMessage, skipPasswordValidation))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Находим сотрудника в базе
                var employee = context.Сотрудники.Find(currentUser.Код_сотрудника);
                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден в базе данных!", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var phone = GetActualText(TxtPhone);

                // Проверка уникальности телефона
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    var phoneExists = context.Сотрудники.Any(s =>
                        s.Телефон == phone && s.Код_сотрудника != currentUser.Код_сотрудника);

                    if (phoneExists)
                    {
                        MessageBox.Show("Этот телефон уже используется другим сотрудником!",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                // Обновление данных
                employee.Фамилия = GetActualText(TxtLastName);
                employee.Имя = GetActualText(TxtFirstName);
                employee.Отчество = GetActualText(TxtMiddleName);
                employee.Телефон = phone;

                // Пароль меняем только если был введён новый
                if (!string.IsNullOrEmpty(password))
                    employee.Пароль = password;

                // Фото меняем только если было выбрано новое
                if (selectedImageData != null)
                    employee.Аватарка = selectedImageData;

                context.SaveChanges();

                // Обновляем локальный объект пользователя
                currentUser.Фамилия = employee.Фамилия;
                currentUser.Имя = employee.Имя;
                currentUser.Отчество = employee.Отчество;
                currentUser.Телефон = employee.Телефон;

                if (!string.IsNullOrEmpty(password))
                    currentUser.Пароль = password;

                if (selectedImageData != null)
                    currentUser.Аватарка = selectedImageData;

                // Обновляем отображение в главном окне
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                mainWindow?.SetCurrentUser(currentUser);

                MessageBox.Show("Данные успешно обновлены!", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                // Возвращаемся на предыдущую страницу
                if (NavigationService.CanGoBack)
                    NavigationService.GoBack();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении данных: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Возвращает реальный текст из TextBox, игнорируя placeholder
        /// </summary>
        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;

            var placeholder = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;

            return (!string.IsNullOrEmpty(placeholder) && text == placeholder)
                ? string.Empty
                : text;
        }

        /// <summary>
        /// Возвращает реальный пароль из PasswordBox, игнорируя placeholder
        /// </summary>
        private string GetActualPassword()
        {
            if (PassBox == null) return string.Empty;

            var placeholder = Addons.PlaceholderBehavior.GetPlaceholderText(PassBox);
            var password = PassBox.Password ?? string.Empty;

            return (!string.IsNullOrEmpty(placeholder) && password == placeholder)
                ? string.Empty
                : password;
        }

        #endregion
    }
}