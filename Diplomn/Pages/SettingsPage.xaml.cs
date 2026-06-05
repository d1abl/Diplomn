using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Contexts;
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

namespace Diplomn.Pages
{
    /// <summary>
    /// Логика взаимодействия для SettingsPage.xaml
    /// </summary>
    public partial class SettingsPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private readonly Dictionary<string, Uri> themes = new Dictionary<string, Uri>
        {
            { "BaseTheme" , new Uri("/Themes/BaseTheme.xaml", UriKind.Relative) },
            { "Theme1", new Uri("/Themes/Theme1.xaml", UriKind.Relative) },
            { "Theme2", new Uri("/Themes/Theme2.xaml", UriKind.Relative) },
            { "Theme3", new Uri("/Themes/Theme3.xaml", UriKind.Relative) }
        };
        public SettingsPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;

        }

        private void ThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyTheme();
        }

        private void ApplyTheme_Click(object sender, RoutedEventArgs e)
        {
            ApplyTheme();
            MessageBox.Show("Тема сохранена", "Информация" ,MessageBoxButton.OK, MessageBoxImage.Information);
        }
        private void ApplyTheme()
        {
            if (ThemeSelector.SelectedItem is ComboBoxItem item && item.Tag is string tag && themes.ContainsKey(tag))
            {
                var uri = themes[tag];
                var dict = new ResourceDictionary() { Source = uri };
                Application.Current.Resources.MergedDictionaries.Clear();
                Application.Current.Resources.MergedDictionaries.Add(dict);
                //MessageBox.Show("Тема применена. Перезапустите окно, чтобы увидеть все изменения.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
