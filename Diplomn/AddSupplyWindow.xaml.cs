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
            public Поставщики Поставщики { get; set; }
            public decimal Сумма => Количество * Цена;
        }

        public AddSupplyWindow(BDEntities context, Сотрудники user)
        {
            InitializeComponent();
            this.context = context;
            this.currentUser = user;

            CmbProduct.ItemsSource = context.Товары.ToList();
            CmbSupplier.ItemsSource = context.Поставщики.ToList();
            CmbProduct.SelectionChanged += CmbProduct_SelectionChanged;
            DataGridOrderItems.ItemsSource = orderItems;
        }

        private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProduct.SelectedItem is Товары product)
            {
                TxtPrice.Text = product.Цена_за_ед_продажа.ToString() + " ₽" ?? "0";
            }
        }

        private void AddToOrder_Click(object sender, RoutedEventArgs e)
        {
            var product = CmbProduct.SelectedItem as Товары;
            if (product == null)
            {
                MessageBox.Show("Выберите товар!");
                return;
            }

            var supplier = CmbSupplier.SelectedItem as Поставщики;
            if (supplier == null)
            {
                MessageBox.Show("Выберите поставщика!");
                return;
            }

            if (!int.TryParse(TxtQuantity.Text, out int quantity) || quantity <= 0)
            {
                MessageBox.Show("Введите корректное количество!");
                return;
            }

            // Ищем существующую позицию с таким же товаром И таким же поставщиком
            var existing = orderItems.FirstOrDefault(i =>
                i.Товары?.Код_товара == product.Код_товара &&
                i.Поставщики?.Код_поставщика == supplier.Код_поставщика);

            if (existing != null)
            {
                // Если нашли - увеличиваем количество
                existing.Количество += quantity;
            }
            else
            {
                // Если не нашли - создаем новую позицию
                orderItems.Add(new OrderItem
                {
                    Товары = product,
                    Количество = quantity,
                    Цена = product.Цена_за_ед_продажа,
                    Поставщики = supplier
                });
            }

            TxtQuantity.Text = "1";
            UpdateTotal();
            DataGridOrderItems.Items.Refresh();
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
                MessageBox.Show("Добавьте товары в поставку!");
                return;
            }

            try
            {
                // Создаем поставку с текущей датой и временем
                var order = new Поставка
                {
                    Код_сотрудника = currentUser.Код_сотрудника,
                    Дата_оформления_постивки = DateTime.Now  // Сохраняем текущую дату и время
                };

                context.Поставка.Add(order);
                context.SaveChanges(); // Сохраняем чтобы получить Код_поставки

                foreach (var item in orderItems)
                {
                    var orderComposition = new Состав_поставки
                    {
                        Код_поставки = order.Код_поставки,
                        Код_товара = item.Товары.Код_товара,
                        Количество = item.Количество,
                        Цена_за_ед_покупка = item.Цена,
                        Код_поставщика = item.Поставщики.Код_поставщика
                    };
                    context.Состав_поставки.Add(orderComposition);

                    // Увеличиваем количество товара на складе
                    var product = context.Товары.Find(item.Товары.Код_товара);
                    if (product != null)
                    {
                        product.Количество += item.Количество;
                    }
                }

                context.SaveChanges();
                MessageBox.Show($"Поставка успешно оформлена!\nНомер поставки: {order.Код_поставки}\nДата: {order.Дата_оформления_постивки:dd.MM.yyyy HH:mm}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}