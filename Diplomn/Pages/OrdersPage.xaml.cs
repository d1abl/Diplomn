using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn.Pages
{
    public partial class OrdersPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public class OrderItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
        }

        public OrdersPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadData();
        }

        private void LoadData()
        {
            DataGridOrders.ItemsSource = context.Поставка
                .Include("Сотрудники")
                .OrderByDescending(o => o.Дата_оформления_постивки)
                .ToList();
        }

        private void DataGridOrders_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridOrders.SelectedItem is Поставка order)
            {
                TxtOrderId.Text = order.Код_поставки.ToString();
                TxtOrderDate.Text = order.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = order.Сотрудники != null ? $"{order.Сотрудники.Фамилия} {order.Сотрудники.Имя}" : "";

                var items = context.Состав_заказа
                    .Include("Товары")
                    .Where(i => i.Код_поставки == order.Код_поставки)
                    .Select(i => new OrderItemDisplay
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена_за_ед_покупка
                    })
                    .ToList();

                DataGridOrderItems.ItemsSource = new ObservableCollection<OrderItemDisplay>(items);
                decimal total = items.Sum(i => i.Сумма);
                TxtTotal.Text = $"{total:N2} ₽";
            }
            else
            {
                TxtOrderId.Text = "";
                TxtOrderDate.Text = "";
                TxtEmployee.Text = "";
                DataGridOrderItems.ItemsSource = null;
                TxtTotal.Text = "";
            }
        }

        private void NewOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new AddOrderWindow(context, currentUser);
                window.ShowDialog();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании поставки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewOrderComposition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var order = DataGridOrders.SelectedItem as Поставка;
                if (order == null)
                {
                    MessageBox.Show("Выберите поставку для просмотра состава!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var window = new OrderCompositionWindow(context, order);
                window.ShowDialog();
                LoadData();
                DataGridOrders_SelectionChanged(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии состава поставки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteOrder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var order = DataGridOrders.SelectedItem as Поставка;
                if (order == null)
                {
                    MessageBox.Show("Выберите поставку для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите удалить поставку №{order.Код_поставки}?\nВместе с поставкой будет удален её состав!",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Уменьшаем количество товаров на складе при удалении поставки
                    var items = context.Состав_заказа.Where(i => i.Код_поставки == order.Код_поставки).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null)
                        {
                            product.Количество -= item.Количество;
                            if (product.Количество < 0) product.Количество = 0;
                        }
                        context.Состав_заказа.Remove(item);
                    }

                    context.Поставка.Remove(order);
                    context.SaveChanges();

                    MessageBox.Show("Поставка успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    DataGridOrders_SelectionChanged(null, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении поставки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            DataGridOrders_SelectionChanged(null, null);
        }
    }
}