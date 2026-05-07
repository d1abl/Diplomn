using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Diplomn.Addons
{
    public static class PlaceholderBehavior
    {
        #region PlaceholderText Attached Property
        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.RegisterAttached(
                "PlaceholderText",
                typeof(string),
                typeof(PlaceholderBehavior),
                new PropertyMetadata(string.Empty, OnPlaceholderTextChanged));

        public static string GetPlaceholderText(DependencyObject obj)
        {
            return (string)obj.GetValue(PlaceholderTextProperty);
        }

        public static void SetPlaceholderText(DependencyObject obj, string value)
        {
            obj.SetValue(PlaceholderTextProperty, value);
        }
        #endregion

        #region HasEye Attached Property
        public static readonly DependencyProperty HasEyeProperty =
            DependencyProperty.RegisterAttached(
                "HasEye",
                typeof(bool),
                typeof(PlaceholderBehavior),
                new PropertyMetadata(false, OnHasEyeChanged));

        public static bool GetHasEye(DependencyObject obj)
        {
            return (bool)obj.GetValue(HasEyeProperty);
        }

        public static void SetHasEye(DependencyObject obj, bool value)
        {
            obj.SetValue(HasEyeProperty, value);
        }
        #endregion

        #region Internal Properties
        private static readonly DependencyProperty OriginalForegroundProperty =
            DependencyProperty.RegisterAttached(
                "OriginalForeground",
                typeof(Brush),
                typeof(PlaceholderBehavior),
                new PropertyMetadata(null));

        private static readonly DependencyProperty EyeGridProperty =
            DependencyProperty.RegisterAttached(
                "EyeGrid",
                typeof(Grid),
                typeof(PlaceholderBehavior),
                new PropertyMetadata(null));

        // ИСПРАВЛЕНО: Изменён тип на Button
        private static readonly DependencyProperty EyeButtonProperty =
            DependencyProperty.RegisterAttached(
                "EyeButton",
                typeof(Button),
                typeof(PlaceholderBehavior),
                new PropertyMetadata(null));
        #endregion

        #region Placeholder Logic
        private static void OnPlaceholderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is Control control)
            {
                if (control.GetValue(OriginalForegroundProperty) == null)
                {
                    control.SetValue(OriginalForegroundProperty, control.Foreground);
                }

                control.Loaded += (s, args) => UpdatePlaceholder(control);

                if (control is TextBox textBox)
                {
                    textBox.TextChanged += (s, args) => UpdatePlaceholder(control);
                    textBox.GotFocus += (s, args) => RemovePlaceholder(control);
                    textBox.LostFocus += (s, args) => ShowPlaceholderIfNeeded(control);
                }
                else if (control is PasswordBox passwordBox)
                {
                    if (!GetHasEye(passwordBox))
                    {
                        passwordBox.PasswordChanged += (s, args) => UpdatePlaceholder(control);
                        passwordBox.GotFocus += (s, args) => RemovePlaceholder(control);
                        passwordBox.LostFocus += (s, args) => ShowPlaceholderIfNeeded(control);
                    }
                }
                else if (control is ComboBox comboBox)
                {
                    comboBox.SelectionChanged += (s, args) => UpdatePlaceholder(control);
                    comboBox.GotFocus += (s, args) => RemovePlaceholder(control);
                    comboBox.LostFocus += (s, args) => ShowPlaceholderIfNeeded(control);
                    comboBox.DropDownOpened += (s, args) => RemovePlaceholder(control);
                }
            }
        }

        private static void UpdatePlaceholder(Control control)
        {
            if (control == null || string.IsNullOrEmpty(GetPlaceholderText(control)))
                return;

            if (!control.IsFocused)
            {
                ShowPlaceholderIfNeeded(control);
            }
        }

        private static void RemovePlaceholder(Control control)
        {
            if (control == null || string.IsNullOrEmpty(GetPlaceholderText(control)))
                return;

            if (control is TextBox textBox)
            {
                var placeholderText = GetPlaceholderText(control);
                if (textBox.Text == placeholderText)
                {
                    textBox.Text = string.Empty;
                }
                RestoreForeground(control);
            }
            else if (control is PasswordBox passwordBox)
            {
                var placeholderText = GetPlaceholderText(control);
                if (passwordBox.Password == placeholderText)
                {
                    passwordBox.Password = string.Empty;
                }
                RestoreForeground(control);
            }
            else if (control is ComboBox comboBox)
            {
                RestoreForeground(control);
            }
        }

        private static void ShowPlaceholderIfNeeded(Control control)
        {
            if (control == null || string.IsNullOrEmpty(GetPlaceholderText(control)))
                return;

            bool shouldShowPlaceholder = false;

            if (control is TextBox textBox)
            {
                shouldShowPlaceholder = string.IsNullOrEmpty(textBox.Text);
            }
            else if (control is PasswordBox passwordBox)
            {
                shouldShowPlaceholder = string.IsNullOrEmpty(passwordBox.Password);
            }
            else if (control is ComboBox comboBox)
            {
                shouldShowPlaceholder = comboBox.SelectedItem == null ||
                                       (comboBox.SelectedIndex == -1);
            }

            if (shouldShowPlaceholder)
            {
                ShowPlaceholder(control);
            }
            else
            {
                RestoreForeground(control);
            }
        }

        private static void ShowPlaceholder(Control control)
        {
            var placeholderText = GetPlaceholderText(control);

            if (control.GetValue(OriginalForegroundProperty) == null)
            {
                control.SetValue(OriginalForegroundProperty, control.Foreground);
            }

            if (control is TextBox textBox)
            {
                if (textBox.Text != placeholderText)
                {
                    textBox.Text = placeholderText;
                }
                textBox.Foreground = Brushes.Gray;
                textBox.FontStyle = FontStyles.Italic;
            }
            else if (control is PasswordBox passwordBox)
            {
                if (passwordBox.Password != placeholderText)
                {
                    passwordBox.Password = placeholderText;
                }
                passwordBox.Foreground = Brushes.Gray;
                passwordBox.FontStyle = FontStyles.Italic;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.Tag = placeholderText;
                comboBox.Foreground = Brushes.Gray;
                comboBox.FontStyle = FontStyles.Italic;
            }
        }

        private static void RestoreForeground(Control control)
        {
            var originalForeground = control.GetValue(OriginalForegroundProperty) as Brush;
            if (originalForeground != null)
            {
                control.Foreground = originalForeground;
            }
            control.FontStyle = FontStyles.Normal;

            if (control is ComboBox comboBox)
            {
                comboBox.Tag = null;
            }
        }
        #endregion

        #region Eye Logic для PasswordBox
        private static void OnHasEyeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                if ((bool)e.NewValue)
                {
                    passwordBox.Loaded += PasswordBoxWithEye_Loaded;
                }
                else
                {
                    passwordBox.Loaded -= PasswordBoxWithEye_Loaded;
                }
            }
        }

        private static void PasswordBoxWithEye_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                passwordBox.Loaded -= PasswordBoxWithEye_Loaded;

                passwordBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyEyeTemplate(passwordBox);
                }), System.Windows.Threading.DispatcherPriority.Loaded);
            }
        }

        private static void ApplyEyeTemplate(PasswordBox passwordBox)
        {
            if (passwordBox.GetValue(EyeGridProperty) != null)
                return;

            var parent = passwordBox.Parent as Panel;
            if (parent == null) return;

            var index = parent.Children.IndexOf(passwordBox);
            if (index == -1) return;

            parent.Children.Remove(passwordBox);

            var originalHeight = passwordBox.Height;
            var originalWidth = passwordBox.Width;
            var originalMargin = passwordBox.Margin;
            var originalVerticalAlignment = passwordBox.VerticalAlignment;
            var originalHorizontalAlignment = passwordBox.HorizontalAlignment;
            var originalBackground = passwordBox.Background;
            var originalBorderBrush = passwordBox.BorderBrush;
            var originalBorderThickness = passwordBox.BorderThickness;

            // Внешний Border (единственный)
            var border = new Border
            {
                Background = originalBackground,
                BorderBrush = originalBorderBrush,
                BorderThickness = originalBorderThickness,
                CornerRadius = new CornerRadius(3),
                SnapsToDevicePixels = true
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            if (!double.IsNaN(originalHeight)) border.Height = originalHeight;
            if (!double.IsNaN(originalWidth)) border.Width = originalWidth;
            border.Margin = originalMargin;
            border.VerticalAlignment = originalVerticalAlignment;
            border.HorizontalAlignment = originalHorizontalAlignment;

            // Сбрасываем свойства PasswordBox, чтобы не было двойных границ
            passwordBox.Height = double.NaN;
            passwordBox.Width = double.NaN;
            passwordBox.Margin = new Thickness(0);
            passwordBox.VerticalAlignment = VerticalAlignment.Stretch;
            passwordBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            passwordBox.Background = Brushes.Transparent;
            passwordBox.BorderThickness = new Thickness(0); // Убираем border у PasswordBox
            passwordBox.ClearValue(PasswordBox.BorderBrushProperty);
            Grid.SetColumn(passwordBox, 0);

            // Видимый TextBox для отображения пароля
            var visibleTextBox = new TextBox
            {
                FontFamily = passwordBox.FontFamily,
                FontSize = passwordBox.FontSize,
                Foreground = passwordBox.Foreground,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), // Убираем border
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                Padding = new Thickness(0, 0, 0, 0),
                Visibility = Visibility.Collapsed,
                Style = null
            };
            Grid.SetColumn(visibleTextBox, 0);

            // Placeholder для PasswordBox
            var placeholderText = GetPlaceholderText(passwordBox);
            var placeholderBlock = new TextBlock
            {
                Text = placeholderText ?? string.Empty,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontStyle = FontStyles.Italic,
                FontFamily = passwordBox.FontFamily,
                FontSize = passwordBox.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                IsHitTestVisible = false,
                Visibility = string.IsNullOrEmpty(passwordBox.Password)
                    ? Visibility.Visible
                    : Visibility.Collapsed
            };
            Grid.SetColumn(placeholderBlock, 0);

            // Placeholder для видимого TextBox
            var visiblePlaceholder = new TextBlock
            {
                Text = placeholderText ?? string.Empty,
                Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)),
                FontStyle = FontStyles.Italic,
                FontFamily = passwordBox.FontFamily,
                FontSize = passwordBox.FontSize,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(5, 0, 0, 0),
                IsHitTestVisible = false,
                Visibility = Visibility.Collapsed
            };
            Grid.SetColumn(visiblePlaceholder, 0);

            // Кнопка-глазик
            var eyeButton = new Button
            {
                Width = 30,
                Height = 30,
                Margin = new Thickness(0, 0, 2, 0),
                Padding = new Thickness(0),
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = System.Windows.Input.Cursors.Hand,
                ToolTip = "Показать пароль",
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Focusable = false
            };

            // Стиль для кнопки без выделения
            var buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            buttonStyle.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Transparent));
            buttonStyle.Setters.Add(new Setter(Button.BorderThicknessProperty, new Thickness(0)));
            buttonStyle.Setters.Add(new Setter(Button.PaddingProperty, new Thickness(0)));
            buttonStyle.Setters.Add(new Setter(Button.FocusableProperty, false));

            var mouseOverTrigger = new Trigger { Property = Button.IsMouseOverProperty, Value = true };
            mouseOverTrigger.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            mouseOverTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Transparent));
            buttonStyle.Triggers.Add(mouseOverTrigger);

            var pressedTrigger = new Trigger { Property = Button.IsPressedProperty, Value = true };
            pressedTrigger.Setters.Add(new Setter(Button.BackgroundProperty, Brushes.Transparent));
            pressedTrigger.Setters.Add(new Setter(Button.BorderBrushProperty, Brushes.Transparent));
            buttonStyle.Triggers.Add(pressedTrigger);

            eyeButton.Style = buttonStyle;

            Grid.SetColumn(eyeButton, 1);

            var eyeIcon = new TextBlock
            {
                Text = "👁",
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            eyeButton.Content = eyeIcon;

            bool isPasswordVisible = false;

            // Обработчики ховера для Border
            var accentBrush = Application.Current.TryFindResource("AccentBrush") as Brush;
            var normalBorderBrush = originalBorderBrush;
            var normalThickness = originalBorderThickness;
            var focusedThickness = new Thickness(2);

            border.MouseEnter += (s, args) =>
            {
                if (accentBrush != null)
                {
                    border.BorderBrush = accentBrush;
                }
            };

            border.MouseLeave += (s, args) =>
            {
                if (!passwordBox.IsFocused && !visibleTextBox.IsFocused)
                {
                    border.BorderBrush = normalBorderBrush;
                    border.BorderThickness = normalThickness;
                }
            };

            // Клик по глазику
            eyeButton.Click += (s, args) =>
            {
                isPasswordVisible = !isPasswordVisible;

                if (isPasswordVisible)
                {
                    visibleTextBox.Text = passwordBox.Password;
                    passwordBox.Visibility = Visibility.Collapsed;
                    visibleTextBox.Visibility = Visibility.Visible;

                    placeholderBlock.Visibility = Visibility.Collapsed;

                    if (string.IsNullOrEmpty(visibleTextBox.Text))
                    {
                        visiblePlaceholder.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        visiblePlaceholder.Visibility = Visibility.Collapsed;
                    }

                    eyeIcon.Text = "🙈";
                    eyeButton.ToolTip = "Скрыть пароль";
                }
                else
                {
                    passwordBox.Password = visibleTextBox.Text;

                    visiblePlaceholder.Visibility = Visibility.Collapsed;
                    visibleTextBox.Visibility = Visibility.Collapsed;
                    passwordBox.Visibility = Visibility.Visible;

                    eyeIcon.Text = "👁";
                    eyeButton.ToolTip = "Показать пароль";

                    if (string.IsNullOrEmpty(passwordBox.Password))
                    {
                        placeholderBlock.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        placeholderBlock.Visibility = Visibility.Collapsed;
                    }
                }
            };

            // Изменение пароля
            passwordBox.PasswordChanged += (s, args) =>
            {
                if (string.IsNullOrEmpty(passwordBox.Password))
                {
                    placeholderBlock.Visibility = Visibility.Visible;
                }
                else
                {
                    placeholderBlock.Visibility = Visibility.Collapsed;
                }

                if (isPasswordVisible)
                {
                    visibleTextBox.Text = passwordBox.Password;
                }
            };

            // Изменение текста в видимом поле
            visibleTextBox.TextChanged += (s, args) =>
            {
                if (isPasswordVisible)
                {
                    passwordBox.Password = visibleTextBox.Text;

                    if (string.IsNullOrEmpty(visibleTextBox.Text))
                    {
                        visiblePlaceholder.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        visiblePlaceholder.Visibility = Visibility.Collapsed;
                    }
                }
            };

            // Фокус на PasswordBox
            passwordBox.GotFocus += (s, args) =>
            {
                placeholderBlock.Visibility = Visibility.Collapsed;
                if (accentBrush != null)
                {
                    border.BorderBrush = accentBrush;
                    border.BorderThickness = focusedThickness;
                }
            };

            passwordBox.LostFocus += (s, args) =>
            {
                if (!visibleTextBox.IsFocused)
                {
                    border.BorderBrush = normalBorderBrush;
                    border.BorderThickness = normalThickness;
                }

                if (string.IsNullOrEmpty(passwordBox.Password) && !isPasswordVisible)
                {
                    placeholderBlock.Visibility = Visibility.Visible;
                }
            };

            // Фокус на видимом TextBox
            visibleTextBox.GotFocus += (s, args) =>
            {
                visiblePlaceholder.Visibility = Visibility.Collapsed;
                if (accentBrush != null)
                {
                    border.BorderBrush = accentBrush;
                    border.BorderThickness = focusedThickness;
                }
            };

            visibleTextBox.LostFocus += (s, args) =>
            {
                if (!passwordBox.IsFocused)
                {
                    border.BorderBrush = normalBorderBrush;
                    border.BorderThickness = normalThickness;
                }

                if (isPasswordVisible && string.IsNullOrEmpty(visibleTextBox.Text))
                {
                    visiblePlaceholder.Visibility = Visibility.Visible;
                }
            };

            // Добавляем элементы в Grid
            grid.Children.Add(passwordBox);
            grid.Children.Add(visibleTextBox);
            grid.Children.Add(visiblePlaceholder);
            grid.Children.Add(placeholderBlock);
            grid.Children.Add(eyeButton);

            // Grid в Border
            border.Child = grid;

            // Border в родительский контейнер
            parent.Children.Insert(index, border);

            passwordBox.SetValue(EyeGridProperty, grid);
            passwordBox.SetValue(EyeButtonProperty, eyeButton);
        }
        #endregion
    }
}