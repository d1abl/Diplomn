using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница авторизации пользователя в системе
    /// </summary>
    public partial class AuthPage : Page
    {
        #region Поля

        // Таймер для автоматического скрытия сообщений об ошибке
        private DispatcherTimer _errorTimer;

        #endregion

        #region Конструктор

        public AuthPage()
        {
            InitializeComponent();

            // Обработка нажатия Enter в полях ввода
            LoginBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) TryLogin(); };
            PassBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) TryLogin(); };
        }

        #endregion

        #region Обработчики событий

        /// <summary>
        /// Обработчик кнопки "Войти"
        /// </summary>
        private void LoginBtn_Click(object sender, RoutedEventArgs e) => TryLogin();

        /// <summary>
        /// Обработчик кнопки "Выход" с подтверждением
        /// </summary>
        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы действительно хотите выйти из приложения?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes);

            if (result == MessageBoxResult.Yes)
                Application.Current.Shutdown();
        }

        #endregion

        #region Авторизация

        /// <summary>
        /// Проверка учётных данных и вход в систему
        /// </summary>
        private void TryLogin()
        {
            // Получаем реальные значения (без плейсхолдера)
            string login = GetActualText(LoginBox);
            string password = GetActualPassword();

            // Проверка заполнения полей
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ShowError("Введите логин и пароль");
                return;
            }

            try
            {
                using (var db = new BDEntities())
                {
                    // Поиск пользователя по логину и паролю
                    var user = db.Сотрудники
                        .AsNoTracking()
                        .Include("Должность")
                        .FirstOrDefault(u => u.Логин == login && u.Пароль == password);

                    if (user != null)
                    {
                        // Сохраняем текущего пользователя в главном окне
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.SetCurrentUser(user);

                        // Переходим на главное меню
                        NavigationService.Navigate(new MainMenu(user));
                    }
                    else
                    {
                        ShowError("Неверный логин или пароль");
                        LoginBox.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка подключения к БД: {ex.Message}");
            }
        }

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Получает реальный текст из TextBox, игнорируя плейсхолдер
        /// </summary>
        private string GetActualText(TextBox textBox)
        {
            if (textBox == null)
                return string.Empty;

            var placeholderText = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;

            // Если текст совпадает с плейсхолдером — возвращаем пустую строку
            if (!string.IsNullOrEmpty(placeholderText) && text == placeholderText)
                return string.Empty;

            return text;
        }

        /// <summary>
        /// Получает реальный пароль из PasswordBox, игнорируя плейсхолдер
        /// </summary>
        private string GetActualPassword()
        {
            if (PassBox == null)
                return string.Empty;

            var placeholderText = Addons.PlaceholderBehavior.GetPlaceholderText(PassBox);
            var password = PassBox.Password ?? string.Empty;

            // Если пароль совпадает с плейсхолдером — возвращаем пустую строку
            if (!string.IsNullOrEmpty(placeholderText) && password == placeholderText)
                return string.Empty;

            return password;
        }

        /// <summary>
        /// Показывает сообщение об ошибке с автоматическим скрытием через 3 секунды
        /// </summary>
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;

            // Сбрасываем предыдущий таймер
            _errorTimer?.Stop();

            // Создаём новый таймер для скрытия ошибки
            _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _errorTimer.Tick += (s, e) =>
            {
                ErrorBorder.Visibility = Visibility.Collapsed;
                _errorTimer.Stop();
            };
            _errorTimer.Start();
        }

        #endregion
    }
}