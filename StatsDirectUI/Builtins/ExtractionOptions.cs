using StatsDirect.Data;
using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class ExtractionOptions : IFillable
    {
        public string Title { get; set; }
        public DoubleVariable Data { get; set; }
        public DataFrame IdentifiersFrame { get; set; }
        public string IdentifierNames { get; set; }

        public string FillerToUse
        {
            get
            {
                return "Extraction";
            }
        }
    }
}