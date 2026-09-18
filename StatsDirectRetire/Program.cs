using System;
using System.Linq;

namespace StatsDirect.Setup
{
    /// <summary>
    /// The installer's errand: see <see cref="OldSetupRetirer"/>.  The installer runs "dotnet StatsDirectRetire.dll" as the system account, with nothing to see.
    /// Run by hand with -whatif, it says what it would retire and changes nothing.
    /// </summary>
    static class Program
    {
        static int Main(string[] args)
        {
            bool whatIf = args.Any(arg => "-whatif".Equals(arg, StringComparison.OrdinalIgnoreCase));
            OldSetupRetirer.Run(whatIf, whatIf ? Console.WriteLine : null);
            return 0;
        }
    }
}
