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
                var L = new List<(string, double)>((List<(string, double)>) value);
                var OrderedList = L.OrderByDescending(p => p.Item2);
                string st = "";
                foreach (var item in OrderedList)
                {
                    var stt = 
                    st += "  " + item.Item1 + ": " + String.Format("{0:0.000}", item.Item2) + "\n";
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
