using System;

using StatsDirect.Numerics;

namespace StatsDirect.Charting
{
    /// <summary>
    /// Stores one x and one or more y points.  The use of the y points is chart-specific.
    /// </summary>
    public class MultiDoubleSeries : Series
    {
        private MultiDoublePoint min;
        private MultiDoublePoint minGreaterThanZero;
        private MultiDoublePoint max;
        public MultiDoublePoint[] Data { get; set; }

        //  Similar to markers
        public MarkerDetails MarkerDetails { get; set; }

        public MultiDoubleSeries()
        {
            //  Do nothing; this is only here because we also have a custom constructor
        }

        public int Points
        {
            get
            {
                return Data.Length;
            }
        }

        public double MinX
        {
            get
            {
                if (null == min)
                    CalcMinMax();
                return min.X;
            }
        }

        public double MinXGreaterThanZero
        {
            get
            {
                if (null == minGreaterThanZero)
                    CalcMinMax();
                return minGreaterThanZero.X;
            }
        }

        public double MaxX
        {
            get
            {
                if (null == max)
                    CalcMinMax();
                return max.X;
            }
        }

        public double MinY(int index)
        {
            if (null == min)
                CalcMinMax();
            return min.get_Y(index);
        }

        public double MinYGreaterThanZero(int index)
        {
            if (null == minGreaterThanZero)
                CalcMinMax();
            return minGreaterThanZero.get_Y(index);
        }

        public double MaxY(int index)
        {
            if (null == max)
                CalcMinMax();
            return max.get_Y(index);
        }

        private void CalcMinMax()
        {
            // Assign points, setting X but leaving Y for later
            min = new MultiDoublePoint() { X = double.MaxValue };
            minGreaterThanZero = new MultiDoublePoint() { X = double.MaxValue };
            max = new MultiDoublePoint() { X = double.MinValue };

            foreach (MultiDoublePoint pt in Data)
            {
                // Calculate X
                double x = pt.X;
                if (x != Constant.MISSING)
                {
                    if (x < min.X)
                        min.X = x;
                    if (x > 0 && x < minGreaterThanZero.X)
                        minGreaterThanZero.X = x;
                    if (x > max.X)
                        max.X = x;
                }

                // Calculate Ys
                min.EnsureYIndicesInclude(pt.YCount, Double.MaxValue);
                minGreaterThanZero.EnsureYIndicesInclude(pt.YCount, Double.MaxValue);
                max.EnsureYIndicesInclude(pt.YCount, Double.MinValue);
                for (int yIndex = 0; yIndex < pt.YCount; yIndex++)
                {
                    double y = pt.get_Y(yIndex);
                    if (y < min.get_Y(yIndex))
                        min.set_Y(yIndex, y);
                    if (y > 0 && x < minGreaterThanZero.get_Y(yIndex))
                        minGreaterThanZero.set_Y(yIndex, y);
                    if (y > max.get_Y(yIndex))
                        max.set_Y(yIndex, y);
                }
            }
        }

        public override MultiDoubleSeries AsMultiDoubleSeries
        {
            get
            {
                return this;
            }
        }
    }
}
