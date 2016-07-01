using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace StatsDirect.Charting
{
    /// <summary>
    /// Histogram Binwidth Optimisation Method
    ///
    /// Shimazaki and Shinomoto, Neural Comput 19 1503-1527, 2007 
    ///2006 Author Hideaki Shimazaki, Matlab
    ///Department of Physics, Kyoto University
    ///shimazaki at ton.scphys.kyoto-u.ac.jp
    ///Please feel free to use/modify this program.
    ///
    ///Version in python adapted Érbet Almeida Costa
    ///
    ///Bugfix by Takuma Torii 2.24.2013
    /// </summary>
    public static class HistogramBinChooser
    {
        public static BinsDescriptor ChooseBins(double[] sortedX, int lowerBound, int length)
        {
            const int N_MIN = 4;   // Minimum number of bins (integer), N_MIN must be more than 1 (N_MIN > 1).
            const int N_MAX = 50;  // Maximum number of bins (integer)

            double xMin = sortedX[lowerBound];
            double xMax = sortedX[lowerBound + length - 1];

            double minCost = double.MaxValue;
            double[] bestCandidate = null;
            int[] bestCounts = null;
            for (int candidate = N_MIN; candidate <= N_MAX; candidate++)
            {
                double[] edges = Linspace(xMin, xMax, candidate + 1); //  Bin edges
                int[] ki = SortedHist(sortedX, edges); //  Count # of events in bins
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

        public static int[] SortedHist(double[] sortedX, double[] edges)
        {
            int bins = edges.Length - 1;
            int[] binCounts = new int[bins];
            int bin = 0;
            double nextEdge = edges[bin + 1];
            int firstIndexAboveBoundary = 0;
            for (int i = 0; i < sortedX.Length; i++)
            {
                while (sortedX[i] >= nextEdge)
                {
                    binCounts[bin++] = i - firstIndexAboveBoundary;
                    firstIndexAboveBoundary = i;
                    if (bin >= bins)
                    {
                        binCounts[bin - 1] += sortedX.Length - firstIndexAboveBoundary;
                        return binCounts;
                    }
                    nextEdge = edges[bin + 1];
                }
                if (bin >= bins)
                    break;
            }
            if (bin < bins)
                binCounts[bin] = sortedX.Length - firstIndexAboveBoundary;
            return binCounts;
        }

        public static double[] Linspace(double min, double max, int pieces)
        {
            double[] edges = new double[pieces + 1];
            for (int i = 0; i < pieces; i++)
                edges[i] = min + ((max - min) * (i / (double)pieces));
            edges[edges.Length - 1] = max;
            return edges;
        }

        private static double Mean(int[] ki)
        {
            double sum = 0;
            for (int i = 0; i < ki.Length; i++)
                sum += ki[i];
            return sum / ki.Length;
        }

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
