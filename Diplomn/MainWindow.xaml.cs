using Diplomn.Addons;
using Diplomn.Pages;
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

namespace Diplomn
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private Сотрудники currentUser;
        public MainWindow()
        {
            InitializeComponent();
            MenuPanel.Visibility = Visibility.Collapsed;
        }

        // Обработчик навигации Frame
        private void MainFrame_Navigated(object sender, NavigationEventArgs e)
        {
            // Устанавливаем заголовок в зависимости от текущей страницы
            if (MainFrame.Content is Page page)
            {
                if (page is AuthPage)
                {
                    this.Title = "HR - Авторизация";
                    ButtomGrid.Visibility = Visibility.Collapsed;
                    MenuPanel.Visibility = Visibility.Collapsed;
                    // Скрываем кнопку "Назад" на странице авторизации
                }
                else if (page is MainMenu)
                {
                    this.Title = "HR - Меню администратора";
                    ButtomGrid.Visibility = Visibility.Visible; // Показываем кнопку "Назад"
                }
                else
                {
                    this.Title = "HR - Система управления";
                    //BtnBack.Visibility = Visibility.Visible;
                }
            }
        }


        /// <summary>
        /// Установка текущего пользователя после авторизации
        /// </summary>
        public void SetCurrentUser(Сотрудники user)
        {
            currentUser = user;

            // Обновляем приветствие в меню
            //WelcomeText.Text = $"Добро пожаловать, {user.Фамилия} {user.Имя}!";
            if (currentUser?.Отчество != null) CurrentUser.Content = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.{currentUser.Отчество?.Substring(0, 1)}.";
            else CurrentUser.Content = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
            // Показываем/скрываем кнопки в зависимости от роли
            if (user.Должность?.Уровень_доступа == 1) // Администратор
            {
                // Администратору доступны все кнопки (уже видны по умолчанию)
                VisiblEmployeesBtn.Visibility = Visibility.Visible;
                RolesBtn.Visibility = Visibility.Visible;
                CategoriesBtn.Visibility = Visibility.Visible;
            }
            else if (user.Должность?.Уровень_доступа == 2) // Менеджер
            {
                // Скрываем кнопки, недоступные менеджеру
                VisiblEmployeesBtn.Visibility = Visibility.Collapsed;
                RolesBtn.Visibility = Visibility.Collapsed;
                CategoriesBtn.Visibility = Visibility.Collapsed;
            }

            // Показываем меню
            MenuPanel.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Выход из системы
        /// </summary>
        //public void Logout()
        //{
        //    currentUser = null;
        //    //WelcomeText.Text = "";
        //    MenuPanel.Visibility = Visibility.Collapsed;
        //    //MainFrame.Navigate(new AuthPage());
        //}

        public void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            // Возврат на предыдущую страницу
            if (MainFrame?.CanGoBack == true)
            {
                MainFrame.GoBack();
                //if (MainFrame.Content is AuthPage)
                //{
                //    Logout();
                //}
            }
        }

        #region Методы навигации

        private void EmployeesBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new EmployeesPage(currentUser));
        }

        private void RolesBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new RolePage(currentUser));
        }

        private void ProductsBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new ProductsPage(currentUser));
        }

        private void CategoriesBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new CategoriesPage(currentUser));
        }

        private void SuppliersBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new SuppliersPage(currentUser));
        }

        private void OrdersBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new OrdersPage(currentUser));
        }

        private void SalesBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new SalesPage(currentUser));
        }

        private void ReportsBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new ReportPage(currentUser));
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new SettingsPage(currentUser));
        }

        private void CalendarBtn_Click(object sender, RoutedEventArgs e)
        {
            MainFrame?.Navigate(new CalendarPage(currentUser));
        }

        // Кнопка выхода
        //private void LogoutBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    var result = MessageBox.Show("Вы действительно хотите выйти из системы?",
        //                                 "Подтверждение выхода",
        //                                 MessageBoxButton.YesNo,
        //                                 MessageBoxImage.Question);
        //    if (result == MessageBoxResult.Yes)
        //    {
        //        Logout();
        //    }
        //}

        #endregion

        #region Сворачивание/разворачивание секций меню

        private void VisiblEmployeesBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = VisiblEmployeesWarp.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblOrdersWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = Visibility.Collapsed;
            if (VisiblReportsWarp.Visibility == Visibility.Visible || VisiblOrdersWarp.Visibility == Visibility.Visible || VisiblProductsWarp.Visibility == Visibility.Visible || VisiblEmployeesWarp.Visibility == Visibility.Visible)
                VisiblBtn.Visibility = Visibility.Visible;
            else
                VisiblBtn.Visibility = Visibility.Collapsed;
        }

        private void VisiblProductsBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = Visibility.Collapsed;
            VisiblProductsWarp.Visibility = VisiblProductsWarp.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
            VisiblOrdersWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = Visibility.Collapsed;
            if (VisiblReportsWarp.Visibility == Visibility.Visible || VisiblOrdersWarp.Visibility == Visibility.Visible || VisiblProductsWarp.Visibility == Visibility.Visible || VisiblEmployeesWarp.Visibility == Visibility.Visible)
                VisiblBtn.Visibility = Visibility.Visible;
            else
                VisiblBtn.Visibility = Visibility.Collapsed;
        }

        private void VisiblOrdersBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = Visibility.Collapsed;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblOrdersWarp.Visibility = VisiblOrdersWarp.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;            
            VisiblReportsWarp.Visibility = Visibility.Collapsed;
            if (VisiblReportsWarp.Visibility == Visibility.Visible || VisiblOrdersWarp.Visibility == Visibility.Visible || VisiblProductsWarp.Visibility == Visibility.Visible || VisiblEmployeesWarp.Visibility == Visibility.Visible)
                VisiblBtn.Visibility = Visibility.Visible;
            else
                VisiblBtn.Visibility = Visibility.Collapsed;
        }

        private void VisiblReportsBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = Visibility.Collapsed;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblOrdersWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = VisiblReportsWarp.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (VisiblReportsWarp.Visibility == Visibility.Visible || VisiblOrdersWarp.Visibility == Visibility.Visible || VisiblProductsWarp.Visibility == Visibility.Visible || VisiblEmployeesWarp.Visibility == Visibility.Visible)
                VisiblBtn.Visibility = Visibility.Visible;
            else
                VisiblBtn.Visibility = Visibility.Collapsed;
        }

        

        private void VisiblBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = Visibility.Collapsed;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblOrdersWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = Visibility.Collapsed;
            VisiblBtn.Visibility = Visibility.Collapsed;
        }
        #endregion

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            GlobalSearchManager.Instance.ShowSearchDialog();
        }
    }
}
