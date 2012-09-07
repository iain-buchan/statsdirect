using System;
using StatsDirect.Numerics;

namespace StatsDirect.Utilities
{
    public class Parsing
    {
        public static DateTime Cdate_Txt(string s)
        {
            DateTime result;
            if (DateTime.TryParse(s, out result))
                return result;
            return DateTime.MinValue;
        }

        public static double Cdbl_Txt(string s)
        {
            try
            {
                if (null != s)
                {
                    // Fix up e.g. 1.0D-3 to 1.0E-3
                    s = s.Replace('D', 'E');
                    s = s.Replace('d', 'e');
                }
                return Math.Round(double.Parse(s), 14);
            }
            catch (FormatException)
            {
                return Constant.MISSING;
            }
            catch (OverflowException)
            {
                return Constant.MISSING;
            }
            catch (ArgumentNullException)
            {
                return Constant.MISSING;
            }
        }

        public static int Cint_Txt(string s)
        {
            try
            {
                return int.Parse(s);
            }
            catch (FormatException)
            {
                return int.MinValue;
            }
            catch (ArgumentNullException)
            {
                return int.MinValue;
            }
        }
    }
}
