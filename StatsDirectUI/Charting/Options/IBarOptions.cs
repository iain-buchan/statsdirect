namespace StatsDirect.Charting.Options
{
    public interface IBarOptions
    {
        ///  <summary>
        ///  The widest a bar may be, as a fraction of its containing space.
        ///  </summary>
        public double MaxBarWidth { get; }

        /// <summary>
        /// If false, stacked bar charts should be drawn per Excel.  If true, they should be drawn per StatsDirect.
        /// </summary>
        public bool RotateWhenStacked { get; }

        ///  <summary>
        ///  If false, bars should be drawn side-by-side.  If true, bars should be drawn end-to-end.
        ///  </summary>
        public bool Stacked { get; }

        ///  <summary>
        ///  If Stacked and true, bars should be drawn end-to-end scaled 0..1.
        ///  If Stacked and false, bars should be drawn end-to-end scaled to the largest bar.
        ///  If not Stacked, no effect.
        ///  </summary>
        public bool Stacked100Percent { get; }
    }
}
