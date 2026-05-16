using Diplomn.Addons;
using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplomn.Pages
{
    /// <summary>
    /// Страница управления брендами товаров
    /// </summary>
    public partial class BrandsPage : Page
    {
        #region Поля

        private BDEntities context;
        private AccessManager.AccessRights rights;
        private WrapPanel actionButtonsPanel;

        #endregion

        #region Конструктор

        public BrandsPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            rights = AccessManager.GetAccessRights(user.Должность?.Уровень_доступа ?? 10);
            actionButtonsPanel = FindName("ActionButtonsPanel") as WrapPanel;
            ButtonHelper.CreateActionButtons(actionButtonsPanel,
                canCreate: rights.Brands.CanCreate,
                canEdit: rights.Brands.CanEdit,
                canDelete: rights.Brands.CanDelete,
                createHandler: Add_Click,
                editHandler: Update_Click,
                deleteHandler: Delete_Click,
                clearHandler: ClearForm_Click
            );
            LoadData();
        }

        #endregion

        #region Загрузка данных

        /// <summary>
        /// Загружает все бренды из базы данных
        /// </summary>
        private void LoadData()
        {
            DataGridBrands.ItemsSource = context.Бренд.ToList();
        }

        /// <summary>
        /// Формирует отфильтрованный запрос по поисковому тексту
        /// </summary>
        private IQueryable<Бренд> GetFilteredQuery()
        {
            var query = context.Бренд.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(b => b.Наименование_бредна.Contains(searchText));

            return query;
        }

        #endregion

        #region Фильтрация

        /// <summary>
        /// Применяет фильтры и обновляет таблицу
        /// </summary>
        private void ApplyFilters()
        {
            DataGridBrands.ItemsSource = GetFilteredQuery().ToList();
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

        /// <summary>
        /// Заполняет форму данными выбранного бренда
        /// </summary>
        private void DataGridBrands_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridBrands.SelectedItem is Бренд brand)
            {
                TxtBrandId.Text = brand.Код_бренда.ToString();
                TxtBrandName.Text = brand.Наименование_бредна;
            }
        }

        #endregion

        #region Валидация

        /// <summary>
        /// Проверяет корректность введённых данных бренда
        /// </summary>
        private bool ValidateBrand(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            var name = GetActualText(TxtBrandName);

            if (string.IsNullOrWhiteSpace(name))
                errors.AppendLine("• Введите наименование бренда");
            else if (name.Length < 2)
                errors.AppendLine("• Название должно быть не короче 2 символов");
            else if (name.Length > 50)
                errors.AppendLine("• Название не должно превышать 50 символов");
            else
            {
                // Проверка уникальности названия
                var exists = excludeId.HasValue
                    ? context.Бренд.Any(b => b.Наименование_бредна == name && b.Код_бренда != excludeId.Value)
                    : context.Бренд.Any(b => b.Наименование_бредна == name);

                if (exists)
                    errors.AppendLine("• Бренд с таким названием уже существует");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        #endregion

        #region CRUD операции

        /// <summary>
        /// Добавляет новый бренд в базу данных
        /// </summary>
        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateBrand(out var error))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var brand = new Бренд { Наименование_бредна = GetActualText(TxtBrandName) };
                context.Бренд.Add(brand);
                context.SaveChanges();

                MessageBox.Show("Бренд добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обновляет данные выбранного бренда
        /// </summary>
        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtBrandId.Text))
                {
                    MessageBox.Show("Выберите бренд!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var brandId = int.Parse(TxtBrandId.Text);
                var brand = context.Бренд.Find(brandId);

                if (brand == null)
                {
                    MessageBox.Show("Бренд не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateBrand(out var error, brandId))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                brand.Наименование_бредна = GetActualText(TxtBrandName);
                context.SaveChanges();

                MessageBox.Show("Бренд обновлён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Удаляет выбранный бренд с проверкой связанных товаров
        /// </summary>
        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtBrandId.Text))
                {
                    MessageBox.Show("Выберите бренд!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var brandId = int.Parse(TxtBrandId.Text);
                var brand = context.Бренд.Find(brandId);

                if (brand == null)
                {
                    MessageBox.Show("Бренд не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Нельзя удалить бренд, если есть товары с ним
                if (context.Товары.Any(p => p.Код_бренда == brandId))
                {
                    MessageBox.Show("Нельзя удалить бренд — есть связанные товары!",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить бренд «{brand.Наименование_бредна}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Бренд.Remove(brand);
                    context.SaveChanges();
                    MessageBox.Show("Бренд удалён!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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

        /// <summary>
        /// Сбрасывает форму редактирования в исходное состояние
        /// </summary>
        private void ClearForm()
        {
            TxtBrandId.Text = "";
            TxtBrandName.Text = "";
            DataGridBrands.SelectedItem = null;
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

        #endregion

        #region Вспомогательные методы

        /// <summary>
        /// Возвращает реальный текст из TextBox, игнорируя placeholder
        /// </summary>
        private string GetActualText(TextBox textBox)
        {
            if (textBox == null) return string.Empty;

            var placeholder = Addons.PlaceholderBehavior.GetPlaceholderText(textBox);
            var text = textBox.Text?.Trim() ?? string.Empty;

            return (!string.IsNullOrEmpty(placeholder) && text == placeholder)
                ? string.Empty
                : text;
        }

        #endregion
    }
}