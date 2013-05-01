using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Text;
using StatsDirect.Templates;

namespace StatsDirect.TemplateProcessing
{
    public abstract class ReportRenderer
    {
        /// <remarks>TODO: Move this to the application, not the template processor - this breaks layering</remarks>
        private static readonly string TEMPLATE_PATH = Path.Combine(Configuration.SDConfiguration.InstallationDirectory, Numerics.Properties.Settings.Default.TemplateDirectory);

        public static string GetContent(string name)
        {
            string path = Path.Combine(TEMPLATE_PATH, name);
            try
            {
                // Only allow read permission to the templates.  If the user tries something sneaky like a ".." in name, this should fail the read.
                new FileIOPermission(FileIOPermissionAccess.Read, TEMPLATE_PATH).Assert();
                using (TextReader tr = new StreamReader(path, Encoding.ASCII))
                {
                    return tr.ReadToEnd();
                }
            }
            finally
            {
                CodeAccessPermission.RevertAssert();
            }
        }

        public abstract string Render(ParameterBag substitutions);
    }
}
