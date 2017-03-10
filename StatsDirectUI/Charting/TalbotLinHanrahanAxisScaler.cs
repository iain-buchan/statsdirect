using System;
using StatsDirect.Templates;
using System.Drawing;

namespace StatsDirect.Charting
{
    class TalbotLinHanrahanAxisScaler : IAxisScaler
    {
        public IAxisScale Q_Axis(double qMin, double qMinGreaterThanZero, double qMax, bool isYAxis)
        {
            Layout.Range dataRange = new Layout.Range(qMin, qMax);
            RectangleF todoScreen = new RectangleF(0, 0, 1100, 800);
            // do axis layout
            Layout.AxisLayout axisLayout = new Layout.AxisLayout(isYAxis, dataRange, dataRange,
                new Func<string, decimal, Layout.Axis, RectangleF>((label, pos, axis) => /* ComputeLabelRect(label, pos, bottomPanel.ToScreen(), screen, axis) */ new RectangleF(0, (float)-pos, 0.1f, 0.1f)), todoScreen);
            Bitmap b = new Bitmap(1, 1);
            Graphics g = Graphics.FromImage(b);
            Layout.Axis tlhAxis = axisLayout.layoutAxis(g);
            return new TalbotLinHanrahanAxisScale(tlhAxis, qMin, qMax);
        }
    }
}
