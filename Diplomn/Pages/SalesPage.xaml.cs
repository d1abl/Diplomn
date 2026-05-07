using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplomn.Pages
{
    public partial class SalesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;
        private ObservableCollection<SaleViewModel> salesView;

        public class SaleItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
            public string PriceQuantityDisplay => $"{Цена:N2} ₽ × {Количество} шт.";
        }

        public SalesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            WelcomeText.Text = $"Продажи — {user.Фамилия} {user.Имя}";
            salesView = new ObservableCollection<SaleViewModel>();
            LoadData();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private IQueryable<Продажи> GetFilteredQuery()
        {
            var query = context.Продажи
                .Include("Сотрудники")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
            {
                var term = TxtSearch.Text.Trim();
                if (int.TryParse(term, out int receiptCode))
                {
                    query = query.Where(s => s.Код_чека == receiptCode);
                }
                else
                {
                    query = query.Where(s => s.Сотрудники.Фамилия.Contains(term) ||
                                            s.Сотрудники.Имя.Contains(term));
                }
            }

            if (DateFrom.SelectedDate.HasValue)
                query = query.Where(s => s.Дата_продажи >= DateFrom.SelectedDate.Value);

            if (DateTo.SelectedDate.HasValue)
                query = query.Where(s => s.Дата_продажи <= DateTo.SelectedDate.Value.AddDays(1));

            return query.OrderByDescending(s => s.Дата_продажи);
        }

        private void LoadData()
        {
            var sales = GetFilteredQuery().ToList();
            UpdateSalesView(sales);
        }

        private void UpdateSalesView(List<Продажи> sales)
        {
            salesView.Clear();
            foreach (var sale in sales)
            {
                var total = context.Состав_продажи
                    .Where(i => i.Код_чека == sale.Код_чека)
                    .Sum(i => (decimal?)i.Количество * i.Цена) ?? 0;
                salesView.Add(new SaleViewModel(sale, total));
            }
            ListViewSales.ItemsSource = salesView;
        }

        private void ApplyFilters()
        {
            LoadData();
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            LoadData();
            ListViewSaleItems.ItemsSource = null;
            TxtSaleId.Text = "";
            TxtSaleDate.Text = "";
            TxtEmployee.Text = "";
            TxtTotal.Text = "";
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sales = GetFilteredQuery().ToList();

                if (!sales.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV файл (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt",
                    Title = "Сохранить отчет о продажах",
                    FileName = $"Отчет_продажи_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Отчет о продажах от {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine($"Сформировал: {currentUser.Фамилия} {currentUser.Имя}");

                    if (!string.IsNullOrWhiteSpace(TxtSearch.Text))
                        sb.AppendLine($"Поиск: \"{TxtSearch.Text}\"");
                    if (DateFrom.SelectedDate.HasValue)
                        sb.AppendLine($"Дата от: {DateFrom.SelectedDate.Value:dd.MM.yyyy}");
                    if (DateTo.SelectedDate.HasValue)
                        sb.AppendLine($"Дата до: {DateTo.SelectedDate.Value:dd.MM.yyyy}");

                    sb.AppendLine();
                    sb.AppendLine($"Всего продаж: {sales.Count}");
                    sb.AppendLine();
                    sb.AppendLine("Код чека;Дата продажи;Сотрудник;Общая сумма");

                    decimal grandTotal = 0;
                    foreach (var sale in sales)
                    {
                        var total = context.Состав_продажи
                            .Where(i => i.Код_чека == sale.Код_чека)
                            .Sum(i => (decimal?)i.Количество * i.Цена) ?? 0;
                        grandTotal += total;
                        var employee = $"{sale.Сотрудники?.Фамилия} {sale.Сотрудники?.Имя}".Trim();
                        sb.AppendLine($"{sale.Код_чека};{sale.Дата_продажи:dd.MM.yyyy HH:mm};{employee};{total:N2}");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"Общая выручка: {grandTotal:N2} ₽");

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

        private void ListViewSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListViewSales.SelectedItem is SaleViewModel selectedSale)
            {
                var sale = selectedSale.OriginalSale;
                TxtSaleId.Text = sale.Код_чека.ToString();
                TxtSaleDate.Text = sale.Дата_продажи.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = sale.Сотрудники != null ? $"{sale.Сотрудники.Фамилия} {sale.Сотрудники.Имя}" : "";

                var items = context.Состав_продажи
                    .Include("Товары")
                    .Where(i => i.Код_чека == sale.Код_чека)
                    .Select(i => new SaleItemDisplay
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена
                    })
                    .ToList();

                ListViewSaleItems.ItemsSource = items;
                decimal total = items.Sum(i => i.Сумма);
                TxtTotal.Text = $"{total:N2} ₽";
            }
            else
            {
                TxtSaleId.Text = "";
                TxtSaleDate.Text = "";
                TxtEmployee.Text = "";
                ListViewSaleItems.ItemsSource = null;
                TxtTotal.Text = "";
            }
        }

        private void NewSale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new AddSaleWindow(context, currentUser);
                window.ShowDialog();
                LoadData();
                ListViewSales.SelectedItem = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при создании продажи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = ListViewSales.SelectedItem as SaleViewModel;
                if (selectedItem == null)
                {
                    MessageBox.Show("Выберите продажу для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var sale = selectedItem.OriginalSale;

                var result = MessageBox.Show($"Вы уверены, что хотите удалить чек №{sale.Код_чека}?\nВместе с чеком будет удален его состав!",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Восстанавливаем количество товаров на складе
                    var items = context.Состав_продажи.Where(i => i.Код_чека == sale.Код_чека).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null)
                        {
                            product.Количество += item.Количество;
                        }
                        context.Состав_продажи.Remove(item);
                    }

                    context.Продажи.Remove(sale);
                    context.SaveChanges();

                    MessageBox.Show("Продажа успешно удалена! Товары возвращены на склад.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ListViewSales.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении продажи: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            ListViewSales.SelectedItem = null;
        }
    }

    /// <summary>
    /// ViewModel для отображения продажи в карточке
    /// </summary>
    public class SaleViewModel
    {
        public Продажи OriginalSale { get; set; }
        public decimal Total { get; set; }

        public string ReceiptDisplay => $"Чек №{OriginalSale.Код_чека}";
        public string DateDisplay => OriginalSale.Дата_продажи.ToString("dd.MM.yyyy HH:mm");
        public string EmployeeDisplay => OriginalSale.Сотрудники != null ?
            $"👤 {OriginalSale.Сотрудники.Фамилия} {OriginalSale.Сотрудники.Имя}" : "👤 Не указан";
        public string TotalDisplay => $"💰 {Total:N2} ₽";

        public SaleViewModel(Продажи sale, decimal total)
        {
            OriginalSale = sale;
            Total = total;
        }
    }
}