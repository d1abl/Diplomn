using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn.Pages
{
    public partial class ReportPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public ReportPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadCategories();
        }

        private void LoadCategories()
        {
            var categories = context.Категории.ToList();
            categories.Insert(0, new Категории { Код_категория = 0, Категория = "Все категории" });
            CmbCategory.ItemsSource = categories;
            CmbCategory.SelectedIndex = 0;
        }

        private void GenerateSalesReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime from = DateFromSales.SelectedDate ?? DateTime.Now.AddMonths(-1);
                DateTime to = DateToSales.SelectedDate ?? DateTime.Now;

                if (from > to)
                {
                    MessageBox.Show("Неверный диапазон дат: дата начала позже даты окончания.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sales = context.Продажи
                    .Include("Сотрудники")
                    .Where(s => s.Дата_продажи >= from && s.Дата_продажи <= to)
                    .ToList();

                var reportData = sales.Select(s => new
                {
                    s.Код_чека,
                    Дата = s.Дата_продажи.ToString("dd.MM.yyyy HH:mm"),
                    Сотрудник = s.Сотрудники != null ? $"{s.Сотрудники.Фамилия} {s.Сотрудники.Имя}" : "",
                    Сумма = context.Состав_продажи
                        .Where(c => c.Код_чека == s.Код_чека)
                        .Sum(c => (c.Количество) * (c.Цена))
                }).ToList();

                TxtReportTitle.Text = $"📈 Отчет по продажам за период: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}";
                DataGridReport.ItemsSource = reportData;

                decimal totalSum = reportData.Sum(r => (decimal?)r.Сумма) ?? 0;
                TxtReportSummary.Text = $"Общая сумма продаж: {totalSum:N2} ₽ | Всего продаж: {reportData.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateSuppliesReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DateTime from = DateFromSupplies.SelectedDate ?? DateTime.Now.AddMonths(-1);
                DateTime to = DateToSupplies.SelectedDate ?? DateTime.Now;

                if (from > to)
                {
                    MessageBox.Show("Неверный диапазон дат: дата начала позже даты окончания.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var Supplies = context.Поставка
                    .Include("Сотрудники")
                    .Where(o => o.Дата_оформления_постивки >= from && o.Дата_оформления_постивки <= to)
                    .ToList();

                var reportData = Supplies.Select(o => new
                {
                    o.Код_поставки,
                    Дата = o.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm"),
                    Сотрудник = o.Сотрудники != null ? $"{o.Сотрудники.Фамилия} {o.Сотрудники.Имя}" : "",
                    Сумма = context.Состав_поставки
                        .Where(c => c.Код_поставки == o.Код_поставки)
                        .Sum(c => c.Количество * c.Цена_за_ед_покупка)
                }).ToList();

                TxtReportTitle.Text = $"📦 Отчет по заказам за период: {from:dd.MM.yyyy} - {to:dd.MM.yyyy}";
                DataGridReport.ItemsSource = reportData;

                decimal totalSum = reportData.Sum(r => (decimal?)r.Сумма) ?? 0;
                TxtReportSummary.Text = $"Общая сумма заказов: {totalSum:N2} ₽ | Всего заказов: {reportData.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GenerateProductsReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var category = CmbCategory.SelectedItem as Категории;

                var products = context.Товары
                    .Include("Категории")
                    .AsQueryable();

                if (category != null && category.Код_категория > 0)
                {
                    products = products.Where(p => p.Код_категория == category.Код_категория);
                }

                var reportData = products.ToList().Select(p => new
                {
                    p.Наименование,
                    Категория = p.Категории?.Категория,
                    Цена_продажи = p.Цена_за_ед_продажа,
                    На_складе = p.Количество,
                    Продано = context.Состав_продажи
                        .Where(s => s.Код_товара == p.Код_товара)
                        .Select(s => (int?)s.Количество)
                        .Sum() ?? 0,
                    Заказано = context.Состав_поставки
                        .Where(o => o.Код_товара == p.Код_товара)
                        .Select(o => (int?)o.Количество)
                        .Sum() ?? 0,
                    Сумма_продаж = context.Состав_продажи
                        .Where(s => s.Код_товара == p.Код_товара)
                        .Select(s => (decimal?)((s.Количество) * (s.Цена)))
                        .Sum() ?? 0m
                }).ToList();

                TxtReportTitle.Text = $"📋 Отчет по товарам {(category != null && category.Код_категория > 0 ? $"- категория: {category.Категория}" : "- все категории")}";
                DataGridReport.ItemsSource = reportData;

                int totalProducts = reportData.Count;
                int totalSold = reportData.Sum(r => r.Продано);
                decimal totalSales = reportData.Sum(r => r.Сумма_продаж);
                TxtReportSummary.Text = $"Всего товаров: {totalProducts} | Всего продано: {totalSold} | Сумма продаж: {totalSales:N2} ₽";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при формировании отчета: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}