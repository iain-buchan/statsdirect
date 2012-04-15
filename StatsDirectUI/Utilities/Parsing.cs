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
