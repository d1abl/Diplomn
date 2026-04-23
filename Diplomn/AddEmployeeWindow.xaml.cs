using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Runtime.Remoting.Contexts;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Diplomn
{

    public partial class AddEmployeeWindow : Window
    {
        // Контекст базы данных
        private BDEntities context;

        // Текущий сотрудник (редактируемый или новый)
        private Сотрудники currentEmployee;

        /// <summary>
        /// Конструктор окна
        /// </summary>
        /// <param name="context">Контекст БД</param>
        /// <param name="employee">Сотрудник для редактирования</param>
        public AddEmployeeWindow(BDEntities context, Сотрудники employee)
        {
            InitializeComponent();

            // Сохраняем переданные параметры
            this.context = context;
            this.currentEmployee = employee;

            // Устанавливаем DataContext для окна (привязка к сотруднику)
            this.DataContext = currentEmployee;

            // Загружаем список должностей в ComboBox
            LoadPositions();

            PassBox.Password = currentEmployee.Пароль;
        }


        /// <summary>
        /// Загрузка списка должностей в ComboBox
        /// </summary>
        private void LoadPositions()
        {
            try
            {
                // Загружаем список должностей из базы данных
                var positions = context.Должность.ToList();

                // Устанавливаем ItemsSource для ComboBox
                CmbPosition.ItemsSource = positions;

                // Если у сотрудника есть должность, выбираем её
                if (currentEmployee.Код_должности > 0)
                {
                    CmbPosition.SelectedValue = currentEmployee.Код_должности;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки должностей: {ex.Message}",
                                "Ошибка",
                                MessageBoxButton.OK,
                                MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Обработчик изменения пароля
        /// </summary>
        private void PassBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            // Обновляем пароль в объекте сотрудника
            if (currentEmployee != null)
            {
                currentEmployee.Пароль = PassBox.Password;
            }
        }

        /// <summary>
        /// Метод для проверки заполнения всех обязательных полей
        /// </summary>
        /// <returns>true - если все поля заполнены, иначе false</returns>
        private bool CheckRegistration()
        {
            // Получаем текущего сотрудника из контекста данных
            var employee = this.DataContext as Сотрудники;

            // Переменная для накопления сообщений об ошибках
            var errorMessage = new StringBuilder();

            if (employee == null)
            {
                MessageBox.Show("Ошибка: данные сотрудника отсутствуют.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            // Проверка фамилии
            if (string.IsNullOrWhiteSpace(employee.Фамилия))
            {
                errorMessage.AppendLine("❌ Фамилия не введена!");
            }
            else
            {
                // Допустимые символы: буквы и дефис
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(employee.Фамилия))
                {
                    errorMessage.AppendLine("❌ Фамилия содержит недопустимые символы!");
                }
                else
                {
                    // Общая длина не более 30
                    if (employee.Фамилия.Length > 30)
                        errorMessage.AppendLine("❌ Фамилия должна быть не длиннее 30 символов!");

                    // Не менее 2 букв (исключая дефисы)
                    var lettersOnly = Regex.Replace(employee.Фамилия, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errorMessage.AppendLine("❌ Фамилия должна содержать минимум 2 буквы!");

                    // Наличие хотя бы одной гласной
                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errorMessage.AppendLine("❌ Фамилия должна содержать хотя бы одну гласную!");
                }
            }

            // Проверка имени
            if (string.IsNullOrWhiteSpace(employee.Имя))
            {
                errorMessage.AppendLine("❌ Имя не введено!");
            }
            else
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(employee.Имя))
                {
                    errorMessage.AppendLine("❌ Имя содержит недопустимые символы!");
                }
                else
                {
                    if (employee.Имя.Length > 30)
                        errorMessage.AppendLine("❌ Имя должно быть не длиннее 30 символов!");

                    var lettersOnly = Regex.Replace(employee.Имя, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errorMessage.AppendLine("❌ Имя должно содержать минимум 2 буквы!");

                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errorMessage.AppendLine("❌ Имя должно содержать хотя бы одну гласную!");
                }
            }

            // Проверка отчества (необязательно, но при вводе — валидируется)
            if (!string.IsNullOrWhiteSpace(employee.Отчество))
            {
                var allowedRegex = new Regex(@"^[A-Za-zА-Яа-яЁё-]+$");
                if (!allowedRegex.IsMatch(employee.Отчество))
                {
                    errorMessage.AppendLine("❌ Отчество содержит недопустимые символы!");
                }
                else
                {
                    if (employee.Отчество.Length > 30)
                        errorMessage.AppendLine("❌ Отчество должно быть не длиннее 30 символов!");

                    var lettersOnly = Regex.Replace(employee.Отчество, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errorMessage.AppendLine("❌ Отчество должно содержать минимум 2 буквы!");

                    var vowelRegex = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                    if (!vowelRegex.IsMatch(lettersOnly))
                        errorMessage.AppendLine("❌ Отчество должно содержать хотя бы одну гласную!");
                }
            }

            // Проверка должности
            if (CmbPosition.SelectedValue == null)
            {
                errorMessage.AppendLine("❌ Должность не выбрана!");
            }

            // Проверка логина
            if (string.IsNullOrWhiteSpace(employee.Логин))
            {
                errorMessage.AppendLine("❌ Логин не введен!");
            }
            else
            {
                // Проверка уникальности логина (исключая текущего сотрудника при редактировании)
                var login = employee.Логин.Trim();
                bool exists = false;
                try
                {
                    exists = context.Сотрудники.Any(s => s.Логин == login && s.Код_сотрудника != employee.Код_сотрудника);
                }
                catch
                {
                    // если по какой-то причине доступ к контексту невозможен — пропускаем эту проверку
                }

                if (exists)
                    errorMessage.AppendLine("❌ Логин уже используется другим сотрудником!");
            }

            // Проверка пароля: минимум 12 символов, есть верхний и нижний регистр, цифра и специальный символ
            if (string.IsNullOrWhiteSpace(employee.Пароль))
            {
                errorMessage.AppendLine("❌ Пароль не введен!");
            }
            else
            {
                var password = employee.Пароль;
                if (password.Length < 12)
                    errorMessage.AppendLine("❌ Пароль должен быть не менее 12 символов!");

                // Верхний регистр (лат/рус)
                if (!Regex.IsMatch(password, "[A-ZА-ЯЁ]") )
                    errorMessage.AppendLine("❌ Пароль должен содержать хотя бы одну заглавную букву!");

                // Нижний регистр (лат/рус)
                if (!Regex.IsMatch(password, "[a-zа-яё]") )
                    errorMessage.AppendLine("❌ Пароль должен содержать хотя бы одну строчную букву!");

                // Цифра
                if (!Regex.IsMatch(password, "\\d"))
                    errorMessage.AppendLine("❌ Пароль должен содержать хотя бы одну цифру!");

                // Специальный символ (не буква и не цифра)
                if (!Regex.IsMatch(password, "[^A-Za-zА-Яа-яЁё0-9]"))
                    errorMessage.AppendLine("❌ Пароль должен содержать хотя бы один специальный символ (например !@#$%).");
            }

            // Проверка телефона (если введён) — ожидаем ровно 11 цифр, допустим '+' в начале
            if (!string.IsNullOrWhiteSpace(employee.Телефон))
            {
                var phoneRegex = new Regex(@"^\+?\d{11}$");
                if (!phoneRegex.IsMatch(employee.Телефон.Trim()))
                {
                    errorMessage.AppendLine("❌ Неверный формат телефона. Ожидается 11 цифр, можно с '+' в начале.");
                }
            }

            // Если ошибок нет, возвращаем true
            if (errorMessage.Length == 0)
            {
                return true;
            }
            else
            {
                MessageBox.Show(errorMessage.ToString(), "Ошибка валидации",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        /// <summary>
        /// Обработчик кнопки "Сохранить"
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Проверяем заполнение всех полей
                if (!CheckRegistration())
                {
                    return;
                }

                // Сохраняем изменения в базе данных
                context.SaveChanges();

                // Закрываем окно
                this.DialogResult = true;
                this.Close();

                MessageBox.Show("Данные успешно сохранены!", "Успех",
                                MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }            
        }

        /// <summary>
        /// Обработчик кнопки "Отмена"
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            // Отменяем все изменения
            context.RejectChanges();

            // Закрываем окно
            this.DialogResult = false;
            this.Close();
        }
    }

    /// <summary>
    /// Метод расширения для отмены изменений в контексте
    /// </summary>
    public static class DbContextExtensions
    {
        public static void RejectChanges(this DbContext context)
        {
            foreach (var entry in context.ChangeTracker.Entries())
            {
                switch (entry.State)
                {
                    case EntityState.Modified:
                    case EntityState.Deleted:
                        entry.State = EntityState.Unchanged;
                        break;
                    case EntityState.Added:
                        entry.State = EntityState.Detached;
                        break;
                }
            }
        }
    }
}
