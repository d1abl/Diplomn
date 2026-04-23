using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;

namespace Diplomn.Pages
{
    /// <summary>
    /// Логика взаимодействия для AdministratorMenu.xaml
    /// </summary>
    public partial class MainMenu : Page
    {
        private Сотрудники currentUser;

        public MainMenu(Сотрудники user)
        {
            InitializeComponent();
            currentUser = user;
        }

        
    }
}