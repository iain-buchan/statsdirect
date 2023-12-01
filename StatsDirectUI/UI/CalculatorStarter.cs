using System.Windows.Forms;
using StatsDirect.Calculator;

namespace StatsDirect.UI
{
    static class CalculatorStarter
    {
        public static void Start()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using frmStatsDirectCalculator mainWindow = new();
            Application.Run(mainWindow);
        }
    }
}