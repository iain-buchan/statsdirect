using System;
using System.Drawing;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.IO;
using System.Globalization;

namespace StatsDirect.UI
{
    internal partial class frmExportGraphic : Form
    {
        private const int IMAGE_TO_EXPORT_SCALE = 2;
        private readonly double scaleFactor;
        private readonly Image originalImage;
        private readonly byte[] originalBytes;
        private bool updating;

        private ISdApplication SdApplication { get; }

        public frmExportGraphic(Image img, byte[] bytes, ISdApplication sdApplication)
        {
            SdApplication = sdApplication;
            InitializeComponent();
            originalImage = img;
            originalBytes = bytes;
            pictureBox1.Image = img;
            scaleFactor = img.Width / (double)img.Height;
            updating = true; // Ensure the text boxes don't try to update each other
            txtWidth.Text = (img.Width / IMAGE_TO_EXPORT_SCALE).ToString();
            txtHeight.Text = (img.Height / IMAGE_TO_EXPORT_SCALE).ToString();
            updating = false;
            SetUi();
        }

        private void txtWidth_TextChanged(object? sender, EventArgs e)
        {
            if (chkKeepAspectRatio.Checked && int.TryParse(txtWidth.Text, out int width) && !updating)
            {
                int newHeight = Convert.ToInt32(width / scaleFactor);
                if (newHeight < 1)
                    newHeight = 1;
                updating = true;
                txtHeight.Text = newHeight.ToString();
                updating = false;
            }
        }

        private void txtHeight_TextChanged(object? sender, EventArgs e)
        {
            if (chkKeepAspectRatio.Checked && int.TryParse(txtHeight.Text, out int height) && !updating)
            {
                int newWidth = Convert.ToInt32(height * scaleFactor);
                if (newWidth < 1)
                    newWidth = 1;
                updating = true;
                txtWidth.Text = newWidth.ToString();
                updating = false;
            }
        }

        private void cmdCancel_Click(object? sender, EventArgs e)
        {
            Close();
        }

        private void cmdExport_Click(object? sender, EventArgs e)
        {
            try
            {
                const string cfp = "PNG|*.png";
                const string cfj = "JPEG|*.jpg;*.jpeg";
                const string cfb = "Windows Bitmap|*.bmp";
                const string cfw = "Windows Metafile|*.emf;*.wmf";
                const string cfa = "All formats|*.png;*.jpeg;*.jpg;*.bmp;*.wmf;*.emf";

                int width = int.MinValue;
                int height = int.MinValue;
                if (!rdoMetafile.Checked)
                {
                    if (!int.TryParse(txtWidth.Text, out width)
                        || !int.TryParse(txtHeight.Text, out height)
                        || width < 1
                        || height < 1)
                    {
                        SdApplication.MsgboxX("Please enter a width and height in pixels for the exported image.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Graphical image export", false);
                        return;
                    }
                }

                // Setup and open save dialog
                saveFileDialog.Title = "Save graphic as";
                // C_FD.Name = vbNullString
                if (rdoPng.Checked)
                {
                    saveFileDialog.Filter = cfp + "|" + cfj + "|" + cfb + "|" + cfw + "|" + cfa;
                    saveFileDialog.DefaultExt = "png";
                }
                else if (rdoJpeg.Checked)
                {
                    saveFileDialog.Filter = cfj + "|" + cfp + "|" + cfb + "|" + cfw + "|" + cfa;
                    saveFileDialog.DefaultExt = "jpg";
                }
                else if (rdoBitmap.Checked)
                {
                    saveFileDialog.Filter = cfb + "|" + cfp + "|" + cfj + "|" + cfw + "|" + cfa;
                    saveFileDialog.DefaultExt = "bmp";
                }
                else
                {
                    saveFileDialog.Filter = cfw + "|" + cfp + "|" + cfj + "|" + cfb + "|" + cfa;
                    saveFileDialog.DefaultExt = "emf";
                }
                DialogResult result = saveFileDialog.ShowDialog(this);
                if (result == DialogResult.OK || result == DialogResult.Yes)
                {
                    if (SaveImage(saveFileDialog.FileName, width, height))
                        Close();
                }
            }
            catch (Exception ex)
            {
                SdApplication.FriendlyError("Couldn't export this graphic", ex, false);
            }
        }

        private void FormatChanged(object? sender, EventArgs e)
        {
            SetUi();
        }

        private void SetUi()
        {
            grpCompression.Enabled = rdoJpeg.Checked;
            grpSize.Enabled = !rdoMetafile.Checked;
        }

        private void HelpRequest(object? sender, EventArgs e)
        {
            try
            {
                SdApplication.ShowHelp(this, "220554");
            }
            catch (Exception ex)
            {
                SdApplication.FriendlyError("Couldn't show help", ex, false);
            }
        }

        private bool SaveImage(string path, int width, int height)
        {
            string extension = path;
            if (extension.Contains("."))
            {
                extension = extension.Substring(extension.LastIndexOf('.') + 1);
            }
            extension = extension.ToLower(CultureInfo.InvariantCulture);

            if ("jpg".Equals(extension) || "jpeg".Equals(extension))
            {
                int quality = trkCompression.Value;
                using EncoderParameter qualityParam = new(Encoder.Quality, quality);
                ImageCodecInfo? jpegCodec = GetEncoderInfo("image/jpeg");
                using EncoderParameters encoderParams = new(1);
                encoderParams.Param[0] = qualityParam;
                WithWhiteBackground(originalImage, width, height).Save(path, jpegCodec, encoderParams);
            }
            else if ("png".Equals(extension))
            {
                WithWhiteBackground(originalImage, width, height).Save(path, ImageFormat.Png);
            }
            else if ("bmp".Equals(extension))
            {
                WithWhiteBackground(originalImage, width, height).Save(path, ImageFormat.Bmp);
            }
            else if ("wmf".Equals(extension) || "emf".Equals(extension))
            {
                SaveMetafile(path);
            }
            else
            {
                SdApplication.MsgboxX("Unknown image format '" + extension + "'.  Please save as a recognised format.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "StatsDirect", false);
                return false;
            }
            return true;
        }

        /// <summary>
        /// Returns the image codec with the given mime type
        /// </summary>
        private static ImageCodecInfo? GetEncoderInfo(string mimeType)
        {
            // Get image codecs for all image formats
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();

            // Find the correct image codec
            foreach (ImageCodecInfo t in codecs)
                if (t.MimeType == mimeType)
                    return t;
            return null;
        }

        private void SaveMetafile(string path)
        {
            using FileStream fs = new(path, FileMode.Create);
            fs.Write(originalBytes, 0, originalBytes.Length);
        }

        /// <summary>
        /// WMFs are transparent, and BMP and JPEGs don't handle transparency.
        /// The background of the saved image is initialised to black, which isn't helpful.
        /// This function produces a white-background bitmap.  For convenience, it scales as well.
        /// </summary>
        /// <param name="original"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        private static Image WithWhiteBackground(Image original, int width, int height)
        {
            Bitmap b = new(width, height);
            using (Graphics g = Graphics.FromImage(b))
            {
                g.Clear(Color.White);
                g.DrawImage(original, 0, 0, width, height);
            }
            return b;
        }
    }
}
