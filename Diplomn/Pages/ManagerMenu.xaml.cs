using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Diplomn.Pages
{
    public partial class ManagerMenu : Page
    {
        private Сотрудники currentUser;

        public ManagerMenu(Сотрудники user)
        {
            InitializeComponent();
            currentUser = user;
            WelcomeText.Text = $"Добро пожаловать, {user.Фамилия} {user.Имя}!";
        }

        private void ProductsBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new ProductsPage(currentUser));
        }

        private void CategoriesBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new CategoriesPage(currentUser));
        }

        private void SuppliersBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new SuppliersPage(currentUser));
        }

        private void OrdersBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new OrdersPage(currentUser));
        }

        private void SalesBtn_Click(object sender, RoutedEventArgs e)
        {
            NavigationService?.Navigate(new SalesPage(currentUser));
        }
    }
}