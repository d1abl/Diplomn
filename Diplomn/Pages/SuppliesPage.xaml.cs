using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Diplomn.Pages
{
    public partial class SuppliesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public class EmployeeCheckItem : INotifyPropertyChanged
        {
            private bool _isSelected;
            public int Id { get; set; }
            public string DisplayName { get; set; }
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    _isSelected = value;
                    OnPropertyChanged();
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged([CallerMemberName] string name = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            }
        }

        public class SuppliesItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
        }

        public SuppliesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadLookups();
            LoadData();
        }

        private void LoadLookups()
        {
            // Получаем данные из БД без форматирования
            var employees = context.Сотрудники.ToList();

            // Форматируем в памяти
            var employeeCheckItems = employees.Select(e => new EmployeeCheckItem
            {
                Id = e.Код_сотрудника,
                DisplayName = e.Фамилия + " " + e.Имя,  // Простая конкатенация вместо string.Format
                IsSelected = false
            }).ToList();

            EmployeesCheckList.ItemsSource = employeeCheckItems;
        }

        private List<int> GetSelectedEmployeeIds()
        {
            if (EmployeesCheckList.ItemsSource is IList<EmployeeCheckItem> items)
            {
                var selectedIds = items.Where(e => e.IsSelected).Select(e => e.Id).ToList();
                // Если выбраны все сотрудники или ни одного - возвращаем пустой список (без фильтра)
                if (selectedIds.Count == 0 || selectedIds.Count == items.Count)
                    return new List<int>();
                return selectedIds;
            }
            return new List<int>();
        }

        private void SelectAllEmployees_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeesCheckList.ItemsSource is IList<EmployeeCheckItem> items)
            {
                foreach (var item in items)
                    item.IsSelected = true;
            }
        }

        private void DeselectAllEmployees_Click(object sender, RoutedEventArgs e)
        {
            if (EmployeesCheckList.ItemsSource is IList<EmployeeCheckItem> items)
            {
                foreach (var item in items)
                    item.IsSelected = false;
            }
        }

        private void TxtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                ApplyFilters();
        }

        private IQueryable<Поставка> GetFilteredQuery()
        {
            var query = context.Поставка.AsQueryable();

            // Поиск по коду поставки
            if (!string.IsNullOrWhiteSpace(TxtSearch.Text) && int.TryParse(TxtSearch.Text, out int supplyId))
                query = query.Where(s => s.Код_поставки == supplyId);

            // Фильтр по дате
            if (DateFrom.SelectedDate.HasValue)
            {
                var fromDate = DateFrom.SelectedDate.Value.Date;
                query = query.Where(s => s.Дата_оформления_постивки >= fromDate);
            }
            if (DateTo.SelectedDate.HasValue)
            {
                var toDate = DateTo.SelectedDate.Value.Date.AddDays(1);
                query = query.Where(s => s.Дата_оформления_постивки < toDate);
            }

            // Фильтр по сотрудникам
            var selectedEmployeeIds = GetSelectedEmployeeIds();
            if (selectedEmployeeIds.Any())
                query = query.Where(s => selectedEmployeeIds.Contains(s.Код_сотрудника));

            return query;
        }

        private void LoadData()
        {
            var supplies = context.Поставка
                .Include(s => s.Сотрудники)
                .OrderByDescending(o => o.Дата_оформления_постивки)
                .ToList();

            DataGridSupplies.ItemsSource = supplies;
            UpdateTotalSum(supplies);
        }

        private void ApplyFilters()
        {
            try
            {
                var result = GetFilteredQuery()
                    .Include(s => s.Сотрудники)
                    .OrderByDescending(s => s.Дата_оформления_постивки)
                    .ToList();

                DataGridSupplies.ItemsSource = result;
                UpdateTotalSum(result);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при применении фильтров: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTotalSum(List<Поставка> supplies)
        {
            decimal totalSum = 0;
            foreach (var supply in supplies)
            {
                totalSum += GetSupplySum(supply.Код_поставки);
            }
            TxtTotalSupplies.Text = $"{totalSum:N2} ₽";
        }

        private decimal GetSupplySum(int supplyId)
        {
            return context.Состав_поставки
                .Where(c => c.Код_поставки == supplyId)
                .Sum(c => (decimal?)c.Количество * c.Цена_за_ед_покупка) ?? 0;
        }

        private void ApplyFilters_Click(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFilters_Click(object sender, RoutedEventArgs e)
        {
            TxtSearch.Text = "";
            DateFrom.SelectedDate = null;
            DateTo.SelectedDate = null;
            DeselectAllEmployees_Click(null, null);
            LoadData();
        }

        private void SaveReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supplies = GetFilteredQuery()
                    .Include(s => s.Сотрудники)
                    .OrderByDescending(s => s.Дата_оформления_постивки)
                    .ToList();

                if (!supplies.Any())
                {
                    MessageBox.Show("Нет данных для сохранения отчета.", "Информация",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "CSV файл (*.csv)|*.csv|Текстовый файл (*.txt)|*.txt",
                    Title = "Сохранить отчет о поставках",
                    FileName = $"Отчет_поставки_{DateTime.Now:yyyy-MM-dd_HH-mm}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine($"Отчет о поставках от {DateTime.Now:dd.MM.yyyy HH:mm}");
                    sb.AppendLine($"Сформировал: {currentUser.Фамилия} {currentUser.Имя}");

                    // Информация о фильтрах
                    var filters = new List<string>();
                    if (DateFrom.SelectedDate.HasValue)
                        filters.Add($"Дата с: {DateFrom.SelectedDate:dd.MM.yyyy}");
                    if (DateTo.SelectedDate.HasValue)
                        filters.Add($"Дата по: {DateTo.SelectedDate:dd.MM.yyyy}");

                    var selectedEmployeeIds = GetSelectedEmployeeIds();
                    if (selectedEmployeeIds.Any())
                    {
                        // Получаем имена выбранных сотрудников в памяти
                        var selectedEmployees = context.Сотрудники
                            .Where(emp => selectedEmployeeIds.Contains(emp.Код_сотрудника))
                            .ToList()
                            .Select(emp => emp.Фамилия + " " + emp.Имя)
                            .ToList();
                        filters.Add($"Сотрудники: {string.Join(", ", selectedEmployees)}");
                    }

                    if (filters.Any())
                    {
                        sb.AppendLine("Примененные фильтры:");
                        filters.ForEach(f => sb.AppendLine($"  • {f}"));
                    }

                    sb.AppendLine();
                    sb.AppendLine($"Всего поставок: {supplies.Count}");

                    // Группировка по сотрудникам
                    var groupedByEmployee = supplies
                        .GroupBy(s => new { s.Код_сотрудника, Фамилия = s.Сотрудники?.Фамилия, Имя = s.Сотрудники?.Имя })
                        .OrderBy(g => g.Key.Фамилия)
                        .ToList();

                    if (groupedByEmployee.Count > 1 || selectedEmployeeIds.Count > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine("=== Группировка по сотрудникам ===");
                        foreach (var group in groupedByEmployee)
                        {
                            var empName = group.Key.Фамилия + " " + group.Key.Имя;
                            var empSum = group.Sum(s => GetSupplySum(s.Код_поставки));
                            sb.AppendLine($"  {empName}: {group.Count()} поставок на сумму {empSum:N2} ₽");
                        }
                        sb.AppendLine();
                    }

                    sb.AppendLine("Код поставки;Дата;Сотрудник;Поставщик;Сумма");

                    // Получаем всех поставщиков для поставок заранее
                    var supplyIds = supplies.Select(s => s.Код_поставки).ToList();
                    var suppliersDict = context.Состав_поставки
                        .Where(c => supplyIds.Contains(c.Код_поставки))
                        .Include("Поставщики")
                        .ToList()
                        .GroupBy(c => c.Код_поставки)
                        .ToDictionary(
                            g => g.Key,
                            g => g.FirstOrDefault()?.Поставщики?.Наименование_поставщика ?? "-"
                        );

                    decimal totalSum = 0;
                    foreach (var supply in supplies)
                    {
                        var sum = GetSupplySum(supply.Код_поставки);
                        totalSum += sum;

                        var employee = supply.Сотрудники != null
                            ? supply.Сотрудники.Фамилия + " " + supply.Сотрудники.Имя
                            : "";

                        var supplier = suppliersDict.ContainsKey(supply.Код_поставки)
                            ? suppliersDict[supply.Код_поставки]
                            : "-";

                        sb.AppendLine($"{supply.Код_поставки};{supply.Дата_оформления_постивки:dd.MM.yyyy HH:mm};{employee};{supplier};{sum:N2}");
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

        private void DataGridSupplies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridSupplies.SelectedItem is Поставка supply)
            {
                TxtSupplyId.Text = supply.Код_поставки.ToString();
                TxtSupplyDate.Text = supply.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = supply.Сотрудники != null
                    ? supply.Сотрудники.Фамилия + " " + supply.Сотрудники.Имя
                    : "";

                var supplier = context.Состав_поставки
                    .Include("Поставщики")
                    .Where(i => i.Код_поставки == supply.Код_поставки)
                    .Select(i => i.Поставщики.Наименование_поставщика)
                    .FirstOrDefault();
                TxtSupplier.Text = supplier ?? "Не указан";

                var items = context.Состав_поставки
                    .Include("Товары")
                    .Where(i => i.Код_поставки == supply.Код_поставки)
                    .ToList()
                    .Select(i => new SuppliesItemDisplay
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена_за_ед_покупка
                    })
                    .ToList();

                DataGridSupplyItems.ItemsSource = new ObservableCollection<SuppliesItemDisplay>(items);
                TxtTotal.Text = $"{items.Sum(i => i.Сумма):N2} ₽";
            }
            else
            {
                ClearDetails();
            }
        }

        private void ClearDetails()
        {
            TxtSupplyId.Text = "";
            TxtSupplyDate.Text = "";
            TxtEmployee.Text = "";
            TxtSupplier.Text = "";
            DataGridSupplyItems.ItemsSource = null;
            TxtTotal.Text = "";
        }

        private void NewSupply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new AddSupplyWindow(context, currentUser);
                window.ShowDialog();
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSupply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var supply = DataGridSupplies.SelectedItem as Поставка;
                if (supply == null)
                {
                    MessageBox.Show("Выберите поставку для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Удалить поставку №{supply.Код_поставки}?\nКоличество товаров будет уменьшено.",
                    "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    var items = context.Состав_поставки.Where(i => i.Код_поставки == supply.Код_поставки).ToList();
                    foreach (var item in items)
                    {
                        var product = context.Товары.Find(item.Код_товара);
                        if (product != null)
                        {
                            product.Количество -= item.Количество;
                            if (product.Количество < 0) product.Количество = 0;
                        }
                        context.Состав_поставки.Remove(item);
                    }

                    context.Поставка.Remove(supply);
                    context.SaveChanges();

                    MessageBox.Show("Поставка удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
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