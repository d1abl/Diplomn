using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

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

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private IQueryable<Категории> GetFilteredQuery()
        {
            var query = context.Категории.AsQueryable();

            if (!string.IsNullOrWhiteSpace(GetActualText(TxtSearch)))
            {
                var term = GetActualText(TxtSearch);
                query = query.Where(c => c.Категория.Contains(term) ||
                                        c.Описание_категории.Contains(term));
            }

            return query;
        }

        private void LoadData()
        {
            DataGridCategories.ItemsSource = context.Категории.ToList();
        }

        private void ApplyFilters()
        {
            DataGridCategories.ItemsSource = GetFilteredQuery().ToList();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            LoadData();
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

        private bool ValidateCategory(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            string name = GetActualText(TxtCategoryName);

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.AppendLine("• Введите название категории");
            }
            else
            {
                if (name.Length < 2)
                    errors.AppendLine("• Название должно содержать минимум 2 символа");

                if (name.Length > 40)
                    errors.AppendLine("• Название не должно превышать 40 символов");

                var allowed = new Regex(@"^[A-Za-zА-Яа-яЁё\-\s]+$");
                if (!allowed.IsMatch(name))
                    errors.AppendLine("• Название содержит недопустимые символы");

                // Проверка уникальности
                bool exists = excludeId.HasValue
                    ? context.Категории.Any(c => c.Категория == name && c.Код_категория != excludeId.Value)
                    : context.Категории.Any(c => c.Категория == name);

                if (exists)
                    errors.AppendLine("• Категория с таким названием уже существует");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateCategory(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var category = new Категории
                {
                    Категория = GetActualText(TxtCategoryName),
                    Описание_категории = GetActualText(TxtDescription)
                };

                context.Категории.Add(category);
                context.SaveChanges();

                MessageBox.Show("Категория успешно добавлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                if (!ValidateCategory(out string errorMessage, categoryId))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                category.Категория = GetActualText(TxtCategoryName);
                category.Описание_категории = GetActualText(TxtDescription);

                context.SaveChanges();

                MessageBox.Show("Категория успешно обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
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

                var productsWithCategory = context.Товары.Any(p => p.Код_категория == categoryId);
                if (productsWithCategory)
                {
                    MessageBox.Show("Нельзя удалить категорию — есть товары в этой категории!\n" +
                                   "Сначала переназначьте или удалите эти товары.",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить категорию '{category.Категория}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Категории.Remove(category);
                    context.SaveChanges();
                    MessageBox.Show("Категория удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

        private void ClearForm()
        {
            TxtCategoryId.Text = "";
            TxtCategoryName.Text = "";
            TxtDescription.Text = "";
            DataGridCategories.SelectedItem = null;
        }

        /// <summary>
        /// Получает реальный текст из TextBox, игнорируя плейсхолдер
        /// </summary>
        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;

            var placeholderText = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(placeholderText) && text == placeholderText)
                return string.Empty;

            return text;
        }
    }
}