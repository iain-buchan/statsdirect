using System.Collections.Generic;
using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class DummyOptions : IFillable
    {
        public string MaxCatTi { get; set; }
        public List<string> Names { get; set; }

        ///  <summary>
        ///  The name that the user selected, or Nothing if no &lt;none> was selected.
        ///  </summary>
        public int JDrop { get; set; }

        public string FillerToUse
        {
            get
            {
                return "Dummy";
            }
        }

        // interface properties implemented by FillerToUse
        string IFillable.FillerToUse
        {
            get
            {
                return FillerToUse;
            }
        }

    }
}