using System.Windows;
using Diplomn.Addons;

namespace Diplomn
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Инициализация глобального поиска
            var searchManager = GlobalSearchManager.Instance;
        }
    }
}