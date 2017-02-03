using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public class CategoryAxisScale: IAxisScale
    {
        public double MinimumDataValue { get { return 0; } }
        public double MaximumDataValue { get { return Categories; } }
        public double MinimumScaleValue { get { return 0; } }
        public double MaximumScaleValue { get { return Categories; } }
        /// The number of intervals between tics (one less than the number of tics).  20 intervals = 21 tics - one extra at the end.

            private int Categories { get; set; }
        public CategoryAxisScale(int categories)
        {
            Categories = categories;
        }

        /// <summary>
        /// Returns a linear list of tics constructed according to the parameters.
        /// </summary>
        /// <param name="min">The value of the first tic</param>
        /// <param name="interval">The interval between minor tics</param>
        /// <returns></returns>
        public IList<Tic> Tics()
        {
            List<Tic> tics = new List<Tic>(Categories + 1);
            for (int i = 0; i <= Categories; i++)
                tics.Add(new Tic { Value = i, TicType = TicType.Major });
            return tics;
        }

        public override string ToString()
        {
            return string.Format("CategoryAxisScale({0})", Categories);
        }
    }
}
