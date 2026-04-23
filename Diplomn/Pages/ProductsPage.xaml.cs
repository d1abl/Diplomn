using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn.Pages
{
    public partial class ProductsPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public ProductsPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Товары - {user.Фамилия} {user.Имя}";
            LoadCategories();
            LoadData();
        }

        private void LoadCategories()
        {
            CmbCategory.ItemsSource = context.Категории.ToList();
        }

        private void LoadData()
        {
            DataGridProducts.ItemsSource = context.Товары
                .Include("Категории")
                .ToList();
        }

        private void DataGridProducts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridProducts.SelectedItem is Товары product)
            {
                TxtProductId.Text = product.Код_товара.ToString();
                TxtProductName.Text = product.Наименование;
                TxtPrice.Text = product.Цена_за_ед_продажа.ToString();
                CmbCategory.SelectedValue = product.Код_категория;
                TxtQuantity.Text = product.Количество.ToString();
            }
        }

        private bool ValidateProduct(out string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(TxtProductName.Text))
            {
                errorMessage = "Введите наименование товара!";
                return false;
            }

            if (!decimal.TryParse(TxtPrice.Text, out decimal price) || price < 0)
            {
                errorMessage = "Введите корректную цену (неотрицательное число)!";
                return false;
            }

            if (CmbCategory.SelectedValue == null)
            {
                errorMessage = "Выберите категорию!";
                return false;
            }

            if (!int.TryParse(TxtQuantity.Text, out int quantity) || quantity < 0)
            {
                errorMessage = "Введите корректное количество (неотрицательное целое число)!";
                return false;
            }

            errorMessage = "";
            return true;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateProduct(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string productName = TxtProductName.Text?.Trim();

                // Проверка уникальности наименования
                bool exists = context.Товары.Any(p => p.Наименование == productName);
                if (exists)
                {
                    MessageBox.Show("Товар с таким наименованием уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var product = new Товары
                {
                    Наименование = productName,
                    Цена_за_ед_продажа = decimal.Parse(TxtPrice.Text),
                    Код_категория = (int)CmbCategory.SelectedValue,
                    Количество = int.Parse(TxtQuantity.Text)
                };

                context.Товары.Add(product);
                context.SaveChanges();

                MessageBox.Show("Товар успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtProductId.Text))
                {
                    MessageBox.Show("Выберите товар для обновления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!ValidateProduct(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int productId = int.Parse(TxtProductId.Text);
                var product = context.Товары.Find(productId);

                if (product == null)
                {
                    MessageBox.Show("Товар не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string productName = TxtProductName.Text?.Trim();

                // Проверка уникальности наименования (исключая текущий товар)
                bool exists = context.Товары.Any(p => p.Наименование == productName && p.Код_товара != productId);
                if (exists)
                {
                    MessageBox.Show("Товар с таким наименованием уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                product.Наименование = productName;
                product.Цена_за_ед_продажа = decimal.Parse(TxtPrice.Text);
                product.Код_категория = (int)CmbCategory.SelectedValue;
                product.Количество = int.Parse(TxtQuantity.Text);

                context.SaveChanges();

                MessageBox.Show("Товар успешно обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtProductId.Text))
                {
                    MessageBox.Show("Выберите товар для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int productId = int.Parse(TxtProductId.Text);
                var product = context.Товары.Find(productId);

                if (product == null)
                {
                    MessageBox.Show("Товар не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var hasSales = context.Состав_продажи.Any(s => s.Код_товара == productId);
                var hasOrders = context.Состав_заказа.Any(o => o.Код_товара == productId);

                if (hasSales || hasOrders)
                {
                    MessageBox.Show("Нельзя удалить товар, так как он используется в продажах или заказах!",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите удалить товар '{product.Наименование}'?",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Товары.Remove(product);
                    context.SaveChanges();
                    MessageBox.Show("Товар успешно удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении товара: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            TxtProductId.Text = "";
            TxtProductName.Text = "";
            TxtPrice.Text = "";
            CmbCategory.SelectedIndex = -1;
            TxtQuantity.Text = "";
            DataGridProducts.SelectedItem = null;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}