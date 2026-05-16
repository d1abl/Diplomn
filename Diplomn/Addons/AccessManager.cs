using Org.BouncyCastle.Asn1.Cmp;
using System.Collections.Generic;
using System.Linq;
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
        /// Создаёт кнопки действий на панели с overlay для tooltip
        /// </summary>
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
                var (button, overlay) = CreateButtonWithOverlay("Добавить", createHandler);
                panel.Children.Add(CreateButtonContainer(button, overlay));
            }

            // Кнопка "Обновить"
            if (canEdit && editHandler != null)
            {
                var (button, overlay) = CreateButtonWithOverlay("Обновить", editHandler);
                panel.Children.Add(CreateButtonContainer(button, overlay));
            }

            // Кнопка "Удалить"
            if (canDelete && deleteHandler != null)
            {
                var (button, overlay) = CreateButtonWithOverlay("Удалить", deleteHandler);
                panel.Children.Add(CreateButtonContainer(button, overlay));
            }

            // Кнопка "Очистить" - всегда доступна
            if (clearHandler != null)
            {
                var (button, overlay) = CreateButtonWithOverlay("Очистить", clearHandler);
                panel.Children.Add(CreateButtonContainer(button, overlay));
            }
        }

        /// <summary>
        /// Создает контейнер Grid, содержащий кнопку и overlay для tooltip
        /// </summary>
        private static Grid CreateButtonContainer(Button button, Border overlay)
        {
            var grid = new Grid
            {
                Margin = new Thickness(3),
                Width = button.Width,
                Height = button.Height
            };

            grid.Children.Add(button);
            grid.Children.Add(overlay);

            return grid;
        }

        /// <summary>
        /// Создает кнопку и прозрачный overlay Border для tooltip
        /// </summary>
        private static (Button button, Border overlay) CreateButtonWithOverlay(string text, RoutedEventHandler handler, double width = 90, double height = 34)
        {
            var button = new Button
            {
                Content = text,
                Width = width,
                Height = height,
                IsEnabled = false
            };

            button.Click += handler;

            var overlay = new Border
            {
                Background = System.Windows.Media.Brushes.Transparent,
                IsHitTestVisible = true,
                ToolTip = GetDefaultTooltip(text)
            };

            button.IsEnabledChanged += (s, e) =>
            {
                var btn = s as Button;
                if (btn != null)
                {
                    if (btn.IsEnabled)
                    {
                        overlay.Visibility = System.Windows.Visibility.Collapsed;
                        overlay.ToolTip = null;
                    }
                    else
                    {
                        overlay.Visibility = System.Windows.Visibility.Visible;
                        overlay.ToolTip = GetDefaultTooltip(btn.Content?.ToString());
                    }
                }
            };

            return (button, overlay);
        }

        /// <summary>
        /// Возвращает текст подсказки по умолчанию для кнопки
        /// </summary>
        private static string GetDefaultTooltip(string buttonContent)
        {
            if (string.IsNullOrEmpty(buttonContent)) return "";

            if (buttonContent.Contains("Добавить"))
                return "Заполните обязательные поля для активации";

            if (buttonContent.Contains("Обновить"))
                return "Выберите запись из списка для редактирования";

            if (buttonContent.Contains("Удалить"))
                return "Выберите запись из списка для удаления";

            if (buttonContent.Contains("Очистить"))
                return "Очистить все поля формы";

            return "Кнопка временно недоступна";
        }

        /// <summary>
        /// Устанавливает состояние кнопки через контейнер
        /// </summary>
        public static void SetButtonState(Grid container, bool isEnabled, string customTooltip = null)
        {
            if (container == null) return;

            // Находим кнопку в контейнере
            var button = container.Children.OfType<Button>().FirstOrDefault();
            if (button != null)
            {
                button.IsEnabled = isEnabled;
            }

            // Находим overlay в контейнере
            var overlay = container.Children.OfType<Border>().FirstOrDefault(b => b.Background == System.Windows.Media.Brushes.Transparent);
            if (overlay != null && !isEnabled && !string.IsNullOrEmpty(customTooltip))
            {
                overlay.ToolTip = customTooltip;
            }
        }

        /// <summary>
        /// Создаёт кнопку без overlay (для обратной совместимости)
        /// </summary>
        public static Button CreateButton(string text, RoutedEventHandler handler,
            double width = 90, double height = 34, Thickness? margin = null)
        {
            var button = new Button
            {
                Content = text,
                Width = width,
                Height = height,
                Margin = margin ?? new Thickness(3),
                IsEnabled = text == "Очистить",
            };

            button.Click += handler;
            return button;
        }
    }
}