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
                panel.Children.Add(CreateButtonWithTooltip("Добавить", createHandler));
            }

            // Кнопка "Обновить"
            if (canEdit && editHandler != null)
            {
                panel.Children.Add(CreateButtonWithTooltip("Обновить", editHandler));
            }

            // Кнопка "Удалить"
            if (canDelete && deleteHandler != null)
            {
                panel.Children.Add(CreateButtonWithTooltip("Удалить", deleteHandler));
            }

            // Кнопка "Очистить" - всегда доступна
            if (clearHandler != null)
            {
                panel.Children.Add(CreateButtonWithTooltip("Очистить", clearHandler));
            }
        }

        /// <summary>
        /// Создаёт кнопку, обернутую в Border для отображения Tooltip даже когда кнопка неактивна
        /// </summary>
        private static Border CreateButtonWithTooltip(string text, RoutedEventHandler handler,
            double width = 90, double height = 34, Thickness? margin = null)
        {
            var button = new Button
            {
                Content = text,
                Width = width,
                Height = height,
                IsEnabled = false // По умолчанию неактивна
            };

            button.Click += handler;

            // Оборачиваем кнопку в Border
            var border = new Border
            {
                Margin = margin ?? new Thickness(3),
                Child = button,
                Tag = button // Сохраняем ссылку на кнопку в Tag
            };

            // Подписываемся на изменение состояния кнопки
            button.IsEnabledChanged += (s, e) =>
            {
                var btn = s as Button;
                if (btn != null)
                {
                    // Находим родительский Border
                    var parentBorder = FindParentBorder(btn);
                    if (parentBorder != null)
                    {
                        if (btn.IsEnabled)
                        {
                            parentBorder.ToolTip = null;
                        }
                        else
                        {
                            parentBorder.ToolTip = GetTooltipText(btn);
                        }
                    }
                }
            };

            return border;
        }

        /// <summary>
        /// Находит родительский Border для кнопки
        /// </summary>
        private static Border FindParentBorder(DependencyObject child)
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is Border))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as Border;
        }

        /// <summary>
        /// Возвращает текст подсказки для кнопки
        /// </summary>
        private static string GetTooltipText(Button button)
        {
            var content = button.Content?.ToString() ?? "";

            if (content.Contains("Добавить"))
                return "Заполните форму и нажмите для добавления нового сотрудника";

            if (content.Contains("Обновить"))
                return "Выберите сотрудника из списка для редактирования";

            if (content.Contains("Удалить"))
                return "Выберите сотрудника из списка для удаления";

            if (content.Contains("Очистить"))
                return "Очистить все поля формы";

            return "Кнопка временно недоступна";
        }

        /// <summary>
        /// Обновляет состояние кнопки
        /// </summary>
        public static void UpdateButtonState(Border buttonBorder, bool isEnabled)
        {
            if (buttonBorder?.Child is Button button)
            {
                button.IsEnabled = isEnabled;
                // Tooltip обновится автоматически через обработчик IsEnabledChanged
            }
        }

        /// <summary>
        /// Создаёт обычную кнопку (без обертки) для обратной совместимости
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