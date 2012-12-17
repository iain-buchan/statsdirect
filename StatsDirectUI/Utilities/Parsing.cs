using System;
using StatsDirect.Numerics;

namespace StatsDirect.Utilities
{
    public class Parsing
    {
        public static DateTime Cdate_Txt(string s)
        {
            DateTime result;
            return DateTime.TryParse(s, out result) ? result : DateTime.MinValue;
        }

        public static double Cdbl_Txt(string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                // Fix up e.g. 1.0D-3 to 1.0E-3
                s = s.Replace('D', 'E');
                s = s.Replace('d', 'e');
            }
            double result;
            return double.TryParse(s, out result) ? Math.Round(result, 14) : Constant.MISSING;
        }

        public static int Cint_Txt(string s)
        {
            int result;
            return int.TryParse(s, out result) ? result : int.MinValue;
        }
    }
}
