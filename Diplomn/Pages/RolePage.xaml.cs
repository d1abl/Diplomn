using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplomn.Pages
{
    public partial class RolePage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public RolePage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadRoles();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private void LoadRoles()
        {
            DataGridRoles.ItemsSource = context.Должность.ToList();
        }

        private void ApplyFilters()
        {
            var query = context.Должность.AsQueryable();

            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                query = query.Where(r => r.Название.Contains(term));
            }

            DataGridRoles.ItemsSource = query.ToList();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            LoadRoles();
        }

        private void DataGridRoles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridRoles.SelectedItem is Должность role)
            {
                TxtRoleId.Text = role.Код_должности.ToString();
                TxtRoleName.Text = role.Название;
                TxtAccessLevel.Text = role.Уровень_доступа.ToString();
            }
        }

        private bool ValidateRole(out string errorMessage, int? excludeId = null)
        {
            var errors = new StringBuilder();
            string roleName = TxtRoleName.Text?.Trim();

            if (string.IsNullOrWhiteSpace(roleName))
                errors.AppendLine("• Введите название должности");

            if (!int.TryParse(TxtAccessLevel.Text, out int accessLevel))
                errors.AppendLine("• Уровень доступа должен быть числом");
            else if (accessLevel < 1 || accessLevel > 10)
                errors.AppendLine("• Уровень доступа должен быть от 1 до 10");

            if (!string.IsNullOrWhiteSpace(roleName))
            {
                bool exists = excludeId.HasValue
                    ? context.Должность.Any(r => r.Название == roleName && r.Код_должности != excludeId.Value)
                    : context.Должность.Any(r => r.Название == roleName);

                if (exists)
                    errors.AppendLine("• Должность с таким названием уже существует");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        private void AddRole_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateRole(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var role = new Должность
                {
                    Название = TxtRoleName.Text.Trim(),
                    Уровень_доступа = int.Parse(TxtAccessLevel.Text)
                };

                context.Должность.Add(role);
                context.SaveChanges();

                MessageBox.Show("Должность успешно добавлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadRoles();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateRole_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtRoleId.Text))
                {
                    MessageBox.Show("Выберите должность для обновления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int roleId = int.Parse(TxtRoleId.Text);
                var role = context.Должность.Find(roleId);

                if (role == null)
                {
                    MessageBox.Show("Должность не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (!ValidateRole(out string errorMessage, roleId))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                role.Название = TxtRoleName.Text.Trim();
                role.Уровень_доступа = int.Parse(TxtAccessLevel.Text);

                context.SaveChanges();

                MessageBox.Show("Должность успешно обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadRoles();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteRole_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtRoleId.Text))
                {
                    MessageBox.Show("Выберите должность для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int roleId = int.Parse(TxtRoleId.Text);
                var role = context.Должность.Find(roleId);

                if (role == null)
                {
                    MessageBox.Show("Должность не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var employeesWithRole = context.Сотрудники.Where(s => s.Код_должности == roleId).Any();
                if (employeesWithRole)
                {
                    MessageBox.Show("Нельзя удалить должность - есть сотрудники с этой должностью!\n" +
                                   "Сначала переназначьте или удалите этих сотрудников.",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить должность '{role.Название}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Должность.Remove(role);
                    context.SaveChanges();
                    MessageBox.Show("Должность удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadRoles();
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
            TxtRoleId.Text = "";
            TxtRoleName.Text = "";
            TxtAccessLevel.Text = "";
            DataGridRoles.SelectedItem = null;
        }
    }
}