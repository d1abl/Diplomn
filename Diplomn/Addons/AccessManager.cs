using Org.BouncyCastle.Asn1.Cmp;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Diplomn
{
    public static class AccessManager
    {
        public class PageRights
        {
            public bool CanView { get; set; } = false;
            public bool CanEdit { get; set; } = false;
            public bool CanCreate { get; set; } = false;
            public bool CanDelete { get; set; } = false;
        }

        public class AccessRights
        {
            public PageRights Employees { get; set; } = new PageRights();
            public PageRights Roles { get; set; } = new PageRights();
            public PageRights Products { get; set; } = new PageRights();
            public PageRights Categories { get; set; } = new PageRights();
            public PageRights Brands { get; set; } = new PageRights();
            public PageRights Manufacturers { get; set; } = new PageRights();
            public PageRights Materials { get; set; } = new PageRights();
            public PageRights Packings { get; set; } = new PageRights();
            public PageRights Suppliers { get; set; } = new PageRights();
            public PageRights Supplies { get; set; } = new PageRights();
            public PageRights Sales { get; set; } = new PageRights();
            public PageRights Reports { get; set; } = new PageRights();
            public PageRights Settings { get; set; } = new PageRights();
        }

        public static AccessRights GetAccessRights(int accessLevel)
        {
            var rights = new AccessRights();

            switch (accessLevel)
            {
                case 1: // Администратор
                    SetFullRights(rights);
                    break;

                case 2: // Старший менеджер
                    SetFullRights(rights, withDelete: false);
                    break;

                case 4: // Менеджер по продажам
                    SetViewRights(rights, rights.Products, rights.Suppliers, rights.Supplies, rights.Sales, rights.Reports);
                    rights.Sales.CanCreate = true;
                    rights.Sales.CanEdit = true;
                    rights.Reports.CanCreate = true;
                    break;

                case 5: // Менеджер по закупкам
                    SetViewRights(rights, rights.Products, rights.Suppliers, rights.Supplies, rights.Sales, rights.Reports);
                    rights.Suppliers.CanEdit = true;
                    rights.Suppliers.CanCreate = true;
                    rights.Supplies.CanCreate = true;
                    rights.Supplies.CanEdit = true;
                    rights.Reports.CanCreate = true;
                    break;

                case 7: // Продавец-консультант
                case 8: // Кассир
                    rights.Products.CanView = true;
                    rights.Sales.CanView = true;
                    rights.Sales.CanCreate = true;
                    break;

                case 9: // Кладовщик
                    SetViewRights(rights, rights.Products, rights.Suppliers, rights.Supplies);
                    break;

                case 10: // Стажёр
                    rights.Products.CanView = true;
                    break;
            }

            return rights;
        }

        private static void SetFullRights(AccessRights rights, bool withDelete = true)
        {
            foreach (var prop in typeof(AccessRights).GetProperties())
            {
                if (prop.PropertyType == typeof(PageRights))
                {
                    var page = (PageRights)prop.GetValue(rights);
                    page.CanView = true;
                    page.CanEdit = true;
                    page.CanCreate = true;
                    if (withDelete) page.CanDelete = true;
                }
            }
        }

        private static void SetViewRights(AccessRights rights, params PageRights[] pages)
        {
            foreach (var page in pages)
            {
                page.CanView = true;
            }
        }
    }

}


namespace Diplomn.Addons
{
    public static class ButtonHelper
    {
        /// <summary>
        /// Создаёт кнопки действий на панели
        /// </summary>
        /// <param name="panel">Панель для размещения кнопок</param>
        /// <param name="rights">Права доступа</param>
        /// <param name="canCreate">Разрешено ли создание</param>
        /// <param name="canEdit">Разрешено ли редактирование</param>
        /// <param name="canDelete">Разрешено ли удаление</param>
        /// <param name="createHandler">Обработчик для кнопки "Добавить"</param>
        /// <param name="editHandler">Обработчик для кнопки "Обновить"</param>
        /// <param name="deleteHandler">Обработчик для кнопки "Удалить"</param>
        /// <param name="clearHandler">Обработчик для кнопки "Очистить"</param>
        public static void CreateActionButtons(Panel panel,
            bool canCreate, bool canEdit, bool canDelete,
            RoutedEventHandler createHandler = null,
            RoutedEventHandler editHandler = null,
            RoutedEventHandler deleteHandler = null,
            RoutedEventHandler clearHandler = null)
        {
            if (panel == null) return;

            panel.Children.Clear();

            // Кнопка "Добавить"
            if (canCreate && createHandler != null)
            {
                panel.Children.Add(CreateButton("Добавить", createHandler));
            }

            // Кнопка "Обновить"
            if (canEdit && editHandler != null)
            {
                panel.Children.Add(CreateButton("Обновить", editHandler));
            }

            // Кнопка "Удалить"
            if (canDelete && deleteHandler != null)
            {
                panel.Children.Add(CreateButton("Удалить", deleteHandler));
            }

            // Кнопка "Очистить" - всегда доступна (если передан обработчик)
            if (clearHandler != null)
            {
                panel.Children.Add(CreateButton("Очистить", clearHandler));
                
            }
        }

        /// <summary>
        /// Создаёт кнопки действий с кастомными названиями
        /// </summary>
        public static void CreateCustomActionButtons(Panel panel,
            (string text, bool condition, RoutedEventHandler handler)[] buttons)
        {
            if (panel == null) return;

            panel.Children.Clear();

            foreach (var button in buttons)
            {
                if (button.condition && button.handler != null)
                {
                    panel.Children.Add(CreateButton(button.text, button.handler));
                }
            }
        }

        /// <summary>
        /// Создаёт одну кнопку
        /// </summary>
        public static Button CreateButton(string text, RoutedEventHandler handler,
            double width = 90, double height = 34, Thickness? margin = null)
        {
            var button = new Button
            {
                Tag = handler,
                Content = text,
                Width = width,
                Height = height,
                Margin = margin ?? new Thickness(3),
                IsEnabled = false,                
                ToolTip = "Кнопка отключена",
            };
            button.Click += handler;
            if (text == "Очистить") { button.IsEnabled = true; button.ToolTip = "Очистить форму"; }
            return button;
        }

        /// <summary>
        /// Обновляет состояние кнопки
        /// </summary>
        public static void UpdateButtonState(Button button, bool isEnabled, string toolTip = null)
        {
            if (button == null) return;

            button.IsEnabled = isEnabled;
            button.ToolTip = toolTip;
        }
    }
}