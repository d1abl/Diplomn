using System;
//using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
//using System.Threading.Tasks;
using System.Windows.Data;

namespace Diplomn.Addons
{
    internal class PhoneNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            string phone = value.ToString();

            //if (string.IsNullOrWhiteSpace(phone))
            //    return string.Empty;

            // Удалить все нецифровые символы
            string digits = Regex.Replace(phone, @"[^\d]", "");

            // Форматирование для российских номеров
            if (digits.Length == 11 && (digits[0] == '7' || digits[0] == '8'))
            {
                string countryCode = digits[0] == '7' ? "+7" : "+7";
                string regionCode = digits.Substring(1, 3);
                string firstPart = digits.Substring(4, 3);
                string secondPart = digits.Substring(7, 2);
                string thirdPart = digits.Substring(9, 2);

                return $"{countryCode} ({regionCode}) {firstPart}-{secondPart}-{thirdPart}";
            }
            //else if (digits.Length == 10)
            //{
            //    return $"({digits.Substring(0, 3)}) {digits.Substring(3, 3)}-{digits.Substring(6, 2)}-{digits.Substring(8, 2)}";
            //}
            //else if (digits.Length == 7)
            //{
            //    return $"{digits.Substring(0, 3)}-{digits.Substring(3, 2)}-{digits.Substring(5, 2)}";
            //}

            // Если формат не распознан, возвращаем исходную строку
            return phone;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return null;

            // Удалить все нецифровые символы
            return Regex.Replace(value.ToString(), @"[^\d]", "");
        }
    }
}
