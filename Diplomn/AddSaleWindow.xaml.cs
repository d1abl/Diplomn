using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn
{
    public partial class AddSaleWindow : Window
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private ObservableCollection<SaleItem> saleItems = new ObservableCollection<SaleItem>();

        public class SaleItem
        {
            public Товары Товары { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
        }

        public AddSaleWindow(BDEntities context, Сотрудники user)
        {
            InitializeComponent();
            this.context = context;
            this.currentUser = user;

            var products = context.Товары.Where(p => p.Количество > 0).ToList();
            CmbProduct.ItemsSource = products;
            CmbProduct.SelectionChanged += CmbProduct_SelectionChanged;
            DataGridSaleItems.ItemsSource = saleItems;
        }

        private void CmbProduct_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbProduct.SelectedItem is Товары product)
            {
                TxtPrice.Text = $"{product.Цена_за_ед_продажа:N2} ₽";
                TxtAvailable.Text = product.Количество.ToString();
            }
        }

        private void AddToSale_Click(object sender, RoutedEventArgs e)
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

            int existingQuantity = saleItems
                .Where(i => i.Товары?.Код_товара == product.Код_товара)
                .Sum(i => i.Количество);

            int totalRequested = existingQuantity + quantity;

            if (totalRequested > product.Количество)
            {
                MessageBox.Show($"Недостаточно товара на складе!\nДоступно: {product.Количество}\nУже в чеке: {existingQuantity}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var existing = saleItems.FirstOrDefault(i => i.Товары?.Код_товара == product.Код_товара);
            if (existing != null)
            {
                existing.Количество += quantity;
            }
            else
            {
                saleItems.Add(new SaleItem
                {
                    Товары = product,
                    Количество = quantity,
                    Цена = product.Цена_за_ед_продажа
                });
            }

            TxtQuantity.Text = "1";
            UpdateTotal();
            DataGridSaleItems.Items.Refresh();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SaleItem item)
            {
                saleItems.Remove(item);
                UpdateTotal();
                DataGridSaleItems.Items.Refresh();
            }
        }

        private void UpdateTotal()
        {
            decimal total = saleItems.Sum(i => i.Сумма);
            TxtTotal.Text = $"{total:N2} ₽";
        }

        private void SaveSale_Click(object sender, RoutedEventArgs e)
        {
            if (!saleItems.Any())
            {
                MessageBox.Show("Добавьте товары в чек!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var sale = new Продажи
                {
                    Код_сотрудника = currentUser.Код_сотрудника,
                    Дата_продажи = DateTime.Now
                };

                context.Продажи.Add(sale);
                context.SaveChanges();

                foreach (var item in saleItems)
                {
                    var saleComposition = new Состав_продажи
                    {
                        Код_чека = sale.Код_чека,
                        Код_товара = item.Товары.Код_товара,
                        Количество = item.Количество,
                        Цена = item.Цена
                    };
                    context.Состав_продажи.Add(saleComposition);

                    var product = context.Товары.Find(item.Товары.Код_товара);
                    if (product != null)
                        product.Количество -= item.Количество;
                }

                context.SaveChanges();
                MessageBox.Show($"Продажа оформлена! Чек №{sale.Код_чека}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
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