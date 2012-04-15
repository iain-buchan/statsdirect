using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using StatsDirect.Numerics;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

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


        // The number of hundredths of millimeters (0.01 mm) in an inch
        // For more information, see GetImagePrefix() method.
        private const int HMM_PER_INCH = 2540;

        // The number of twips in an inch
        // For more information, see GetImagePrefix() method.
        private const int TWIPS_PER_INCH = 1440;

        /* RTF HEADER
         * ----------
         * 
         * \rtf[N]		- For text to be considered to be RTF, it must be enclosed in this tag.
         *				  rtf1 is used because the RichTextBox conforms to RTF Specification
         *				  version 1.
         * \ansi		- The character set.
         * \ansicpg[N]	- Specifies that unicode characters might be embedded. ansicpg1252
         *				  is the default used by Windows.
         * \deff[N]		- The default font. \deff0 means the default font is the first font
         *				  found.
         * \deflang[N]	- The default language. \deflang1033 specifies US English.
         * */
        private const string RTF_HEADER = @"{\rtf1\ansi\ansicpg1252\deff0\deflang1033";
        private const string RTF_FOOTER = @"}";
        private const string RTF_IMAGE_POST = @"}";

        private static string decimalSeparator;

        [DllImport("gdi32")]
        private static extern uint GetEnhMetaFileBits(IntPtr hemf, uint cbBuffer, byte[] lpbBuffer);

        public static string DecimalSeparator
        {
            get { return decimalSeparator ?? (decimalSeparator = (5.5D).ToString().Substring(1, 1)); }
        }

        public static string XRound(double amount, int places)
        {
            try
            {
                if (Constant.MISSING == amount || -Constant.MISSING == amount)
                    return "*";
                if (Double.IsPositiveInfinity(amount))
                    return INFRES;
                if (Double.IsNegativeInfinity(amount))
                    return INFRESNEG;
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
                    return "*";
                if (Double.IsPositiveInfinity(amount))
                    return INFRES;
                if (Double.IsNegativeInfinity(amount))
                    return INFRESNEG;
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
                    return "*";
                if (Double.IsPositiveInfinity(x))
                    return INFRES;
                if (Double.IsNegativeInfinity(x))
                    return INFRESNEG;
            }
            catch
            {
                return ERRR;
            }
            if (Math.Abs(x) < Math.Pow(10D, -decpm) && 0 != x)
            {
                return x.ToString("E");
            }
            return x.ToString("F" + decpm.ToString());
        }

        public static string RoundMeta(double x, double min, int decpm)
        {
            if (Constant.MISSING == x || -Constant.MISSING == x)
                return "*";
            if (Double.IsPositiveInfinity(x))
                return INFRES;
            if (Double.IsNegativeInfinity(x))
                return INFRESNEG;
            try
            {
                if (Math.Abs(x) < Math.Pow(10D, -decpm) && 0 != x)
                    return x.ToString("E");
            }
            catch (Exception)
            {
                return ERRR;
            }
            return x.ToString("F" + decpm.ToString());
        }

        public static string pval(double P, int DecimalPlaces)
        {
            if (Math.Abs(P) > 10)
                return "P = *";
            if (P < Math.Pow(10D, -DecimalPlaces))
                return "P < 0" + DecimalSeparator + new String('0', DecimalPlaces - 1) + "1";
            if (P > 1D - Math.Pow(10D, -DecimalPlaces))
                return "P > 0" + DecimalSeparator + new String('9', DecimalPlaces);
            return P.ToString("P = 0." + new String('#', DecimalPlaces));
        }

        public static string pval_half(double P, int DecimalPlaces)
        {
            if (Math.Abs(P) > 10)
                return "P = err";
            if (P < Math.Pow(10D, -DecimalPlaces))
                return "P < 0" + DecimalSeparator + new String('0', DecimalPlaces - 1) + "1";
            if (P > 0.5D - Math.Pow(10D, -DecimalPlaces))
                return "P > 0" + DecimalSeparator + "4" + new String('9', DecimalPlaces - 1);
            return P.ToString("P = 0." + new String('#', DecimalPlaces));
        }

        public static string pwr(double pwr, double P0)
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
            return "(for " + XRound(100 * (P0), 1) + "% significance) " + pwr_o;
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
            {
                return Constant.MISSING;
            }
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

        public static string RoundOut(double Q, int flt)
        {
            if (Constant.MISSING == Q)
                return ASTERISK;
            if (flt > 6)
                return Q.ToString();
            return XRound(Q, flt);
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
        /// <summary>
        /// Returns the RTF corresponding to an image.  The image is wrapped in a Windows
        /// Format Metafile, because although Microsoft discourages the use of a WMF,
        /// the RichTextBox (and even MS Word), wraps an image in a WMF before inserting
        /// the image into a document.  The WMF is attached in HEX format (a string of
        /// HEX numbers).
        /// 
        /// The RTF Specification v1.6 says that you should be able to insert bitmaps,
        /// .jpegs, .gifs, .pngs, and Enhanced Metafiles (.emf) directly into an RTF
        /// document without the WMF wrapper. This works fine with MS Word,
        /// however, when you don't wrap images in a WMF, WordPad and
        /// RichTextBoxes simply ignore them.  Both use the riched20.dll or msfted.dll.
        /// </summary>
        /// <param name="_image"></param>
        public static string ImageToRtf(Image _image)
        {
            StringBuilder _rtf = new StringBuilder();

            // Append the RTF header
            _rtf.Append(RTF_HEADER);

            // Create the font table using the RichTextBox's current font and append it to the RTF string
            // _rtf.Append(GetFontTable(this.Font));
            // _rtf.Append(GetFontTable(FontFamily.GenericSansSerif));

            // Create the image control string and append it to the RTF string
            float pixelWidth = _image.Width;
            const float desiredInches = 6.0F;
            float desiredPixelsPerInch = (float)Math.Ceiling(pixelWidth / desiredInches);
            _rtf.Append(GetImagePrefix(_image, desiredPixelsPerInch, desiredPixelsPerInch));

            // Create the Windows Metafile and append its bytes in HEX format
            _rtf.Append(GetRtfImage(_image));

            // Close the RTF image control string
            _rtf.Append(RTF_IMAGE_POST);
            _rtf.Append(RTF_FOOTER);

            return _rtf.ToString();
        }

        /// <summary>
        /// Creates the RTF control string that describes the image being inserted.
        /// This description (in this case) specifies that the image is an
        /// MM_ANISOTROPIC metafile, meaning that both X and Y axes can be scaled
        /// independently.  The control string also gives the images current dimensions,
        /// and its target dimensions, so if you want to control the size of the
        /// image being inserted, this would be the place to do it. The prefix should
        /// have the form ...
        /// 
        /// {\pict\wmetafile8\picw[A]\pich[B]\picwgoal[C]\pichgoal[D]
        /// 
        /// where ...
        /// 
        /// A	= current width of the metafile in hundredths of millimeters (0.01mm)
        ///		= Image Width in Inches * Number of (0.01mm) per inch
        ///		= (Image Width in Pixels / Graphics Context's Horizontal Resolution) * 2540
        ///		= (Image Width in Pixels / Graphics.DpiX) * 2540
        /// 
        /// B	= current height of the metafile in hundredths of millimeters (0.01mm)
        ///		= Image Height in Inches * Number of (0.01mm) per inch
        ///		= (Image Height in Pixels / Graphics Context's Vertical Resolution) * 2540
        ///		= (Image Height in Pixels / Graphics.DpiX) * 2540
        /// 
        /// C	= target width of the metafile in twips
        ///		= Image Width in Inches * Number of twips per inch
        ///		= (Image Width in Pixels / Graphics Context's Horizontal Resolution) * 1440
        ///		= (Image Width in Pixels / Graphics.DpiX) * 1440
        /// 
        /// D	= target height of the metafile in twips
        ///		= Image Height in Inches * Number of twips per inch
        ///		= (Image Height in Pixels / Graphics Context's Horizontal Resolution) * 1440
        ///		= (Image Height in Pixels / Graphics.DpiX) * 1440
        ///	
        /// </summary>
        /// <remarks>
        /// The Graphics Context's resolution is simply the current resolution at which
        /// windows is being displayed.  Normally it's 96 dpi, but instead of assuming
        /// I just added the code.
        /// 
        /// According to Ken Howe at pbdr.com, "Twips are screen-independent units
        /// used to ensure that the placement and proportion of screen elements in
        /// your screen application are the same on all display systems."
        /// 
        /// Units Used
        /// ----------
        /// 1 Twip = 1/20 Point
        /// 1 Point = 1/72 Inch
        /// 1 Twip = 1/1440 Inch
        /// 
        /// 1 Inch = 2.54 cm
        /// 1 Inch = 25.4 mm
        /// 1 Inch = 2540 (0.01)mm
        /// </remarks>
        /// <param name="_image"></param>
        ///<param name="xDpi"></param>
        ///<param name="yDpi"></param>
        ///<returns></returns>
        private static string GetImagePrefix(Image _image, float xDpi, float yDpi)
        {

            StringBuilder _rtf = new StringBuilder();

            // Calculate the current width of the image in (0.01)mm
            // TODO: HACK: DevExpress seems to undo+redo insertion with the image very large unless this 2.6 bodge factor is in place.
            int picw = (int)Math.Round((_image.Width / xDpi) * HMM_PER_INCH * 2.6);

            // Calculate the current height of the image in (0.01)mm
            int pich = (int)Math.Round((_image.Height / yDpi) * HMM_PER_INCH * 2.6);

            // Calculate the target width of the image in twips
            int picwgoal = (int)Math.Round((_image.Width / xDpi) * TWIPS_PER_INCH);

            // Calculate the target height of the image in twips
            int pichgoal = (int)Math.Round((_image.Height / yDpi) * TWIPS_PER_INCH);

            // Append values to RTF string
            _rtf.Append(@"{\pict");
#if WANT_WMF
            _rtf.Append(@"\wmetafile8");
#else
            _rtf.Append(@"\emfblip");
#endif
            _rtf.Append(@"\picw");
            _rtf.Append(picw);
            _rtf.Append(@"\pich");
            _rtf.Append(pich);
            _rtf.Append(@"\picwgoal");
            _rtf.Append(picwgoal);
            _rtf.Append(@"\pichgoal");
            _rtf.Append(pichgoal);
            _rtf.Append(" ");

            return _rtf.ToString();
        }
        /// <summary>
        /// Wraps the image in an Enhanced Metafile by drawing the image onto the
        /// graphics context, then converts the Enhanced Metafile to a Windows
        /// Metafile, and finally appends the bits of the Windows Metafile in HEX
        /// to a string and returns the string.
        /// </summary>
        /// <param name="_image"></param>
        /// <returns>
        /// A string containing the bits of a Windows Metafile in HEX
        /// </returns>
        private static string GetRtfImage(Image _image)
        {
            // Used to store the enhanced metafile
            MemoryStream _stream = null;

            // Used to create the metafile and draw the image
            Graphics _graphics = null;

            // The enhanced metafile
            Metafile _metaFile = null;

            // Handle to the device context used to create the metafile

            try
            {
                StringBuilder _rtf = new StringBuilder();
                _stream = new MemoryStream();

                // Get a graphics context from the RichTextBox
                using (_graphics = Graphics.FromImage(new Bitmap(_image.Width, _image.Height, PixelFormat.Format32bppArgb)))
                {

                    // Get the device context from the graphics context
                    IntPtr _hdc = _graphics.GetHdc();

                    // Create a new Enhanced Metafile from the device context
                    _metaFile = new Metafile(_stream, _hdc);

                    // Release the device context
                    _graphics.ReleaseHdc(_hdc);
                }

                // Get a graphics context from the Enhanced Metafile
                using (_graphics = Graphics.FromImage(_metaFile))
                {

                    // Draw the image on the Enhanced Metafile
                    _graphics.DrawImage(_image, new Rectangle(0, 0, _image.Width, _image.Height));

                }

                // Get the handle of the Enhanced Metafile
                IntPtr _hEmf = _metaFile.GetHenhmetafile();

#if WANT_WMF
				// A call to EmfToWmfBits with a null buffer return the size of the
				// buffer need to store the WMF bits.  Use this to get the buffer
				// size.
				uint _bufferSize = GdipEmfToWmfBits(_hEmf, 0, null, MM_ANISOTROPIC,
					EmfToWmfBitsFlags.EmfToWmfBitsFlagsDefault);

				// Create an array to hold the bits
				byte[] _buffer = new byte[_bufferSize];

				// A call to EmfToWmfBits with a valid buffer copies the bits into the
				// buffer and returns the number of bits in the WMF.  
				uint _convertedSize = GdipEmfToWmfBits(_hEmf, _bufferSize, _buffer, MM_ANISOTROPIC,
					EmfToWmfBitsFlags.EmfToWmfBitsFlagsDefault);
#else // EMF
                uint _bufferSize = GetEnhMetaFileBits(_hEmf, 0, null);
                byte[] _buffer = new byte[_bufferSize];
                GetEnhMetaFileBits(_hEmf, _bufferSize, _buffer);
#endif

                // Append the bits to the RTF string
                foreach (byte t in _buffer)
                {
                    _rtf.Append(String.Format("{0:X2}", t));
                }

                return _rtf.ToString();
            }
            finally
            {
                if (_graphics != null)
                    _graphics.Dispose();
                if (_metaFile != null)
                    _metaFile.Dispose();
                if (_stream != null)
                    _stream.Close();
            }
        }
    }
}
