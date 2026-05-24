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
        private AccessManager.AccessRights currentRights;
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
                    this.Title = "Oculus+ - Авторизация";
                    ButtomGrid.Visibility = Visibility.Collapsed;
                    MenuPanel.Visibility = Visibility.Collapsed;                    
                }
                else if (page is MainMenu)
                {
                    this.Title = "Oculus+ - Главное меню";
                    ButtomGrid.Visibility = Visibility.Visible;
                }
                else
                {
                    this.Title = "Oculus+ - Система управления";
                }

                if (currentUser != null)
                {
                    LoadUserPhoto(currentUser);
                    UpdateCurrentUserDisplay();
                }
            }
        }

        #region Загрузка пользователя
        private void UpdateCurrentUserDisplay()
        {
            string roleName = currentUser.Должность?.Название ?? "Сотрудник";

            if (currentUser?.Отчество != null)
                CurrentUser.Content = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}.{currentUser.Отчество?.Substring(0, 1)}. ({roleName})";
            else
                CurrentUser.Content = $"{currentUser.Фамилия} {currentUser.Имя?.Substring(0, 1)}. ({roleName})";
        }
        
        public void SetCurrentUser(Сотрудники user)
        {
            currentUser = user;
            currentRights = AccessManager.GetAccessRights(user.Должность?.Уровень_доступа ?? 10);

            UpdateCurrentUserDisplay();
            LoadUserPhoto(user);

            // Настройка меню на основе прав
            ConfigureMenuByRights();

            // Показываем меню
            MenuPanel.Visibility = Visibility.Visible;
        }
        #region Создание меню
        private void ConfigureMenuByRights()
        {
            // Скрываем все секции сначала
            VisiblEmployeesBtn.Visibility = Visibility.Collapsed;
            VisiblProductsBtn.Visibility = Visibility.Collapsed;
            VisiblSuppliesBtn.Visibility = Visibility.Collapsed;
            VisiblReportsBtn.Visibility = Visibility.Collapsed;
            VisiblEmployeesWarp.Visibility = Visibility.Collapsed;
            VisiblProductsWarp.Visibility = Visibility.Collapsed;
            VisiblSuppliesWarp.Visibility = Visibility.Collapsed;
            VisiblReportsWarp.Visibility = Visibility.Collapsed;

            // Управление персоналом
            if (currentRights.Employees.CanView || currentRights.Roles.CanView)
            {
                VisiblEmployeesBtn.Visibility = Visibility.Visible;
                ConfigureEmployeesSection();
            }

            // Управление товарами
            if (currentRights.Products.CanView || currentRights.Categories.CanView ||
                currentRights.Brands.CanView || currentRights.Manufacturers.CanView ||
                currentRights.Materials.CanView || currentRights.Packings.CanView ||
                currentRights.Suppliers.CanView)
            {
                VisiblProductsBtn.Visibility = Visibility.Visible;
                ConfigureProductsSection();
            }

            // Управление заказами и продажами
            if (currentRights.Supplies.CanView || currentRights.Sales.CanView)
            {
                VisiblSuppliesBtn.Visibility = Visibility.Visible;
                ConfigureSuppliesSection();
            }

            // Отчеты и настройки
            if (currentRights.Reports.CanView || currentRights.Settings.CanView)
            {
                VisiblReportsBtn.Visibility = Visibility.Visible;
                ConfigureReportsSection();
            }
        }

        private void ConfigureEmployeesSection()
        {
            VisiblEmployeesWarp.Children.Clear();

            // Сотрудники
            if (currentRights.Employees.CanView)
                VisiblEmployeesWarp.Children.Add(CreateMenuButton("👥 Сотрудники", EmployeesBtn_Click));

            // Роли
            if (currentRights.Roles.CanView)
                VisiblEmployeesWarp.Children.Add(CreateMenuButton("🔐 Должности", RolesBtn_Click));
        }

        private void ConfigureProductsSection()
        {
            VisiblProductsWarp.Children.Clear();

            // Товары
            if (currentRights.Products.CanView)
                VisiblProductsWarp.Children.Add(CreateMenuButton("📦 Товары", ProductsBtn_Click));

            // Категории
            if (currentRights.Categories.CanView)
                VisiblProductsWarp.Children.Add(CreateMenuButton("📁 Категории", CategoriesBtn_Click));

            // Бренды
            if (currentRights.Brands.CanView)
                VisiblProductsWarp.Children.Add(CreateMenuButton("🏷 Бренды", BrandsBtn_Click));

            // Производители
            if (currentRights.Manufacturers.CanView)
                VisiblProductsWarp.Children.Add(CreateMenuButton("🏭 Производители", ManufacturersBtn_Click));

            // Материалы
            if (currentRights.Materials.CanView)
                VisiblProductsWarp.Children.Add(CreateMenuButton("🔧 Материалы", MaterialsBtn_Click));

            // Фасовка
            if (currentRights.Packings.CanView)
                VisiblProductsWarp.Children.Add(CreateMenuButton("📏 Фасовка", PackingsBtn_Click));

            // Поставщики
            if (currentRights.Suppliers.CanView)
                VisiblProductsWarp.Children.Add(CreateMenuButton("🚚 Поставщики", SuppliersBtn_Click));
        }

        private void ConfigureSuppliesSection()
        {
            VisiblSuppliesWarp.Children.Clear();

            // Поставки
            if (currentRights.Supplies.CanView)
                VisiblSuppliesWarp.Children.Add(CreateMenuButton("📋 Поставки", SuppliesBtn_Click));

            // Продажи
            if (currentRights.Sales.CanView)
                VisiblSuppliesWarp.Children.Add(CreateMenuButton("💰 Продажи", SalesBtn_Click));
        }

        private void ConfigureReportsSection()
        {
            VisiblReportsWarp.Children.Clear();

            // Настройки
            if (currentRights.Settings.CanView)
                VisiblReportsWarp.Children.Add(CreateMenuButton("⚙️ Настройки", SettingsBtn_Click));
        }
        private Button CreateMenuButton(string text, RoutedEventHandler clickHandler)
        {
            var button = new Button
            {
                Width = 200,
                Height = 50,
                Margin = new Thickness(10),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Content = new TextBlock
                {
                    Text = text,
                    FontSize = 24,
                    Width = 200,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(10, 0, 0, 0)
                }
            };
            button.Click += clickHandler;
            return button;
        }
        #endregion
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
        private void BtnMainMenu_Click(object sender, RoutedEventArgs e) => 
            MainFrame?.NavigateIfDifferent(new MainMenu(currentUser));

        private void BtnExit_Click(object sender, RoutedEventArgs e) => 
            MainFrame?.Navigate(new AuthPage());

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
        
        private void SettingsBtn_Click(object sender, RoutedEventArgs e) =>        
            MainFrame?.NavigateIfDifferent(new SettingsPage(currentUser));

        private void BrandsBtn_Click(object sender, RoutedEventArgs e) =>
            MainFrame?.NavigateIfDifferent(new BrandsPage(currentUser));

        private void ManufacturersBtn_Click(object sender, RoutedEventArgs e) =>
            MainFrame?.NavigateIfDifferent(new ManufacturersPage(currentUser));

        private void MaterialsBtn_Click(object sender, RoutedEventArgs e) =>
            MainFrame?.NavigateIfDifferent(new MaterialsPage(currentUser));

        private void PackingsBtn_Click(object sender, RoutedEventArgs e) =>
            MainFrame?.NavigateIfDifferent(new PackingsPage(currentUser));
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
