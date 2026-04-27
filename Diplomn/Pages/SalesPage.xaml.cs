using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn.Pages
{
    public partial class SalesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public class SaleItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
        }

        public SalesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadData();
        }

        private void LoadData()
        {
            DataGridSales.ItemsSource = context.Продажи
                .Include("Сотрудники")
                .OrderByDescending(s => s.Дата_продажи)
                .ToList();
        }

        private void DataGridSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridSales.SelectedItem is Продажи sale)
            {
                TxtSaleId.Text = sale.Код_чека.ToString();
                TxtSaleDate.Text = sale.Дата_продажи.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = sale.Сотрудники != null ? $"{sale.Сотрудники.Фамилия} {sale.Сотрудники.Имя}" : "";

                var items = context.Состав_продажи
                    .Include("Товары")
                    .Where(i => i.Код_чека == sale.Код_чека)
                    .Select(i => new SaleItemDisplay
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена
                    })
                    .ToList();

                DataGridSaleItems.ItemsSource = new ObservableCollection<SaleItemDisplay>(items);
                decimal total = items.Sum(i => i.Сумма);
                TxtTotal.Text = $"{total:N2} ₽";
            }
            else
            {
                TxtSaleId.Text = "";
                TxtSaleDate.Text = "";
                TxtEmployee.Text = "";
                DataGridSaleItems.ItemsSource = null;
                TxtTotal.Text = "";
            }
        }

        private void NewSale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new AddSaleWindow(context, currentUser);
                window.ShowDialog();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании продажи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewSaleComposition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sale = DataGridSales.SelectedItem as Продажи;
                if (sale == null)
                {
                    MessageBox.Show("Выберите продажу для просмотра состава!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var window = new AddSaleWindow(context, currentUser);
                window.ShowDialog();
                LoadData();
                DataGridSales_SelectionChanged(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии состава чека: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sale = DataGridSales.SelectedItem as Продажи;
                if (sale == null)
                {
                    MessageBox.Show("Выберите продажу для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите удалить чек №{sale.Код_чека}?\nВместе с чеком будет удален его состав!",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Восстанавливаем количество товаров на складе
                    var items = context.Состав_продажи.Where(i => i.Код_чека == sale.Код_чека).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null)
                        {
                            product.Количество += item.Количество;
                        }
                        context.Состав_продажи.Remove(item);
                    }

                    context.Продажи.Remove(sale);
                    context.SaveChanges();

                    MessageBox.Show("Продажа успешно удалена! Товары возвращены на склад.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    DataGridSales_SelectionChanged(null, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении продажи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            DataGridSales_SelectionChanged(null, null);
        }

    }
}