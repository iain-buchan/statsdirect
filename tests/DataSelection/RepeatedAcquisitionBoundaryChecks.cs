using System.Reflection;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.UI;
using StatsDirect.Utilities;

// Exercise the actual wide repeated-measures acquisition helper and Gidx3's
// pivot/cancellation returns without an input dialog.
internal static class RepeatedAcquisitionBoundaryChecks
{
    const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static readonly Type processor = typeof(CellSelection).Assembly.GetType("StatsDirect.UI.GridSelectionProcessor");
    static readonly MethodInfo readRepeat = processor.GetMethod("ReadRepeatedObservations", flags);
    static readonly MethodInfo longReader = processor.GetMethod("Gidx3", flags);
    static int checks, failures;
    static void Check(bool ok, string text) { checks++; if (!ok) failures++; Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  repeated acquisition: {text}"); }
    static DataFrame Wide(MatchedCells cells, (int column, int first, int count)[] ranges, DataFrame reference)
    {
        IGrid grid = DispatchProxy.Create<IGrid, MatchedGridProxy>();
        var proxy = (MatchedGridProxy)(object)grid; proxy.Cells = cells; proxy.Selection = cells.Selection(ranges);
        object[] arguments = { reference?.MaxRows ?? 0, reference == null ? 1 : 2, null, 8, reference?.Variables[0].Origin, false, false };
        using var guard = new MatchedSelectionChecks.UnexpectedDialogGuard();
        var frame = (DataFrame)readRepeat.Invoke(Activator.CreateInstance(processor, new object[] { grid }), arguments);
        if (guard.Blocked || proxy.ReadCount != 1 || (bool)arguments[5] || (bool)arguments[6])
            throw new InvalidOperationException("Valid repeated wide selection unexpectedly warned, retried or cancelled.");
        return frame;
    }
    static void RepeatCase(string name, int header, int[] hidden = null, bool hideHeader = false)
    {
        object[][] raw = { new object[] { null,1.0,2.0,3.0,4.0 }, new object[] { 10.0,40.0,20.0,90.0,null },
            new object[] { null,11.0,12.0,13.0,14.0 }, new object[] { 20.0,60.0,30.0,100.0,null } };
        hidden ??= Array.Empty<int>();
        var columns = header > 0 ? raw.Select((c,i) => new[] {
            header == 3 && i >= 2 ? null : (object)("Treatment " + (i % 2))
        }.Concat(c).ToArray()).ToArray() : raw;
        var hiddenRows = hidden.Select(i => i + (header > 0 ? 1 : 0)).Concat(hideHeader ? new[] {0} : Array.Empty<int>()).ToArray();
        var cells = new MatchedCells(columns, hiddenRows);
        int first = header == 2 ? 1 : 0, count = columns[0].Length - first;
        var firstRepeat = Wide(cells, new[] { (0,first,count), (1,first,count) }, null);
        var secondRepeat = Wide(cells, new[] { (2,first,count), (3,first,count) }, firstRepeat);
        var subjects = Enumerable.Range(0,5).Except(hidden).ToArray();
        Check(firstRepeat.MinRows == subjects.Length && secondRepeat.MinRows == subjects.Length, name + " retains all selected subject positions in both repeats");
        for (int repeat = 0; repeat < 2; repeat++)
            for (int treatment = 0; treatment < 2; treatment++)
            {
                var frame = repeat == 0 ? firstRepeat : secondRepeat;
                var expected = subjects.Select(r => MatchedSelectionChecks.Number(raw[repeat * 2 + treatment][r]));
                Check(((DoubleVariable)frame.Variables[treatment]).Data.SequenceEqual(expected),
                    name + " keeps repeat " + repeat + ", treatment " + treatment + " on the original subjects");
            }
    }
    static void LongReturn(string name, int step, string action)
    {
        var cells = new MatchedCells(new[] { new object[] { "A","A","B","B" }, new object[] { "x","y","x","y" },
            new object[] { 1.0,2.0,3.0,4.0 } });
        IGrid grid = DispatchProxy.Create<IGrid, RepeatedBoundaryGridProxy>();
        var proxy = (RepeatedBoundaryGridProxy)(object)grid; proxy.Cells = cells; proxy.Step = step; proxy.Action = action;
        object[] arguments = { 2,2,0,null,DataAcquisitionMode2D.GroupThenBlock,false,false,9 };
        try
        {
            object result = longReader.Invoke(Activator.CreateInstance(processor,new object[] {grid}),arguments);
            Check(action != "cancel" && result == null && (bool)arguments[6] == (action == "pivot"),
                name + " returns safely without dereferencing a missing frame");
        }
        catch (TargetInvocationException ex)
        {
            Check(action == "cancel" && ex.InnerException is TemplateOperationCancelledException cancelled && !cancelled.ShouldShowError,
                name + " preserves explicit user cancellation");
        }
    }
    internal static int Run()
    {
        checks = failures = 0;
        try
        {
            RepeatCase("wide repeat opposite-edge missing values",0);
            RepeatCase("wide repeat selected headers",1);
            RepeatCase("wide repeat excluded headers",2);
            RepeatCase("wide later repeat empty headers",3);
            RepeatCase("wide repeat hidden subject",1,new[] {2});
            RepeatCase("wide repeat hidden header",1,hideHeader:true);
            foreach (int step in new[] {1,2,3})
                foreach (string action in new[] {"pivot","cancel","null"})
                    LongReturn("long step " + step + " / " + action,step,action);
        }
        catch (Exception ex) { Check(false,"unexpected exception: " + ex); }
        Console.WriteLine($"REPEATED ACQUISITION: {checks} checks, {failures} failures");
        return failures;
    }
}

public class RepeatedBoundaryGridProxy : MatchedGridProxy
{
    internal int Step;
    internal string Action;
    int reads;
    protected override object Invoke(MethodInfo method,object[] arguments)
    {
        if (method.Name == "ClearSelection") return null;
        if (method.Name == "GetCellArray")
        {
            reads++; arguments[8] = false; arguments[9] = false;
            if (reads == Step)
            {
                arguments[8] = Action == "cancel"; arguments[9] = Action == "pivot";
                return null;
            }
            Selection = Cells.Selection(new[] {(reads-1,0,4)});
        }
        return base.Invoke(method,arguments);
    }
}
