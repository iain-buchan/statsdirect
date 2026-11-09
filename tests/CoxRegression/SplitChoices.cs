// Cox regression: the plots of survival and hazard are split by the choice made in the list that the regression offers, known by its place
// in the list: "Strata" alone for a stratified model, and otherwise each binary predictor in turn and then "None". A predictor titled
// "None" or "Strata" is split by when it is chosen (the first was taken for no split, the second for the strata), and of two binary
// predictors with the same title the one chosen (the first of them was taken).
using StatsDirect.Builtins;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Templates;

internal static partial class Program
{
    private static void SplitChoices()
    {
        Console.WriteLine("The choice the plots are split by");
        double[] time = { 6, 7, 10, 15, 19, 25, 2, 4, 8, 12, 14, 20, 23, 30 }, code = { 1, 1, 1, 1, 0, 1, 1, 1, 1, 0, 1, 1, 1, 0 };
        double[] treated = { 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1 }, female = { 1, 1, 0, 0, 1, 0, 1, 1, 0, 0, 0, 1, 1, 0 };
        double[] age = { 51, 63, 47, 58, 66, 49, 71, 55, 60, 44, 68, 52, 57, 62 };
        void Case(string title, (string title, double[] values)[] predictors, int choice, int split, bool stratified = false)
        {
            try
            {
                ParameterBag bag = new();
                bag.AddInput("times", new DataFrame(new DoubleVariable((double[])time.Clone(), "Time")));
                bag.AddInput("events", new DataFrame(new DoubleVariable((double[])code.Clone(), "Event")));
                DataFrame frame = new();
                foreach (var (name, values) in predictors) frame.Variables.Add(new DoubleVariable((double[])values.Clone(), name));
                bag.AddInput("predictors", frame);
                if (stratified)
                {
                    ClassifierVariable strata = new();
                    strata.Title = "Stratum";
                    strata.Data = Enumerable.Range(0, time.Length).Select(i => i < 7 ? 0.0 : 1.0).ToArray();
                    bag.AddInput("strata", new DataFrame(strata));
                }
                bag.AddInput("accuracy", 1e-10);
                bag.AddInput("centre-continuous-covariates", true);
                StepOutput fit = Coxreg.RptCoxRegression(bag);
                int choices = ((StringVariable)fit.ParameterBag["subgroups"].AsDataFrame.Variables[0]).Length;
                ParameterBag forPlots = PassedOn(bag, fit, "use-tics", true);
                forPlots["use-markers"] = FilledParameterFactory.Input(true);
                bool[] group = new bool[choices];
                group[choice] = true;
                forPlots["group"] = FilledParameterFactory.Input(group);
                List<ChartOptions> charts = Rows(Coxreg.RptCoxHazardPlots(forPlots), "*chart").Select(row => ((ChartDefinition)row["chart"].AsObject).ChartOptions).ToList();
                var survival = (CoxSurvivalOrHazardOptions)charts[0];
                bool ok = charts.Count == (split > 0 ? 3 : 2) && survival.grouped == split > 0 && survival.stratified == stratified &&
                    (split == 0 || survival.groupid == split && ((Cox2Options)charts[2]).groupid == split);
                Say(ok, title + ": choice " + choice + " of " + choices + (split > 0 ? " splits by predictor " + split : stratified ? " splits by the strata" : " does not split") +
                    $" (charts {charts.Count}, grouped {survival.grouped}, predictor {survival.groupid}, stratified {survival.stratified})", true);
            }
            catch (Exception ex)
            {
                Say(false, title + ": " + Message(ex), true);
            }
        }
        Case("a binary predictor titled None", new[] { ("None", treated), ("Age", age) }, 0, 1);
        Case("no split beside a predictor titled None", new[] { ("None", treated), ("Age", age) }, 1, 0);
        Case("a binary predictor titled Strata, without strata", new[] { ("Age", age), ("Strata", treated) }, 0, 2);
        Case("the second of two binary predictors titled Z", new[] { ("Z", treated), ("Z", female), ("Age", age) }, 1, 2);
        Case("the first of two binary predictors titled Z", new[] { ("Z", treated), ("Z", female), ("Age", age) }, 0, 1);
        Case("a binary predictor titled none, in lower case", new[] { ("none", treated) }, 0, 1);
        Case("the strata of a stratified model", new[] { ("None", treated), ("Age", age) }, 0, 0, stratified: true);
    }
}
