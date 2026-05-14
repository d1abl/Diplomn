using System;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Diplomn.Pages
{
    public partial class MainMenu : Page
    {
        private Сотрудники currentUser;
        private BDEntities context;

        public MainMenu(Сотрудники user)
        {
            InitializeComponent();
            currentUser = user;
            context = new BDEntities();
            LoadUserInfo();
            LoadStatistics();
        }

        private void LoadUserInfo()
        {
            if (currentUser == null) return;

            // Приветствие
            TxtWelcome.Text = $"Добро пожаловать, {currentUser.Фамилия} {currentUser.Имя}!";

            // Должность
            TxtPosition.Text = currentUser.Должность?.Название ?? "Не назначена";

            // Уровень доступа
            var level = currentUser.Должность?.Уровень_доступа;
            if (level.HasValue)
            {
                string levelText;
                if (level <= 3)
                    levelText = $"👑 Администратор (уровень {level})";
                else if (level <= 6)
                    levelText = $"⭐ Менеджер (уровень {level})";
                else
                    levelText = $"🔒 Сотрудник (уровень {level})";
                TxtAccessLevel.Text = levelText;
            }
            else
            {
                TxtAccessLevel.Text = "Не определён";
            }

            // Дата входа
            TxtLoginDate.Text = DateTime.Now.ToString("dd MMMM yyyy, HH:mm");

            // Аватар
            LoadAvatar();
        }

        private void LoadAvatar()
        {
            try
            {
                if (currentUser?.Аватарка != null && currentUser.Аватарка.Length > 0)
                {
                    using (var ms = new MemoryStream(currentUser.Аватарка))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = ms;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        UserAvatar.Source = bitmap;
                    }
                }
            }
            catch
            {
                // Оставляем изображение по умолчанию
            }
        }

        private void LoadStatistics()
        {
            try
            {
                // Количество товаров
                var productsCount = context.Товары.Count();
                TxtProductsCount.Text = productsCount.ToString();

                // Количество сотрудников
                var employeesCount = context.Сотрудники.Count();
                TxtEmployeesCount.Text = employeesCount.ToString();

                // Общая выручка
                var totalSales = context.Состав_продажи
                    .Sum(s => (decimal?)s.Количество * s.Цена) ?? 0;
                TxtTotalSales.Text = $"{totalSales:N0} ₽";

                // Количество поставщиков
                var suppliersCount = context.Поставщики.Count();
                TxtSuppliersCount.Text = suppliersCount.ToString();
            }
            catch (Exception)
            {
                TxtProductsCount.Text = "—";
                TxtEmployeesCount.Text = "—";
                TxtTotalSales.Text = "—";
                TxtSuppliersCount.Text = "—";
            }
        }
    }
}