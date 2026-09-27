// The lists of the categories of two classifications, made the same by putting into each the categories that only the other has
// (Tables.XSymmetriseXtab, which the measures of agreement of two raters use with lists that start at element 0, and Crosstabs with lists
// that start at element 1).
using System.Reflection;
using StatsDirect.Builtins;

internal static partial class Program
{
    private static void Symmetrise()
    {
        Console.WriteLine();
        Console.WriteLine("The categories of two classifications made the same");
        Type tables = typeof(Tables), namevar = tables.GetNestedType("Namevar", BindingFlags.NonPublic);
        MethodInfo method = tables.GetMethod("XSymmetriseXtab", BindingFlags.NonPublic | BindingFlags.Static);
        PropertyInfo label = namevar.GetProperty("Title"), value = namevar.GetProperty("X");
        (string[] first, string[] second, string what)[] cases =
        {
            (new[] { "a", "b", "c" }, new[] { "a", "b" }, "the last category missing from the second"),
            (new[] { "a", "b" }, new[] { "a", "b", "c" }, "the last category missing from the first"),
            (new[] { "a", "b", "c", "d" }, new[] { "a", "b" }, "the last two missing from the second"),
            (new[] { "b", "c" }, new[] { "a", "b", "c" }, "the first category missing from the first"),
            (new[] { "a", "c" }, new[] { "a", "b", "c" }, "a middle category missing from the first"),
            (new[] { "a", "b" }, new[] { "c", "d" }, "no category in common"),
            (new[] { "1", "2", "10" }, new[] { "2", "10", "30" }, "numbers as labels, one missing from each"),
            (new[] { "a", "b", "c" }, new[] { "a", "b", "c" }, "the same categories in both")
        };
        foreach (int lowerBound in new[] { 0, 1 })
            foreach ((string[] first, string[] second, string what) in cases)
            {
                string title = $"lists from element {lowerBound}, {what}";
                try
                {
                    Array Make(string[] labels)
                    {
                        Array list = Array.CreateInstance(namevar, labels.Length + lowerBound);
                        for (int i = 0; i < labels.Length; i++) list.SetValue(Activator.CreateInstance(namevar, labels[i], (double)(i + 100)), i + lowerBound);
                        return list;
                    }
                    object[] arguments = { first.Length, Make(first), second.Length, Make(second), lowerBound };
                    method.Invoke(null, arguments);
                    string[] all = first.Concat(second).Distinct().OrderBy(l => l, Comparer<string>.Create(CompareLabels)).ToArray();
                    bool right = (int)arguments[0] == all.Length && (int)arguments[2] == all.Length;
                    foreach ((int list, string[] own) in new[] { (1, first), (3, second) })
                    {
                        Array made = (Array)arguments[list];
                        for (int i = 0; right && i < all.Length; i++)
                        {
                            object entry = made.GetValue(i + lowerBound);
                            int was = Array.IndexOf(own, all[i]);
                            // a category of its own keeps its value; one put in has the value that no rating has
                            right = entry != null && (string)label.GetValue(entry) == all[i] && (double)value.GetValue(entry) == (was >= 0 ? was + 100 : double.MaxValue);
                        }
                    }
                    Say(right, title + ": both lists hold every category, in order");
                    if (right) Console.WriteLine("ok    " + title);
                }
                catch (Exception ex) { checks++; failures++; Console.WriteLine("FAIL  " + title + ": " + Message(ex)); }
            }
    }
}
