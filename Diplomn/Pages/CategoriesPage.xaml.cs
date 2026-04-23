using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn.Pages
{
    public partial class CategoriesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public CategoriesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadData();
        }

        private void LoadData()
        {
            DataGridCategories.ItemsSource = context.Категории.ToList();
        }

        private void DataGridCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridCategories.SelectedItem is Категории category)
            {
                TxtCategoryId.Text = category.Код_категория.ToString();
                TxtCategoryName.Text = category.Категория;
                TxtDescription.Text = category.Описание_категории;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string name = TxtCategoryName.Text?.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Введите название категории!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (name.Length < 2)
                {
                    MessageBox.Show("Название категории должно содержать минимум 2 буквы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (name.Length > 40)
                {
                    MessageBox.Show("Название категории не должно превышать 40 символов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var allowed = new Regex(@"^[A-Za-zА-Яа-яЁё\-\s]+$");
                if (!allowed.IsMatch(name))
                {
                    MessageBox.Show("Название содержит недопустимые символы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var lettersOnly = Regex.Replace(name, @"[^A-Za-zА-Яа-яЁё]", "");
                if (lettersOnly.Length < 2)
                {
                    MessageBox.Show("Название должно содержать минимум 2 буквы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var vowel = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                var consonant = new Regex(@"[B-DF-HJ-NP-TV-Zb-df-hj-np-tv-zБ-ЖЗЙ-НП-РСТ-Яб-жзй-нп-рст-я]");

                if (!vowel.IsMatch(lettersOnly))
                {
                    MessageBox.Show("Название должно содержать хотя бы одну гласную!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!consonant.IsMatch(lettersOnly))
                {
                    MessageBox.Show("Название должно содержать хотя бы одну согласную!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool exists = context.Категории.Any(c => c.Категория == name);
                if (exists)
                {
                    MessageBox.Show("Категория с таким названием уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var category = new Категории
                {
                    Категория = name,
                    Описание_категории = TxtDescription.Text
                };

                context.Категории.Add(category);
                context.SaveChanges();

                MessageBox.Show("Категория успешно добавлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении категории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtCategoryId.Text))
                {
                    MessageBox.Show("Выберите категорию для обновления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int categoryId = int.Parse(TxtCategoryId.Text);
                var category = context.Категории.Find(categoryId);

                if (category == null)
                {
                    MessageBox.Show("Категория не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string name = TxtCategoryName.Text?.Trim();

                if (string.IsNullOrWhiteSpace(name))
                {
                    MessageBox.Show("Введите название категории!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (name.Length < 2)
                {
                    MessageBox.Show("Название категории должно содержать минимум 2 буквы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (name.Length > 40)
                {
                    MessageBox.Show("Название категории не должно превышать 40 символов!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var allowed = new Regex(@"^[A-Za-zА-Яа-яЁё\-\s]+$");
                if (!allowed.IsMatch(name))
                {
                    MessageBox.Show("Название содержит недопустимые символы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var lettersOnly = Regex.Replace(name, @"[^A-Za-zА-Яа-яЁё]", "");
                if (lettersOnly.Length < 2)
                {
                    MessageBox.Show("Название должно содержать минимум 2 буквы!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var vowel = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                var consonant = new Regex(@"[B-DF-HJ-NP-TV-Zb-df-hj-np-tv-zБ-ЖЗЙ-НП-РСТ-Яб-жзй-нп-рст-я]");

                if (!vowel.IsMatch(lettersOnly))
                {
                    MessageBox.Show("Название должно содержать хотя бы одну гласную!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!consonant.IsMatch(lettersOnly))
                {
                    MessageBox.Show("Название должно содержать хотя бы одну согласную!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool exists = context.Категории.Any(c => c.Категория == name && c.Код_категория != categoryId);
                if (exists)
                {
                    MessageBox.Show("Категория с таким названием уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                category.Категория = name;
                category.Описание_категории = TxtDescription.Text;

                context.SaveChanges();

                MessageBox.Show("Категория успешно обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении категории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtCategoryId.Text))
                {
                    MessageBox.Show("Выберите категорию для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int categoryId = int.Parse(TxtCategoryId.Text);
                var category = context.Категории.Find(categoryId);

                if (category == null)
                {
                    MessageBox.Show("Категория не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var productsWithCategory = context.Товары.Where(p => p.Код_категория == categoryId).Any();
                if (productsWithCategory)
                {
                    MessageBox.Show("Нельзя удалить категорию, так как есть товары в этой категории!\n" +
                                   "Сначала переназначьте или удалите эти товары.",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите удалить категорию '{category.Категория}'?",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Категории.Remove(category);
                    context.SaveChanges();
                    MessageBox.Show("Категория успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении категории: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            TxtCategoryId.Text = "";
            TxtCategoryName.Text = "";
            TxtDescription.Text = "";
            DataGridCategories.SelectedItem = null;
        }
    }
}