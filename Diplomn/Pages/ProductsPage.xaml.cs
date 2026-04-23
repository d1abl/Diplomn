using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Diplomn.Pages
{
    public partial class ProductsPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;

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

                // Загрузка фото товара
                LoadProductPhoto(product);

                // Сброс выбранного изображения
                selectedImageData = null;
            }
        }

        private void LoadProductPhoto(Товары product)
        {
            try
            {
                if (product?.Фото != null && product.Фото.Length > 0)
                {
                    using (var ms = new MemoryStream(product.Фото))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ProductPhoto.Source = bitmap;
                    }
                }
                else
                {
                    ProductPhoto.Source = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки фото: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                ProductPhoto.Source = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            }
        }

        private void SelectPhoto_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg; *.jpeg; *.png; *.bmp)|*.jpg;*.jpeg;*.png;*.bmp|Все файлы (*.*)|*.*",
                    Title = "Выберите фотографию товара"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedImageData = File.ReadAllBytes(openFileDialog.FileName);

                    using (var ms = new MemoryStream(selectedImageData))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        ProductPhoto.Source = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе фото: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateProduct(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();

            string productName = TxtProductName.Text?.Trim();

            // Наименование
            if (string.IsNullOrWhiteSpace(productName))
                errors.AppendLine("❌ Введите наименование товара!");

            // Цена
            if (!decimal.TryParse(TxtPrice.Text, out decimal price))
                errors.AppendLine("❌ Введите корректную цену (число)!");
            else if (price < 0)
                errors.AppendLine("❌ Цена не может быть отрицательной!");

            // Категория
            if (CmbCategory.SelectedValue == null)
                errors.AppendLine("❌ Выберите категорию!");

            // Количество
            if (!int.TryParse(TxtQuantity.Text, out int quantity))
                errors.AppendLine("❌ Введите корректное количество (целое число)!");
            else if (quantity < 0)
                errors.AppendLine("❌ Количество не может быть отрицательным!");

            // Проверка уникальности наименования
            if (!string.IsNullOrWhiteSpace(productName))
            {
                bool exists;
                if (excludeId.HasValue)
                    exists = context.Товары.Any(p => p.Наименование == productName && p.Код_товара != excludeId.Value);
                else
                    exists = context.Товары.Any(p => p.Наименование == productName);

                if (exists)
                    errors.AppendLine("❌ Товар с таким наименованием уже существует!");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
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

                var product = new Товары
                {
                    Наименование = TxtProductName.Text?.Trim(),
                    Цена_за_ед_продажа = decimal.Parse(TxtPrice.Text),
                    Код_категория = (int)CmbCategory.SelectedValue,
                    Количество = int.Parse(TxtQuantity.Text),
                    Фото = selectedImageData  // Сохраняем фото
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

                int productId = int.Parse(TxtProductId.Text);
                var product = context.Товары.Find(productId);

                if (product == null)
                {
                    MessageBox.Show("Товар не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateProduct(out string errorMessage, productId))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                product.Наименование = TxtProductName.Text?.Trim();
                product.Цена_за_ед_продажа = decimal.Parse(TxtPrice.Text);
                product.Код_категория = (int)CmbCategory.SelectedValue;
                product.Количество = int.Parse(TxtQuantity.Text);

                // Обновляем фото только если было выбрано новое
                if (selectedImageData != null)
                {
                    product.Фото = selectedImageData;
                }

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
            ProductPhoto.Source = new BitmapImage(new Uri("/Photos/istockproductphoto.png", UriKind.RelativeOrAbsolute));
            selectedImageData = null;
            DataGridProducts.SelectedItem = null;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}