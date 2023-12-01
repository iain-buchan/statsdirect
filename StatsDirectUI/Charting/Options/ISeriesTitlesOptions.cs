using System.Collections.Generic;

namespace StatsDirect.Charting.Options
{
    internal interface ISeriesTitlesOptions
    {
        public IReadOnlyList<string?>? SeriesTitles { get; }
    }
}
