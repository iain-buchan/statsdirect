using System.Collections.Generic;

namespace StatsDirect.UI
{
    /// <summary>
    /// A representation of (potentially multiple) selection areas
    /// </summary>
    public sealed class Range
    {
        private readonly IList<Area> areas;

        public Range(IEnumerable<Area> Areas)
        {
            areas = new List<Area>(Areas);
        }

        public IList<Area> Areas => areas;
    }
}
