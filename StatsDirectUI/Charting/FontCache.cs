using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;

namespace StatsDirect.Charting
{
    internal class FontCache : IDisposable
    {
        const float PIXELS_PER_INCH = 96.0f;
        const float POINTS_PER_INCH = 72.0f;
        const float PIXELS_PER_POINT = PIXELS_PER_INCH / POINTS_PER_INCH;

        const float SMALLEST_PIXEL_SIZE = 5;
        const float LARGEST_PIXEL_SIZE = 100;

        private static FontCache SoleInstance { get; } = new FontCache();

        private readonly Dictionary<FontDescriptor, Font?> fonts = new();

        public static bool TryGetFont(FontDescriptor fontDescriptor, [NotNullWhen(true)] out Font? font)
        {
            return SoleInstance.TryGetValue(fontDescriptor, out font);
        }

        public bool TryGetValue(FontDescriptor fontDescriptor, [NotNullWhen(true)] out Font? font)
        {
            // Check for an existing resolution - note that this may be null if we already know resolution has failed.
            if (!fonts.TryGetValue(fontDescriptor, out font))
            {
                font = FontFromDescriptor(fontDescriptor);
                fonts.Add(fontDescriptor, font);
            }
            return font is not null;
        }

        [return: NotNullIfNotNull(nameof(f))]
        public static FontDescriptor? DescriptorFromFont(Font? f)
        {
            if (f is null)
                return null;

            float emSize = f.Unit switch
            {
                GraphicsUnit.Pixel => f.Size / PIXELS_PER_POINT,
                GraphicsUnit.Point => f.Size,
                _ => throw new Exception("Cannot save font - unknown conversion from unit " + f.Unit),
            };
            return new FontDescriptor(f.FontFamily.Name, Convert.ToInt32(f.Style), emSize);
        }

        /// <summary>
        /// Returns a font matching the descriptor appropriate for drawing on a metafile, or null if no font can be derived from the descriptor.  #830: To prevent scaling issues, assume the metafile is drawn at 96dpi.
        /// </summary>
        public static Font? FontFromDescriptor(FontDescriptor? descriptor)
        {
            if (null == descriptor)
                return null;
            FontStyle style = (FontStyle)descriptor.Style;
            float pixelSize = descriptor.SizeInPoints * PIXELS_PER_POINT;

            // #1380: Prevent crazy font sizes
            if (pixelSize < SMALLEST_PIXEL_SIZE)
                pixelSize = SMALLEST_PIXEL_SIZE;
            if (pixelSize > LARGEST_PIXEL_SIZE)
                pixelSize = LARGEST_PIXEL_SIZE;
            try
            {
                return new Font(descriptor.FontFamily, pixelSize, style, GraphicsUnit.Pixel);
            }
            catch (Exception)
            {
                return null;
            }
        }

        #region IDisposable Support
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    foreach (Font? f in fonts.Values)
                        f?.Dispose();
                    fonts.Clear();
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }
}
