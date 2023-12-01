using System.IO;
using System.Text;
using StatsDirect.Templates;
using StatsDirect.Configuration;
using StatsDirect.Utilities;

namespace StatsDirect.TemplateProcessing
{
    public abstract class ReportRenderer: RendererBase
    {
        protected ReportRenderer(ISdPreferences sdPreferences)
            : base(sdPreferences)
        {
        }

        public static string GetContent(string name)
        {
            string path = Path.Combine(SDConfiguration.TemplatePath, name);
            using TextReader tr = new StreamReader(path, Encoding.ASCII);
            return tr.ReadToEnd();
        }

        public abstract string Render(string content, ParameterBag substitutions);
    }
}
