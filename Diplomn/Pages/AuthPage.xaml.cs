using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Diplomn.Pages
{
    /// <summary>
    /// Логика взаимодействия для AuthPage.xaml
    /// </summary>
    public partial class AuthPage : Page
    {
        private DispatcherTimer _errorTimer;
        public AuthPage()
        {
            InitializeComponent();
            LoginBox.Focus();
            // Обработка нажатия Enter в полях
            LoginBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) TryLogin(); };
            PassBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) TryLogin(); };
        }

        // Обработчик кнопки "Войти"
        private void LoginBtn_Click(object sender, RoutedEventArgs e)
        {
            TryLogin();
        }

        // Обработчик кнопки "Выход"
        private void ExitBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вы действительно хотите выйти из приложения?",
                "Подтверждение выхода",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question,
                MessageBoxResult.Yes); // По умолчанию фокус на "Yes"

            if (result == MessageBoxResult.Yes)
            {
                Application.Current.Shutdown();
            }
        }

        // Метод попытки входа
        private void TryLogin()
        {
            string login = LoginBox.Text.Trim();
            string password = PassBox.Password;

            // Простая валидация
            if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
            {
                ShowError("Введите логин и пароль");
                return;
            }

            try
            {
                using (var db = new BDEntities())
                {
                    // Поиск пользователя в БД
                    var user = db.Сотрудники.AsNoTracking()
                        .Include("Должность")
                        .FirstOrDefault(u => u.Логин == login && u.Пароль == password);

                    if (user != null)
                    {
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        mainWindow?.SetCurrentUser(user);
                        // Проверка роли и переход на соответствующую страницу
                        //switch (user.Должность?.Уровень_доступа)
                        //{
                        //    case 1: // Администратор
                        //        NavigationService?.Navigate(new AdministratorMenu(user));
                        //        break;
                        //    case 2: // Менеджер
                        //        NavigationService?.Navigate(new ManagerMenu(user));
                        //        break;
                        //    default:
                        //        ShowError("У пользователя не назначена роль");
                        //        break;
                        //}
                        NavigationService.Navigate(new MainMenu(user));
                    }
                    else
                    {
                        ShowError("Неверный логин или пароль");
                        PassBox.Password = ""; // Очищаем поле пароля
                        PassBox.Focus();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                ShowError($"Ошибка подключения к БД: {ex.Message}");
            }
        }

        // Метод отображения ошибки
        private void ShowError(string message)
        {
            ErrorText.Text = message;
            ErrorText.Visibility = Visibility.Visible;

            // Останавливаем предыдущий таймер, если был
            _errorTimer?.Stop();

            // Автоматически скрываем ошибку через 3 секунды
            _errorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3)};
            _errorTimer.Tick += (s, e) =>
            {
                ErrorText.Visibility = Visibility.Collapsed;
                _errorTimer.Stop();
            };
            _errorTimer.Start();
        }

    }
}