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
    /// Страница управления должностями и уровнями доступа
    /// </summary>
    public partial class RolePage : Page
    {
        #region Поля

        private BDEntities context;
        private AccessManager.AccessRights rights;
        private WrapPanel actionButtonsPanel;
        #endregion

        #region Конструктор

        public RolePage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();

            rights = AccessManager.GetAccessRights(user.Должность?.Уровень_доступа ?? 10);
            actionButtonsPanel = FindName("ActionButtonsPanel") as WrapPanel;
            ButtonHelper.CreateActionButtons(actionButtonsPanel,
                canCreate: rights.Roles.CanCreate,
                canEdit: rights.Roles.CanDelete,
                canDelete: rights.Roles.CanDelete,
                createHandler: Add_Click,
                editHandler: Update_Click,
                deleteHandler: Delete_Click,
                clearHandler: ClearForm_Click
                );
            LoadData();
        }

        #endregion

        #region Загрузка данных

        private void LoadData()
        {
            DataGridRoles.ItemsSource = context.Должность.ToList();
        }

        private IQueryable<Должность> GetFilteredQuery()
        {
            var query = context.Должность.AsQueryable();
            var searchText = GetActualText(TxtSearch);

            if (!string.IsNullOrWhiteSpace(searchText))
                query = query.Where(r => r.Название.Contains(searchText));

            return query;
        }

        #endregion

        #region Фильтрация

        private void ApplyFilters() => DataGridRoles.ItemsSource = GetFilteredQuery().ToList();
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

        private void DataGridRoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridRoles.SelectedItem is Должность role)
            {
                TxtRoleId.Text = role.Код_должности.ToString();
                TxtRoleName.Text = role.Название;
                TxtAccessLevel.Text = role.Уровень_доступа.ToString();
            }
        }

        #endregion

        #region Валидация

        private bool Validate(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            var name = GetActualText(TxtRoleName);
            var levelText = GetActualText(TxtAccessLevel);

            if (string.IsNullOrWhiteSpace(name))
                errors.AppendLine("• Введите название должности");

            if (!int.TryParse(levelText, out int level))
                errors.AppendLine("• Уровень доступа должен быть числом");
            else if (level < 1 || level > 10)
                errors.AppendLine("• Уровень доступа должен быть от 1 до 10");

            if (!string.IsNullOrWhiteSpace(name))
            {
                var exists = excludeId.HasValue
                    ? context.Должность.Any(r => r.Название == name && r.Код_должности != excludeId.Value)
                    : context.Должность.Any(r => r.Название == name);

                if (exists)
                    errors.AppendLine("• Должность с таким названием уже существует");
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

                var role = new Должность
                {
                    Название = GetActualText(TxtRoleName),
                    Уровень_доступа = int.Parse(GetActualText(TxtAccessLevel))
                };

                context.Должность.Add(role);
                context.SaveChanges();

                MessageBox.Show("Должность добавлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtRoleId.Text))
                {
                    MessageBox.Show("Выберите должность!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtRoleId.Text);
                var role = context.Должность.Find(id);

                if (role == null)
                {
                    MessageBox.Show("Должность не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!Validate(out var error, id))
                {
                    MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                role.Название = GetActualText(TxtRoleName);
                role.Уровень_доступа = int.Parse(GetActualText(TxtAccessLevel));
                context.SaveChanges();

                MessageBox.Show("Должность обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtRoleId.Text))
                {
                    MessageBox.Show("Выберите должность!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var id = int.Parse(TxtRoleId.Text);
                var role = context.Должность.Find(id);

                if (role == null)
                {
                    MessageBox.Show("Должность не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (context.Сотрудники.Any(s => s.Код_должности == id))
                {
                    MessageBox.Show("Нельзя удалить — есть сотрудники с этой должностью!\nСначала переназначьте их.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить должность «{role.Название}»?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Должность.Remove(role);
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
            TxtRoleId.Text = "";
            TxtRoleName.Text = "";
            TxtAccessLevel.Text = "";
            DataGridRoles.SelectedItem = null;
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