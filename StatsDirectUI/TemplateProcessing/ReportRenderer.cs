using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using StatsDirect.Templates;

namespace StatsDirect.TemplateProcessing
{
    public abstract class ReportRenderer
    {
        /// <remarks>TODO: Move this to the application, not the template processor - this breaks layering</remarks>
        private static readonly string TEMPLATE_PATH = Path.Combine(Configuration.SDConfiguration.InstallationDirectory, UI.Properties.Settings.Default.TemplateDirectory);

        public static string GetContent(string name)
        {
            string path = Path.Combine(TEMPLATE_PATH, name);
            using (TextReader tr = new StreamReader(path, Encoding.ASCII))
            {
                return tr.ReadToEnd();
            }
        }

        public abstract string Render(ITemplateHost host, ParameterBag substitutions);
    }
}
