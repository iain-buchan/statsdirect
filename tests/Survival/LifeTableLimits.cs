// The life tables with data that a table cannot be made from: each should be refused with a message, not stop the analysis or give a
// table that is wrong.
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void Refused(string what, Func<ParameterBag> report, string saying)
    {
        string message = null;
        try { report(); }
        catch (Exception ex) { message = Message(ex); }
        Say(message != null && message.StartsWith("TemplateOperationCancelledException") && message.Contains(saying), what + ": refused, with a message that has \"" + saying + "\"" + (message == null ? " (a table was given)" : " (" + message.Substring(0, Math.Min(90, message.Length)) + ")"), true);
    }

    private static ParameterBag AbridgedReport(double[] lengths, double[] population, double[] deaths, double[] fraction = null, double[] weights = null)
    {
        ParameterBag bag = new();
        bag.AddInput("gamma", 0.95);
        bag.AddInput("intervals", new DataFrame(new DoubleVariable((double[])lengths.Clone(), "Length")));
        bag.AddInput("population", new DataFrame(new DoubleVariable((double[])population.Clone(), "Population")));
        bag.AddInput("deaths", new DataFrame(new DoubleVariable((double[])deaths.Clone(), "Deaths")));
        if (fraction != null) bag.AddInput("fractions", new DataFrame(new DoubleVariable((double[])fraction.Clone(), "Fraction")));
        if (weights != null) bag.AddInput("weights", new DataFrame(new DoubleVariable((double[])weights.Clone(), "Healthy")));
        bag.AddInput("iterations", "3000");
        bag.AddInput("save", false);
        return Survival.RptAbridgedLifetable(new PlainWithProgress(), bag).ParameterBag;
    }

    private static ParameterBag FollowUpReport(double[] time, double[] deaths, double[] withdrawn, double start)
    {
        ParameterBag bag = new();
        bag.AddInput("gamma", 0.95);
        bag.AddInput("times", new DataFrame(new DoubleVariable(time, "Interval")));
        bag.AddInput("deaths", new DataFrame(new DoubleVariable(deaths, "Deaths")));
        bag.AddInput("withdrawals", new DataFrame(new DoubleVariable(withdrawn, "Withdrawn")));
        bag.AddInput("natst", start);
        return Survival.RptFollowUpLifetable(bag).ParameterBag;
    }

    private static void LifeTableLimits()
    {
        Console.WriteLine();
        Console.WriteLine("The life tables with data that a table cannot be made from");
        double[] lengths = { 1, 4, 10, 10 }, population = { 1000, 4000, 9000, 8000, 3000 }, deaths = { 10, 4, 9, 40, 300 };
        Refused("abridged, a blank population", () => AbridgedReport(lengths, new double[] { 1000, M, 9000, 8000, 3000 }, deaths), "blank cell in row 2");
        Refused("abridged, a blank number of deaths", () => AbridgedReport(lengths, population, new double[] { 10, 4, M, 40, 300 }), "blank cell in row 3");
        Refused("abridged, a blank length", () => AbridgedReport(new double[] { 1, M, 10, 10 }, population, deaths), "blank cell in row 2");
        Refused("abridged, no deaths in the open interval", () => AbridgedReport(lengths, population, new double[] { 10, 4, 9, 40, 0 }), "open interval");
        Refused("abridged, a population of nothing", () => AbridgedReport(lengths, new double[] { 1000, 0, 9000, 8000, 3000 }, deaths), "more than zero");
        Refused("abridged, a length of nothing", () => AbridgedReport(new double[] { 1, 0, 10, 10 }, population, deaths), "more than zero");
        Refused("abridged, a row too few of populations", () => AbridgedReport(lengths, new double[] { 1000, 4000, 9000, 8000 }, deaths), "one more row");
        Refused("abridged, a row too many of lengths", () => AbridgedReport(new double[] { 1, 4, 10, 10, 10 }, population, deaths), "one more row");
        Refused("abridged, a blank fraction", () => AbridgedReport(lengths, population, deaths, new double[] { 0.1, M, 0.5, 0.5 }), "blank cell in row 2 of the fractions");
        Refused("abridged, a blank weight", () => AbridgedReport(lengths, population, deaths, null, new double[] { 1, 1, 0.9, M, 0.7 }), "blank cell in row 4 of the weights");
        try
        {
            ParameterBag o = AbridgedReport(lengths, population, new double[] { 10, 0, 9, 40, 300 });
            Say(double.IsFinite(o["elb"].AsDouble) && o["elb_lci"].AsDouble == M, "abridged, an interval without a death before the open interval: a table without the limits by formula", true);
        }
        catch (Exception ex) { Say(false, "abridged, an interval without a death: " + Message(ex), true); }

        Refused("follow-up, fewer alive at the start than die or withdraw", () => FollowUpReport(new double[] { 0, 1, 2 }, new double[] { 10, 10, 10 }, new double[] { 5, 5, 5 }, 20), "less than the deaths and withdrawals");
        Refused("follow-up, a number of deaths below nothing", () => FollowUpReport(new double[] { 0, 1, 2 }, new double[] { -3, 5, 5 }, new double[] { 0, 0, 0 }, 30), "below zero");
        Refused("follow-up, a number withdrawn below nothing", () => FollowUpReport(new double[] { 0, 1, 2 }, new double[] { 3, 5, 5 }, new double[] { 0, -1, 0 }, 30), "below zero");
        try
        {
            List<ParameterBag> rows = Rows(FollowUpReport(new double[] { 0, 1, 2 }, new double[] { 30, 0, 0 }, new double[] { 0, 0, 0 }, 30), "*survival");
            Check("follow-up, everybody dead in the first interval: survivors of 100, 0 and 0 per cent", Math.Abs(rows[0]["lx"].AsDouble - 100) + Math.Abs(rows[1]["lx"].AsDouble) + (double.IsNaN(rows[2]["lx"].AsDouble) ? 1 : Math.Abs(rows[2]["lx"].AsDouble)), 0, true);
        }
        catch (Exception ex) { Say(false, "follow-up, everybody dead in the first interval: " + Message(ex), true); }
    }
}
