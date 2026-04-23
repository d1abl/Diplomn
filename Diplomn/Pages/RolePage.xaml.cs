using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

        private void LoadRoles()
        {
            DataGridRoles.ItemsSource = context.Должность.ToList();
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

        private void AddRole_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtRoleName.Text))
                {
                    MessageBox.Show("Введите название роли!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(TxtAccessLevel.Text, out int accessLevel) || accessLevel < 1 || accessLevel > 10)
                {
                    MessageBox.Show("Уровень доступа должен быть числом от 1 до 10!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string roleName = TxtRoleName.Text.Trim();

                // Проверка уникальности названия роли
                bool exists = context.Должность.Any(r => r.Название == roleName);
                if (exists)
                {
                    MessageBox.Show("Роль с таким названием уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var role = new Должность
                {
                    Название = roleName,
                    Уровень_доступа = accessLevel
                };

                context.Должность.Add(role);
                context.SaveChanges();

                MessageBox.Show("Роль успешно добавлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadRoles();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении роли: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateRole_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtRoleId.Text))
                {
                    MessageBox.Show("Выберите роль для обновления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int roleId = int.Parse(TxtRoleId.Text);
                var role = context.Должность.Find(roleId);

                if (role == null)
                {
                    MessageBox.Show("Роль не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (string.IsNullOrWhiteSpace(TxtRoleName.Text))
                {
                    MessageBox.Show("Введите название роли!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!int.TryParse(TxtAccessLevel.Text, out int accessLevel) || accessLevel < 1 || accessLevel > 10)
                {
                    MessageBox.Show("Уровень доступа должен быть числом от 1 до 10!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string roleName = TxtRoleName.Text.Trim();

                // Проверка уникальности названия роли (исключая текущую роль)
                bool exists = context.Должность.Any(r => r.Название == roleName && r.Код_должности != roleId);
                if (exists)
                {
                    MessageBox.Show("Роль с таким названием уже существует!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                role.Название = roleName;
                role.Уровень_доступа = accessLevel;

                context.SaveChanges();

                MessageBox.Show("Роль успешно обновлена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadRoles();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении роли: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteRole_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtRoleId.Text))
                {
                    MessageBox.Show("Выберите роль для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int roleId = int.Parse(TxtRoleId.Text);
                var role = context.Должность.Find(roleId);

                if (role == null)
                {
                    MessageBox.Show("Роль не найдена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Проверяем, есть ли сотрудники с этой ролью
                var employeesWithRole = context.Сотрудники.Where(s => s.Код_должности == roleId).Any();
                if (employeesWithRole)
                {
                    MessageBox.Show("Нельзя удалить роль, так как есть сотрудники, занимающие эту должность!\n" +
                                   "Сначала переназначьте или удалите этих сотрудников.",
                                   "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите удалить роль '{role.Название}'?",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Должность.Remove(role);
                    context.SaveChanges();
                    MessageBox.Show("Роль успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadRoles();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении роли: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            TxtRoleId.Text = "";
            TxtRoleName.Text = "";
            TxtAccessLevel.Text = "";
            DataGridRoles.SelectedItem = null;
        }
    }
}