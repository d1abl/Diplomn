using Diplomn.Addons;
using System;
using System.Windows;

namespace Diplomn
{
    public partial class App : Application
    {
        //public App()
        //{
        //    LoadSavedTheme();
        //}

        ///// <summary>
        ///// Загружает сохраненную тему при старте приложения
        ///// </summary>
        //private void LoadSavedTheme()
        //{
        //    try
        //    {
        //        // Правильный синтаксис: Properties.Settings.Default.CurrentTheme
        //        string themeKey = Diplomn.Properties.Settings.Default.CurrentTheme;

        //        if (!string.IsNullOrEmpty(themeKey))
        //        {
        //            var uri = new Uri($"/Themes/{themeKey}.xaml", UriKind.Relative);

        //            // Проверяем существование темы
        //            try
        //            {
        //                var dict = new ResourceDictionary { Source = uri };

        //                // Очищаем текущие словари и добавляем сохраненную тему
        //                Application.Current.Resources.MergedDictionaries.Clear();
        //                Application.Current.Resources.MergedDictionaries.Add(dict);
        //            }
        //            catch
        //            {
        //                // Если тема не найдена, используем стандартную
        //                LoadDefaultTheme();
        //            }
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine($"Ошибка загрузки темы: {ex.Message}");
        //        LoadDefaultTheme();
        //    }
        //}

        ///// <summary>
        ///// Загружает тему по умолчанию
        ///// </summary>
        //private void LoadDefaultTheme()
        //{
        //    try
        //    {
        //        var uri = new Uri("/Themes/Theme1.xaml", UriKind.Relative);
        //        var dict = new ResourceDictionary { Source = uri };
        //        Application.Current.Resources.MergedDictionaries.Clear();
        //        Application.Current.Resources.MergedDictionaries.Add(dict);
        //    }
        //    catch
        //    {
        //        // Если и стандартная тема не загрузилась - продолжаем без темы
        //    }
        //}
    }
}