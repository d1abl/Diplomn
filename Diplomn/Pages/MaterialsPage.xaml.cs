using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления материалами товаров
    /// </summary>
    public partial class MaterialsPage : Page
    {
        #region Поля

        private BDEntities context;

        #endregion

        #region Конструктор

        public MaterialsPage()
        {
            InitializeComponent();
            context = new BDEntities();
            LoadData();
        }

        #endregion

        #region Загрузка данных

        private void LoadData()
        {
            DataGridMaterials.ItemsSource = context.Материал.ToList();
        }

        private IQueryable<Материал> GetFilteredQuery()
        {
            var query = context.Материал.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(m => m.Наименование_материала.Contains(searchText));

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters() => DataGridMaterials.ItemsSource = GetFilteredQuery().ToList();
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

        private void DataGridMaterials_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridMaterials.SelectedItem is Материал material)
            {
                TxtMaterialId.Text = material.Код_материала.ToString();
                TxtMaterialName.Text = material.Наименование_материала;
            }
        }

        #endregion

        #region Валидация

        private bool Validate(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            var name = GetActualText(TxtMaterialName);

            if (string.IsNullOrWhiteSpace(name))
                errors.AppendLine("• Введите наименование");
            else if (name.Length < 2)
                errors.AppendLine("• Название должно быть не короче 2 символов");
            else if (name.Length > 50)
                errors.AppendLine("• Название не должно превышать 50 символов");
            else
            {
                var exists = excludeId.HasValue
                    ? context.Материал.Any(m => m.Наименование_материала == name && m.Код_материала != excludeId.Value)
                    : context.Материал.Any(m => m.Наименование_материала == name);

                if (exists)
                    errors.AppendLine("• Материал с таким названием уже существует");
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

                var item = new Материал { Наименование_материала = GetActualText(TxtMaterialName) };
                context.Материал.Add(item);
                context.SaveChanges();

                MessageBox.Show("Материал добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtMaterialId.Text))
                {
                    MessageBox.Show("Выберите материал!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtMaterialId.Text);
                var item = context.Материал.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Материал не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Validate(out var error, id))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                item.Наименование_материала = GetActualText(TxtMaterialName);
                context.SaveChanges();

                MessageBox.Show("Материал обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtMaterialId.Text))
                {
                    MessageBox.Show("Выберите материал!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtMaterialId.Text);
                var item = context.Материал.Find(id);

                if (item == null)
                {
                    MessageBox.Show("Материал не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Товары.Any(p => p.Код_материала == id))
                {
                    MessageBox.Show("Нельзя удалить — есть связанные товары!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить «{item.Наименование_материала}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Материал.Remove(item);
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
            TxtMaterialId.Text = "";
            TxtMaterialName.Text = "";
            DataGridMaterials.SelectedItem = null;
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