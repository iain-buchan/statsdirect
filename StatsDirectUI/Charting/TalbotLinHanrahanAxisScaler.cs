using System.Drawing;
using Layout;
using StatsDirect.Templates;

namespace StatsDirect.Charting
{
    class TalbotLinHanrahanAxisScaler : IAxisScaler
    {
        public IAxisScale Q_Axis(double qMin, double qMinGreaterThanZero, double qMax, bool isYAxis)
        {
            Range dataRange = new Range(qMin, qMax);
            RectangleF todoScreen = new RectangleF(0, 0, 1100, 800);
            // do axis layout
            AxisLayout axisLayout = new AxisLayout(isYAxis, dataRange, dataRange,
                (label, pos, axis) => /* ComputeLabelRect(label, pos, bottomPanel.ToScreen(), screen, axis) */ new RectangleF(0, (float)-pos, 0.1f, 0.1f), todoScreen);
            Bitmap b = new Bitmap(1, 1);
            Graphics g = Graphics.FromImage(b);
            Axis tlhAxis = axisLayout.layoutAxis(g);
            return new TalbotLinHanrahanAxisScale(tlhAxis, qMin, qMax);
        }
    }
}
