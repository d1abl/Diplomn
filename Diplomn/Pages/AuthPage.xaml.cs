using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Diplomn.Pages
{
    public partial class AuthPage : Page
    {
        private DispatcherTimer _errorTimer;

        public AuthPage()
        {
            InitializeComponent();

            LoginBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) TryLogin(); };
            PassBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) TryLogin(); };
        }

        private void LoginBtn_Click(object sender, RoutedEventArgs e) => TryLogin();

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

        private void TryLogin()
        {
            string login = GetActualText(LoginBox);
            string password = GetActualPassword();

            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ShowError("Введите логин и пароль");
                return;
            }

            try
            {
                using (var db = new BDEntities())
                {
                    var user = db.Сотрудники.AsNoTracking()
                        .Include("Должность")
                        .FirstOrDefault(u => u.Логин == login && u.Пароль == password);

                    if (user != null)
                    {
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.SetCurrentUser(user);
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

        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorBorder.Visibility = Visibility.Visible;

            _errorTimer?.Stop();
            _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            _errorTimer.Tick += (s, e) =>
            {
                ErrorBorder.Visibility = Visibility.Collapsed;
                _errorTimer.Stop();
            };
            _errorTimer.Start();
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