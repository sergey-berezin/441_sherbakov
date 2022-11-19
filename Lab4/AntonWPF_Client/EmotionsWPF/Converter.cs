using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace EmotionsWPF
{
    public class EightEmotions : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            try
            {
                if (value == null)
                    return "";
                var val = value as List<AntonContracts.emotion>;
                if (val == null)
                    return "";
                string st = "";
                foreach (var item in val)
                {
                    st += item.ToString();
                }
                return st;
            }
            catch (Exception)
            {
                return "";
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            
            return "";
        }

    }
}
