using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления производителями товаров
    /// </summary>
    public partial class ManufacturersPage : Page
    {
        #region Поля

        private BDEntities context;

        #endregion

        #region Конструктор

        public ManufacturersPage()
        {
            InitializeComponent();
            context = new BDEntities();
            LoadData();
        }

        #endregion

        #region Загрузка данных

        private void LoadData()
        {
            DataGridManufacturers.ItemsSource = context.Производитель.ToList();
        }

        private IQueryable<Производитель> GetFilteredQuery()
        {
            var query = context.Производитель.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(m => m.Наименование_произваодителя.Contains(searchText));

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters()
        {
            DataGridManufacturers.ItemsSource = GetFilteredQuery().ToList();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            LoadData();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) ApplyFilters();
        }

        #endregion

        #region Выбор в таблице

        private void DataGridManufacturers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridManufacturers.SelectedItem is Производитель m)
            {
                TxtManufacturerId.Text = m.Код_производителя.ToString();
                TxtManufacturerName.Text = m.Наименование_произваодителя;
            }
        }

        #endregion

        #region Валидация

        private bool Validate(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            var name = GetActualText(TxtManufacturerName);

            if (string.IsNullOrWhiteSpace(name))
                errors.AppendLine("• Введите наименование");
            else if (name.Length < 2)
                errors.AppendLine("• Название должно быть не короче 2 символов");
            else if (name.Length > 50)
                errors.AppendLine("• Название не должно превышать 50 символов");
            else
            {
                var exists = excludeId.HasValue
                    ? context.Производитель.Any(m => m.Наименование_произваодителя == name && m.Код_производителя != excludeId.Value)
                    : context.Производитель.Any(m => m.Наименование_произваодителя == name);

                if (exists)
                    errors.AppendLine("• Производитель с таким названием уже существует");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        #endregion

        #region CRUD операции

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!Validate(out var error))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var item = new Производитель { Наименование_произваодителя = GetActualText(TxtManufacturerName) };
                context.Производитель.Add(item);
                context.SaveChanges();

                MessageBox.Show("Производитель добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtManufacturerId.Text))
                {
                    MessageBox.Show("Выберите производителя!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtManufacturerId.Text);
                var item = context.Производитель.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Производитель не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Validate(out var error, id))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                item.Наименование_произваодителя = GetActualText(TxtManufacturerName);
                context.SaveChanges();

                MessageBox.Show("Производитель обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtManufacturerId.Text))
                {
                    MessageBox.Show("Выберите производителя!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtManufacturerId.Text);
                var item = context.Производитель.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Производитель не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Товары.Any(p => p.Код_производителя == id))
                {
                    MessageBox.Show("Нельзя удалить — есть связанные товары!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить «{item.Наименование_произваодителя}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Производитель.Remove(item);
                    context.SaveChanges();
                    MessageBox.Show("Удалён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Очистка формы

        private void ClearForm()
        {
            TxtManufacturerId.Text = "";
            TxtManufacturerName.Text = "";
            DataGridManufacturers.SelectedItem = null;
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

        #endregion

        #region Вспомогательные методы

        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;
            var placeholder = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;
            return (!string.IsNullOrEmpty(placeholder) && text == placeholder) ? string.Empty : text;
        }

        #endregion
    }
}