using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Layout.Formatters
{
    public abstract class Format
    {
        protected double Weight { get; private set; }

        public Format(double weight)
        {
            Weight = weight;
        }

        public abstract double Score(IEnumerable<decimal> val);

        public abstract Tuple<IEnumerable<string>, string> FormalLabels(IEnumerable<decimal> o);
    }

    public abstract class NumericFormat : Format
    {
        /// <summary>if true, 10^power portion will be placed on the axis title</summary>
        protected bool IsFactored { get; private set; }
        /// <summary>if true, labels will be extended to the same number of decimal places</summary>
        protected bool DecimalExtend { get; private set; }

        public NumericFormat(bool isFactored, bool decimalExtend, double weight) : base(weight)
        {
            IsFactored = isFactored;
            DecimalExtend = decimalExtend;
        }

        public override double Score(IEnumerable<decimal> val)
        {
            return 0.9 * val.Select(x => x == 0 ? 1 : Weight * Score(x)).Average() + 0.1 * (DecimalExtend ? 1 : 0);
        }

        public abstract double Score(decimal d);

        public override Tuple<IEnumerable<string>, string> FormalLabels(IEnumerable<decimal> o)
        {
            return FormatLabels(o);
        }

        public abstract Tuple<IEnumerable<string>, string> FormatLabels(IEnumerable<decimal> d);

        protected int FloorLog10(decimal val)
        {
            return (int)Math.Floor(Math.Log10((double)Math.Abs(val)));
        }

        protected decimal Pow10(int i)
        {
            int modI = Math.Abs(i);
            decimal multiplier = i < 0 ? 0.1m : 10m;
            decimal a = 1m;
            for (int j = 0; j < modI; j++)
                a *= multiplier;
            return a;
        }

        protected int decimalPlaces(decimal i)
        {
            string t = i.ToString("G29", CultureInfo.InvariantCulture);
            int s = t.IndexOf(".");
            return s < 0 ? 0 : t.Length - (s + 1);
        }
    }

    public class UnitFormat : NumericFormat
    {
        private decimal unit;
        private string name;
        private Range potRange;

        public UnitFormat(decimal unit, string name, Range potRange, bool factored, bool decimalExtend, double weight)
            : base(factored, decimalExtend, weight)
        {
            this.unit = unit;
            this.name = name;
            this.potRange = potRange;
        }

        public override double Score(decimal d)
        {
            return (FloorLog10(d) >= potRange.Min && FloorLog10(d) <= potRange.Max) ? 1 : 0;
        }

        public override Tuple<IEnumerable<string>, string> FormatLabels(IEnumerable<decimal> d)
        {
            IEnumerable<decimal> r = from x in d select x / unit;
            int decimals = (from x in r select decimalPlaces(x)).Max();
            return new Tuple<IEnumerable<string>, string>(from x in r select x.ToString(DecimalExtend ? "N" + decimals : "G29") + (IsFactored ? "" : name), (IsFactored ? name : ""));
        }
    }

    public class ScientificFormat : NumericFormat
    {
        public ScientificFormat(bool factored, bool decimalExtend, double weight)
            : base(factored, decimalExtend, weight)
        {
        }

        //scientific format for general numbers
        public override double Score(decimal d)
        {
            return 1;
        }

        public override Tuple<IEnumerable<string>, string> FormatLabels(IEnumerable<decimal> d)
        {
            int avgpot = (int)Math.Round((from x in d.Where(x => x != 0) select FloorLog10(x)).Average());
            decimal s = Pow10(avgpot);
            IEnumerable<decimal> r = from x in d select x / s;
            int decimals = (from x in r select decimalPlaces(x)).Max();
            string label = "x10\\^" + avgpot + "\\^";
            return new Tuple<IEnumerable<string>, string>(from x in r select x.ToString(DecimalExtend ? "N" + decimals : "0.#") + (IsFactored ? "" : label), (IsFactored ? label : ""));
        }
    }
}