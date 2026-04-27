using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Diplomn.Pages
{
    public partial class EmployeesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private byte[] selectedImageData;

        public EmployeesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Сотрудники — {user.Фамилия} {user.Имя}";
            LoadPositions();
            LoadData();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private void LoadPositions()
        {
            CmbPosition.ItemsSource = context.Должность.ToList();
        }

        private IQueryable<Сотрудники> GetFilteredQuery()
        {
            var query = context.Сотрудники.AsQueryable();

            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                query = query.Where(e => e.Фамилия.Contains(term) ||
                                        e.Имя.Contains(term) ||
                                        e.Логин.Contains(term));
            }

            return query;
        }

        private void LoadData()
        {
            DataGridEmployees.ItemsSource = context.Сотрудники
                .Include("Должность")
                .ToList();
        }

        private void ApplyFilters()
        {
            DataGridEmployees.ItemsSource = GetFilteredQuery()
                .Include("Должность")
                .ToList();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            LoadData();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var employees = GetFilteredQuery()
                    .Include("Должность")
                    .ToList();

                if (!employees.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV файл (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt",
                    Title = "Сохранить отчет о сотрудниках",
                    FileName = $"Отчет_сотрудники_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Отчет о сотрудниках от {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine($"Сформировал: {currentUser.Фамилия} {currentUser.Имя}");

                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                        sb.AppendLine($"Поиск: \"{TxtSearch.Text}\"");

                    sb.AppendLine();
                    sb.AppendLine($"Всего сотрудников: {employees.Count}");
                    sb.AppendLine();
                    sb.AppendLine("Код;Фамилия;Имя;Отчество;Телефон;Должность;Логин;Уровень доступа");

                    foreach (var employee in employees)
                    {
                        var position = employee.Должность?.Название ?? "-";
                        var accessLevel = employee.Должность?.Уровень_доступа.ToString() ?? "-";
                        sb.AppendLine($"{employee.Код_сотрудника};{employee.Фамилия};{employee.Имя};{employee.Отчество ?? "-"};{employee.Телефон ?? "-"};{position};{employee.Логин};{accessLevel}");
                    }

                    File.WriteAllText(saveFileDialog.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Отчет сохранен!\n{saveFileDialog.FileName}", "Успех",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                LoadEmployeePhoto(employee);
                selectedImageData = null;
            }
        }

        private void LoadEmployeePhoto(Сотрудники employee)
        {
            try
            {
                if (employee?.Аватарка != null && employee.Аватарка.Length > 0)
                {
                    using (var ms = new MemoryStream(employee.Аватарка))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        EmployeePhoto.Source = bitmap;
                    }
                }
                else
                {
                    EmployeePhoto.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
                }
            }
            catch
            {
                EmployeePhoto.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
            }
        }

        private void SelectPhoto_Click(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "Изображения (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp",
                    Title = "Выберите фотографию сотрудника"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    selectedImageData = File.ReadAllBytes(openFileDialog.FileName);
                    using (var ms = new MemoryStream(selectedImageData))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        EmployeePhoto.Source = bitmap;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при выборе фото: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
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
                errors.AppendLine("• Фамилия не введена");
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(lastName))
                    errors.AppendLine("• Фамилия содержит недопустимые символы");
                else if (lastName.Length > 30)
                    errors.AppendLine("• Фамилия должна быть не длиннее 30 символов");
            }

            // Имя
            if (string.IsNullOrWhiteSpace(firstName))
                errors.AppendLine("• Имя не введено");
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(firstName))
                    errors.AppendLine("• Имя содержит недопустимые символы");
                else if (firstName.Length > 30)
                    errors.AppendLine("• Имя должно быть не длиннее 30 символов");
            }

            // Должность
            if (CmbPosition.SelectedValue == null)
                errors.AppendLine("• Должность не выбрана");

            // Логин
            if (string.IsNullOrWhiteSpace(login))
                errors.AppendLine("• Логин не введен");

            // Пароль
            if (!skipPasswordValidation)
            {
                if (string.IsNullOrWhiteSpace(password))
                    errors.AppendLine("• Пароль не введен");
                else if (password.Length < 12)
                    errors.AppendLine("• Пароль должен быть не менее 12 символов");
            }

            // Телефон
            if (!string.IsNullOrWhiteSpace(phone))
            {
                var phoneRegex = new Regex(@"^\+?\d{11}$");
                if (!phoneRegex.IsMatch(phone))
                    errors.AppendLine("• Неверный формат телефона (11 цифр)");
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

                if (context.Сотрудники.Any(s => s.Логин == login))
                {
                    MessageBox.Show("Логин уже используется!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(phone) && context.Сотрудники.Any(s => s.Телефон == phone))
                {
                    MessageBox.Show("Телефон уже используется!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var employee = new Сотрудники
                {
                    Фамилия = TxtLastName.Text?.Trim(),
                    Имя = TxtFirstName.Text?.Trim(),
                    Отчество = TxtMiddleName.Text?.Trim(),
                    Телефон = phone,
                    Код_должности = (int)CmbPosition.SelectedValue,
                    Логин = login,
                    Пароль = PassBox.Password,
                    Аватарка = selectedImageData
                };

                context.Сотрудники.Add(employee);
                context.SaveChanges();

                MessageBox.Show("Сотрудник добавлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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
                if (string.IsNullOrWhiteSpace(TxtEmployeeId.Text))
                {
                    MessageBox.Show("Выберите сотрудника!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

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

                if (context.Сотрудники.Any(s => s.Логин == login && s.Код_сотрудника != employeeId))
                {
                    MessageBox.Show("Логин уже используется!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(phone) && context.Сотрудники.Any(s => s.Телефон == phone && s.Код_сотрудника != employeeId))
                {
                    MessageBox.Show("Телефон уже используется!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                employee.Фамилия = TxtLastName.Text?.Trim();
                employee.Имя = TxtFirstName.Text?.Trim();
                employee.Отчество = TxtMiddleName.Text?.Trim();
                employee.Телефон = phone;
                employee.Код_должности = (int)CmbPosition.SelectedValue;
                employee.Логин = login;

                if (!string.IsNullOrEmpty(PassBox.Password))
                    employee.Пароль = PassBox.Password;

                if (selectedImageData != null)
                    employee.Аватарка = selectedImageData;

                // Обновление currentUser если редактируется текущий пользователь
                if (currentUser.Код_сотрудника == employee.Код_сотрудника)
                {
                    currentUser.Фамилия = employee.Фамилия;
                    currentUser.Имя = employee.Имя;
                    currentUser.Отчество = employee.Отчество;
                    currentUser.Телефон = employee.Телефон;
                    if (!string.IsNullOrEmpty(PassBox.Password))
                        currentUser.Пароль = employee.Пароль;
                    if (selectedImageData != null)
                        currentUser.Аватарка = selectedImageData;

                    var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                    mainWindow?.SetCurrentUser(currentUser);
                }

                context.SaveChanges();

                MessageBox.Show("Сотрудник обновлен!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadData();
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
                if (string.IsNullOrWhiteSpace(TxtEmployeeId.Text))
                {
                    MessageBox.Show("Выберите сотрудника!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int employeeId = int.Parse(TxtEmployeeId.Text);
                var employee = context.Сотрудники.Find(employeeId);

                if (employee == null)
                {
                    MessageBox.Show("Сотрудник не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (currentUser.Код_сотрудника == employee.Код_сотрудника)
                {
                    MessageBox.Show("Нельзя удалить свою учетную запись!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить сотрудника '{employee.Фамилия} {employee.Имя}'?",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    context.Сотрудники.Remove(employee);
                    context.SaveChanges();
                    MessageBox.Show("Сотрудник удален!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearForm();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClearForm_Click(object sender, RoutedEventArgs e) => ClearForm();

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
            EmployeePhoto.Source = new BitmapImage(new Uri("/Photos/istockavatar.png", UriKind.RelativeOrAbsolute));
            selectedImageData = null;
            DataGridEmployees.SelectedItem = null;
        }
    }
}