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
            string login = LoginBox.Text.Trim();
            string password = PassBox.Password;

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
    }
}