using System.Text;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using StatsDirect.Data;

using System;
using System.Collections.Generic;
namespace StatsDirect.Builtins
{
    public class Describe
    {
        private class SortGroupByTitleAscending : IComparer<Group>
        {
            private static int Compare(Group x, Group y)
            {
                if (x.Label.Equals(y.Label))
                {
                    return 0;
                }

                //  If both titles are numeric, compare numerically; else, compare as text
                bool lower;
                double numericX;
                double numericY;
                if (double.TryParse(x.Label, out numericX) && double.TryParse(y.Label, out numericY))
                {
                    lower = numericX <= numericY;
                }
                else
                {
                    lower = String.CompareOrdinal(x.Label, y.Label) < 0;
                }

                return lower ? -1 : 1;
            }

            // interface methods implemented by Compare
            int IComparer<Group>.Compare(Group x, Group y)
            {
                return Compare(x, y);
            }
        }

        private class SortGroupByTitleDescending : IComparer<Group>
        {
            private static int Compare(Group x, Group y)
            {
                if (x.Label.Equals(y.Label))
                {
                    return 0;
                }

                //  If both titles are numeric, compare numerically; else, compare as text
                bool lower;
                double numericX;
                double numericY;
                if (double.TryParse(x.Label, out numericX) && double.TryParse(y.Label, out numericY))
                {
                    lower = numericX <= numericY;
                }
                else
                {
                    lower = String.CompareOrdinal(x.Label, y.Label) < 0;
                }

                return lower ? 1 : -1;
            }

            // interface methods implemented by Compare
            int IComparer<Group>.Compare(Group x, Group y)
            {
                return Compare(x, y);
            }
        }

        private class SortGroupByNbinAscending : IComparer<Group>
        {
            private static int Compare(Group x, Group y)
            {
                return x.NBin == y.NBin ? 0 : x.NBin < y.NBin ? -1 : 1;
            }

            // interface methods implemented by Compare
            int IComparer<Group>.Compare(Group x, Group y)
            {
                return Compare(x, y);
            }
        }

        private class SortGroupByNbinDescending : IComparer<Group>
        {
            private static int Compare(Group x, Group y)
            {
                return x.NBin == y.NBin ? 0 : x.NBin < y.NBin ? 1 : -1;
            }

            // interface methods implemented by Compare
            int IComparer<Group>.Compare(Group x, Group y)
            {
                return Compare(x, y);
            }
        }

        public static StepResult RptPreferences(ITemplateHost host, ParameterBag parameters)
        {
            const string pg = "Preference Groups";

            int seed = parameters["seed"].AsInt32;
            DataFrame capacitiesFrame = parameters["capacities"].AsDataFrame;
            DoubleVariable capacitiesVariable = capacitiesFrame.Variables[0].AsDoubleVariable;
            int groups = capacitiesVariable.Length;
            int[] groupCapacities = new int[groups + 1];
            int capacity = 0;
            for (int i = 1; i <= groups; i++)
            {
                groupCapacities[i] = (int)capacitiesVariable.Data[i - 1];
                capacity += groupCapacities[i];
            }
            DataFrame preferencesFrame = parameters["preferences"].AsDataFrame;
            int preferences = preferencesFrame.VariableCount;
            int subjects = preferencesFrame.Variables[0].Length;
            int[,] x = new int[preferences + 1, subjects + 1];
            for (int i = 1; i <= preferences; i++)
            {
                DoubleVariable preferencesVariable = preferencesFrame.Variables[i - 1].AsDoubleVariable;
                for (int j = 1; j <= subjects; j++)
                {
                    x[i, j] = (int)preferencesVariable.Data[j - 1];
                    if (x[i, j] < 1 || x[i, j] > groups)
                    {
                        host.Error("invalid preference in group " + i.ToString() + "at row " + j.ToString(), pg);
                        throw new TemplateOperationCancelledException();
                    }
                }
            }
            if (groups < preferences)
            {
                host.Error("fewer groups than preferences", pg);
                throw new TemplateOperationCancelledException();
            }
            if (capacity < Convert.ToDouble(subjects))
            {
                host.Error("more subects (" + subjects.ToString() + ") than total capacity of groups (" + capacity.ToString() + ")", pg);
                throw new TemplateOperationCancelledException();
            }
            bool[] done = new bool[subjects + 1];
            int[] allocatedGroup = new int[subjects + 1];
            int[] allocatedSoFar = new int[groups + 1];
            int[] toConsider = new int[subjects + 2];
            MersenneTwister mt = new MersenneTwister(seed);
            int ok = 0;
            // First allocate according to preferences where possible - 1st preference, then 2nd preference, etc..
            for (int preference = 1; preference <= preferences; preference++)
            {
                for (int grp = 1; grp <= groups; grp++)
                {
                    // If the group is already full, there's no point trying to assign any more at this preference
                    if (allocatedSoFar[grp] < groupCapacities[grp])
                    {
                        // Gather all subjects who have expressed a preference here for group grp
                        int underConsideration = 0;
                        for (int subject = 1; subject <= subjects; subject++)
                        {
                            if (x[preference, subject] == grp && !done[subject])
                            {
                                underConsideration++;
                                toConsider[underConsideration] = subject;
                            }
                        }
                        // Allocate those who want this group to it; if it's over-subscribed, shuffle the candidates so that all have an equal chance to get their choice.
                        if (underConsideration > 0)
                        {
                            Shuffle(mt, toConsider, 1, underConsideration);
                            int space = groupCapacities[grp] - allocatedSoFar[grp];
                            int successfulCandidates = Math.Min(space, underConsideration);
                            for (int toAllocate = 1; toAllocate <= successfulCandidates; toAllocate++)
                            {
                                int subject = toConsider[toAllocate];
                                allocatedGroup[subject] = grp;
                                done[subject] = true;
                                allocatedSoFar[grp]++;
                                ok++;
                            }
                        }
                    }
                }
            }

            // By now, we've assigned by preference wherever possible.  Allocate any remaining subjects randomly to groups that have space.
            while (ok < subjects)
            {
                // Put groups with remaining space into toConsider...
                int availableGroups = 0;
                for (int grp = 1; grp <= groups; grp++)
                {
                    if (allocatedSoFar[grp] < groupCapacities[grp])
                    {
                        for (int k = 1; k <= groupCapacities[grp] - allocatedSoFar[grp]; k++)
                        {
                            availableGroups++;
                            toConsider[availableGroups] = grp;
                        }
                    }
                }
                // ... and shuffle them so that they're filled in random order
                Shuffle(mt, toConsider, 1, availableGroups);

                // Find unallocated subjects and allocate one to a random group until we run out of subjects or groups.
                int groupToUse = 0;
                for (int k = 1; k <= subjects; k++)
                {
                    if (!done[k])
                    {
                        groupToUse++;
                        allocatedGroup[k] = toConsider[groupToUse];
                        ok++;
                    }

                    // Go round again if we have more subjects than groups into which to place them in this pass
                    if (groupToUse >= availableGroups)
                        break;
                }
            }
            //  RTF_LoadTemplate("prefer.rtf") Then
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("groups", groups.ToString());
            outputParameters.AddOutput("capacity", capacity.ToString());
            outputParameters.AddOutput("subjects", subjects.ToString());
            outputParameters.AddOutput("seed", seed.ToString());
            IList<ParameterBag> groupsList = new List<ParameterBag>();
            outputParameters.AddOutput("*groups", groupsList);
            for (int i = 1; i <= subjects; i++)
            {
                ParameterBag groupsParameters = new ParameterBag();
                groupsList.Add(groupsParameters);
                groupsParameters.AddOutput("sub", i.ToString());
                groupsParameters.AddOutput("grp", allocatedGroup[i].ToString());
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        /// <summary>
        /// Randomly change the order of items ary[lowerBound] to ary[upperBound] inclusive, taking random numbers from mt.
        /// </summary>
        private static void Shuffle(MersenneTwister mt, int[] ary, int lowerBound, int upperBound)
        {
            if (upperBound - lowerBound <= 0)
                return;
            {
                for (int tn = 1; tn <= 3; tn++)
                {
                    for (int k = lowerBound; k <= upperBound; k++)
                    {
                        int nrp = Convert.ToInt32((upperBound - lowerBound - 1) * mt.NextDouble()) + lowerBound;
                        int tp = ary[k];
                        ary[k] = ary[nrp];
                        ary[nrp] = tp;
                    }
                }
            }
        }

        public static StepResult RptFrequency(ITemplateHost host, ParameterBag parameters)
        {
            DataFrame data = parameters["data"].AsDataFrame;
            for (int v = 0; v <= data.VariableCount - 1; v++)
            {
                if (!(data.Variables[v].IsClassifier))
                    data.Variables[v] = TemplateProcessor.gidx_bins(data.Variables[v].AsDoubleVariable);
            }

            bool shouldSortByValue = "value".Equals(parameters["sortBy"].AsString);
            bool shouldSortAscending = "asc".Equals(parameters["sortOrder"].AsString);

            //  RTF_LoadTemplate("freq.rtf")
            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> variableList = new List<ParameterBag>();
            outputParameters.AddOutput("*variable", variableList);
            foreach (Variable v in data.Variables)
            {
                ClassifierVariable vc = v.AsClassifierVariable;
                ParameterBag variableParameters = new ParameterBag();
                variableList.Add(variableParameters);
                variableParameters.AddOutput("ti", vc.Title);
                variableParameters.AddOutput("n", vc.Length.ToString());
                int cm = 0;
                int xtot = vc.Length;
                int bins = vc.GroupCount;
                Group[] bin = new Group[bins + 1 ];
                int i;
                for (i = 1; i <= bins; i++)
                {
                    bin[i] = vc.get_Group(i - 1);
                    if (bin[i].Label == Formatting.MISSINGLABEL)
                        xtot -= bin[i].NBin;
                }

                IComparer<Group> comparer;
                if (shouldSortByValue)
                {
                    if (shouldSortAscending)
                        comparer = new SortGroupByTitleAscending();
                    else
                        comparer = new SortGroupByTitleDescending();
                }
                else
                {
                    if (shouldSortAscending)
                        comparer = new SortGroupByNbinAscending();
                    else
                        comparer = new SortGroupByNbinDescending();
                }
                Array.Sort(bin, 1, bins, comparer);

                List<ParameterBag> binList = new List<ParameterBag>();
                variableParameters.AddOutput("*bin", binList);
                for (i = 1; i <= bins; i++)
                {
                    int xn = bin[i].NBin;
                    ParameterBag binParameters = new ParameterBag();
                    binList.Add(binParameters);
                    binParameters.AddOutput("x", bin[i].Label);
                    binParameters.AddOutput("fx", xn.ToString());
                    if (bin[i].Label != Formatting.MISSINGLABEL)
                    {
                        binParameters.AddOutput("%", host.RoundU(100.0 * Convert.ToDouble(xn) / Convert.ToDouble(xtot)));
                        cm = cm + xn;
                        binParameters.AddOutput("cm", cm.ToString());
                        binParameters.AddOutput("%2", host.RoundU(100.0 * Convert.ToDouble(cm) / Convert.ToDouble(xtot)));
                    }
                    else
                    {
                        binParameters.AddOutput("%", "na");
                        binParameters.AddOutput("cm", "na");
                        binParameters.AddOutput("%2", "na");
                    }
                }
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult QuickSummary(ITemplateHost host, ParameterBag parameters)
        {
            double GAMMA = parameters["gamma"].AsDouble;
            DataFrame data = parameters["data"].AsDataFrame;
            DoubleVariable v0 = data.Variables[0].AsDoubleVariable;
            double[] x = new double[v0.Length + 1 /* VB to C# conversion */ ];
            Array.Copy(v0.Data, 0, x, 1, v0.Length);
            Summary sx = new Summary();
            sx.FullSummaryFromX(x, v0.Length, v0.Title, GAMMA, 5, 95, 1);
            const int flt = 6;
            const int k = 24;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Title: " + v0.Title);
            sb.AppendLine("");
            sb.AppendLine(Formatting.PadTo("Valid data", k) + sx.ValidData);
            sb.AppendLine(Formatting.PadTo("Missing", k) + sx.MissingData);
            sb.AppendLine(Formatting.PadTo("Sum", k) + Formatting.RoundOut(sx.Sum, flt));
            sb.AppendLine(Formatting.PadTo("Mean", k) + Formatting.RoundOut(sx.Mean, flt));
            sb.AppendLine(Formatting.PadTo("Variance", k) + Formatting.RoundOut(sx.Variance, flt));
            sb.AppendLine(Formatting.PadTo("Standard deviation", k) + Formatting.RoundOut(sx.Sd, flt));
            sb.AppendLine(Formatting.PadTo("Variation coefficient", k) + Formatting.RoundOut(sx.VarianceCoefficient, flt));
            sb.AppendLine(Formatting.PadTo("Standard error of mean", k) + Formatting.RoundOut(sx.Sem, flt));
            sb.AppendLine(Formatting.PadTo(Formatting.XRound(100 * GAMMA, 1) + "% Upper CL of mean", k) + Formatting.RoundOut(sx.MeanUCL, flt));
            sb.AppendLine(Formatting.PadTo(Formatting.XRound(100 * GAMMA, 1) + "% Lower CL of mean", k) + Formatting.RoundOut(sx.MeanLCL, flt));
            sb.AppendLine(Formatting.PadTo("Geometric mean", k) + Formatting.RoundOut(sx.GeometricMean, flt));
            sb.AppendLine(Formatting.PadTo("Skewness", k) + Formatting.RoundOut(sx.Skewness, flt));
            sb.AppendLine(Formatting.PadTo("Kurtosis", k) + Formatting.RoundOut(sx.Kurtosis, flt));
            sb.AppendLine(Formatting.PadTo("Maximum", k) + Formatting.RoundOut(sx.Maximum, flt));
            sb.AppendLine(Formatting.PadTo("95th percentile", k) + Formatting.RoundOut(sx.UserCentileU, flt));
            sb.AppendLine(Formatting.PadTo("Upper quartile", k) + Formatting.RoundOut(sx.UpperQuartile, flt));
            sb.AppendLine(Formatting.PadTo("Median", k) + Formatting.RoundOut(sx.Median, flt));
            sb.AppendLine(Formatting.PadTo("Lower quartile", k) + Formatting.RoundOut(sx.LowerQuartile, flt));
            sb.AppendLine(Formatting.PadTo("5th percentile", k) + Formatting.RoundOut(sx.UserCentileL, flt));
            sb.AppendLine(Formatting.PadTo("Minimum", k) + Formatting.RoundOut(sx.Minimum, flt));
            sb.AppendLine(Formatting.PadTo("Range", k) + Formatting.RoundOut(sx.Range, flt));
            SummaryStatisticsOptions sso = new SummaryStatisticsOptions { Text = sb.ToString() };
            host.Amend(sso, parameters);
            return new StepResult(StepSuccess.Success, new ParameterBag());
        }


        public static StepResult RptUnivariateSummary(ITemplateHost host, ParameterBag parameters)
        {
            return RptDescriptive(host, parameters, false);
        }


        public static StepResult RptWeightedUnivariateSummary(ITemplateHost host, ParameterBag parameters)
        {
            return RptDescriptive(host, parameters, true);
        }

        private enum SummaryType
        {
            ValidData = 0,
            MissingData = 1,
            Sum = 2,
            Mean = 3,
            Variance = 4,
            Sd = 5,
            VarianceCoefficient = 6,
            Sem = 7,
            MeanUcl = 8,
            MeanLcl = 9,
            GeometricMean = 10,
            Skewness = 11,
            Kurtosis = 12,
            Maximum = 13,
            UpperQuartile = 14,
            Median = 15,
            LowerQuartile = 16,
            Minimum = 17,
            Range = 18,
            Udc1 = 19,
            Udc2 = 20
        };

        private static StepResult RptDescriptive(ITemplateHost host, ParameterBag parameters, bool isWeighted)
        {
            int maxrows; int cols;
            double nsumwt = 0;
            string wti = null;
            ColumnData[] cdx;
            double[,] x; double[,] w = null;

            // get data
            if (isWeighted)
            {
                nsumwt = Constant.MISSING;
                // store the data
                DataFrame data = parameters["data"].AsDataFrame;
                maxrows = data.MaxRows;
                cols = data.VariableCount;
                x = new double[cols, maxrows + 1];
                cdx = new ColumnData[cols];
                for (int i = 0; i < cols; i++)
                {
                    DoubleVariable vi = data.Variables[i].AsDoubleVariable;
                    cdx[i] = new ColumnData { Title = vi.Title, Rows = vi.Length };
                }

                w = new double[cols, maxrows + 1];
                DataFrame weightsFrame = parameters["weights"].AsDataFrame;
                DoubleVariable weightsVariable = weightsFrame.Variables[0].AsDoubleVariable;
                wti = weightsVariable.Title;

                // Load the data, skipping rows where weights are 0 or missing
                int targetRow = 1;
                int removed = 0;
                for (int row = 0; row < maxrows; row++)
                {
                    double weight = weightsVariable.Data[row];
                    if (weight == Constant.MISSING || weight == 0)
                    {
                        // Remove the row from any variables that are at least this long
                        for (int col = 0; col < cols; col++)
                        {
                            if (row < cdx[col].Rows + removed)
                                cdx[col].Rows--;
                        }
                        removed++;
                        continue;
                    }

                    if (weight < 0.0)
                    {
                        host.Error("Weights must not be negative", "Descriptive Statistics");
                        throw new TemplateOperationCancelledException();
                    }

                    for (int col = 0; col < cols; col++)
                    {
                        w[col, targetRow] = weight;
                        DoubleVariable vi = data.Variables[col].AsDoubleVariable;
                        double value = Constant.MISSING;
                        if (vi.Length > row)
                            value = vi.Data[row];
                        x[col, targetRow] = value;
                    }
                    targetRow++;
                }
                // Account for missing or zero weights
                maxrows = targetRow - 1;
            }
            else
            {
                //  Index = 1: Univariate summary
                // store the data
                DataFrame data = parameters["data"].AsDataFrame;
                maxrows = data.MaxRows;
                cols = data.VariableCount;
                x = new double[cols, maxrows + 1];
                cdx = new ColumnData[cols];
                for (int i = 0; i < cols; i++)
                {
                    DoubleVariable vi = data.Variables[i].AsDoubleVariable;
                    cdx[i] = new ColumnData { Title = vi.Title, Rows = vi.Length };
                    for (int j = 1; j <= vi.Length; j++)
                        x[i, j] = vi.Data[j - 1];
                }
            }

            // get options
            double GAMMA = parameters["gamma"].AsDouble;
            string qxcl = " " + Formatting.XRound(GAMMA * 100, 1) + "% CL of mean";
            string sumTitle = isWeighted ? "Sum of weights" : "Sum";
            string[] titles = { "Valid data", "Missing data", sumTitle, "Mean", "Variance", "Standard deviation", "Variance coefficient", "Standard error of mean", "Upper" + qxcl, "Lower" + qxcl, "Geometric mean", "Skewness", "Kurtosis", "Maximum", "Upper quartile", "Median", "Lower quartile", "Minimum", "Range", "User defined centiles", null };

            Dictionary<SummaryType, bool> shouldOutput = new Dictionary<SummaryType, bool>();
            shouldOutput[SummaryType.ValidData] = parameters["report-valid-data"].AsBoolean;
            shouldOutput[SummaryType.MissingData] = parameters["report-missing-data"].AsBoolean;
            shouldOutput[SummaryType.Sum] = parameters["report-sum"].AsBoolean;
            shouldOutput[SummaryType.Mean] = parameters["report-mean"].AsBoolean;
            shouldOutput[SummaryType.Variance] = parameters["report-variance"].AsBoolean;
            shouldOutput[SummaryType.Sd] = parameters["report-sd"].AsBoolean;
            shouldOutput[SummaryType.VarianceCoefficient] = parameters["report-variance-coeff"].AsBoolean;
            shouldOutput[SummaryType.Sem] = parameters["report-sem"].AsBoolean;
            shouldOutput[SummaryType.MeanUcl] = parameters["report-u95cl"].AsBoolean;
            shouldOutput[SummaryType.MeanLcl] = parameters["report-l95cl"].AsBoolean;
            shouldOutput[SummaryType.GeometricMean] = parameters["report-geometric-mean"].AsBoolean;
            shouldOutput[SummaryType.Skewness] = parameters["report-skewness"].AsBoolean;
            shouldOutput[SummaryType.Kurtosis] = parameters["report-kurtosis"].AsBoolean;
            shouldOutput[SummaryType.Maximum] = parameters["report-maximum"].AsBoolean;
            shouldOutput[SummaryType.UpperQuartile] = parameters["report-uq"].AsBoolean;
            shouldOutput[SummaryType.Median] = parameters["report-median"].AsBoolean;
            shouldOutput[SummaryType.LowerQuartile] = parameters["report-lq"].AsBoolean;
            shouldOutput[SummaryType.Minimum] = parameters["report-minimum"].AsBoolean;
            shouldOutput[SummaryType.Range] = parameters["report-range"].AsBoolean;
            shouldOutput[SummaryType.Udc1] = parameters["report-udc"].AsBoolean;
            shouldOutput[SummaryType.Udc2] = parameters["report-udc"].AsBoolean;

            int userCentileA1 = Convert.ToInt32(parameters["report-udca"].AsDouble);
            int userCentileB1 = Convert.ToInt32(parameters["report-udcb"].AsDouble);
            int prevCentileType = Parsing.Cint_Txt(parameters["report-centile-type"].AsString);
            double centxl = userCentileA1;
            double centxu = userCentileB1;
            // prevchk1 = true; 
            // If there's no output-to-frame, we're being called from the summary - which always wants this.
            bool shouldSave = !parameters.ContainsKey("output-to-frame") || parameters["output-to-frame"].AsBoolean;

            // get a result object for each column of data
            Summary[] sx = new Summary[cols];
            for (int i = 0; i < cols; i++)
            {
                sx[i] = new Summary();
                if (isWeighted)
                    sx[i].WeightedSummaryFromXK(i, x, cdx[i].Rows, cdx[i].Title, GAMMA, centxl, centxu, w, wti, nsumwt);
                else
                    sx[i].FullSummaryFromXK(i, x, cdx[i].Rows, cdx[i].Title, GAMMA, centxl, centxu, prevCentileType);
            }

            // Fill the report
            //  RTF_LoadTemplate("describe.rtf") Then
            // Insert the result into the report
            ParameterBag outputParameters = new ParameterBag();
            List<ParameterBag> titlesList = new List<ParameterBag>();
            outputParameters.AddOutput("*titles", titlesList);
            for (int i = 0; i < cols; i++)
            { // Title

                ParameterBag titlesParameters = new ParameterBag();
                titlesList.Add(titlesParameters);
                string val = sx[i].Title;
                if ((i + 1) % 3 == 0 && cols > 3)
                    val += Formatting.RTFCRLF;
                titlesParameters.AddOutput("title", val);
            }
            List<ParameterBag> fieldsList = new List<ParameterBag>();
            outputParameters.AddOutput("*fields", fieldsList);
            foreach (SummaryType s in Enum.GetValues(typeof(SummaryType)))
            {
                if (shouldOutput[s])
                    fieldsList.Add(FillField(host, s, sx, cols, shouldOutput[s], titles[(int)s], isWeighted));
            }

            // Fill the worksheet if required
            if (shouldSave)
            {
                // Find how many columns have been selected
                int lc = 1;
                foreach (SummaryType s in Enum.GetValues(typeof(SummaryType)))
                {
                    if (shouldOutput[s])
                    {
                        lc += 1;
                    }
                }
                int userCentileA2 = userCentileA1;
                int userCentileB2 = userCentileB1;
                centxl = userCentileA2;
                centxu = userCentileB2;

                // recalculate if centiles selected have changed
                if ((userCentileA2 > 0 && userCentileA2 != userCentileA1) || (userCentileB2 > 0 && userCentileB2 != userCentileB1))
                {
                    for (int i = 0; i < cols; i++)
                    {
                        if (isWeighted)
                            sx[i].WeightedSummaryFromXK(i, x, cdx[i].Rows, cdx[i].Title, GAMMA, centxl, centxu, w, wti, nsumwt);
                        else
                            sx[i].FullSummaryFromXK(i, x, cdx[i].Rows, cdx[i].Title, GAMMA, centxl, centxu, prevCentileType);
                    }
                }

                if (userCentileA2 > 0)
                {
                    lc += 1;
                }
                if (userCentileB2 > 0)
                {
                    lc += 1;
                }
                // prevchk2 = true; 
                if (lc > 0)
                {
                    DataFrame outputFrame = new DataFrame();
                    outputParameters.AddOutput("output", outputFrame);
                    if (cdx.Length == 1)
                    {
                        // Short form, single column
                        StringVariable labels = new StringVariable(lc, "Measure");
                        outputFrame.Variables.Add(labels);
                        DoubleVariable values = new DoubleVariable(lc, "Value");
                        outputFrame.Variables.Add(values);
                        int row = 0;
                        foreach (SummaryType s in Enum.GetValues(typeof(SummaryType)))
                        {
                            if (shouldOutput[s])
                            {
                                labels.Data[row] = Caption(s, sx[0], titles);
                                values.Data[row] = Value(s, sx[0]);
                                row++;
                            }
                        }
                    }
                    else
                    {
                        // If the worksheet is loaded then fill it
                        StringVariable totalsVariable = new StringVariable { Title = "Title" };
                        totalsVariable.EnsureLength(cols);
                        for (int i = 0; i < cols; i++)
                        {
                            totalsVariable.set_Data(i, sx[i].Title);
                        }
                        outputFrame.Variables.Add(totalsVariable);
                        foreach (SummaryType s in Enum.GetValues(typeof(SummaryType)))
                        {
                            if (shouldOutput[s])
                            outputFrame.Variables.Add(FillCell(s, sx, cols, titles));
                        }
                    }
                }
            }

            return new StepResult(StepSuccess.Success, outputParameters);
        }


        ///  <summary>
        ///  Return the median of the elements of x from ia to iz inclusive.
        ///  </summary>
        ///  <param name="x"></param>
        ///  <param name="ia"></param>
        ///  <param name="iz"></param>
        ///  <returns></returns>
        ///  <remarks></remarks>
        public static double Median(double[] x, int ia, int iz)
        {
            double[] ao = new double[iz - ia + 1 ];
            int reali = 0;
            for (int i = ia; i <= iz; i++)
            {
                if (x[i] != Constant.MISSING)
                {
                    ao[reali] = x[i];
                    reali = reali + 1;
                }
            }
            //  At this point, reali counts the number of elements that have been copied to ao.
            if (reali > 1)
            {
                // create temp variable for copying values 
                double[] transTemp0 = new double[reali - 1 + 1];
                Array.Copy(ao, transTemp0, Math.Min(ao.Length, transTemp0.Length));
                ao = transTemp0;
                Array.Sort(ao);
                //  The -1 is because ao is 0-based
                double imdn = (0.5 * (reali + 1)) - 1;
                if (imdn < 0)
                    imdn = 0;
                if (imdn > reali - 1)
                    imdn = reali - 1;
                double fimdn = Math.Floor(imdn);
                int iimdn = (int)fimdn;
                if (imdn - fimdn == 0)
                {
                    //  Exact element
                    return ao[iimdn];
                }
                //  Weighted mean of adjacent elements
                return ao[iimdn] + (ao[iimdn + 1] - ao[iimdn]) * (imdn - fimdn);
            }
            return Constant.MISSING;
        }


        private static string Caption(SummaryType summaryType, Summary sx, string[] titles)
        {
            switch (summaryType)
            {
                case SummaryType.Udc1:
                    return sx.UserCentileUCaption;
                case SummaryType.Udc2:
                    return sx.UserCentileLCaption;
                default:
                    return titles[(int)summaryType];
            }

        }

        private static double Value(SummaryType summaryType, Summary sx)
        {
            double res;
            switch (summaryType)
            {
                case SummaryType.ValidData:
                    res = sx.ValidData;
                    break;
                case SummaryType.MissingData:
                    res = sx.MissingData;
                    break;
                case SummaryType.Sum:
                    res = sx.Sum;
                    break;
                case SummaryType.Mean:
                    res = sx.Mean;
                    break;
                case SummaryType.Variance:
                    res = sx.Variance;
                    break;
                case SummaryType.Sd:
                    res = sx.Sd;
                    break;
                case SummaryType.VarianceCoefficient:
                    res = sx.VarianceCoefficient;
                    break;
                case SummaryType.Sem:
                    res = sx.Sem;
                    break;
                case SummaryType.MeanUcl:
                    res = sx.MeanUCL;
                    break;
                case SummaryType.MeanLcl:
                    res = sx.MeanLCL;
                    break;
                case SummaryType.GeometricMean:
                    res = sx.GeometricMean;
                    break;
                case SummaryType.Skewness:
                    res = sx.Skewness;
                    break;
                case SummaryType.Kurtosis:
                    res = sx.Kurtosis;
                    break;
                case SummaryType.Maximum:
                    res = sx.Maximum;
                    break;
                case SummaryType.UpperQuartile:
                    res = sx.UpperQuartile;
                    break;
                case SummaryType.Median:
                    res = sx.Median;
                    break;
                case SummaryType.LowerQuartile:
                    res = sx.LowerQuartile;
                    break;
                case SummaryType.Minimum:
                    res = sx.Minimum;
                    break;
                case SummaryType.Range:
                    res = sx.Range;
                    break;
                case SummaryType.Udc1:
                    res = sx.UserCentileU;
                    break;
                case SummaryType.Udc2:
                    res = sx.UserCentileL;
                    break;
                default:
                    throw new ArgumentException("Unknown option", "summaryType");
            }
            return res;
        }

        private static DoubleVariable FillCell(SummaryType summaryType, Summary[] sx, int cols, string[] titles)
        {
            DoubleVariable v = new DoubleVariable();
            v.EnsureLength(cols);
            v.Title = Caption(summaryType, sx[0], titles);
            for (int i = 0; i < cols; i++)
                v.set_Data(i, Value(summaryType, sx[i]));
            return v;
        }


        private static ParameterBag FillField(ITemplateHost host, SummaryType opt, Summary[] sx, int cols, bool optChecked, string optTitle, bool isWeighted)
        {
            ParameterBag fieldParameters = new ParameterBag();
            List<ParameterBag> resultsList = new List<ParameterBag>();
            fieldParameters.AddOutput("*results", resultsList);
            if (optChecked)
            {
                switch (opt)
                {
                    case SummaryType.Udc1:
                        fieldParameters.AddOutput("title", sx[0].UserCentileUCaption);
                        break;
                    case SummaryType.Udc2:
                        fieldParameters.AddOutput("title", sx[0].UserCentileLCaption);
                        break;
                    default:
                        fieldParameters.AddOutput("title", optTitle);
                        break;
                }
                for (int i = 0; i < cols; i++)
                {
                    ParameterBag resultsParameters = new ParameterBag();
                    resultsList.Add(resultsParameters);
                    string res;
                    switch (opt)
                    {
                        case SummaryType.ValidData:
                            res = sx[i].ValidData.ToString();
                            break;
                        case SummaryType.MissingData:
                            res = sx[i].MissingData.ToString();
                            break;
                        case SummaryType.Sum:
                            res = host.RoundU(isWeighted ? sx[i].SumOfWeights : sx[i].Sum);
                            break;
                        case SummaryType.Mean:
                            res = host.RoundU(sx[i].Mean);
                            break;
                        case SummaryType.Variance:
                            res = host.RoundU(sx[i].Variance);
                            break;
                        case SummaryType.Sd:
                            res = host.RoundU(sx[i].Sd);
                            break;
                        case SummaryType.VarianceCoefficient:
                            res = host.RoundU(sx[i].VarianceCoefficient);
                            break;
                        case SummaryType.Sem:
                            res = host.RoundU(sx[i].Sem);
                            break;
                        case SummaryType.MeanUcl:
                            res = host.RoundU(sx[i].MeanUCL);
                            break;
                        case SummaryType.MeanLcl:
                            res = host.RoundU(sx[i].MeanLCL);
                            break;
                        case SummaryType.GeometricMean:
                            res = host.RoundU(sx[i].GeometricMean);
                            break;
                        case SummaryType.Skewness:
                            res = host.RoundU(sx[i].Skewness);
                            break;
                        case SummaryType.Kurtosis:
                            res = host.RoundU(sx[i].Kurtosis);
                            break;
                        case SummaryType.Maximum:
                            res = host.RoundU(sx[i].Maximum);
                            break;
                        case SummaryType.UpperQuartile:
                            res = host.RoundU(sx[i].UpperQuartile);
                            break;
                        case SummaryType.Median:
                            res = host.RoundU(sx[i].Median);
                            break;
                        case SummaryType.LowerQuartile:
                            res = host.RoundU(sx[i].LowerQuartile);
                            break;
                        case SummaryType.Minimum:
                            res = host.RoundU(sx[i].Minimum);
                            break;
                        case SummaryType.Range:
                            res = host.RoundU(sx[i].Range);
                            break;
                        case SummaryType.Udc1:
                            res = host.RoundU(sx[i].UserCentileU);
                            break;
                        case SummaryType.Udc2:
                            res = host.RoundU(sx[i].UserCentileL);
                            break;
                        default:
                            throw new ArgumentException("Unknown opt", "opt");
                    }

                    if ((i + 1) % 3 == 0 && cols > 3)
                    {
                        res += Formatting.RTFCRLF;
                    }
                    resultsParameters.AddOutput("result", res);
                }
                string s = resultsList[resultsList.Count - 1]["result"].AsString;
                resultsList[resultsList.Count - 1]["result"] = new FilledParameter(false, s);
            }
            return fieldParameters;
        }
    }
}
