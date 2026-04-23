using System;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn.Pages
{
    public partial class EmployeesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public EmployeesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Добро пожаловать, {user.Фамилия} {user.Имя}!";
            LoadPositions();
            LoadData();
        }

        private void LoadPositions()
        {
            CmbPosition.ItemsSource = context.Должность.ToList();
        }

        private void LoadData()
        {
            DataGridEmployees.ItemsSource = context.Сотрудники
                .Include("Должность")
                .ToList();
        }

        private void DataGridEmployees_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridEmployees.SelectedItem is Сотрудники employee)
            {
                TxtEmployeeId.Text = employee.Код_сотрудника.ToString();
                TxtLastName.Text = employee.Фамилия;
                TxtFirstName.Text = employee.Имя;
                TxtMiddleName.Text = employee.Отчество;
                TxtPhone.Text = employee.Телефон;
                CmbPosition.SelectedValue = employee.Код_должности;
                TxtLogin.Text = employee.Логин;
                PassBox.Password = employee.Пароль;
            }
        }

        private bool ValidateEmployee(out string errorMessage, bool skipPasswordValidation = false)
        {
            var errors = new StringBuilder();

            string lastName = TxtLastName.Text?.Trim();
            string firstName = TxtFirstName.Text?.Trim();
            string middleName = TxtMiddleName.Text?.Trim();
            string phone = TxtPhone.Text?.Trim();
            string login = TxtLogin.Text?.Trim();
            string password = PassBox.Password;

            // Фамилия
            if (string.IsNullOrWhiteSpace(lastName))
                errors.AppendLine("❌ Фамилия не введена!");
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(lastName))
                    errors.AppendLine("❌ Фамилия содержит недопустимые символы!");
                else if (lastName.Length > 30)
                    errors.AppendLine("❌ Фамилия должна быть не длиннее 30 символов!");
                else
                {
                    var lettersOnly = Regex.Replace(lastName, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errors.AppendLine("❌ Фамилия должна содержать минимум 2 буквы!");
                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Фамилия должна содержать хотя бы одну гласную!");
                }
            }

            // Имя
            if (string.IsNullOrWhiteSpace(firstName))
                errors.AppendLine("❌ Имя не введено!");
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(firstName))
                    errors.AppendLine("❌ Имя содержит недопустимые символы!");
                else if (firstName.Length > 30)
                    errors.AppendLine("❌ Имя должно быть не длиннее 30 символов!");
                else
                {
                    var lettersOnly = Regex.Replace(firstName, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errors.AppendLine("❌ Имя должно содержать минимум 2 буквы!");
                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Имя должно содержать хотя бы одну гласную!");
                }
            }

            // Отчество (опционально)
            if (!string.IsNullOrWhiteSpace(middleName))
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(middleName))
                    errors.AppendLine("❌ Отчество содержит недопустимые символы!");
                else if (middleName.Length > 30)
                    errors.AppendLine("❌ Отчество должно быть не длиннее 30 символов!");
                else
                {
                    var lettersOnly = Regex.Replace(middleName, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errors.AppendLine("❌ Отчество должно содержать минимум 2 буквы!");
                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Отчество должно содержать хотя бы одну гласную!");
                }
            }

            // Должность
            if (CmbPosition.SelectedValue == null)
                errors.AppendLine("❌ Должность не выбрана!");

            // Логин
            if (string.IsNullOrWhiteSpace(login))
                errors.AppendLine("❌ Логин не введен!");

            // Пароль
            if (!skipPasswordValidation)
            {
                if (string.IsNullOrWhiteSpace(password))
                    errors.AppendLine("❌ Пароль не введен!");
                else
                {
                    if (password.Length < 12)
                        errors.AppendLine("❌ Пароль должен быть не менее 12 символов!");
                    if (!Regex.IsMatch(password, "[A-ZА-ЯЁ]"))
                        errors.AppendLine("❌ Пароль должен содержать хотя бы одну заглавную букву!");
                    if (!Regex.IsMatch(password, "[a-zа-яё]"))
                        errors.AppendLine("❌ Пароль должен содержать хотя бы одну строчную букву!");
                    if (!Regex.IsMatch(password, "\\d"))
                        errors.AppendLine("❌ Пароль должен содержать хотя бы одну цифру!");
                    if (!Regex.IsMatch(password, "[^A-Za-zА-Яа-яЁё0-9]"))
                        errors.AppendLine("❌ Пароль должен содержать хотя бы один специальный символ!");
                }
            }

            // Телефон (проверка формата и уникальности будет в методах Add/Update)
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneRegex = new Regex(@"^\+?\d{11}$");
                if (!phoneRegex.IsMatch(phone))
                    errors.AppendLine("❌ Неверный формат телефона. Ожидается 11 цифр, можно с '+' в начале.");
            }

            errorMessage = errors.ToString();
            return errors.Length == 0;
        }

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateEmployee(out string errorMessage))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string login = TxtLogin.Text?.Trim();
                string phone = TxtPhone.Text?.Trim();

                // Проверка уникальности логина
                bool loginExists = context.Сотрудники.Any(s => s.Логин == login);
                if (loginExists)
                {
                    MessageBox.Show("Логин уже используется другим сотрудником!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка уникальности телефона (если указан)
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    bool phoneExists = context.Сотрудники.Any(s => s.Телефон == phone);
                    if (phoneExists)
                    {
                        MessageBox.Show("Телефон уже используется другим сотрудником!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var employee = new Сотрудники
                {
                    Фамилия = TxtLastName.Text?.Trim(),
                    Имя = TxtFirstName.Text?.Trim(),
                    Отчество = TxtMiddleName.Text?.Trim(),
                    Телефон = phone,
                    Код_должности = (int)CmbPosition.SelectedValue,
                    Логин = login,
                    Пароль = PassBox.Password
                };

                context.Сотрудники.Add(employee);
                context.SaveChanges();

                MessageBox.Show("Сотрудник успешно добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при добавлении сотрудника: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtEmployeeId.Text))
                {
                    MessageBox.Show("Выберите сотрудника для обновления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // При обновлении пароль может оставаться пустым (не изменяется)
                bool skipPasswordValidation = string.IsNullOrEmpty(PassBox.Password);

                if (!ValidateEmployee(out string errorMessage, skipPasswordValidation))
                {
                    MessageBox.Show(errorMessage, "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int employeeId = int.Parse(TxtEmployeeId.Text);
                var employee = context.Сотрудники.Find(employeeId);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                string login = TxtLogin.Text?.Trim();
                string phone = TxtPhone.Text?.Trim();

                // Проверка уникальности логина (исключая текущего сотрудника)
                bool loginExists = context.Сотрудники.Any(s => s.Логин == login && s.Код_сотрудника != employeeId);
                if (loginExists)
                {
                    MessageBox.Show("Логин уже используется другим сотрудником!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Проверка уникальности телефона (если указан, исключая текущего сотрудника)
                if (!string.IsNullOrWhiteSpace(phone))
                {
                    bool phoneExists = context.Сотрудники.Any(s => s.Телефон == phone && s.Код_сотрудника != employeeId);
                    if (phoneExists)
                    {
                        MessageBox.Show("Телефон уже используется другим сотрудником!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                employee.Фамилия = TxtLastName.Text?.Trim();
                employee.Имя = TxtFirstName.Text?.Trim();
                employee.Отчество = TxtMiddleName.Text?.Trim();
                employee.Телефон = phone;
                employee.Код_должности = (int)CmbPosition.SelectedValue;
                employee.Логин = login;

                // Обновляем пароль только если он был введен
                if (!string.IsNullOrEmpty(PassBox.Password))
                {
                    employee.Пароль = PassBox.Password;
                }

                context.SaveChanges();

                MessageBox.Show("Сотрудник успешно обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
                ClearForm();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении сотрудника: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtEmployeeId.Text))
                {
                    MessageBox.Show("Выберите сотрудника для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int employeeId = int.Parse(TxtEmployeeId.Text);
                var employee = context.Сотрудники.Find(employeeId);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (currentUser.Логин == employee.Логин)
                {
                    MessageBox.Show("Вы не можете удалить свою собственную учетную запись!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите удалить сотрудника '{employee.Фамилия} {employee.Имя}'?",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Сотрудники.Remove(employee);
                    context.SaveChanges();
                    MessageBox.Show("Сотрудник успешно удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении сотрудника: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e)
        {
            ClearForm();
        }

        private void ClearForm()
        {
            TxtEmployeeId.Text = "";
            TxtLastName.Text = "";
            TxtFirstName.Text = "";
            TxtMiddleName.Text = "";
            TxtPhone.Text = "";
            CmbPosition.SelectedIndex = -1;
            TxtLogin.Text = "";
            PassBox.Password = "";
            DataGridEmployees.SelectedItem = null;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
        }
    }
}