using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplomn
{
    public partial class SupplierSelectDialog : Window
    {
        private List<Поставщики> allSuppliers;
        public Поставщики SelectedSupplier { get; private set; }

        public SupplierSelectDialog(List<Поставщики> suppliers)
        {
            InitializeComponent();
            allSuppliers = suppliers;
            DataGridSuppliers.ItemsSource = allSuppliers;
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplySearch();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearch();
        }

        private void ApplySearch()
        {
            var term = TxtSearch.Text?.Trim().ToLower();
            if (string.IsNullOrWhiteSpace(term))
            {
                DataGridSuppliers.ItemsSource = allSuppliers;
            }
            else
            {
                var filtered = allSuppliers.Where(s =>
                    s.Наименование_поставщика.ToLower().Contains(term) ||
                    s.ИНН.Contains(term) ||
                    s.Фамилия_контактного_лица.ToLower().Contains(term) ||
                    s.Имя_контактного_лица.ToLower().Contains(term)
                ).ToList();
                DataGridSuppliers.ItemsSource = filtered;
            }
        }

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            ApplySearch();
        }

        private void DataGridSuppliers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridSuppliers.SelectedItem is Поставщики supplier)
            {
                SelectedSupplier = supplier;
                TxtSelectedSupplier.Text = $"Выбран: {supplier.Наименование_поставщика} (ИНН: {supplier.ИНН})";
                BtnSelect.IsEnabled = true;
            }
            else
            {
                SelectedSupplier = null;
                TxtSelectedSupplier.Text = "Поставщик не выбран";
                BtnSelect.IsEnabled = false;
            }
        }

        private void DataGridSuppliers_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (SelectedSupplier != null)
            {
                DialogResult = true;
                Close();
            }
        }

        private void SelectSupplier_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedSupplier != null)
            {
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Выберите поставщика!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}