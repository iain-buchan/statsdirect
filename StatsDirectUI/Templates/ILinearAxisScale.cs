using System.Collections.Generic;

namespace StatsDirect.Templates
{
    public interface ILinearAxisScale: IAxisScale
    {
        /// The number of intervals between major tics. If this is 5, every 5th tic will be a major tic.
        int IntervalsPerMajorTic { get; }
        /// Where to put the major tics.  Phase 0 gives the first tic as a major, phase 1 gives the second tic as a major, etc..
        int Phase { get; }

        double Interval { get; }

        double FirstMajorTicValue { get; }
    }
}
