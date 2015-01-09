using System;
using System.Text;
using StatsDirect.Numerics;
using System.Text.RegularExpressions;

namespace StatsDirect.Utilities
{
    public static class Formatting
    {
        public const string ERRR = "error";
        public const string FULLSTOP = ".";
        public const string INFRES = "infinity";
        public const string INFRESNEG = "-infinity";
        public const string MISSINGLABEL = "* (missing)";
        public const string ASTERISK = "*";
        public const string WRNCOLON = "Warning: ";
        public const string ERRCOLON = "Error: ";
        public const string RTFCRLF = @"\par ";


        private static string decimalSeparator;

        public static string DecimalSeparator
        {
            get { return decimalSeparator ?? (decimalSeparator = (5.5D).ToString().Substring(1, 1)); }
        }

        public static string XRound(double amount, int places)
        {
            try
            {
                if (Constant.MISSING == amount || -Constant.MISSING == amount)
                    return ASTERISK;
                if (Double.IsPositiveInfinity(amount))
                    return INFRES;
                if (Double.IsNegativeInfinity(amount))
                    return INFRESNEG;
                if (Double.IsNaN(amount))
                    return ASTERISK;
            }
            catch
            {
                return ERRR;
            }
            if (0.0 != amount && (Math.Abs(amount) < Math.Pow(10, -places) || Math.Abs(amount) > 1e8))
                return amount.ToString("E");
            return amount.ToString("#,##0." + new string('#', places));
        }

        public static string XUnrounded(double amount)
        {
            try
            {
                if (Constant.MISSING == amount || -Constant.MISSING == amount)
                    return ASTERISK;
                if (Double.IsPositiveInfinity(amount))
                    return INFRES;
                if (Double.IsNegativeInfinity(amount))
                    return INFRESNEG;
                if (Double.IsNaN(amount))
                    return ASTERISK;
            }
            catch
            {
                return ERRR;
            }
            return amount.ToString();
        }

        public static string RoundMeta(double x, double min)
        {
            int decpm = 2;
            try
            {
                if (Math.Abs(min) < Math.Pow(10D, -decpm) && min != 0D)
                {
                    decpm = 3;
                    if (Math.Abs(min) < Math.Pow(10D, -decpm) && min != 0D)
                        decpm = 4;
                }
                if (Constant.MISSING == x || -Constant.MISSING == x)
                    return ASTERISK;
                if (Double.IsPositiveInfinity(x))
                    return INFRES;
                if (Double.IsNegativeInfinity(x))
                    return INFRESNEG;
                if (Double.IsNaN(x))
                    return ASTERISK;
            }
            catch
            {
                return ERRR;
            }
            if (Math.Abs(x) < Math.Pow(10D, -decpm) && 0 != x)
            {
                return x.ToString(PreferredScientificMask(decpm));
            }
            return x.ToString("F" + decpm.ToString());
        }

        public static string PreferredScientificMask(int decpm)
        {
            int extraSigDigits = Math.Max(0, decpm - 2);
            return "0.0" + new string('#', extraSigDigits) + "E0";
        }

        public static string RoundMeta(double x, double min, int decpm)
        {
            if (Constant.MISSING == x || -Constant.MISSING == x)
                return ASTERISK;
            if (Double.IsPositiveInfinity(x))
                return INFRES;
            if (Double.IsNegativeInfinity(x))
                return INFRESNEG;
            if (Double.IsNaN(x))
                return ASTERISK;
            try
            {
                if (Math.Abs(x) < Math.Pow(10, -decpm) && 0 != x)
                    return x.ToString(PreferredScientificMask(decpm));
            }
            catch (Exception)
            {
                return ERRR;
            }
            return x.ToString("F" + decpm.ToString());
        }

        public static string pval(double P, int decimalPlaces, bool useScientificNotationForSmallPValues)
        {
            if (double.IsNaN(P) || double.IsInfinity(P) || Math.Abs(P) > 10)
                return "P = *";
            if (P < Math.Pow(10D, -decimalPlaces))
            {
                if (useScientificNotationForSmallPValues && P != 0.0)
                    return "P = " + P.ToString("E");
                else
                    return "P < 0" + DecimalSeparator + new String('0', decimalPlaces - 1) + "1";
            }
            if (P > 1D - Math.Pow(10D, -decimalPlaces))
                return "P > 0" + DecimalSeparator + new String('9', decimalPlaces);
            return P.ToString("P = 0." + new String('#', decimalPlaces));
        }

        public static string pval_half(double P, int decimalPlaces, bool useScientificNotationForSmallPValues)
        {
            if (Math.Abs(P) > 10)
                return "P = err";
            if (P < Math.Pow(10D, -decimalPlaces))
            {
                if (useScientificNotationForSmallPValues && P != 0.0)
                    return "P = " + P.ToString("E");
                else
                    return "P < 0" + DecimalSeparator + new String('0', decimalPlaces - 1) + "1";
            }
            if (P > 0.5D - Math.Pow(10D, -decimalPlaces))
                return "P > 0" + DecimalSeparator + "4" + new String('9', decimalPlaces - 1);
            return P.ToString("P = 0." + new String('#', decimalPlaces));
        }

        public static string pwr(double pwr, double p0)
        {
            string pwr_o;
            if (Constant.MISSING == pwr)
                pwr_o = ASTERISK;
            else if (pwr > 0.9999)
                pwr_o = "> 99.99%";
            else if (pwr < 0.0001)
                pwr_o = "< 0.01%";
            else
                pwr_o = "= " + XRound(pwr * 100.0, 2) + "%";
            return "(for " + XRound(100 * (p0), 1) + "% significance) " + pwr_o;
        }

        /// <summary>
        /// Returns Math.Exp(x) if safe to do so, otherwise MISSING.
        /// </summary>
        /// <param name="x"></param>
        /// <returns></returns>
        /// <remarks>Converted from Safe_Exp</remarks>
        public static double SafeExp(double x)
        {
            if (Math.Abs(x) > Math.Log(Constant.LMREAL))
                return Constant.MISSING;
            double z = Math.Exp(x);
            if (z > Constant.LMREAL || z < Constant.SPREAL)
                return Constant.MISSING;
            return z;
        }

        /// <summary>
        /// Returns the sum of the non-MISSING elements in array x, starting from lowerBound
        /// </summary>
        /// <param name="x"></param>
        /// <param name="lowerBound"></param>
        /// <returns></returns>
        public static double dsum(double[] x, int lowerBound)
        {
            double sum = 0;
            for (int i = lowerBound; i <= x.GetUpperBound(0); i++)
            {
                double v = x[i];
                if (v != Constant.MISSING)
                    sum += v;
            }
            return sum;
        }

        public static string SFormat(double x)
        {
            return Constant.MISSING == x ? ASTERISK : x.ToString();
        }

        public static string RoundOut(double q, int flt)
        {
            if (Constant.MISSING == q)
                return ASTERISK;
            if (flt > 6)
                return q.ToString();
            return XRound(q, flt);
        }

        public static string PadTo(string txt, int spaces)
        {
            int l = txt.Length;
            if (l < spaces)
                return txt.PadRight(spaces);
            if (l == spaces)
                return txt;
            return txt.Substring(0, spaces);
        }

        public static string RoundUp(double x)
        {
            if (x < 0)
                return ((int)x).ToString();
            return (((int)x) + 1).ToString();
        }

        public static string pr15(double Q)
        {
            return Q.ToString(Q < Constant.EPSNEG ? "#.##########E+000" : "0.000000000000000");
        }

        /// <summary>
        /// Try to return a relatively short path.
        /// If less than 40 characters, return the input path.
        /// Otherwise, return the first two components of the path, an ellipsis, and the last two components.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string ShortPath(string path)
        {
            if (path.Length < 40)
                return path;

            const string pattern = @"^(\w+:|\\)(\\[^\\]+\\[^\\]+\\).*(\\[^\\]+\\[^\\]+)$";
            const string replacement = "$1$2...$3";
            return Regex.IsMatch(path, pattern) ? Regex.Replace(path, pattern, replacement) : path;
        }

        public static string ToExcelColumnName(int zeroBasedColumnNumber)
        {
            int pos2 = zeroBasedColumnNumber < 26 + 26 * 26 ? -1 : ((zeroBasedColumnNumber - (26 + 26 * 26)) / (26 * 26)) % 26;
            int pos1 = zeroBasedColumnNumber < 26 ? -1 : ((zeroBasedColumnNumber - 26) / 26) % 26;
            int pos0 = zeroBasedColumnNumber % 26;

            StringBuilder sb = new StringBuilder();
            if (pos2 >= 0)
                sb.Append((char)('A' + pos2));
            if (pos1 >= 0)
                sb.Append((char)('A' + pos1));
            sb.Append((char)('A' + pos0));
            return sb.ToString();
        }
    }
}
