using System;
using System.Linq;

namespace StatsDirect.Setup
{
    /// <summary>
    /// The installer's errands: see <see cref="OldSetupRetirer"/> and <see cref="ExcelAddInRetirer"/>.  The installer runs "dotnet StatsDirectRetire.dll" as the system account, with nothing to see.
    /// Run by hand with -whatif, it says what it would retire and changes nothing; with -excel-addin-only as well, it leaves the old setups out.
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            bool whatIf = args.Any(arg => "-whatif".Equals(arg, StringComparison.OrdinalIgnoreCase));
            if (!args.Any(arg => "-excel-addin-only".Equals(arg, StringComparison.OrdinalIgnoreCase)))
                OldSetupRetirer.Run(whatIf, whatIf ? Console.WriteLine : null);
            ExcelAddInRetirer.RunMachine(whatIf, whatIf ? Console.WriteLine : null);
            return 0;
        }
    }
}
