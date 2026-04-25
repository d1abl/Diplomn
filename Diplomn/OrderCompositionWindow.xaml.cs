using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Diplomn
{
    public partial class OrderCompositionWindow : Window
    {
        private BDEntities context;
        private Поставка order;
        private ObservableCollection<OrderItemDisplay2> items;

        public class OrderItemDisplay2
        {
            public int Id { get; set; }
            public int Код_товара { get; set; }
            public string Товар { get; set; }
            public int Количество { get; set; }
            public decimal Цена { get; set; }
            public decimal Сумма => Количество * Цена;
            public string Поставщик { get; set; }
        }

        public OrderCompositionWindow(BDEntities context, Поставка order)
        {
            InitializeComponent();
            this.context = context;
            this.order = order;

            var q = context.Состав_заказа
                .Include("Товары")
                .Include("Поставщики")
                .Where(i => i.Код_поставки == order.Код_поставки)
                .Select(i => new OrderItemDisplay2
                {
                    Id = i.Код_записиСЗ,
                    Код_товара = i.Код_товара,
                    Товар = i.Товары.Наименование,
                    Количество = i.Количество,
                    Цена = i.Цена_за_ед_покупка,
                    Поставщик = i.Поставщики != null ? i.Поставщики.Наименование_поставщика : "Не указан"
                })
                .ToList();

            items = new ObservableCollection<OrderItemDisplay2>(q);
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
            if (DataGridItems.SelectedItem is OrderItemDisplay2 sel)
            {
                if (MessageBox.Show($"Удалить позицию '{sel.Товар}'?", "Подтверждение", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                {
                    // remove from context if exists
                    var entity = context.Состав_заказа.Find(sel.Id);
                    if (entity != null)
                        context.Состав_заказа.Remove(entity);

                    items.Remove(sel);
                    UpdateTotal();
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // persist changes to quantities/prices
            foreach (var it in items)
            {
                var entity = context.Состав_заказа.Find(it.Id);
                if (entity != null)
                {
                    entity.Количество = it.Количество;
                    entity.Цена_за_ед_покупка = it.Цена;
                }
            }

            context.SaveChanges();
            MessageBox.Show("Изменения сохранены");
            this.DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            Close();
        }
    }
}