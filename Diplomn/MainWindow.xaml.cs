using Diplomn.Addons;
using Diplomn.Pages;
using System;
using System.Collections.Generic;
using System.IO;
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
            if (MainFrame.Content is Page page)
            {
                if (page is AuthPage)
                {
                    this.Title = "HR - Авторизация";
                    ButtomGrid.Visibility = Visibility.Collapsed;
                    MenuPanel.Visibility = Visibility.Collapsed;
                }
                else if (page is MainMenu)
                {
                    this.Title = "HR - Меню администратора";
                    ButtomGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    this.Title = "HR - Система управления";
                }
                if (currentUser != null)
                {
                    LoadUserPhoto(currentUser);
                    UpdateCurrentUserDisplay();
                }
            }
        }

        private void UpdateCurrentUserDisplay()
        {
            if (currentUser?.Отчество != null)
                CurrentUser.Content = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.{currentUser.Отчество?.Substring(0, 1)}.";
            else
                CurrentUser.Content = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
        }

        #region Загрузка пользователя
        public void SetCurrentUser(Сотрудники user)
        {
            currentUser = user;


            if (currentUser?.Отчество != null) CurrentUser.Content = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.{currentUser.Отчество?.Substring(0, 1)}.";
            else CurrentUser.Content = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.";
            LoadUserPhoto(user);
            if (user.Должность?.Уровень_доступа == 1) // Администратор
            {
                VisiblEmployeesBtn.Visibility = Visibility.Visible;
                RolesBtn.Visibility = Visibility.Visible;
                CategoriesBtn.Visibility = Visibility.Visible;
            }
            else if (user.Должность?.Уровень_доступа == 2) // Менеджер
            {
                VisiblEmployeesBtn.Visibility = Visibility.Collapsed;
                RolesBtn.Visibility = Visibility.Collapsed;
                CategoriesBtn.Visibility = Visibility.Collapsed;
            }

            // Показываем меню
            MenuPanel.Visibility = Visibility.Visible;
        }

        private void LoadUserPhoto(Сотрудники user)
        {
            try
            {
                if (user?.Аватарка != null && user.Аватарка.Length > 0)
                {
                    using (var ms = new MemoryStream(user.Аватарка))
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

        #endregion

        #region Методы навигации
        public void BtnBack_Click(object sender, RoutedEventArgs e) => 
            MainFrame?.GoBack();
               
        private void CurrentUser_Click(object sender, RoutedEventArgs e) =>
            MainFrame.NavigateIfDifferent(new CurrentEmployeeEditPage(currentUser));
        

        private void EmployeesBtn_Click(object sender, RoutedEventArgs e) =>        
            MainFrame?.NavigateIfDifferent(new EmployeesPage(currentUser));
        

        private void RolesBtn_Click(object sender, RoutedEventArgs e) =>       
            MainFrame?.NavigateIfDifferent(new RolePage(currentUser));
        

        private void ProductsBtn_Click(object sender, RoutedEventArgs e) =>        
            MainFrame?.NavigateIfDifferent(new ProductsPage(currentUser));
        

        private void CategoriesBtn_Click(object sender, RoutedEventArgs e) =>        
            MainFrame?.NavigateIfDifferent(new CategoriesPage(currentUser));
        

        private void SuppliersBtn_Click(object sender, RoutedEventArgs e) =>        
            MainFrame?.NavigateIfDifferent(new SuppliersPage(currentUser));
        

        private void SuppliesBtn_Click(object sender, RoutedEventArgs e) =>        
            MainFrame?.NavigateIfDifferent(new SuppliesPage(currentUser));
        

        private void SalesBtn_Click(object sender, RoutedEventArgs e) =>        
            MainFrame?.NavigateIfDifferent(new SalesPage(currentUser));
        

        //private void ReportsBtn_Click(object sender, RoutedEventArgs e) =>        
        //    MainFrame?.NavigateIfDifferent(new ReportPage(currentUser));
        

        private void SettingsBtn_Click(object sender, RoutedEventArgs e) =>        
            MainFrame?.NavigateIfDifferent(new SettingsPage(currentUser));
        

        //private void CalendarBtn_Click(object sender, RoutedEventArgs e)
        //{
        //    MainFrame?.NavigateIfDifferent(new CalendarPage(currentUser));
        //}

        #endregion

        #region Сворачивание/разворачивание секций меню

        private void VisiblEmployeesBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = VisiblEmployeesWarp.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblSuppliesWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = Visibility.Collapsed;
            if (VisiblReportsWarp.Visibility == Visibility.Visible || VisiblSuppliesWarp.Visibility == Visibility.Visible || VisiblProductsWarp.Visibility == Visibility.Visible || VisiblEmployeesWarp.Visibility == Visibility.Visible)
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
            VisiblSuppliesWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = Visibility.Collapsed;
            if (VisiblReportsWarp.Visibility == Visibility.Visible || VisiblSuppliesWarp.Visibility == Visibility.Visible || VisiblProductsWarp.Visibility == Visibility.Visible || VisiblEmployeesWarp.Visibility == Visibility.Visible)
                VisiblBtn.Visibility = Visibility.Visible;
            else
                VisiblBtn.Visibility = Visibility.Collapsed;
        }

        private void VisiblSuppliesBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = Visibility.Collapsed;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblSuppliesWarp.Visibility = VisiblSuppliesWarp.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;            
            VisiblReportsWarp.Visibility = Visibility.Collapsed;
            if (VisiblReportsWarp.Visibility == Visibility.Visible || VisiblSuppliesWarp.Visibility == Visibility.Visible || VisiblProductsWarp.Visibility == Visibility.Visible || VisiblEmployeesWarp.Visibility == Visibility.Visible)
                VisiblBtn.Visibility = Visibility.Visible;
            else
                VisiblBtn.Visibility = Visibility.Collapsed;
        }

        private void VisiblReportsBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = Visibility.Collapsed;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblSuppliesWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = VisiblReportsWarp.Visibility == Visibility.Visible
                ? Visibility.Collapsed
                : Visibility.Visible;

            if (VisiblReportsWarp.Visibility == Visibility.Visible || VisiblSuppliesWarp.Visibility == Visibility.Visible || VisiblProductsWarp.Visibility == Visibility.Visible || VisiblEmployeesWarp.Visibility == Visibility.Visible)
                VisiblBtn.Visibility = Visibility.Visible;
            else
                VisiblBtn.Visibility = Visibility.Collapsed;
        }

        

        private void VisiblBtn_Click(object sender, RoutedEventArgs e)
        {
            VisiblEmployeesWarp.Visibility = Visibility.Collapsed;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblSuppliesWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = Visibility.Collapsed;
            VisiblBtn.Visibility = Visibility.Collapsed;
        }
        #endregion

    }
}
