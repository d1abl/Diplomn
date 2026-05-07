using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Diplomn
{
    public partial class AddSaleWindow : Window
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private ObservableCollection<SaleItem> saleItems = new ObservableCollection<SaleItem>();
        private ObservableCollection<ProductSaleViewModel> productsView;

        public class SaleItem
        {
            public Товары Товары { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
            public string PriceQuantityDisplay => $"{Цена:N2} ₽ × {Количество} шт.";
        }

        public AddSaleWindow(BDEntities context, Сотрудники user)
        {
            InitializeComponent();
            this.context = context;
            this.currentUser = user;
            productsView = new ObservableCollection<ProductSaleViewModel>();
            ListViewSaleItems.ItemsSource = saleItems;
            LoadProducts();
        }

        private void LoadProducts()
        {
            var products = context.Товары.ToList();
            UpdateProductsView(products);
        }

        private void UpdateProductsView(List<Товары> products)
        {
            productsView.Clear();
            foreach (var product in products)
            {
                productsView.Add(new ProductSaleViewModel(product));
            }
            ListViewProducts.ItemsSource = productsView;
        }

        private IQueryable<Товары> GetFilteredQuery()
        {
            var query = context.Товары.AsQueryable();

            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                query = query.Where(p => p.Наименование.Contains(term));
            }

            if (ChkInStock.IsChecked == true)
                query = query.Where(p => p.Количество > 0);

            if (decimal.TryParse(TxtPriceMin.Text, out decimal pmin))
                query = query.Where(p => p.Цена_за_ед_продажа >= pmin);
            if (decimal.TryParse(TxtPriceMax.Text, out decimal pmax))
                query = query.Where(p => p.Цена_за_ед_продажа <= pmax);

            if (int.TryParse(TxtQtyMin.Text, out int qmin))
                query = query.Where(p => p.Количество >= qmin);
            if (int.TryParse(TxtQtyMax.Text, out int qmax))
                query = query.Where(p => p.Количество <= qmax);

            return query;
        }

        private void ApplyFilters()
        {
            var products = GetFilteredQuery().ToList();
            UpdateProductsView(products);
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            TxtPriceMin.Text = "";
            TxtPriceMax.Text = "";
            TxtQtyMin.Text = "";
            TxtQtyMax.Text = "";
            ChkInStock.IsChecked = false;
            LoadProducts();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private void ListViewProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewProducts.SelectedItem is ProductSaleViewModel selectedProduct)
            {
                var product = selectedProduct.OriginalProduct;
                TxtSelectedProduct.Text = product.Наименование;
                TxtPrice.Text = $"{product.Цена_за_ед_продажа:N2} ₽";
                TxtAvailable.Text = product.Количество.ToString();
            }
        }

        private void AddToSale_Click(object sender, RoutedEventArgs e)
        {
            var selectedItem = ListViewProducts.SelectedItem as ProductSaleViewModel;
            if (selectedItem == null)
            {
                MessageBox.Show("Выберите товар!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var product = selectedItem.OriginalProduct;

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
            ListViewSaleItems.Items.Refresh();
        }

        private void RemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is SaleItem item)
            {
                saleItems.Remove(item);
                UpdateTotal();
                ListViewSaleItems.Items.Refresh();
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

    /// <summary>
    /// ViewModel для отображения товара в каталоге
    /// </summary>
    public class ProductSaleViewModel
    {
        public Товары OriginalProduct { get; set; }
        public string Наименование { get; set; }
        public decimal Цена_за_ед_продажа { get; set; }
        public int Количество { get; set; }
        public string CategoryName { get; set; }
        public BitmapImage PhotoSource { get; set; }

        public string PriceDisplay => $"{Цена_за_ед_продажа:N2} ₽";
        public string QuantityDisplay => $"📦 В наличии: {Количество} шт.";

        public ProductSaleViewModel(Товары product)
        {
            OriginalProduct = product;
            Наименование = product.Наименование;
            Цена_за_ед_продажа = product.Цена_за_ед_продажа;
            Количество = product.Количество;
            CategoryName = product.Категории?.Категория != null ? $"📂 {product.Категории.Категория}" : "📂 Без категории";

            if (product.Фото != null && product.Фото.Length > 0)
            {
                try
                {
                    using (var ms = new MemoryStream(product.Фото))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        PhotoSource = bitmap;
                    }
                }
                catch
                {
                    PhotoSource = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
                }
            }
            else
            {
                PhotoSource = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            }
        }
    }
}