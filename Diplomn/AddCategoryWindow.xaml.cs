using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace Diplomn
{
    public partial class AddCategoryWindow : Window
    {
        private BDEntities context;
        private Категории currentCategory;

        public AddCategoryWindow(BDEntities context, Категории category = null)
        {
            InitializeComponent();
            this.context = context;
            this.currentCategory = category;

            if (currentCategory != null)
            {
                this.DataContext = currentCategory;
                TxtName.Text = currentCategory.Категория;
                TxtDescription.Text = currentCategory.Описание_категории;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var name = TxtName.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Введите название категории!");
                return;
            }

            if (name.Length < 2)
            {
                MessageBox.Show("Название категории должно содержать минимум 2 буквы!");
                return;
            }

            if (name.Length > 40)
            {
                MessageBox.Show("Название категории не должно превышать 40 символов!");
                return;
            }

            // Only letters and hyphen/spaces allowed
            var allowed = new Regex(@"^[A-Za-zА-Яа-яЁё\-\s]+$");
            if (!allowed.IsMatch(name))
            {
                MessageBox.Show("Название содержит недопустимые символы!");
                return;
            }

            // Remove non-letters for counting vowels/consonants
            var lettersOnly = Regex.Replace(name, @"[^A-Za-zА-Яа-яЁё]", "");
            if (lettersOnly.Length < 2)
            {
                MessageBox.Show("Название должно содержать минимум 2 букв!");
                return;
            }

            var vowel = new Regex(@"[AEIOUYaeiouyАЕЁИОУЫЭЮЯаеёиоуыэюя]");
            var consonant = new Regex(@"[B-DF-HJ-NP-TV-Zb-df-hj-np-tv-zБ-ЖЗЙ-НП-РСТ-Яб-жзй-нп-рст-я]");

            if (!vowel.IsMatch(lettersOnly))
            {
                MessageBox.Show("Название должно содержать хотя бы одну гласную!");
                return;
            }

            if (!consonant.IsMatch(lettersOnly))
            {
                MessageBox.Show("Название должно содержать хотя бы одну согласную!");
                return;
            }

            // Check uniqueness
            bool exists;
            if (currentCategory == null)
            {
                exists = context.Категории.Any(c => c.Категория == name);
            }
            else
            {
                var currentId = currentCategory.Код_категория;
                exists = context.Категории.Any(c => c.Категория == name && c.Код_категория != currentId);
            }
            if (exists)
            {
                MessageBox.Show("Категория с таким названием уже существует!");
                return;
            }

            if (currentCategory == null)
            {
                var category = new Категории
                {
                    Категория = name,
                    Описание_категории = TxtDescription.Text
                };
                context.Категории.Add(category);
            }
            else
            {
                currentCategory.Категория = name;
                currentCategory.Описание_категории = TxtDescription.Text;
            }

            context.SaveChanges();
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}