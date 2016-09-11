namespace StatsDirect.Charting
{
    /// <summary>
    /// Histogram Binwidth Optimisation Method
    ///
    /// Shimazaki and Shinomoto, Neural Comput 19 1503-1527, 2007 
    /// 2006 Author Hideaki Shimazaki, Matlab
    /// Department of Physics, Kyoto University
    /// shimazaki at ton.scphys.kyoto-u.ac.jp
    /// Please feel free to use/modify this program.
    ///
    /// Version in python adapted Érbet Almeida Costa
    ///
    // /Bugfix by Takuma Torii 2.24.2013
    /// </summary>
    public static class HistogramBinChooser
    {
        /// <summary>
        /// Uses a cost function to estimate the optimal number of bins into which to place values sortedX[0] to sortedX[length - 1] to give an informative histogram.
        /// </summary>
        /// <returns>Edges for the most informative histogram according to the cost function.  Bin counts have also had to be calculated in order to estimate the cost function, so in order to save recalculation this returns the counts as well.</returns>
        public static BinsDescriptor ChooseBins(double[] sortedX, int length)
        {
            const int N_MIN = 4;   // Minimum number of bins (integer), N_MIN must be more than 1 (N_MIN > 1).
            const int N_MAX = 20;  // Maximum number of bins (integer)

            double xMin = sortedX[0];
            double xMax = sortedX[length - 1];

            double minCost = double.MaxValue;
            double[] bestCandidate = null;
            int[] bestCounts = null;
            for (int candidate = N_MIN; candidate <= N_MAX; candidate++)
            {
                double[] edges = Linspace(xMin, xMax, candidate + 1); //  Bin edges
                int[] ki = SortedHist(sortedX, length, edges); //  Count # of events in bins
                double k = Mean(ki); // Mean of event count
                double v = Variance(ki, k, candidate); // Variance of event count
                double d = (xMax - xMin) / candidate;
                double cost = (2 * k - v) / (d * d); // The cost function
                if (cost < minCost)
                {
                    minCost = cost;
                    bestCandidate = edges;
                    bestCounts = ki;
                }
            }

            return new BinsDescriptor { Edges = bestCandidate, Counts = bestCounts };
        }

        /// <summary>
        /// Returns an array of bin counts, placing values from sortedX[0] to sortedX[length - 1] into bins defined by edges.
        /// </summary>
        /// <param name="sortedX">Array of values to be counted into bins. PRECONDITION: This array must be sorted low to high by value.</param>
        /// <param name="edges">Bin edges.  The ith element in the returned array will correspond to values [edges[i], edges[i+1]).</param>
        public static int[] SortedHist(double[] sortedX, int length, double[] edges)
        {
            int bins = edges.Length - 1;
            int[] binCounts = new int[bins];
            int bin = 0;
            double nextEdge = edges[bin + 1];
            int firstIndexAboveBoundary = 0;
            for (int i = 0; i < length; i++)
            {
                while (sortedX[i] >= nextEdge)
                {
                    binCounts[bin++] = i - firstIndexAboveBoundary;
                    firstIndexAboveBoundary = i;
                    if (bin >= bins)
                    {
                        binCounts[bin - 1] += length - firstIndexAboveBoundary;
                        return binCounts;
                    }
                    nextEdge = edges[bin + 1];
                }
                if (bin >= bins)
                    break;
            }
            if (bin < bins)
                binCounts[bin] = length - firstIndexAboveBoundary;
            return binCounts;
        }

        /// <summary>
        /// Divide the number range [min, max] into pieces parts and return an array of pieces+1 values representing the lower and upper bounds of each piece.
        /// </summary>
        public static double[] Linspace(double min, double max, int pieces)
        {
            double[] edges = new double[pieces + 1];
            for (int i = 0; i < pieces; i++)
                edges[i] = min + ((max - min) * (i / (double)pieces));
            edges[edges.Length - 1] = max;
            return edges;
        }

        /// <summary>
        /// Return the mean of the values in ki
        /// </summary>
        private static double Mean(int[] ki)
        {
            double sum = 0;
            for (int i = 0; i < ki.Length; i++)
                sum += ki[i];
            return sum / ki.Length;
        }

        /// <summary>
        /// Return the variance of the values in ki given that their mean is mean.
        /// </summary>
        /// <param name="ki"></param>
        /// <param name="mean"></param>
        /// <param name="divisor"></param>
        /// <returns></returns>
        private static double Variance(int[] ki, double mean, int divisor)
        {
            double sumSq = 0;
            for (int i = 0; i < ki.Length; i++)
                sumSq += (ki[i] - mean) * (ki[i] - mean);
            return sumSq / divisor;
        }
    }

    public class BinsDescriptor
    {
        /// <summary>
        /// One count per bin
        /// </summary>
        public int[] Counts { get; set; }
        /// <summary>
        /// One edge per bin, plus the upper edge of the last bin in Edges[Edges.Length - 1].
        /// </summary>
        public double[] Edges { get; set; }
        public int Bins { get { return Counts.Length; } }
        public double LowestEdge { get { return Edges[0]; } }
        public double HighestEdge { get { return Edges[Edges.Length - 1]; } }
    }

}
