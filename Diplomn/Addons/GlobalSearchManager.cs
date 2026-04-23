using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Diplomn.Addons
{
    public class GlobalSearchManager
    {
        private static GlobalSearchManager _instance;
        private Window _searchDialog;
        private TextBox _searchTextBox;
        private ListBox _resultsListBox;
        private FrameworkElement _currentTarget;
        private string _currentSearchText = "";
        private List<SearchResultItem> _foundItems = new List<SearchResultItem>();
        private int _currentIndex = -1;
        private HwndSource _hwndSource;
        private bool _isHotKeysRegistered = false;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private const int CTRL_F_ID = 9000;
        private const int CTRL_G_ID = 9001;
        private const int CTRL_SHIFT_G_ID = 9002;

        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_F = 0x46;
        private const uint VK_G = 0x47;

        public static GlobalSearchManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GlobalSearchManager();
                }
                return _instance;
            }
        }

        private GlobalSearchManager()
        {
            Application.Current.Activated += OnApplicationActivated;
            Application.Current.Exit += OnApplicationExit;
            Application.Current.Startup += OnApplicationStartup;
        }

        private void OnApplicationStartup(object sender, StartupEventArgs e)
        {
            // Регистрируем горячие клавиши после запуска приложения
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
            {
                RegisterGlobalHotKeys();
            }));
        }

        private void OnApplicationActivated(object sender, EventArgs e)
        {
            UpdateCurrentTarget();
            // Перерегистрируем горячие клавиши при активации
            if (!_isHotKeysRegistered)
            {
                RegisterGlobalHotKeys();
            }
        }

        private void OnApplicationExit(object sender, ExitEventArgs e)
        {
            UnregisterGlobalHotKeys();
            CloseSearchDialog();
        }

        private void RegisterGlobalHotKeys()
        {
            if (_isHotKeysRegistered) return;

            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    // Ждем загрузки окна
                    if (!mainWindow.IsLoaded)
                    {
                        mainWindow.Loaded += (s, e) => RegisterHotKeysForWindow(mainWindow);
                    }
                    else
                    {
                        RegisterHotKeysForWindow(mainWindow);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка регистрации горячих клавиш: {ex.Message}");
            }
        }

        private void RegisterHotKeysForWindow(Window window)
        {
            try
            {
                var handle = new WindowInteropHelper(window).Handle;
                if (handle != IntPtr.Zero)
                {
                    _hwndSource = HwndSource.FromHwnd(handle);
                    if (_hwndSource != null)
                    {
                        _hwndSource.AddHook(HwndHook);

                        RegisterHotKey(handle, CTRL_F_ID, MOD_CONTROL, VK_F);
                        RegisterHotKey(handle, CTRL_G_ID, MOD_CONTROL, VK_G);
                        RegisterHotKey(handle, CTRL_SHIFT_G_ID, MOD_CONTROL | MOD_SHIFT, VK_G);
                        _isHotKeysRegistered = true;

                        System.Diagnostics.Debug.WriteLine("Горячие клавиши успешно зарегистрированы");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка регистрации для окна: {ex.Message}");
            }
        }

        private void UnregisterGlobalHotKeys()
        {
            if (!_isHotKeysRegistered) return;

            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow != null)
                {
                    var handle = new WindowInteropHelper(mainWindow).Handle;
                    if (handle != IntPtr.Zero)
                    {
                        UnregisterHotKey(handle, CTRL_F_ID);
                        UnregisterHotKey(handle, CTRL_G_ID);
                        UnregisterHotKey(handle, CTRL_SHIFT_G_ID);
                    }
                }

                if (_hwndSource != null)
                {
                    _hwndSource.RemoveHook(HwndHook);
                    _hwndSource = null;
                }

                _isHotKeysRegistered = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка отмены регистрации: {ex.Message}");
            }
        }

        private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_HOTKEY = 0x0312;

            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();

                switch (id)
                {
                    case CTRL_F_ID:
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            ShowSearchDialog();
                        }));
                        handled = true;
                        break;
                    case CTRL_G_ID:
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            FindNext();
                        }));
                        handled = true;
                        break;
                    case CTRL_SHIFT_G_ID:
                        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            FindPrevious();
                        }));
                        handled = true;
                        break;
                }
            }

            return IntPtr.Zero;
        }

        private void UpdateCurrentTarget()
        {
            var activeWindow = Application.Current.Windows.Cast<Window>()
                .FirstOrDefault(w => w.IsActive && w.IsVisible);

            if (activeWindow != null)
            {
                var focusedElement = Keyboard.FocusedElement as FrameworkElement;

                if (focusedElement != null)
                {
                    _currentTarget = FindScrollableParent(focusedElement) ?? activeWindow;
                }
                else
                {
                    _currentTarget = activeWindow;
                }
            }
        }

        private FrameworkElement FindScrollableParent(DependencyObject element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is ScrollViewer || parent is ListBox || parent is DataGrid)
                {
                    return parent as FrameworkElement;
                }
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        public void ShowSearchDialog()
        {
            UpdateCurrentTarget();

            if (_currentTarget == null)
            {
                MessageBox.Show("Нет активного окна для поиска", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (_searchDialog != null)
            {
                if (_searchDialog.IsVisible)
                {
                    _searchDialog.Activate();
                    _searchTextBox?.Focus();
                    return;
                }
                else
                {
                    _searchDialog = null;
                }
            }

            CreateSearchDialog();

            var owner = Application.Current.Windows.Cast<Window>()
                .FirstOrDefault(w => w.IsActive && w.IsVisible);

            if (owner != null)
            {
                _searchDialog.Owner = owner;
            }

            _searchDialog.ShowDialog();
        }

        // В методе CreateSearchDialog() - исправленный стиль для ListBoxItem

        private void CreateSearchDialog()
        {
            _searchDialog = new Window
            {
                Title = "Глобальный поиск (Ctrl+F)",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                ShowInTaskbar = false,
                Topmost = true,
                MinWidth = 500,
                MinHeight = 400
            };

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.Margin = new Thickness(10);

            // Заголовок
            var titleLabel = new TextBlock
            {
                Text = "Поиск по приложению",
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(titleLabel, 0);
            mainGrid.Children.Add(titleLabel);

            // Панель поиска
            var searchPanel = new Grid();
            searchPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            searchPanel.Margin = new Thickness(0, 0, 0, 10);

            var searchLabel = new TextBlock
            {
                Text = "Найти:",
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            Grid.SetColumn(searchLabel, 0);
            searchPanel.Children.Add(searchLabel);

            _searchTextBox = new TextBox
            {
                FontSize = 14,
                Padding = new Thickness(5)
            };
            _searchTextBox.KeyDown += SearchTextBox_KeyDown;
            _searchTextBox.TextChanged += SearchTextBox_TextChanged;
            Grid.SetColumn(_searchTextBox, 1);
            searchPanel.Children.Add(_searchTextBox);

            var searchButton = new Button
            {
                Content = "🔍 Найти",
                Width = 80,
                Margin = new Thickness(10, 0, 0, 0),
                Padding = new Thickness(5)
            };
            searchButton.Click += (s, e) => PerformSearch(_searchTextBox.Text);
            Grid.SetColumn(searchButton, 2);
            searchPanel.Children.Add(searchButton);

            Grid.SetRow(searchPanel, 1);
            mainGrid.Children.Add(searchPanel);

            // Статусная строка
            var statusPanel = new Grid();
            statusPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statusPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            statusPanel.Margin = new Thickness(0, 0, 0, 5);

            var statusText = new TextBlock
            {
                Foreground = Brushes.Gray,
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(statusText, 0);
            statusPanel.Children.Add(statusText);

            var infoText = new TextBlock
            {
                Foreground = Brushes.DarkGray,
                FontSize = 10,
                FontStyle = FontStyles.Italic,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(infoText, 1);
            statusPanel.Children.Add(infoText);

            Grid.SetRow(statusPanel, 3);
            mainGrid.Children.Add(statusPanel);

            // Результаты поиска
            var resultsLabel = new TextBlock
            {
                Text = "Результаты поиска:",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 5, 0, 5)
            };
            Grid.SetRow(resultsLabel, 2);
            Grid.SetZIndex(resultsLabel, 1);
            mainGrid.Children.Add(resultsLabel);

            var resultsScrollViewer = new ScrollViewer
            {
                Margin = new Thickness(0, 25, 0, 0),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            _resultsListBox = new ListBox
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1)
            };

            // Создаем DataTemplate для отображения результатов
            _resultsListBox.ItemTemplate = CreateResultItemTemplate();

            // Стиль для ListBoxItem (без использования IsMouseOver как статического)
            var itemContainerStyle = new Style(typeof(ListBoxItem));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.PaddingProperty, new Thickness(5)));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.MarginProperty, new Thickness(0)));
            itemContainerStyle.Setters.Add(new Setter(ListBoxItem.CursorProperty, Cursors.Hand));

            // Триггер для наведения мыши
            var mouseOverTrigger = new Trigger();
            mouseOverTrigger.Property = ListBoxItem.IsMouseOverProperty;
            mouseOverTrigger.Value = true;
            mouseOverTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(230, 240, 255))));
            itemContainerStyle.Triggers.Add(mouseOverTrigger);

            // Триггер для выбранного элемента
            var selectedTrigger = new Trigger();
            selectedTrigger.Property = ListBoxItem.IsSelectedProperty;
            selectedTrigger.Value = true;
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BackgroundProperty, new SolidColorBrush(Color.FromRgb(200, 220, 255))));
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BorderBrushProperty, new SolidColorBrush(Color.FromRgb(100, 120, 200))));
            selectedTrigger.Setters.Add(new Setter(ListBoxItem.BorderThicknessProperty, new Thickness(1)));
            itemContainerStyle.Triggers.Add(selectedTrigger);

            _resultsListBox.ItemContainerStyle = itemContainerStyle;

            _resultsListBox.MouseDoubleClick += ResultsListBox_MouseDoubleClick;
            _resultsListBox.SelectionChanged += ResultsListBox_SelectionChanged;

            resultsScrollViewer.Content = _resultsListBox;
            Grid.SetRow(resultsScrollViewer, 2);
            mainGrid.Children.Add(resultsScrollViewer);

            _searchDialog.Content = mainGrid;

            _searchDialog.Closing += (s, e) =>
            {
                if (_searchDialog != null)
                {
                    e.Cancel = true;
                    _searchDialog.Hide();
                    ClearHighlights();
                }
            };

            _searchDialog.Loaded += (s, e) =>
            {
                _searchTextBox?.Focus();
            };

            // Обновление статуса
            SearchCompleted += (count, currentIndex) =>
            {
                statusText.Text = count > 0
                    ? $"✓ Найдено: {currentIndex + 1} из {count}"
                    : "✗ Ничего не найдено";
                statusText.Foreground = count > 0 ? Brushes.Green : Brushes.Red;
                infoText.Text = _currentTarget != null
                    ? $"Поиск в: {GetWindowTitle(_currentTarget)}"
                    : "Поиск по всему приложению";

                resultsLabel.Text = count > 0
                    ? $"Результаты поиска ({count}):"
                    : "Результаты поиска:";
            };
        }

        // Добавьте этот метод для создания шаблона отображения элементов
        private DataTemplate CreateResultItemTemplate()
        {
            var template = new DataTemplate(typeof(SearchResultItem));
            var stackPanel = new FrameworkElementFactory(typeof(StackPanel));
            stackPanel.SetValue(StackPanel.OrientationProperty, Orientation.Vertical);

            // Верхняя строка: тип и контекст
            var topPanel = new FrameworkElementFactory(typeof(StackPanel));
            topPanel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            topPanel.SetValue(StackPanel.MarginProperty, new Thickness(0, 0, 0, 3));

            var typeText = new FrameworkElementFactory(typeof(TextBlock));
            typeText.SetValue(TextBlock.TextProperty, new Binding("ElementType"));
            typeText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            typeText.SetValue(TextBlock.FontSizeProperty, 11.0);
            typeText.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0, 120, 200)));
            typeText.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 10, 0));
            topPanel.AppendChild(typeText);

            var contextText = new FrameworkElementFactory(typeof(TextBlock));
            contextText.SetValue(TextBlock.TextProperty, new Binding("Context"));
            contextText.SetValue(TextBlock.FontSizeProperty, 10.0);
            contextText.SetValue(TextBlock.ForegroundProperty, Brushes.Gray);
            contextText.SetValue(TextBlock.FontStyleProperty, FontStyles.Italic);
            topPanel.AppendChild(contextText);

            stackPanel.AppendChild(topPanel);

            // Текст содержимого
            var contentText = new FrameworkElementFactory(typeof(TextBlock));
            contentText.SetValue(TextBlock.TextProperty, new Binding("DisplayText"));
            contentText.SetValue(TextBlock.FontSizeProperty, 12.0);
            contentText.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            contentText.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 3));
            stackPanel.AppendChild(contentText);

            // Разделитель
            var separator = new FrameworkElementFactory(typeof(Separator));
            separator.SetValue(Separator.MarginProperty, new Thickness(0, 3, 0, 0));
            separator.SetValue(Separator.BackgroundProperty, Brushes.LightGray);
            stackPanel.AppendChild(separator);

            template.VisualTree = stackPanel;
            return template;
        }

        private void ResultsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_resultsListBox.SelectedItem is SearchResultItem item)
            {
                NavigateToResult(item);
            }
        }

        private void ResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_resultsListBox.SelectedItem is SearchResultItem item)
            {
                // При выборе подсвечиваем элемент
                ClearCurrentHighlight();
                _currentIndex = _foundItems.IndexOf(item);
                HighlightCurrentItem();
            }
        }

        private void NavigateToResult(SearchResultItem item)
        {
            try
            {
                // Активируем окно с элементом
                var window = Window.GetWindow(item.TargetElement);
                if (window != null)
                {
                    window.Activate();
                    window.Focus();
                }

                // Прокручиваем к элементу
                ScrollToElement(item.TargetElement);

                // Подсвечиваем элемент
                HighlightElement(item.TargetElement, item.SearchText);

                // Закрываем диалог поиска
                CloseSearchDialog();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка навигации: {ex.Message}");
            }
        }

        private string GetWindowTitle(FrameworkElement element)
        {
            var window = Window.GetWindow(element);
            return window?.Title ?? "активном окне";
        }

        private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = _searchTextBox.Text;
            if (string.IsNullOrWhiteSpace(searchText))
            {
                ClearHighlights();
                _foundItems.Clear();
                _resultsListBox.ItemsSource = null;
                _currentIndex = -1;
                SearchCompleted?.Invoke(0, -1);
            }
            else
            {
                PerformSearch(searchText);
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (_foundItems.Count > 0)
                {
                    if (_resultsListBox.SelectedItem != null)
                    {
                        NavigateToResult((SearchResultItem)_resultsListBox.SelectedItem);
                    }
                    else
                    {
                        NavigateToResult(_foundItems[0]);
                    }
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CloseSearchDialog();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && _resultsListBox != null)
            {
                if (_resultsListBox.SelectedIndex < _foundItems.Count - 1)
                {
                    _resultsListBox.SelectedIndex++;
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Up && _resultsListBox != null)
            {
                if (_resultsListBox.SelectedIndex > 0)
                {
                    _resultsListBox.SelectedIndex--;
                }
                e.Handled = true;
            }
        }

        private void PerformSearch(string searchText)
        {
            ClearHighlights();

            _currentSearchText = searchText;
            _foundItems.Clear();
            _currentIndex = -1;

            if (string.IsNullOrWhiteSpace(searchText) || _currentTarget == null)
            {
                _resultsListBox.ItemsSource = null;
                SearchCompleted?.Invoke(0, -1);
                return;
            }

            // Поиск в визуальном дереве
            FindInVisualTree(_currentTarget, searchText);

            // Отображаем результаты
            _resultsListBox.ItemsSource = _foundItems;

            if (_foundItems.Count > 0)
            {
                _currentIndex = 0;
                _resultsListBox.SelectedIndex = 0;
                HighlightCurrentItem();
            }

            SearchCompleted?.Invoke(_foundItems.Count, _currentIndex);
        }

        private void FindInVisualTree(DependencyObject parent, string searchText)
        {
            if (parent == null) return;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child == null) continue;

                // Поиск в TextBlock
                if (child is TextBlock textBlock)
                {
                    if (!string.IsNullOrEmpty(textBlock.Text) &&
                        textBlock.Text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!_foundItems.Any(x => x.TargetElement == textBlock))
                        {
                            _foundItems.Add(new SearchResultItem
                            {
                                TargetElement = textBlock,
                                SearchText = searchText,
                                DisplayText = GetPreviewText(textBlock.Text, searchText),
                                ElementType = "Текст",
                                Context = GetContext(textBlock)
                            });
                        }
                    }
                }
                // Поиск в TextBox
                else if (child is TextBox textBox)
                {
                    if (!string.IsNullOrEmpty(textBox.Text) &&
                        textBox.Text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!_foundItems.Any(x => x.TargetElement == textBox))
                        {
                            _foundItems.Add(new SearchResultItem
                            {
                                TargetElement = textBox,
                                SearchText = searchText,
                                DisplayText = GetPreviewText(textBox.Text, searchText),
                                ElementType = "Поле ввода",
                                Context = GetContext(textBox)
                            });
                        }
                    }
                }
                // Поиск в Label
                else if (child is Label label && label.Content is string strContent)
                {
                    if (strContent.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!_foundItems.Any(x => x.TargetElement == label))
                        {
                            _foundItems.Add(new SearchResultItem
                            {
                                TargetElement = label,
                                SearchText = searchText,
                                DisplayText = GetPreviewText(strContent, searchText),
                                ElementType = "Метка",
                                Context = GetContext(label)
                            });
                        }
                    }
                }
                // Поиск в Button
                else if (child is Button button && button.Content is string buttonText)
                {
                    if (buttonText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!_foundItems.Any(x => x.TargetElement == button))
                        {
                            _foundItems.Add(new SearchResultItem
                            {
                                TargetElement = button,
                                SearchText = searchText,
                                DisplayText = GetPreviewText(buttonText, searchText),
                                ElementType = "Кнопка",
                                Context = GetContext(button)
                            });
                        }
                    }
                }
                // Поиск в Run
                else if (child is Run run)
                {
                    if (!string.IsNullOrEmpty(run.Text) &&
                        run.Text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var parentTextBlock = FindParent<TextBlock>(run);
                        if (parentTextBlock != null && !_foundItems.Any(x => x.TargetElement == parentTextBlock))
                        {
                            _foundItems.Add(new SearchResultItem
                            {
                                TargetElement = parentTextBlock,
                                SearchText = searchText,
                                DisplayText = GetPreviewText(parentTextBlock.Text, searchText),
                                ElementType = "Текст",
                                Context = GetContext(parentTextBlock)
                            });
                        }
                    }
                }

                // Рекурсивный поиск
                FindInVisualTree(child, searchText);
            }
        }

        private string GetPreviewText(string fullText, string searchText)
        {
            if (string.IsNullOrEmpty(fullText)) return "";

            int index = fullText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
            if (index < 0) return fullText.Length > 100 ? fullText.Substring(0, 100) + "..." : fullText;

            int start = Math.Max(0, index - 30);
            int length = Math.Min(100, fullText.Length - start);
            string preview = fullText.Substring(start, length);

            if (start > 0) preview = "..." + preview;
            if (start + length < fullText.Length) preview = preview + "...";

            return preview;
        }

        private string GetContext(FrameworkElement element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null)
            {
                if (parent is Window window)
                    return window.Title;
                if (parent is UserControl control)
                    return control.GetType().Name;
                if (parent is GroupBox groupBox)
                    return groupBox.Header?.ToString();
                parent = VisualTreeHelper.GetParent(parent);
            }
            return "Главное окно";
        }

        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parent = VisualTreeHelper.GetParent(child);
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as T;
        }

        public void FindNext()
        {
            if (_foundItems.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(_currentSearchText))
                {
                    PerformSearch(_currentSearchText);
                }
                return;
            }

            ClearCurrentHighlight();
            _currentIndex = (_currentIndex + 1) % _foundItems.Count;
            _resultsListBox.SelectedIndex = _currentIndex;
            HighlightCurrentItem();

            SearchCompleted?.Invoke(_foundItems.Count, _currentIndex);
        }

        public void FindPrevious()
        {
            if (_foundItems.Count == 0) return;

            ClearCurrentHighlight();
            _currentIndex--;
            if (_currentIndex < 0) _currentIndex = _foundItems.Count - 1;
            _resultsListBox.SelectedIndex = _currentIndex;
            HighlightCurrentItem();

            SearchCompleted?.Invoke(_foundItems.Count, _currentIndex);
        }

        private void HighlightCurrentItem()
        {
            if (_currentIndex >= 0 && _currentIndex < _foundItems.Count)
            {
                var item = _foundItems[_currentIndex];
                HighlightElement(item.TargetElement, item.SearchText);
            }
        }

        private void HighlightElement(FrameworkElement element, string searchText)
        {
            if (element is TextBlock textBlock)
            {
                if (textBlock.Tag == null)
                {
                    textBlock.Tag = textBlock.Background;
                }
                textBlock.Background = new SolidColorBrush(Colors.Gold);

                var animation = new ColorAnimation
                {
                    From = Colors.Gold,
                    To = Colors.LightGoldenrodYellow,
                    Duration = TimeSpan.FromSeconds(0.5),
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(2)
                };

                if (textBlock.Background is SolidColorBrush brush)
                {
                    brush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
                }
            }
            else if (element is TextBox textBox)
            {
                textBox.Focus();
                int index = textBox.Text.IndexOf(searchText, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    textBox.Select(index, searchText.Length);
                }
            }
        }

        private void ScrollToElement(FrameworkElement element)
        {
            try
            {
                element.BringIntoView();

                var scrollViewer = FindScrollViewer(element);
                if (scrollViewer != null)
                {
                    var transform = element.TransformToAncestor(scrollViewer);
                    var position = transform.Transform(new Point(0, 0));
                    scrollViewer.ScrollToVerticalOffset(position.Y - 50);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка скролла: {ex.Message}");
            }
        }

        private ScrollViewer FindScrollViewer(DependencyObject element)
        {
            var parent = VisualTreeHelper.GetParent(element);
            while (parent != null && !(parent is ScrollViewer))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            return parent as ScrollViewer;
        }

        private void ClearCurrentHighlight()
        {
            if (_currentIndex >= 0 && _currentIndex < _foundItems.Count)
            {
                var item = _foundItems[_currentIndex];
                if (item.TargetElement is TextBlock textBlock)
                {
                    textBlock.Background = textBlock.Tag as Brush ?? Brushes.Transparent;
                    textBlock.Tag = null;
                }
            }
        }

        private void ClearHighlights()
        {
            foreach (var item in _foundItems)
            {
                if (item.TargetElement is TextBlock textBlock && textBlock != null)
                {
                    textBlock.Background = textBlock.Tag as Brush ?? Brushes.Transparent;
                    textBlock.Tag = null;
                }
            }
        }

        public void CloseSearchDialog()
        {
            if (_searchDialog != null)
            {
                _searchDialog.Hide();
            }
            ClearHighlights();
        }

        public event Action<int, int> SearchCompleted;
    }

    public class SearchResultItem
    {
        public FrameworkElement TargetElement { get; set; }
        public string SearchText { get; set; }
        public string DisplayText { get; set; }
        public string ElementType { get; set; }
        public string Context { get; set; }

        public override string ToString()
        {
            return $"[{ElementType}] {DisplayText} (в: {Context})";
        }
    }
}