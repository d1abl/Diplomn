using System;
using System.Windows.Controls;

namespace Diplomn.Addons
{
    public static class FrameExtensions
    {
        /// <summary>
        /// Навигация с проверкой: если страница того же типа уже открыта, повторно не открывается
        /// </summary>
        public static bool NavigateIfDifferent(this Frame frame, Page page)
        {
            if (frame.Content is Page currentPage && currentPage.GetType() == page.GetType())
            {
                // Страница уже открыта
                return false;
            }

            frame.Navigate(page);
            return true;
        }
    }
}