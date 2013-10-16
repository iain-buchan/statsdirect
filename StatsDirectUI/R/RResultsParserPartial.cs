using StatsDirect.Charting;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;

namespace StatsDirect.R
{
    partial class RResultsParser
    {
        private object PathToChart(string path)
        {
            int width;
            int height;
            using (Image i = Metafile.FromFile(path))
            {
                width = i.Width;
                height = i.Height;
            }
            using (Stream s = File.OpenRead(path))
            {
                return ChartRenderer.MetastreamToRtf(s, width, height);
            }
        }

        private string PathToName(string path)
        {
            return Path.GetFileNameWithoutExtension(path);
        }

        private string ToStringBody(string rawParsedString)
        {
            return rawParsedString.Substring(1, rawParsedString.Length - 2).Replace("\\\"", "\"");
        }
    }
}
