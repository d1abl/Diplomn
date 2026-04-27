using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn.Pages
{
    public partial class SuppliesPage : Page
    {
        private BDEntities context;
        private Сотрудники currentUser;

        public class SuppliesItemDisplay
        {
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
            public string Поставщик { get; set; }
        }

        public SuppliesPage(Сотрудники user)
        {
            InitializeComponent();
            context = new BDEntities();
            currentUser = user;
            LoadData();
        }

        private void LoadData()
        {
            DataGridSupplies.ItemsSource = context.Поставка
                .Include("Сотрудники")
                .OrderByDescending(o => o.Дата_оформления_постивки)
                .ToList();
        }

        private void DataGridSupplies_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataGridSupplies.SelectedItem is Поставка Supply)
            {
                TxtSupplyId.Text = Supply.Код_поставки.ToString();
                TxtSupplyDate.Text = Supply.Дата_оформления_постивки.ToString("dd.MM.yyyy HH:mm");
                TxtEmployee.Text = Supply.Сотрудники != null ? $"{Supply.Сотрудники.Фамилия} {Supply.Сотрудники.Имя}" : "";

                var items = context.Состав_поставки
                    .Include("Товары")
                    .Include("Поставщики")
                    .Where(i => i.Код_поставки == Supply.Код_поставки)
                    .Select(i => new SuppliesItemDisplay
                    {
                        Товар = i.Товары.Наименование,
                        Количество = i.Количество,
                        Цена = i.Цена_за_ед_покупка,
                        Поставщик = i.Поставщики != null ? i.Поставщики.Наименование_поставщика : "Не указан"
                    })
                    .ToList();

                DataGridSupplyItems.ItemsSource = new ObservableCollection<SuppliesItemDisplay>(items);
                decimal total = items.Sum(i => i.Сумма);
                TxtTotal.Text = $"{total:N2} ₽";
            }
            else
            {
                TxtSupplyId.Text = "";
                TxtSupplyDate.Text = "";
                TxtEmployee.Text = "";
                DataGridSupplyItems.ItemsSource = null;
                TxtTotal.Text = "";
            }
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
                MessageBox.Show($"Ошибка при создании поставки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ViewSupplyComposition_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var Supply = DataGridSupplies.SelectedItem as Поставка;
                if (Supply == null)
                {
                    MessageBox.Show("Выберите поставку для просмотра состава!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var window = new OrderCompositionWindow(context, Supply);
                window.ShowDialog();
                LoadData();
                DataGridSupplies_SelectionChanged(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при открытии состава поставки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteSupply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var Supply = DataGridSupplies.SelectedItem as Поставка;
                if (Supply == null)
                {
                    MessageBox.Show("Выберите поставку для удаления!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Вы уверены, что хотите удалить поставку №{Supply.Код_поставки}?\nВместе с поставкой будет удален её состав!",
                                            "Подтверждение удаления",
                                            MessageBoxButton.YesNo,
                                            MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Уменьшаем количество товаров на складе при удалении поставки
                    var items = context.Состав_поставки.Where(i => i.Код_поставки == Supply.Код_поставки).ToList();
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

                    context.Поставка.Remove(Supply);
                    context.SaveChanges();

                    MessageBox.Show("Поставка успешно удалена!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadData();
                    DataGridSupplies_SelectionChanged(null, null);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при удалении поставки: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            DataGridSupplies_SelectionChanged(null, null);
        }
    }
}