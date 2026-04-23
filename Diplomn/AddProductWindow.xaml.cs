using System;
using System.Linq;
using System.Windows;

namespace Diplomn
{
    public partial class AddProductWindow : Window
    {
        private BDEntities context;
        private Товары currentProduct;

        public AddProductWindow(BDEntities context, Товары product)
        {
            InitializeComponent();
            this.context = context;
            this.currentProduct = product;
            this.DataContext = currentProduct;

            CmbCategory.ItemsSource = context.Категории.ToList();

            if (product.Код_категория > 0)
                CmbCategory.SelectedValue = product.Код_категория;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Basic validation
                if (string.IsNullOrWhiteSpace(currentProduct.Наименование))
                {
                    MessageBox.Show("Введите наименование товара!");
                    return;
                }

                if (currentProduct.Цена_за_ед_продажа > 0 && currentProduct.Цена_за_ед_продажа < 0)
                {
                    MessageBox.Show("Цена не может быть отрицательной!");
                    return;
                }

                if (CmbCategory.SelectedValue == null)
                {
                    MessageBox.Show("Выберите категорию!");
                    return;
                }

                if (currentProduct.Количество < 0)
                {
                    MessageBox.Show("Количество не может быть отрицательным!");
                    return;
                }

                if (currentProduct.Код_товара == 0)
                    context.Товары.Add(currentProduct);

                context.SaveChanges();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}