using StatsDirect.R;
using System.Text.Json;

internal static partial class Program
{
    private static void CheckRound2Host(string folder)
    {
        // Check the byte patterns independently in R, including the lowest subnormal,
        // largest subnormal, extreme normal values, and both signs of zero.
        double[] values =
        {
            0.10000000000000053, -1.1000000000000005, 1.234567890123456, double.Epsilon,
            BitConverter.UInt64BitsToDouble(0x000f_ffff_ffff_ffff), double.MaxValue, -double.MaxValue,
            0.0, -0.0, Math.ScaleB(1, -1022), Math.ScaleB(1, 1000)
        };
        string literal = RScriptWriter.Vector(values.Cast<object>().ToArray());
        Check(literal.Contains("0x1.99999999999cp-4") && literal.Contains("-0x1.199999999999cp0"),
            "the long decimal literals are written using their exact hexadecimal significands");
        Check(RScriptWriter.Vector(new object[] { 1.25, -0.0, 5.0, null, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            == "c(1.25, -0, 5, NA, NaN, Inf, -Inf)", "short numbers and special values retain their former script text");
        string bytes = Convert.ToHexString(values.SelectMany(BitConverter.GetBytes).ToArray()).ToLowerInvariant();
        string result = DeferredR538("x <- " + literal + "\n"
            + "stopifnot(paste(sprintf('%02x', as.integer(writeBin(x, raw(), size=8L, endian='little'))), collapse='') == '" + bytes + "')\n"
            + "cat('EXACT_NUMBERS_OK\\n')\n", Path.Combine(folder, "exact-bits"), 30);
        Check(result.Contains("EXACT_NUMBERS_OK"), "R reads the generated literals as the original doubles");

        using var tableRecord = JsonDocument.Parse("""
            {"operation":"UnknownForR","inputs":[{"name":"table","kind":"table","counts":[[0.10000000000000053,2]],"rowLabels":["row"],"columnLabels":["a","b"]}],"parameters":{"threshold":0.10000000000000053}}
            """);
        string table = RScriptWriter.Write(tableRecord.RootElement, null, null).Text;
        Check(table.Contains("# counts <- matrix(c(0.10000000000000053, 2)") &&
            table.Contains("\ncounts <- matrix(c(0x1.99999999999cp-4, 2)") &&
            table.Contains("threshold <- 0x1.99999999999cp-4   # 0.10000000000000053"),
            "the matrix and scalar assignments include the original decimals in comments");

        foreach (double shift in new[] { 0.1, 1.1 })
        {
            double[] valuesBefore = Enumerable.Range(0, 6).Select(i => Math.Round(5.1 + 1.1 * i, 10)).ToArray();
            double[] differences = valuesBefore.Select(value => value - Math.Round(value - shift, 10)).ToArray();
            var cells = new RecordCells(Cells539("Differences", differences));
            var (run, output) = Run("TPaired", new() { ["data"] = Select539(cells, differences.Length + 1, 0), ["gamma"] = 0.95, ["doAgreement"] = false });
            RScript script = RScriptWriter.Write(Record(run), null, cells.Read);
            string[] bindings = script.Text.Split('\n').Where(line => line.StartsWith("# before <- c(") || line.StartsWith("before <- c(")).ToArray();
            Check(bindings.Length == 2 && bindings[0].StartsWith("# before <- c(" + differences[0].ToString("R", inv)) &&
                bindings[1].Contains("0x"), "paired t replay keeps the former decimal vector as a comment (" + string.Join(" | ", bindings) + ")");
            Replay539(run, Equal539("r$statistic", new[] { Num539(output.ParameterBag, "t") }, 1e-12),
                Path.Combine(folder, "paired-" + shift.ToString("R", inv)), cells.Read);
        }
    }
}
