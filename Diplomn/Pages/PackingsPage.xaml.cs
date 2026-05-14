using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления фасовкой товаров
    /// </summary>
    public partial class PackingsPage : Page
    {
        #region Поля

        private BDEntities context;

        #endregion

        #region Конструктор

        public PackingsPage()
        {
            InitializeComponent();
            context = new BDEntities();
            LoadData();
        }

        #endregion

        #region Загрузка данных

        private void LoadData()
        {
            DataGridPackings.ItemsSource = context.Фасовка.ToList();
        }

        private IQueryable<Фасовка> GetFilteredQuery()
        {
            var query = context.Фасовка.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText) && int.TryParse(searchText, out int qty))
                query = query.Where(p => p.Количество == qty);

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters() => DataGridPackings.ItemsSource = GetFilteredQuery().ToList();
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

        private void DataGridPackings_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridPackings.SelectedItem is Фасовка packing)
            {
                TxtPackingId.Text = packing.Код_фасовки.ToString();
                TxtPackingQuantity.Text = packing.Количество.ToString();
            }
        }

        #endregion

        #region Валидация

        private bool Validate(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            var quantityText = GetActualText(TxtPackingQuantity);

            if (string.IsNullOrWhiteSpace(quantityText))
                errors.AppendLine("• Введите количество");
            else if (!int.TryParse(quantityText, out int qty))
                errors.AppendLine("• Количество должно быть целым числом");
            else if (qty <= 0)
                errors.AppendLine("• Количество должно быть больше 0");
            else if (qty > 100000)
                errors.AppendLine("• Количество не должно превышать 100 000");
            else
            {
                var exists = excludeId.HasValue
                    ? context.Фасовка.Any(p => p.Количество == qty && p.Код_фасовки != excludeId.Value)
                    : context.Фасовка.Any(p => p.Количество == qty);

                if (exists)
                    errors.AppendLine("• Фасовка с таким количеством уже существует");
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

                var item = new Фасовка { Количество = int.Parse(GetActualText(TxtPackingQuantity)) };
                context.Фасовка.Add(item);
                context.SaveChanges();

                MessageBox.Show("Фасовка добавлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtPackingId.Text))
                {
                    MessageBox.Show("Выберите фасовку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtPackingId.Text);
                var item = context.Фасовка.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Фасовка не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Validate(out var error, id))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                item.Количество = int.Parse(GetActualText(TxtPackingQuantity));
                context.SaveChanges();

                MessageBox.Show("Фасовка обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtPackingId.Text))
                {
                    MessageBox.Show("Выберите фасовку!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtPackingId.Text);
                var item = context.Фасовка.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Фасовка не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Товары.Any(p => p.Код_фасовки == id))
                {
                    MessageBox.Show("Нельзя удалить — есть связанные товары!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить фасовку «{item.Количество}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Фасовка.Remove(item);
                    context.SaveChanges();
                    MessageBox.Show("Удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
            TxtPackingId.Text = "";
            TxtPackingQuantity.Text = "";
            DataGridPackings.SelectedItem = null;
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