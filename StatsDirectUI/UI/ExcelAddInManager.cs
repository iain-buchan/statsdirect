using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public static class ExcelAddInManager
    {
        private const string Sdxlam = "StatsDirect3ExcelLink.xlam";
        private const string Sdxla = "StatsDirect3ExcelLink.xla";

        public static bool IsExcelInstalled()
        {
            bool isInstalled;
            try
            {
                //Get the excel object
                var excel = Type.GetTypeFromProgID("Excel.Application");
                if (null == excel)
                    return false;
                // Try to create an instance of Excel.  This will throw an exception if it fails.
                var excelObject = Activator.CreateInstance(excel);
                // If we get here, we can start Excel.
                isInstalled = true;
                excelObject.GetType().InvokeMember("quit", BindingFlags.InvokeMethod, null, excelObject, null);
            }
            catch (Exception)
            {
                isInstalled = false;
            }
            return isInstalled;
        }

        public static bool InstallAddIn()
        {
            //To switch the add-in on simply copy it from SD assets to the Excel startup path
            try
            {
                var excel = Type.GetTypeFromProgID("Excel.Application");
                var excelObject = Activator.CreateInstance(excel);
                var startpath = excelObject.GetType().InvokeMember("StartupPath", BindingFlags.GetProperty, null, excelObject, null);
                excelObject.GetType().InvokeMember("quit", BindingFlags.InvokeMethod, null, excelObject, null);
                string addInTargetPath = Path.Combine((string)startpath, Sdxlam);
                string appPath = Path.GetDirectoryName(Application.ExecutablePath);
                if (null == appPath)
                    return false;
                string addInSourcePath = Path.Combine(Path.Combine(appPath, "Excel"), Sdxlam);
                File.Copy(addInSourcePath, addInTargetPath);
                return true;
            }
            catch (Exception)
            {
            }
            return false;
        }

        public static bool IsAddInInstalled()
        {
            bool isInstalled = false;
            try
            {
                //Get the excel object
                var excel = Type.GetTypeFromProgID("Excel.Application");
                //Create instance of excel
                var excelObject = Activator.CreateInstance(excel);
                //Add a workbook and get addins collection
                var oBooks = excelObject.GetType().InvokeMember("Workbooks", BindingFlags.GetProperty, null, excelObject, null);
                oBooks.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, oBooks, null);
                var oAddins = excelObject.GetType().InvokeMember("Addins", BindingFlags.GetProperty, null, excelObject, null);

                // First possibility: Look for statsdirectexcel addin if user has installed it via the Excel add-in manager
                var oAddinCount = (int)oAddins.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, oAddins, null);
                var oParam = new object[1];
                for (var i = 1; i <= oAddinCount; i++)
                {
                    oParam[0] = i;
                    var addIn = oAddins.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, oAddins, oParam);
                    string oAddinName = (string)addIn.GetType().InvokeMember("Fullname", BindingFlags.GetProperty, null, addIn, null);
                    if (oAddinName.ToLower(CultureInfo.InvariantCulture).Contains("statsdirectexcel"))
                    {
                        bool oInstalled = (bool)addIn.GetType().InvokeMember("Installed", BindingFlags.GetProperty, null, addIn, null);
                        isInstalled = oInstalled; 
                        break;
                    }
                }

                if (!isInstalled)
                {
                    // Second possibility: Get the excel paths and look for the xla in the startup path
                    var startpath = excelObject.GetType().InvokeMember("StartupPath", BindingFlags.GetProperty, null, excelObject, null);
                    isInstalled |= (File.Exists(Path.Combine((string) startpath, Sdxlam)));
                }

                //Clean up: Close Excel
                excelObject.GetType().InvokeMember("quit", BindingFlags.InvokeMethod, null, excelObject, null);
                return isInstalled;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void UninstallAddIn()
        {
            try
			{
				//Get the excel object
				var excel = Type.GetTypeFromProgID("Excel.Application");
			    //Create instance of excel
				var excelObject = Activator.CreateInstance(excel);
                //Add a workbook and get addins collection
                var oBooks = excelObject.GetType().InvokeMember("Workbooks", BindingFlags.GetProperty, null, excelObject, null);
                oBooks.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, oBooks, null);
                var oAddins = excelObject.GetType().InvokeMember("Addins", BindingFlags.GetProperty, null, excelObject, null);

                // First possibility: Look for statsdirectexcel addin and uninstall it if user has installed it via the Excel add-in manager
			    var oAddinCount = (int)oAddins.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, oAddins, null);
                var oParam = new object[1];
                for (var i = 1; i <= oAddinCount; i++)
                {
                    oParam[0] = i;
                    var addIn = oAddins.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, oAddins, oParam);
                    string addinName = (string) addIn.GetType().InvokeMember("Fullname", BindingFlags.GetProperty, null, addIn , null);
                    if (addinName.EndsWith("StatsDirect3ExcelLink.xlam"))
                    {
                        oParam[0] = "False";
                        addIn.GetType().InvokeMember("Installed", BindingFlags.SetProperty, null, addIn, oParam);
                    }
                }

			    // Second possibility: Get the excel paths and remove the xla from the startup path if StatsDirect has activated it by putting it there
                string startpath = (string)excelObject.GetType().InvokeMember("StartupPath", BindingFlags.GetProperty, null, excelObject , null);
                string librarypath = (string)excelObject.GetType().InvokeMember("LibraryPath", BindingFlags.GetProperty, null, excelObject, null);
                if (File.Exists(Path.Combine(startpath, Sdxlam)))
                    File.Delete(Path.Combine(startpath, Sdxlam));
                if (File.Exists(Path.Combine(librarypath, Sdxlam)))
                    File.Delete(Path.Combine(librarypath, Sdxlam));

                //Clean up: Close Excel
			    excelObject.GetType().InvokeMember("quit", BindingFlags.InvokeMethod, null, excelObject, null);
			}
			catch(Exception)
			{
			}
        }

        public static void UninstallOldAddIn()
        {
            try
            {
                //Get the excel object
                var excel = Type.GetTypeFromProgID("Excel.Application");
                //Create instance of excel
                var excelObject = Activator.CreateInstance(excel);
                //Add a workbook and get addins collection
                var oBooks = excelObject.GetType().InvokeMember("Workbooks", BindingFlags.GetProperty, null, excelObject, null);
                oBooks.GetType().InvokeMember("Add", BindingFlags.InvokeMethod, null, oBooks, null);
                var oAddins = excelObject.GetType().InvokeMember("Addins", BindingFlags.GetProperty, null, excelObject, null);

                // First possibility: Look for statsdirectexcel addin and uninstall it if user has installed it via the Excel add-in manager
                var oAddinCount = (int)oAddins.GetType().InvokeMember("Count", BindingFlags.GetProperty, null, oAddins, null);
                var oParam = new object[1];
                for (var i = 1; i <= oAddinCount; i++)
                {
                    oParam[0] = i;
                    var addIn = oAddins.GetType().InvokeMember("Item", BindingFlags.GetProperty, null, oAddins, oParam);
                    string addinName = (string)addIn.GetType().InvokeMember("Fullname", BindingFlags.GetProperty, null, addIn, null);
                    if (addinName.EndsWith("StatsDirect3ExcelLink.xla"))
                    {
                        oParam[0] = "False";
                        addIn.GetType().InvokeMember("Installed", BindingFlags.SetProperty, null, addIn, oParam);
                    }
                }

                // Second possibility: Get the excel paths and remove the xlam from the startup path if StatsDirect has activated it by putting it there
                string startpath = (string)excelObject.GetType().InvokeMember("StartupPath", BindingFlags.GetProperty, null, excelObject, null);
                string librarypath = (string)excelObject.GetType().InvokeMember("LibraryPath", BindingFlags.GetProperty, null, excelObject, null);
                if (File.Exists(Path.Combine(startpath, Sdxla)))
                    File.Delete(Path.Combine(startpath, Sdxla));
                if (File.Exists(Path.Combine(librarypath, Sdxla)))
                    File.Delete(Path.Combine(librarypath, Sdxla));

                //Clean up: Close Excel
                excelObject.GetType().InvokeMember("quit", BindingFlags.InvokeMethod, null, excelObject, null);
            }
            catch (Exception)
            {
            }
        }
    }
}
