using System.Collections.Generic;
using System.IO;

namespace StatsDirect.Templates
{
    public sealed class TemplateFactory
    {
        private static IDictionary<string, Operation> operations;
        private static IList<Operation> userOperations;

        public static IDictionary<string, Operation> Operations
        {
            get
            {
                if (null == operations)
                    LoadOperations();
                return operations;
            }
        }

        public static IList<Operation> UserOperations
        {
            get
            {
                if (null == userOperations)
                    LoadOperations();
                return userOperations;
            }
        }

        /// <summary>
        /// Load all the menu and user operations
        /// </summary>
        /// <returns>A collection of the user operations</returns>
        private static void LoadOperations()
        {
            operations = new Dictionary<string, Operation>();
            System.Xml.Serialization.XmlSerializer s = new System.Xml.Serialization.XmlSerializer(typeof(Operation));
            DirectoryInfo di = new DirectoryInfo(Path.Combine(Configuration.SDConfiguration.InstallationDirectory, Numerics.Properties.Settings.Default.OperationsDirectory));
            FileInfo[] knownOperations = di.GetFiles();
            foreach (FileInfo info in knownOperations)
            {
                // Asking a DirectoryInfo for all files of the pattern "*.xml" gets eg. "scatter.xml~" - so we do it the hard way.
                if (".xml".Equals(info.Extension.ToLower()))
                {
                    TextReader fs = info.OpenText();
                    Operation o = (Operation)s.Deserialize(fs);
                    foreach (string name in o.Names)
                        operations.Add(name, o);
                    fs.Close();
                    o.FixAfterLoading();
                }
            }

            userOperations = new List<Operation>();
            string userOperationDir = Path.Combine(Configuration.SDConfiguration.InstallationDirectory, System.Configuration.ConfigurationManager.AppSettings["UserOperationDir"]);
            if (Directory.Exists(userOperationDir))
            {
                di = new DirectoryInfo(userOperationDir);
                knownOperations = di.GetFiles();
                foreach (FileInfo info in knownOperations)
                {
                    // Asking a DirectoryInfo for all files of the pattern "*.xml" gets eg. "scatter.xml~" - so we do it the hard way.
                    if (".xml".Equals(info.Extension.ToLower()))
                    {
                        TextReader fs = info.OpenText();
                        Operation o = (Operation)s.Deserialize(fs);
                        fs.Close();
                        o.FixAfterLoading();
                        userOperations.Add(o);
                    }
                }
            }
        }
    }
}
