using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Checks of the operation definitions (StatsDirectUI/Assets/Operations/*.xml) that need no build of the program.  A parameter may be
// asked for on a condition (acquire-if-true), an expression over the parameters acquired so far; a condition that names a parameter
// which is itself asked for only on a condition must first ask whether it is there (parameters.ContainsKey), because the bag's
// indexer throws "Cannot find parameter" for one that is not, and the operation stops.  Run with: dotnet run -c Release
static class Program
{
    private static int Main()
    {
        string folder = typeof(Program).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(a => a.Key == "Operations").Value;
        if (!Directory.Exists(folder))
        {
            Console.WriteLine("FAIL  the folder of the definitions is not there: " + folder);
            Console.WriteLine("1 CHECKS FAILED");
            return 1;
        }
        int files = 0, conditions = 0, failures = 0;
        Regex named = new("parameters\\[\"([^\"]+)\"\\]");
        foreach (string file in Directory.GetFiles(folder, "*.xml").OrderBy(f => f, StringComparer.Ordinal))
        {
            XDocument doc = XDocument.Load(file);
            XNamespace ns = doc.Root.Name.Namespace;
            var parameters = doc.Descendants(ns + "parameters").Elements()
                .Where(e => e.Element(ns + "name") != null)
                .Select(e => (name: e.Element(ns + "name").Value, condition: e.Element(ns + "acquire-if-true")?.Value))
                .ToList();
            HashSet<string> conditional = parameters.Where(p => p.condition != null).Select(p => p.name).ToHashSet();
            files++;
            foreach ((string name, string condition) in parameters.Where(p => p.condition != null))
            {
                conditions++;
                foreach (Match m in named.Matches(condition))
                {
                    string other = m.Groups[1].Value;
                    if (!conditional.Contains(other) || condition.Contains("ContainsKey(\"" + other + "\")"))
                        continue;
                    failures++;
                    Console.WriteLine($"FAIL  {Path.GetFileName(file)}: {name} is asked for on a condition that names {other}, which is itself asked for only on a condition, without asking whether it is there");
                }
            }
        }
        Console.WriteLine($"ok    {files} definitions read, {conditions} conditions of asking looked at");
        Console.WriteLine(failures == 0 ? "ALL CHECKS PASS" : $"{failures} CHECKS FAILED");
        return failures == 0 ? 0 : 1;
    }
}
