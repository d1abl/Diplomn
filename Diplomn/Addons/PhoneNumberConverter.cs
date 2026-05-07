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
            // В PhoneNumberConverter.cs, метод Convert
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return "☎ Телефон не указан";

            string phone = value.ToString();
            string digits = Regex.Replace(phone, @"[^\d]", "");

            if (digits.Length == 11 && (digits[0] == '7' || digits[0] == '8'))
            {
                string regionCode = digits.Substring(1, 3);
                string firstPart = digits.Substring(4, 3);
                string secondPart = digits.Substring(7, 2);
                string thirdPart = digits.Substring(9, 2);
                return $"☎ +7 ({regionCode}) {firstPart}-{secondPart}-{thirdPart}";
            }

            return $"☎ {phone}";
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
