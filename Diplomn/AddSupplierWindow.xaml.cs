using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;

namespace Diplomn
{
    public partial class AddSupplierWindow : Window
    {
        private BDEntities context;
        private Поставщики currentSupplier;

        public AddSupplierWindow(BDEntities context, Поставщики supplier = null)
        {
            InitializeComponent();
            this.context = context;
            currentSupplier = supplier ?? new Поставщики();
            this.DataContext = currentSupplier;

            if (supplier != null)
            {
                // bind UI fields if present
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var errors = new StringBuilder();

                // Mandatory fields
                if (string.IsNullOrWhiteSpace(currentSupplier.Наименование_поставщика))
                    errors.AppendLine("❌ Поле 'Наименование поставщика' обязательно для заполнения.");

                if (string.IsNullOrWhiteSpace(currentSupplier.ИНН))
                    errors.AppendLine("❌ Поле 'ИНН' обязательно для заполнения.");

                if (string.IsNullOrWhiteSpace(currentSupplier.Фамилия_контактного_лица))
                    errors.AppendLine("❌ Поле 'Фамилия контактного лица' обязательно для заполнения.");

                if (string.IsNullOrWhiteSpace(currentSupplier.Имя_контактного_лица))
                    errors.AppendLine("❌ Поле 'Имя контактного лица' обязательно для заполнения.");

                if (string.IsNullOrWhiteSpace(currentSupplier.Адрес_поставщика))
                    errors.AppendLine("❌ Поле 'Адрес поставщика' обязательно для заполнения.");

                if (string.IsNullOrWhiteSpace(currentSupplier.Email_поставщика))
                    errors.AppendLine("❌ Поле 'Email поставщика' обязательно для заполнения.");

                var vowel = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
                var consonant = new Regex(@"[B-DF-HJ-NP-TV-Zb-df-hj-np-tv-zБ-ЖЗЙ-НП-РСТ-Яб-жзй-нп-рст-я]");

                // Наименование
                if (!string.IsNullOrWhiteSpace(currentSupplier.Наименование_поставщика))
                {
                    var name = currentSupplier.Наименование_поставщика.Trim();
                    var lettersOnly = Regex.Replace(name, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (lettersOnly.Length < 2)
                        errors.AppendLine("❌ Наименование должно содержать минимум 2 буквы.");
                    else if (!vowel.IsMatch(lettersOnly) || !consonant.IsMatch(lettersOnly))
                        errors.AppendLine("❌ Наименование должно содержать хотя бы одну гласную и одну согласную.");
                }

                // INN - only digits
                if (!string.IsNullOrWhiteSpace(currentSupplier.ИНН))
                {
                    if (!Regex.IsMatch(currentSupplier.ИНН.Trim(), "^\\d+$"))
                        errors.AppendLine("❌ ИНН должен содержать только цифры.");
                }

                // Email basic check
                if (!string.IsNullOrWhiteSpace(currentSupplier.Email_поставщика))
                {
                    var email = currentSupplier.Email_поставщика.Trim();
                    var emailRegex = new Regex(@"^[A-Za-z0-9]+@[A-Za-z0-9]+\.[A-Za-z0-9]+$");
                    if (!emailRegex.IsMatch(email))
                        errors.AppendLine("❌ Неверный формат Email. Ожидается формат name@domain.tld");
                }

                // Contact person FIO validation (mandatory fields)
                if (!string.IsNullOrWhiteSpace(currentSupplier.Фамилия_контактного_лица))
                {
                    var fam = currentSupplier.Фамилия_контактного_лица.Trim();
                    var famLetters = Regex.Replace(fam, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (famLetters.Length < 2 || !vowel.IsMatch(famLetters) || !consonant.IsMatch(famLetters))
                        errors.AppendLine("❌ Фамилия контактного лица должна содержать минимум 2 буквы, одну гласную и одну согласную.");
                }

                if (!string.IsNullOrWhiteSpace(currentSupplier.Имя_контактного_лица))
                {
                    var im = currentSupplier.Имя_контактного_лица.Trim();
                    var imLetters = Regex.Replace(im, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (imLetters.Length < 2 || !vowel.IsMatch(imLetters) || !consonant.IsMatch(imLetters))
                        errors.AppendLine("❌ Имя контактного лица должно содержать минимум 2 буквы, одну гласную и одну согласную.");
                }

                if (!string.IsNullOrWhiteSpace(currentSupplier.Отчество_контактного_лица))
                {
                    var ot = currentSupplier.Отчество_контактного_лица.Trim();
                    var otLetters = Regex.Replace(ot, @"[^A-Za-zА-Яа-яЁё]", "");
                    if (otLetters.Length < 2 || !vowel.IsMatch(otLetters) || !consonant.IsMatch(otLetters))
                        errors.AppendLine("❌ Отчество контактного лица должно содержать минимум 2 буквы, одну гласную и одну согласную.");
                }

                // Phone: + and 11 digits (if provided)
                if (!string.IsNullOrWhiteSpace(currentSupplier.Телефон_контактного_лица))
                {
                    var phone = currentSupplier.Телефон_контактного_лица.Trim();
                    if (!Regex.IsMatch(phone, @"^\+?\d{11}$"))
                        errors.AppendLine("❌ Телефон контактного лица должен содержать 11 цифр, можно с '+' в начале.");
                }

                if (errors.Length > 0)
                {
                    MessageBox.Show(errors.ToString(), "Ошибка валидации", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (currentSupplier.Код_поставщика == 0)
                    context.Поставщики.Add(currentSupplier);

                context.SaveChanges();
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}