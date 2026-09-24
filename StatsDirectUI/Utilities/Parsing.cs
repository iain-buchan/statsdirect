using System;
using StatsDirect.Numerics;

namespace StatsDirect.Utilities
{
    public class Parsing
    {
        public static DateTime Cdate_Txt(string s)
        {
            return DateTime.TryParse(s, out DateTime result) ? result : DateTime.MinValue;
        }

        public static double Cdbl_Txt(string s)
        {
            if (!string.IsNullOrEmpty(s))
            {
                // Fix up e.g. 1.0D-3 to 1.0E-3
                s = s.Replace('D', 'E');
                s = s.Replace('d', 'e');
            }
            // The nearest double to the text as typed. Rounded to 14 decimal places, as it used to be, a P below 5e-15 became 0
            // and the fifteenth figure of anything typed to 15 was tidied away.
            return double.TryParse(s, out double result) ? result : Constant.MISSING;
        }

        public static int Cint_Txt(string s)
        {
            return int.TryParse(s, out int result) ? result : int.MinValue;
        }
    }
}
