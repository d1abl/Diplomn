using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn
{
    public partial class AddSupplyWindow : Window
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private ObservableCollection<OrderItem> orderItems = new ObservableCollection<OrderItem>();

        public class OrderItem
        {
            public Товары Товары { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
        }

        public AddSupplyWindow(BDEntities context, Сотрудники user)
        {
            InitializeComponent();
            this.context = context;
            this.currentUser = user;

            CmbProduct.ItemsSource = context.Товары.ToList();
            CmbProduct.SelectionChanged += CmbProduct_SelectionChanged;
            DataGridOrderItems.ItemsSource = orderItems;
        }

        private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProduct.SelectedItem is Товары product)
            {
                TxtPrice.Text = $"{product.Цена_за_ед_продажа:N2} ₽";
            }
        }

        private void AddToOrder_Click(object sender, RoutedEventArgs e)
        {
            var product = CmbProduct.SelectedItem as Товары;
            if (product == null)
            {
                MessageBox.Show("Выберите товар!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TxtQuantity.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Введите корректное количество!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = orderItems.FirstOrDefault(i => i.Товары?.Код_товара == product.Код_товара);
            if (existing != null)
            {
                existing.Количество += quantity;
            }
            else
            {
                orderItems.Add(new OrderItem
                {
                    Товары = product,
                    Количество = quantity,
                    Цена = product.Цена_за_ед_продажа
                });
            }

            TxtQuantity.Text = "1";
            UpdateTotal();
            DataGridOrderItems.Items.Refresh();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is OrderItem item)
            {
                orderItems.Remove(item);
                UpdateTotal();
                DataGridOrderItems.Items.Refresh();
            }
        }

        private void UpdateTotal()
        {
            decimal total = orderItems.Sum(i => i.Сумма);
            TxtTotal.Text = $"{total:N2} ₽";
        }

        private void SaveOrder_Click(object sender, RoutedEventArgs e)
        {
            if (!orderItems.Any())
            {
                MessageBox.Show("Добавьте товары в поставку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Выбор поставщика для всей поставки
                var suppliers = context.Поставщики.ToList();
                if (!suppliers.Any())
                {
                    MessageBox.Show("Нет доступных поставщиков!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                Поставщики selectedSupplier = null;
                if (suppliers.Count == 1)
                {
                    selectedSupplier = suppliers.First();
                }
                else
                {
                    // Показываем диалог выбора поставщика
                    var supplierNames = suppliers.Select(s => s.Наименование_поставщика).ToArray();
                    var dialog = new SupplierSelectDialog(suppliers);
                    if (dialog.ShowDialog() == true)
                    {
                        selectedSupplier = dialog.SelectedSupplier;
                    }
                    else
                    {
                        return;
                    }
                }

                var order = new Поставка
                {
                    Код_сотрудника = currentUser.Код_сотрудника,
                    Дата_оформления_постивки = DateTime.Now,
                    Код_поставщика = selectedSupplier.Код_поставщика
                };

                context.Поставка.Add(order);
                context.SaveChanges();

                foreach (var item in orderItems)
                {
                    var orderComposition = new Состав_поставки
                    {
                        Код_поставки = order.Код_поставки,
                        Код_товара = item.Товары.Код_товара,
                        Количество = item.Количество,
                        Цена_за_ед_покупка = item.Цена,
                    };
                    context.Состав_поставки.Add(orderComposition);

                    var product = context.Товары.Find(item.Товары.Код_товара);
                    if (product != null)
                        product.Количество += item.Количество;
                }

                context.SaveChanges();
                MessageBox.Show($"Поставка №{order.Код_поставки} оформлена!\nПоставщик: {selectedSupplier.Наименование_поставщика}",
                    "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}