using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn
{
    public partial class SaleCompositionWindow : Window
    {
        private BDEntities context;
        private Продажи sale;
        private ObservableCollection<SaleItemDisplay> items;

        public class SaleItemDisplay
        {
            public int Id { get; set; }
            public int Код_товара { get; set; }
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => (Количество) * (Цена);
        }

        public SaleCompositionWindow(BDEntities context, Продажи sale)
        {
            InitializeComponent();
            this.context = context;
            this.sale = sale;

            var q = context.Состав_продажи
                .Include("Товары")
                .Where(i => i.Код_чека == sale.Код_чека)
                .Select(i => new SaleItemDisplay
                {
                    Id = i.Код_записиСП,
                    Код_товара = i.Код_товара,
                    Товар = i.Товары.Наименование,
                    Количество = i.Количество,
                    Цена = i.Цена
                })
                .ToList();

            items = new ObservableCollection<SaleItemDisplay>(q);
            DataGridItems.ItemsSource = items;
            DataGridItems.IsReadOnly = false;

            UpdateTotal();
        }

        private void UpdateTotal()
        {
            decimal total = items.Sum(i => i.Сумма);
            TxtTotal.Text = $"{total:N2} ₽";
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (DataGridItems.SelectedItem is SaleItemDisplay sel)
            {
                if (MessageBox.Show($"Удалить позицию '{sel.Товар}'?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    var entity = context.Состав_продажи.Find(sel.Id);
                    if (entity != null)
                        context.Состав_продажи.Remove(entity);

                    items.Remove(sel);
                    UpdateTotal();
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            foreach (var it in items)
            {
                var entity = context.Состав_продажи.Find(it.Id);
                if (entity != null)
                {
                    entity.Количество = it.Количество;
                    entity.Цена = it.Цена;
                }
            }

            context.SaveChanges();
            MessageBox.Show("Изменения сохранены");
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