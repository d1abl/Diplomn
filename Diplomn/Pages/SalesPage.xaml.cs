using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
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

        public class SaleItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
        }

        public SalesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadData();
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private IQueryable<Продажи> GetFilteredQuery()
        {
            var query = context.Продажи.AsQueryable();

            // Поиск по коду чека
            if (!string.IsNullOrWhiteSpace(TxtSearch.Text) && int.TryParse(TxtSearch.Text, out int checkId))
                query = query.Where(s => s.Код_чека == checkId);

            // Фильтр по дате
            if (DateFrom.SelectedDate.HasValue)
                query = query.Where(s => s.Дата_продажи >= DateFrom.SelectedDate.Value);
            if (DateTo.SelectedDate.HasValue)
                query = query.Where(s => s.Дата_продажи <= DateTo.SelectedDate.Value.AddDays(1));

            return query;
        }

        private void LoadData()
        {
            DataGridSales.ItemsSource = context.Продажи
                .Include("Сотрудники")
                .OrderByDescending(s => s.Дата_продажи)
                .ToList();
        }

        private void ApplyFilters()
        {
            try
            {
                var result = GetFilteredQuery()
                    .Include("Сотрудники")
                    .OrderByDescending(s => s.Дата_продажи)
                    .ToList();

                DataGridSales.ItemsSource = result;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при применении фильтров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            LoadData();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sales = GetFilteredQuery()
                    .Include("Сотрудники")
                    .OrderByDescending(s => s.Дата_продажи)
                    .ToList();

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

                    if (DateFrom.SelectedDate.HasValue || DateTo.SelectedDate.HasValue)
                    {
                        sb.AppendLine($"Период: {DateFrom.SelectedDate:dd.MM.yyyy} - {DateTo.SelectedDate:dd.MM.yyyy}");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"Всего продаж: {sales.Count}");
                    sb.AppendLine();
                    sb.AppendLine("Код чека;Дата;Сотрудник;Сумма");

                    decimal totalSum = 0;
                    foreach (var sale in sales)
                    {
                        var sum = context.Состав_продажи
                            .Where(c => c.Код_чека == sale.Код_чека)
                            .Sum(c => c.Количество * c.Цена);
                        totalSum += sum;

                        var employee = sale.Сотрудники != null ? $"{sale.Сотрудники.Фамилия} {sale.Сотрудники.Имя}" : "";
                        sb.AppendLine($"{sale.Код_чека};{sale.Дата_продажи:dd.MM.yyyy HH:mm};{employee};{sum:N2}");
                    }

                    sb.AppendLine();
                    sb.AppendLine($"Общая сумма:;{totalSum:N2} ₽");

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

        private void DataGridSales_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridSales.SelectedItem is Продажи sale)
            {
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

                DataGridSaleItems.ItemsSource = new ObservableCollection<SaleItemDisplay>(items);
                TxtTotal.Text = $"{items.Sum(i => i.Сумма):N2} ₽";
            }
            else
            {
                ClearDetails();
            }
        }

        private void ClearDetails()
        {
            TxtSaleId.Text = "";
            TxtSaleDate.Text = "";
            TxtEmployee.Text = "";
            DataGridSaleItems.ItemsSource = null;
            TxtTotal.Text = "";
        }

        private void NewSale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new AddSaleWindow(context, currentUser);
                window.ShowDialog();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewSaleComposition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sale = DataGridSales.SelectedItem as Продажи;
                if (sale == null)
                {
                    MessageBox.Show("Выберите продажу!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var window = new SaleCompositionWindow(context, sale);
                window.ShowDialog();
                LoadData();
                DataGridSales_SelectionChanged(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSale_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var sale = DataGridSales.SelectedItem as Продажи;
                if (sale == null)
                {
                    MessageBox.Show("Выберите продажу для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить чек №{sale.Код_чека}?\nТовары будут возвращены на склад.",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var items = context.Состав_продажи.Where(i => i.Код_чека == sale.Код_чека).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null)
                            product.Количество += item.Количество;
                        context.Состав_продажи.Remove(item);
                    }

                    context.Продажи.Remove(sale);
                    context.SaveChanges();

                    MessageBox.Show("Продажа удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    ClearDetails();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}