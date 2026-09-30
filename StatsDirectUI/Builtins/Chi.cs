using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using static StatsDirect.Builtins.ExactBB;

namespace StatsDirect.Builtins
{
    public static class Chi
    {
        /// <summary>
        /// The chi-square test of a 2 by 2 table, with the odds ratio if the table is of a case-control study and the risk ratio
        /// if it is of a cohort study.
        /// </summary>
        /// <remarks>
        /// The table is a, b in its first row and c, d in its second; p and q are the totals of the rows, r and s those of the
        /// columns, and n is the number of observations.  The expected count of a cell is the total of its row times the total of
        /// its column over n.  Chi-square is the sum over the cells of the squared difference of the observed and the expected
        /// count over the expected count, which is n (a d - b c)^2 / (p q r s); with the correction for continuity of Yates, a
        /// half is taken from each difference, which makes it n (|a d - b c| - n / 2)^2 / (p q r s), and nothing if |a d - b c| is
        /// below n / 2.  Each has 1 degree of freedom.
        /// Pearson's coefficient of contingency is the root of chi-square / (chi-square + n), and V is
        /// (a d - b c) / root(p q r s), which is the correlation of the row and the column over the observations.
        /// The report warns if n is below 20 or an expected count is below 5.
        /// Case-control study: the odds ratio a d / (b c) has the limits of Woolf, which are the exponentials of its logarithm
        /// less and plus the normal deviate times the root of 1 / a + 1 / b + 1 / c + 1 / d; with an empty cell they are not
        /// worked out, and the limit at the end at which the odds ratio is (0, or infinity) is given alone.  The exact limits and
        /// P values are those of ExactBB.OddsRatioCMLE, which rounds counts that are not whole numbers.
        /// Cohort study: the risk ratio and what goes with it are those of Analysis.RptMiscRelRisk, of the frequencies as
        /// entered (that analysis rounds them for its own report); there is no risk ratio if b is 0, and the report says so.
        /// Fisher's exact test (Exact.RptExactFisher) is added if n is below 20 or an expected count is below 5, if it is asked
        /// for, and if the exact method of the odds ratio, which has its P values, could not be used; but not if that method
        /// was used.
        /// </remarks>
        /// <param name="host">Where progress is shown.</param>
        /// <param name="parameters">"a", "b": the first row of the table; "c", "d": the second; "cco": the confidence level, for
        /// which 0.95 is taken if it is not between 0 and 1; "study_type": "casecontrol", "cohort" or another word; "doFisher":
        /// whether Fisher's exact test is asked for.</param>
        /// <returns>"tab3_a1" to "tab3_c3": the table with its totals; "tab_a1" to "tab_b2": the expected counts; "chi" and
        /// "chi_p", "yates_chi" and "yates_chi_p": the two chi-squares and their P values; "pearson" and "vs": the two
        /// coefficients; "*warn": a row with "wrn", what is too small, if the report is to warn; "*note": a row with "note" for
        /// each thing that the report says of an analysis that it has not; "*odds": a row for a case-control study, with
        /// "odds", "woolf_ci" (the confidence level as a percentage), "woolf_ci_1" and "woolf_ci_2", "ci", "llf", "ulf", "p1f",
        /// "p2f", "llm", "ulm", "p1m", "p2m", and "*note" if the exact method was not used; "*relrisk": a row for a cohort
        /// study, with what Analysis.RptMiscRelRisk returns; "*fisher": a row with what Exact.RptExactFisher returns.</returns>
        public static StepOutput RptChi2By2(IProgressBarHost host, ParameterBag parameters)
        {
            double cco = parameters["cco"].AsDouble;
            string studyType = parameters["study_type"].AsString;
            bool isCaseControl = "casecontrol".Equals(studyType);
            bool isCohort = "cohort".Equals(studyType);
            bool doFisher = parameters.ContainsKey("doFisher") && parameters["doFisher"].AsBoolean;

            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0, out int fault);

            double a = parameters["a"].AsDouble;
            double b = parameters["b"].AsDouble;
            double c = parameters["c"].AsDouble;
            double d = parameters["d"].AsDouble;
            double p = a + b;
            double q = c + d;
            double r = a + c;
            double s = b + d;
            double n = p + q;
            ParameterBag outputParameters = new();
            if (fault != 0)
                throw new InvalidDataException();
            if (a < 0 || b < 0 || c < 0 || d < 0)
                throw new InvalidDataException("A count of the table is below 0.");
            if (!(p * q * r * s > 0))
                throw new InvalidDataException("A row or a column of the table has no observations: chi-square can not be calculated for such a table.");

            // what the report says of an analysis that it has not
            List<ParameterBag> noteList = new();
            outputParameters.AddOutput("*note", noteList);
            void Note(string note)
            {
                ParameterBag noteParameters = new();
                noteList.Add(noteParameters);
                noteParameters.AddOutput("note", note);
            }

            outputParameters.AddOutput("tab3_a1", a);
            outputParameters.AddOutput("tab3_b1", b);
            outputParameters.AddOutput("tab3_c1", p);
            outputParameters.AddOutput("tab3_a2", c);
            outputParameters.AddOutput("tab3_b2", d);
            outputParameters.AddOutput("tab3_c2", q);
            outputParameters.AddOutput("tab3_a3", r);
            outputParameters.AddOutput("tab3_b3", s);
            outputParameters.AddOutput("tab3_c3", n);

            double e1 = p * r / n;
            double e2 = p * s / n;
            double e3 = q * r / n;
            double e4 = q * s / n;

            outputParameters.AddOutput("tab_a1", e1);
            outputParameters.AddOutput("tab_b1", e2);
            outputParameters.AddOutput("tab_a2", e3);
            outputParameters.AddOutput("tab_b2", e4);

            // chi-square, and chi-square with the correction for continuity
            double f = a * d - b * c;
            double x2 = f * f * n / (p * q * r * s);
            outputParameters.AddOutput("chi", x2);
            outputParameters.AddOutput("chi_p", PDF.chivalp(x2, 1.0));

            f = Math.Abs(f) - n / 2;
            if (f < 0)
                f = 0;

            double x2C = f * f * n / (p * q * r * s);
            outputParameters.AddOutput("yates_chi", x2C);
            outputParameters.AddOutput("yates_chi_p", PDF.chivalp(x2C, 1.0));

            // coefficients (see Agresti p 23-4)
            double p1 = Math.Sqrt(x2 / (x2 + n));
            double c1 = (a * d - b * c) / Math.Sqrt(p * q * r * s);
            outputParameters.AddOutput("pearson", p1);
            outputParameters.AddOutput("vs", c1);

            List<ParameterBag> warnList = new();
            outputParameters.AddOutput("*warn", warnList);
            if (e1 < 5 || e2 < 5 || e3 < 5 || e4 < 5 || n < 20)
            {
                ParameterBag warnParameters = new();
                warnList.Add(warnParameters);
                string wrn = n < 20 ? "Number of observations" : "Expected frequencies";
                warnParameters.AddOutput("wrn", wrn);
            }

            bool doneExact = false;

            List<ParameterBag> oddsList = new();
            outputParameters.AddOutput("*odds", oddsList);
            List<ParameterBag> relRiskList = new();
            outputParameters.AddOutput("*relrisk", relRiskList);
            if (isCaseControl)
            {
                ParameterBag oddsParameters = new();
                oddsList.Add(oddsParameters);
                // Woolf/logit CI
                double odr = OddsRatio(a, b, c, d);
                double yodr;
                double xodr;
                // with an empty cell the logarithm of the odds ratio has no standard error: the limit that is known is given
                if (b * c > 0 && a * d > 0)
                {
                    double seodr = Math.Sqrt(1.0 / a + 1.0 / b + 1.0 / c + 1.0 / d);
                    yodr = Math.Exp(Math.Log(odr) - cit * seodr);
                    xodr = Math.Exp(Math.Log(odr) + cit * seodr);
                }
                else
                {
                    yodr = Constant.MISSING;
                    xodr = Constant.MISSING;
                    if (a == 0 || d == 0)
                        yodr = 0;
                    else if (b == 0 || c == 0)
                        xodr = double.PositiveInfinity;
                }
                oddsParameters.AddOutput("odds", odr);
                oddsParameters.AddOutput("woolf_ci", cco * 100.0);
                oddsParameters.AddOutput("woolf_ci_1", yodr);
                oddsParameters.AddOutput("woolf_ci_2", xodr);
                // CMLE
                //ExactBB.Rec2X2[] tabl = new ExactBB.Rec2X2[1 + 1];
                //tabl[1].Freq = 1;
                //tabl[1].A = a;
                //tabl[1].M1 = a + b;
                //tabl[1].N1 = a + c;
                //tabl[1].N0 = b + d;
                //tabl[1].Informative = (a * d != 0) | (b * c != 0);
                //bool useLogScale = false;
                //new ExactBB().Exact22K(host, 1, 1, tabl, cco, ref eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
                OddsRatioCMLE(host, cco, a, b, c, d, out double _, out double llf, out double ulf, out double llm, out double ulm, out double p1f, out double p2f, out double p1m, out double p2m, out int ierr);
                // The odds ratio block carries the exact P values, so the Fisher's exact test block is only needed when this routine fails
                if (ierr == 0)
                    doneExact = true;
                List<ParameterBag> oddsNoteList = new();
                oddsParameters.AddOutput("*note", oddsNoteList);
                if (ierr != 0)
                {
                    ParameterBag oddsNoteParameters = new();
                    oddsNoteList.Add(oddsNoteParameters);
                    oddsNoteParameters.AddOutput("note", ierr == -1
                        ? "The table is too large for the exact method of the odds ratio, which considers no more than a million values of the first count."
                        : "The exact method of the odds ratio could not be completed for this table.");
                }
                oddsParameters.AddOutput("ci", cco * 100.0);
                oddsParameters.AddOutput("llf", llf);
                oddsParameters.AddOutput("ulf", ulf);
                oddsParameters.AddOutput("p1f", p1f);
                oddsParameters.AddOutput("p2f", p2f);
                oddsParameters.AddOutput("llm", llm);
                oddsParameters.AddOutput("ulm", ulm);
                oddsParameters.AddOutput("p1m", p1m);
                oddsParameters.AddOutput("p2m", p2m);
            }
            else if (isCohort)
            {
                // The risk ratio has no value if none of those without the characteristic has the outcome: the tests are given all
                // the same
                if (b > 0)
                {
                    // the table is of the frequencies as entered, whole numbers or not, as the chi-square is
                    relRiskList.Add(Analysis.XRelRisk(parameters, true).ParameterBag);
                }
                else
                    Note("The risk ratio is not given: none of those without the characteristic has the outcome.");
            }

            // Fisher's exact test: if the observations or the expected counts are few, if it is asked for, and if the exact method
            // of the odds ratio, which has the P values of the test, could not be completed
            List<ParameterBag> fisherList = new();
            outputParameters.AddOutput("*fisher", fisherList);
            if (!doneExact && (e1 < 5 || e2 < 5 || e3 < 5 || e4 < 5 || n < 20 || doFisher || isCaseControl))
            {
                if (n <= int.MaxValue)
                    fisherList.Add(Exact.RptExactFisher(parameters).ParameterBag);
                else
                    Note("Fisher's exact test is not given for a table of more than 2,147,483,647 observations.");
            }
            return new StepOutput(outputParameters);
        }

        // the three analyses of a 2 by k table: without the test for trend, with it for the scores 1 to k, and with it for scores
        // that are given
        private enum Chi2ByNTrend
        {
            WithoutTrend = 0,
            LinearTrend = 1,
            WithTrend = 2
        }

        // The chi-square test of a 2 by k table without the test for trend, with it for the scores 1 to k, and with it for scores
        // that are given (RptChi2ByN)
        public static StepOutput RptChi2ByNWithoutTrend(ParameterBag parameters) => RptChi2ByN(parameters, Chi2ByNTrend.WithoutTrend);
        public static StepOutput RptChi2ByNLinearTrend(ParameterBag parameters) => RptChi2ByN(parameters, Chi2ByNTrend.LinearTrend);
        public static StepOutput RptChi2ByNWithTrend(ParameterBag parameters) => RptChi2ByN(parameters, Chi2ByNTrend.WithTrend);

        /// <summary>
        /// The chi-square test of a 2 by k table: k groups, with the numbers of each that are successes and failures; and the
        /// test for a linear trend of the proportion of successes with a score of the groups.
        /// </summary>
        /// <remarks>
        /// Chi-square is the sum over the cells of the squared difference of the observed and the expected count over the
        /// expected count, the expected count being the total of the row times the total of the column over the number of
        /// observations; it has k - 1 degrees of freedom.  The report warns of the cells whose expected count is below 5.
        /// Trend: with a_i successes and b_i failures in the n_i observations of group i, whose score is v_i, and with A
        /// successes and B failures in all N, the root of the chi-square for trend is
        ///   sum(v (a - n A / N)) / root((A / N) (B / N) [sum(n v^2) - (sum(n v))^2 / N]),
        /// which is above 0 if the proportion rises with the score; its square has 1 degree of freedom.  The variance of the
        /// scores is over N, where the test for trend of the r by c analysis (Tables.SChi) has N - 1.  The scores that the
        /// sums are made of are the scores less their mean over the observations (Centred): a number that all the scores have
        /// in common then has no part in the sums, where it would take their figures from them.  What is left of chi-square
        /// has k - 2 degrees of freedom, and tests the departure of the proportions from the line; it is taken as nothing if
        /// it is below 1 part in 10^9 of chi-square.
        /// There is no chi-square if there are no successes or no failures, and no test for trend if the scores are all the
        /// same: the report says so.
        /// </remarks>
        /// <param name="parameters">"data": a column of the successes, one of the failures and, if the scores are given, one of
        /// the scores, with a row for each group.  A group is to have an observation, and there are to be two groups or
        /// more.</param>
        /// <param name="z">Which of the three analyses.</param>
        /// <returns>"*row": for each group "obs_succ", "obs_fail", "obs_tot", "obs_pc" (the successes as a percentage),
        /// "exp_succ", "exp_fail" and, in "*score", "score"; "tot_succ", "tot_fail", "tot_tot", "tot_pc"; "*scoreHead": a row if
        /// there are scores; "*warn": a row with "num" and "den", the number of cells with an expected count below 5 and the
        /// number of cells; "*note": a row with "note" if there is no chi-square, or no trend to test; "chi", "chi_abs" (its
        /// root), "totdf", "chi_p"; "*z": a row for the test for trend, with "chi_lin", "chi_1df" (its root, without its
        /// sign), "chi_lin_p", and in "*non" "chi_non", "df" and "chi_non_p" for what is left, if there are more than two
        /// groups.  Handed on to a step that follows: "x2", and "x2_lin" if there is a test for trend.</returns>
        private static StepOutput RptChi2ByN(ParameterBag parameters, Chi2ByNTrend z)
        {
            DataFrame datFrame = parameters["data"].AsDataFrame;
            if (datFrame.VariableCount < (z == Chi2ByNTrend.WithTrend ? 3 : 2))
                throw new InvalidDataException("Invalid data: Please fill in the same number of rows in each column without gaps");
            DoubleVariable datV0 = (DoubleVariable)datFrame.Variables[0];
            DoubleVariable datV1 = (DoubleVariable)datFrame.Variables[1];
            DoubleVariable datV2 = null;
            if (z == Chi2ByNTrend.WithTrend)
                datV2 = (DoubleVariable)datFrame.Variables[2];
            int rows = datFrame.MaxRows;
            if (rows < 2)
                throw new TemplateOperationCancelledException("At least two rows (groups) are needed for a 2 by k table.", "Chi-square test (2 by k)");
            double[] f = new double[rows + 1];
            double[] g = new double[rows + 1];
            double[] h = new double[rows + 1];
            double[] s = new double[rows + 1];

            ParameterBag outputParameters = new();
            double c = 0;
            double t = 0;
            double b = 0;
            double a = 0;
            for (int r = 1; r <= rows; r++)
            {
                double a1 = datV0.Data[r - 1];
                double b1 = datV1.Data[r - 1];
                double t1 = a1 + b1;
                if (t1 <= 0.0)
                    throw new InvalidDataException("Invalid data: row " + r.ToString() + " total is not greater than zero, which it must be for this calculation");
                Debug.Assert(z != Chi2ByNTrend.WithTrend || datV2 != null);
                double s1 = z == Chi2ByNTrend.WithTrend ? datV2.Data[r - 1] : r;
                f[r] = a1;
                g[r] = b1;
                h[r] = t1;
                s[r] = s1;
                a += a1;
                b += b1;
                t += t1;
            }
            double n1 = 0;
            // chi-square is the sum over the cells of (observed - expected)^2 / expected; it has no value if a column has nothing
            double x2 = 0.0;
            bool sameScores = true;
            List<ParameterBag> rowList = new();
            outputParameters.AddOutput("*row", rowList);
            for (int r = 1; r <= rows; r++)
            {
                double a1 = f[r];
                double b1 = g[r];
                double t1 = h[r];
                double s1 = s[r];
                double e1 = a * t1 / t;
                if (e1 < 5)
                    n1++;
                double e2 = b * t1 / t;
                if (e2 < 5)
                    n1++;
                x2 += (a1 - e1) * (a1 - e1) / e1 + (b1 - e2) * (b1 - e2) / e2;
                if (s1 != s[1])
                    sameScores = false;
                ParameterBag rowParameters = new();
                rowList.Add(rowParameters);
                rowParameters.AddOutput("obs_succ", a1);
                rowParameters.AddOutput("obs_fail", b1);
                rowParameters.AddOutput("obs_tot", t1);
                rowParameters.AddOutput("obs_pc", 100 * a1 / t1);
                // the Score column is printed only when the scores are used, in the test for linear trend
                IList<ParameterBag> scoreList = new List<ParameterBag>();
                if (z != Chi2ByNTrend.WithoutTrend)
                {
                    ParameterBag scoreParameters = new();
                    scoreParameters.AddOutput("score", s1);
                    scoreList.Add(scoreParameters);
                }
                rowParameters.AddOutput("*score", scoreList);

                rowParameters.AddOutput("exp_succ", e1);
                rowParameters.AddOutput("exp_fail", e2);
            }

            outputParameters.AddOutput("tot_succ", a);
            outputParameters.AddOutput("tot_fail", b);
            outputParameters.AddOutput("tot_tot", t);
            outputParameters.AddOutput("tot_pc", 100 * a / t);
            IList<ParameterBag> scoreHeadList = new List<ParameterBag>();
            if (z != Chi2ByNTrend.WithoutTrend)
                scoreHeadList.Add(new ParameterBag());
            outputParameters.AddOutput("*scoreHead", scoreHeadList);

            List<ParameterBag> warnList = new();
            outputParameters.AddOutput("*warn", warnList);
            if (n1 != 0)
            {
                ParameterBag warnParameters = new();
                warnList.Add(warnParameters);
                warnParameters.AddOutput("num", n1);
                warnParameters.AddOutput("den", 2 * rows);
            }

            double n2 = rows - 1;

            // what the report says of a test that it has not
            List<ParameterBag> noteList = new();
            outputParameters.AddOutput("*note", noteList);
            void Note(string note)
            {
                ParameterBag noteParameters = new();
                noteList.Add(noteParameters);
                noteParameters.AddOutput("note", note);
            }
            bool emptyColumn = a <= 0.0 || b <= 0.0;
            if (emptyColumn)
                Note("Chi-square can not be calculated: there are no " + (a <= 0.0 ? "successes" : "failures") + ".");
            else if (z != Chi2ByNTrend.WithoutTrend && sameScores)
                Note("The scores are all the same: there is no trend to test.");

            outputParameters.AddOutput("chi", x2);
            outputParameters.AddInput("x2", x2); //  For use with follow-on functions
            outputParameters.AddOutput("chi_abs", Math.Sqrt(x2));
            outputParameters.AddOutput("totdf", n2);
            outputParameters.AddOutput("chi_p", PDF.chivalp(x2, n2));

            List<ParameterBag> zList = new();
            outputParameters.AddOutput("*z", zList);
            if (z != Chi2ByNTrend.WithoutTrend && !emptyColumn && !sameScores)
            {
                c = x2;
                // the sums, of the scores less their mean: the products of the scores and what the successes are above their
                // expectation, the squares of the scores, and the scores, which add to nothing but for their rounding
                double[] v = Centred(s, h, rows);
                double trend = 0.0;
                double squares = 0.0;
                double scores = 0.0;
                for (int r = 1; r <= rows; r++)
                {
                    trend += v[r] * (f[r] - a * h[r] / t);
                    squares += v[r] * v[r] * h[r];
                    scores += v[r] * h[r];
                }
                double d = Math.Sqrt((squares - scores * scores / t) * (a / t) * (b / t));
                double x1 = trend / d;
                x2 = x1 * x1;
                n2 = 1;
                ParameterBag zParameters = new();
                zList.Add(zParameters);

                zParameters.AddOutput("chi_lin", x2);
                outputParameters.AddInput("x2_lin", x2); //  For use with follow-on functions
                zParameters.AddOutput("chi_1df", Math.Abs(x1));
                zParameters.AddOutput("chi_lin_p", PDF.chivalp(x2, n2));

                // The remaining (non-linearity) chi-square has k - 2 degrees of freedom, so there is none with two rows
                List<ParameterBag> nonList = new();
                zParameters.AddOutput("*non", nonList);
                if (rows > 2)
                {
                    x2 = c - x2;
                    // the trend accounts for the whole chi-square: what is left is a residue of rounding only (the chi-square
                    // for trend of a table of thousands of millions is right to 9 figures)
                    if (Math.Abs(x2) < 1.0E-9 * c)
                        x2 = 0;
                    n2 = rows - 2;
                    ParameterBag nonParameters = new();
                    nonList.Add(nonParameters);
                    nonParameters.AddOutput("chi_non", x2);
                    nonParameters.AddOutput("df", n2);
                    nonParameters.AddOutput("chi_non_p", PDF.chivalp(x2, n2));
                }
            }

            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The test of Mantel and Haenszel and the pooled odds ratio of a series of 2 by 2 tables that are typed: the analysis
        /// of Meta.RptMantel, which has the tables from a worksheet, with the same report.
        /// </summary>
        /// <remarks>
        /// What is worked out is in Meta.Mantel.  The exact methods are used if they are asked for here, whatever the
        /// preference of the meta-analysis menu is: the pooled odds ratio by conditional maximum likelihood with its exact
        /// limits and P values (ExactBB.Exact22K), for which counts that are not whole numbers are rounded; the exact limits
        /// of the odds ratio of each table; and the limits of I-squared from the non-central chi-square distribution.
        /// Tables of which none can be pooled are refused.
        /// </remarks>
        /// <param name="host">The preferences (the continuity correction), and where progress is shown.</param>
        /// <param name="parameters">"data": two columns with two rows for each table, as the tables are typed: the first
        /// column is of those with the characteristic and the second of those without it, and the first row of a table is of
        /// those with the outcome and the second of those without it; "cco": the confidence level, for which 0.95 is taken if
        /// it is not between 0 and 1; "try_exact": whether the exact methods are asked for; "plot_forest": whether the two
        /// plots of the odds ratios are made.</param>
        /// <returns>As Meta.RptMantel, with the strata named "stratum 1" and so on; "*chart" has the two plots, or
        /// nothing.</returns>
        public static StepOutput RptChiMantel(IPreferencesAndProgressBar host, ParameterBag parameters)
        {
            double p2M = 0;
            double p1M = 0;
            double p2F = 0;
            double p1F = 0;
            double llm = 0;
            double ulm = 0;
            double llf = 0;
            double ulf = 0;
            double eor = 0;
            DataFrame datFrame = parameters["data"].AsDataFrame;
            DoubleVariable datV0 = (DoubleVariable)datFrame.Variables[0];
            DoubleVariable datV1 = (DoubleVariable)datFrame.Variables[1];
            int rows = datFrame.MaxRows;

            if (rows <= 0)
                throw new InvalidDataException();

            int k = rows / 2;
            double[,] o = new double[k + 1, 4 + 1];
            string[] title = new string[k + 1];
            double[] axll = new double[k + 1];
            double[] axul = new double[k + 1];
            // The tables are typed with the characteristic in their columns and the outcome in their rows: the first column is of
            // those with the characteristic (the exposed), and the first row of those with the outcome.  A table is held as the
            // analysis of tables from a worksheet holds it: exposed with the outcome, not exposed with it, exposed without it, not
            // exposed without it
            for (int r = 1; r <= rows; r += 2)
            {
                int strat = 1 + r / 2;
                title[strat] = "stratum " + strat.ToString();
                o[strat, 1] = datV0.Data[r - 1];
                o[strat, 2] = datV1.Data[r - 1];
                o[strat, 3] = datV0.Data[r];
                o[strat, 4] = datV1.Data[r];
                if (o[strat, 1] < 0.0 || o[strat, 2] < 0.0 || o[strat, 3] < 0.0 || o[strat, 4] < 0.0)
                    throw new InvalidDataException("A count of table " + strat.ToString() + " is below 0.");
            }

            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;

            bool plotForest = parameters["plot_forest"].AsBoolean;
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0);

            // the exact methods are those of the conditional maximum likelihood estimate, of the limits of the odds ratio of each
            // table and of the limits of I-squared
            bool tryExact = parameters["try_exact"].AsBoolean;

            Meta.Mantel(host, 1, k, out int realk, o, out double rmh, out double ll, out double ul, out double x2, out double sk, cit, cco, out double[] odr, out double[] odw, out double[] dswt, out double[] odrl, out double[] odru, out double[] odx, out bool[] lerr, out bool[] uerr, out double qc, out double bd, out double dsor, out double dsx2, out double dsll, out double dsul, out bool[] cced, out double tausq, out bool[] included, out int ierr, tryExact);
            if (ierr != 0)
            {
                if (ierr != 99)
                    throw new InvalidDataException();
                throw new TemplateOperationCancelledException();
            }
            if (realk == 0)
                throw new TemplateOperationCancelledException("None of the tables can be pooled: each of them has no events, or events in every subject, or a group with no subjects.", "Mantel-Haenszel test");

            // Try exact Mantel
            if (tryExact)
            {
                // An exact method is of counts: counts that are not whole numbers are rounded, a half to the even number
                Rec2X2[] tbl = new Rec2X2[k + 1];
                for (int i = 1; i <= k; i++)
                {
                    double a = Math.Round(o[i, 1]);
                    double b = Math.Round(o[i, 2]);
                    double c = Math.Round(o[i, 3]);
                    double d = Math.Round(o[i, 4]);
                    tbl[i].Freq = 1;
                    tbl[i].A = a;
                    tbl[i].M1 = a + b;
                    tbl[i].N1 = a + c;
                    tbl[i].N0 = b + d;
                    tbl[i].IsInformative = (a * d != 0.0) | (b * c != 0.0);
                }
                bool useLogScale = false;
                new ExactBB().Exact22K(host, 1, k, Exact22KDataType.Type1, tbl, cco, out eor, out ulf, out llf, out ulm, out llm, out p1F, out p2F, out p1M, out p2M, ref useLogScale, out ierr);
            }
            else
            {
                ierr = -9;
            }
            if (ierr != 0)
            {
                eor = Constant.MISSING;
                ulf = Constant.MISSING;
                llf = Constant.MISSING;
                ulm = Constant.MISSING;
                llm = Constant.MISSING;
                p1F = Constant.MISSING;
                p2F = Constant.MISSING;
                p1M = Constant.MISSING;
                p2M = Constant.MISSING;
            }

            ParameterBag outputParameters = new();
            List<ParameterBag> inputsList = new();
            outputParameters.AddOutput("*inputs", inputsList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag inputsParameters = new();
                inputsList.Add(inputsParameters);
                inputsParameters.AddOutput("st", i);
                inputsParameters.AddOutput("a", o[i, 1]);
                inputsParameters.AddOutput("b", o[i, 2]);
                inputsParameters.AddOutput("c", o[i, 3]);
                inputsParameters.AddOutput("d", o[i, 4]);
                inputsParameters.AddOutput("lb", string.Empty);
            }
            outputParameters.AddOutput("pc", cco * 100);
            outputParameters.AddOutput("method", tryExact ? "CML" : "logit");
            List<ParameterBag> orList = new();
            outputParameters.AddOutput("*or", orList);
            for (int i = 1; i <= k; i++)
            {
                ParameterBag orParameters = new();
                orList.Add(orParameters);
                orParameters.AddOutput("st", i);
                orParameters.AddOutput("or", odr[i]);
                orParameters.AddOutput("yi", odr[i] > 0 ? Math.Log(odr[i]) : 0);
                orParameters.AddOutput("vi", Meta.VarianceOfLogOddsRatio(host, o, i));
                orParameters.AddOutput("lci", odrl[i]);
                orParameters.AddOutput("uci", odru[i]);
                orParameters.AddOutput("wt", 100 * odw[i] / Formatting.dsum(odw, 1));
                orParameters.AddOutput("dwt", 100 * dswt[i] / Formatting.dsum(dswt, 1));
                //orParameters.AddOutput("lb", Meta.GetMetaLabel(host, o, i, false, cced, title));
                string tmp = Meta.GetMetaLabel(host, o, i, false, cced, title);
                if (host.Preferences.DelayContinuityCorrection)
                {
                    tmp = tmp.Replace("[CC", "[late CC");
                }
                orParameters.AddOutput("lb", tmp);
                //if (host.Preferences.MetaExact & ((i) == Constant.MISSING | odru[i] == Constant.MISSING))
                //{
                //    Meta.OrciCorn(host, ref cco, ref o[i, 1], ref o[i, 2], ref o[i, 3], ref o[i, 4], out odr[i], out odrl[i], out odru[i]);
                //    orParameters = new ParameterBag();
                //    orList.Add(orParameters);
                //    orParameters.AddOutput("st", "* " + i.ToString());
                //    orParameters.AddOutput("or", string.Empty);
                //    orParameters.AddOutput("lci", host.RoundU(odrl[i]));
                //    orParameters.AddOutput("uci", host.RoundU(odru[i]));
                //    orParameters.AddOutput("wt", string.Empty);
                //    orParameters.AddOutput("dwt", string.Empty);
                //    orParameters.AddOutput("lb", " * [Cornfield limits]");
                //}
            }

            if (sk == 0)
            {
                outputParameters.AddOutput("meth", "Sato");
                outputParameters.AddOutput("odds", "undefined");
                outputParameters.AddOutput("from", ll);
                outputParameters.AddOutput("to", double.PositiveInfinity);
            }
            else
            {
                outputParameters.AddOutput("meth", "Robins-Breslow-Greenland");
                outputParameters.AddOutput("odds", rmh);
                outputParameters.AddOutput("from", ll);
                outputParameters.AddOutput("to", ul);
            }
            outputParameters.AddOutput("chi_mantel", x2);
            outputParameters.AddOutput("chi_p", PDF.chivalp(x2, 1.0));

            List<ParameterBag> cmlList = new();
            outputParameters.AddOutput("*cml", cmlList);
            if (ierr != -9)
            {
                ParameterBag cmlParameters = new();
                cmlList.Add(cmlParameters);
                cmlParameters.AddOutput("eor", eor);
                cmlParameters.AddOutput("llf", llf);
                cmlParameters.AddOutput("ulf", ulf);
                cmlParameters.AddOutput("p1f", p1F);
                cmlParameters.AddOutput("p2f", p2F);
                cmlParameters.AddOutput("llm", llm);
                cmlParameters.AddOutput("ulm", ulm);
                cmlParameters.AddOutput("p1m", p1M);
                cmlParameters.AddOutput("p2m", p2M);
            }

            outputParameters.AddOutput("bd", realk > 1 ? bd : 0.0);
            outputParameters.AddOutput("df", realk - 1);
            outputParameters.AddOutput("xp", PDF.chivalp(bd, realk - 1));

            outputParameters.AddOutput("qc", realk > 1 ? qc : 0.0);   // 0 by definition with one stratum, not the rounding residue of one squared deviation
            outputParameters.AddOutput("df_cochran", realk - 1);
            outputParameters.AddOutput("xp_cochran", PDF.chivalp(qc, realk - 1));
            outputParameters.AddOutput("tausq", tausq);
            Meta.IsquareNcc(host, qc, realk, cco, cit, out double isq, out double llisq, out double ulisq, tryExact);
            outputParameters.AddOutput("isq", isq);
            outputParameters.AddOutput("pc1", cco * 100);
            outputParameters.AddOutput("llisq", llisq);
            outputParameters.AddOutput("ulisq", ulisq);

            outputParameters.AddOutput("dsor", dsor);
            outputParameters.AddOutput("dsll", dsll);
            outputParameters.AddOutput("dsul", dsul);
            outputParameters.AddOutput("dsx2", dsx2);
            outputParameters.AddOutput("df_ds", 1);
            outputParameters.AddOutput("xp_ds", PDF.chivalp(dsx2, 1.0));

            Meta.GetLogitCi(host, o, k, cit, axll, axul);

            List<ParameterBag> eggerList = new();
            outputParameters.AddOutput("*egger", eggerList);
            ParameterBag eggerParameters = new();
            eggerList.Add(eggerParameters);
            bool biasReported = Meta.Metabias(host, eggerParameters, odr, axll, axul, k, ref cco, Transformation.Log);
            Meta.FewStrata(outputParameters, eggerList, biasReported);

            List<ParameterBag> harbordList = new();
            outputParameters.AddOutput("*harbord", harbordList);
            ParameterBag harbordParameters = new();
            harbordList.Add(harbordParameters);
            Meta.ModMetabias(host, harbordParameters, o, k, cco, 1);
            if (!biasReported)
                harbordList.Clear();

            IList<ParameterBag> chartList = new List<ParameterBag>();
            outputParameters.AddOutput("*chart", chartList);

            if (plotForest)
            {
                ParameterBag chartParameters = new();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, odw, title, rmh, ll, ul, cco, odr, odrl, odru, lerr, uerr, included, "Odds ratio meta-analysis plot [fixed effects]", 1, "odds ratio")));

                chartParameters = new ParameterBag();
                chartList.Add(chartParameters);
                chartParameters.AddOutput("chart", ChartRendererFactory.PrepForLater(ChartType.MH, new MHOptions(1, k, dswt, title, dsor, dsll, dsul, cco, odr, odrl, odru, lerr, uerr, included, "Odds ratio meta-analysis plot [random effects]", 1, "odds ratio")));
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The analysis of an r by c table of counts that is typed or is in a worksheet: the table is handed to Tables.SChi,
        /// which has the chi-square test, the measures of association and the exact and simulated P values.
        /// </summary>
        /// <param name="host">The preferences, questions to the user and where progress is shown.</param>
        /// <param name="parameters">"data": a column of counts for each column of the table; "cco": the confidence level;
        /// "doExact", "doMonteCarlo", "show_pc", "xp", "cs", "xs" and "specify_scores": the options, as Tables.SChi has them;
        /// and, if P values are to be simulated, "iterations", "ci" (the confidence level of a simulated P value) and "seed".
        /// A table without an observation is refused.</param>
        /// <returns>What Tables.SChi returns.</returns>
        public static StepOutput RptChiRbyC(ITemplateHost host, ParameterBag parameters)
        {
            double cco = parameters["cco"].AsDouble;
            DataFrame dataFrame = parameters["data"].AsDataFrame;
            int rows = dataFrame.MaxRows;
            int cols = dataFrame.VariableCount;
            double[,] a = new double[rows + 1, cols + 1];
            double t = 0;
            for (int r = 1; r <= rows; r++)
            {
                for (int c = 1; c <= cols; c++)
                {
                    double a1 = ((DoubleVariable)dataFrame.Variables[c - 1]).Data[r - 1];
                    a[r, c] = a1;
                    t += a1;
                }
            }
            if (t <= 0.0)
                throw new InvalidDataException();

            bool doExact = parameters["doExact"].AsBoolean;
            bool doMonteCarlo = parameters["doMonteCarlo"].AsBoolean;
            bool pc = parameters["show_pc"].AsBoolean;
            bool xp = parameters["xp"].AsBoolean;
            bool cs = parameters["cs"].AsBoolean;
            bool xs = parameters["xs"].AsBoolean;
            bool specifyScores = parameters["specify_scores"].AsBoolean;
            int iterations = 1000000;
            double mcci = 0.99;
            int seed = 0;
            if (doMonteCarlo)
            {
                iterations = parameters["iterations"].AsInt32;
                mcci = parameters["ci"].AsDouble;
                seed = parameters["seed"].AsInt32;
            }

            return new StepOutput(Tables.SChi(host, ref cco, a, rows, cols, doExact, doMonteCarlo, pc, xp, cs, xs, specifyScores, mcci, iterations, seed));
        }

        /// <summary>
        /// Woolf's analysis of a series of 2 by 2 tables that are typed (Tables.Woolf).
        /// </summary>
        /// <param name="parameters">"data": two columns with two rows for each table, as the tables are typed; "cco": the
        /// confidence level, for which 0.95 is taken if it is not between 0 and 1; "show_intermediates": whether the report has
        /// the figures of each table.</param>
        /// <returns>What Tables.Woolf returns.</returns>
        public static StepOutput RptChiWoolf(ParameterBag parameters)
        {
            int rc;

            DataFrame datFrame = parameters["data"].AsDataFrame;
            DoubleVariable datV0 = (DoubleVariable)datFrame.Variables[0];
            DoubleVariable datV1 = (DoubleVariable)datFrame.Variables[1];
            int rows = datFrame.MaxRows;
            if (rows <= 0)
                throw new InvalidDataException();

            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 || cco >= 1.0)
                cco = 0.95;

            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0);

            bool showIntermediates = parameters["show_intermediates"].AsBoolean;

            int k = rows / 2;
            double[,] o = new double[k + 1, 5];
            int cnt = 0;
            for (rc = 1; rc <= rows; rc += 2)
            {
                cnt += 1;
                double rtd = datV0.Data[rc - 1];
                o[cnt, 1] = rtd;
                rtd = datV1.Data[rc - 1];
                o[cnt, 2] = rtd;
                rtd = datV0.Data[rc];
                o[cnt, 3] = rtd;
                rtd = datV1.Data[rc];
                o[cnt, 4] = rtd;
            }

            return new StepOutput(Tables.Woolf(o, k, showIntermediates, cit, cco, out bool _));
        }

        /// <summary>
        /// The simulated exact P value of the chi-square for trend of a 2 by k table: the share of tables drawn at random, with
        /// the totals of the rows and of the columns of the table observed, whose chi-square for trend is no less than that of
        /// the table observed; with the limits of Clopper and Pearson of that share (MathDbl.binci).
        /// </summary>
        /// <remarks>
        /// The tables are drawn by Rcont2, each with the probability that the totals give it, and are counted by
        /// Chi2TrendResample.  They are of whole numbers: counts that are not are rounded, a half to the even number, and the
        /// table observed is that of the counts as rounded, of which the report tells.  A P value is not simulated, and the
        /// report says why, if a row or a column has no observations, if the scores are all the same, if the table has more
        /// than 5,000,000 observations, or if the simulation is stopped before a table is drawn; one that is stopped later gives
        /// the P value of the tables drawn.
        /// </remarks>
        /// <param name="host">Where progress is shown, and the simulation can be stopped.</param>
        /// <param name="parameters">"data": as for RptChi2ByN, the scores being 1 to k if there is no third column;
        /// "iterations": the number of tables to draw; "ci": the confidence level of the limits; "seed": the seed of the random
        /// numbers, or 0 for one from the clock.</param>
        /// <returns>"*result": a row with "p", "pc" (the confidence level as a percentage), "ll", "ul", "warn" (what the report
        /// says after the limits of a one sided interval), "k" (the number of tables drawn), "seed_fmt" and "rounded" (what the
        /// report says if counts were rounded); "*note": a row with "note" if no P value is simulated.</returns>
        public static StepOutput RptChi2ByNWithTrendSimulateExactP(IProgressBarHost host, ParameterBag parameters)
        {
            int iterations = parameters["iterations"].AsInt32;
            double ci = parameters["ci"].AsDouble;
            int seed = parameters["seed"].AsInt32;

            DataFrame datFrame = parameters["data"].AsDataFrame;
            bool hasSpecifiedTrend = datFrame.VariableCount == 3;
            DoubleVariable datV0 = (DoubleVariable)datFrame.Variables[0];
            DoubleVariable datV1 = (DoubleVariable)datFrame.Variables[1];
            DoubleVariable datV2 = null;
            if (hasSpecifiedTrend)
                datV2 = (DoubleVariable)datFrame.Variables[2];
            int rows = datFrame.MaxRows;
            const int cols = 2;

            // the report has the P value, or what it says in the place of one
            ParameterBag outputParameters = new();
            List<ParameterBag> resultList = new();
            outputParameters.AddOutput("*result", resultList);
            List<ParameterBag> noteList = new();
            outputParameters.AddOutput("*note", noteList);
            StepOutput NotSimulated(string why)
            {
                ParameterBag noteParameters = new();
                noteList.Add(noteParameters);
                noteParameters.AddOutput("note", "The P value is not simulated: " + why + ".");
                return new StepOutput(outputParameters);
            }

            // The tables that are drawn are of whole numbers: counts that are not are rounded, a half to the even number, and the
            // table observed is the table of the counts as rounded
            double[,] counts = new double[rows + 1, 3];
            double[] wt = new double[rows + 1];
            double successes = 0.0;
            double failures = 0.0;
            bool rounded = false;
            bool sameScores = true;
            for (int row = 1; row <= rows; row++)
            {
                counts[row, 1] = Math.Round(datV0.Data[row - 1]);
                counts[row, 2] = Math.Round(datV1.Data[row - 1]);
                if (counts[row, 1] != datV0.Data[row - 1] || counts[row, 2] != datV1.Data[row - 1])
                    rounded = true;
                if (counts[row, 1] + counts[row, 2] <= 0.0)
                    return NotSimulated("row " + row.ToString() + " has no observations");
                successes += counts[row, 1];
                failures += counts[row, 2];
                wt[row] = hasSpecifiedTrend ? datV2.Data[row - 1] : row;
                if (wt[row] != wt[1])
                    sameScores = false;
            }
            if (successes <= 0.0 || failures <= 0.0)
                return NotSimulated("there are no " + (successes <= 0.0 ? "successes" : "failures"));
            if (sameScores)
                return NotSimulated("the scores are all the same, so that there is no trend to test");
            if (successes + failures > MaximumSimulated)
                return NotSimulated("the simulation is for tables of no more than 5,000,000 observations");

            int[,] x = new int[rows + 1, 3];
            for (int row = 1; row <= rows; row++)
            {
                x[row, 1] = Convert.ToInt32(counts[row, 1]);
                x[row, 2] = Convert.ToInt32(counts[row, 2]);
            }
            int ierror = 0;
            Chi2TrendResample(host, x, wt, rows, cols, iterations, out int r, out int actualIterations, seed, ref ierror);
            if (actualIterations < 1)
                return NotSimulated("the simulation was stopped before a table was drawn");

            ParameterBag resultParameters = new();
            resultList.Add(resultParameters);
            double p = r / (double)actualIterations;
            resultParameters.AddOutput("p", p);
            //  CI
            MathDbl.binci(r, actualIterations, out double ll, out double ul, ci, out string warn);
            resultParameters.AddOutput("pc", 100.0 * ci);
            resultParameters.AddOutput("ll", ll);
            resultParameters.AddOutput("ul", ul);
            resultParameters.AddOutput("warn", warn);
            resultParameters.AddOutput("k", actualIterations.ToString());
            resultParameters.AddOutput("seed_fmt", seed.ToString());
            resultParameters.AddOutput("rounded", rounded ? "; counts that are not whole numbers were rounded" : string.Empty);
            return new StepOutput(outputParameters);
        }

        // the greatest number of observations of a table that is simulated
        private const int MaximumSimulated = 5000000;

        /// <summary>
        /// Why tables with the totals of the table observed cannot be drawn, from the fault that Rcont2 gives.
        /// </summary>
        private static string NotDrawn(int fault) => "Monte Carlo simulation not possible: " + fault switch
        {
            3 or 4 => "all row and column totals must be greater than zero",
            5 => "the table has more than 5,000,000 observations",
            _ => "the tables could not be drawn (fault " + fault.ToString() + ")"
        };

        ///  <summary>
        ///  Simulated exact P for Cochran-Armitage trend test
        ///  </summary>
        /// <param name="host"></param>
        /// <param name="x">(1..nrow,1..ncol) input 2 by k table</param>
        ///  <param name="wt">(1..nrow) input weights</param>
        ///  <param name="nrow">rows</param>
        ///  <param name="ncol">columns (could modify this for r by c)</param>
        ///  <param name="iter">Monte Carlo iterations</param>
        ///  <param name="r">Monte Carlo P numerator</param>
        /// <param name="actualIterations">The number of Monte Carlo iterations actually performed</param>
        /// <param name="iseed">RNG seed (0 for automatic)</param>
        ///  <param name="ierror">return non-zero if fault (-1 if interrupted)</param>
        ///  <remarks>
        ///  With the totals of the rows and columns given, the chi-square for trend of a table is a constant times the square of
        ///  T = sum(score x successes), the scores being the scores less their mean over the observations (Centred): T is then
        ///  what it is above its expectation, and a number that all the scores have in common has no part in it.  A table is
        ///  counted if its T is as far from 0 as that of the table observed, or short of that by less than the rounding of the sum
        ///  could account for: 1 part in 10^12 of the sum of the same products, each without its sign, of the table observed or of
        ///  the table drawn, whichever is the greater.  Two tables that have the same chi-square have it from sums that are
        ///  rounded differently, and are both to be counted.
        ///  </remarks>
        private static void Chi2TrendResample(IProgressBarHost host, int[,] x, double[] wt, int nrow, int ncol, int iter, out int r, out int actualIterations, int iseed, ref int ierror)
        {
            int[] ncolt = new int[ncol + 1];
            int[] nrowt = new int[nrow + 1];
            int ntotal = 0;
            int i;
            int j;
            MersenneTwister rng = new();

            int bootsDivisor = Math.Max(1, iter / 1000);

            using IProgressBar progress = host.StartProgress("Simulating exact P", true);
            if (iseed != 0)
                rng.Seed(iseed);
            else
                rng.Seed();

            for (j = 1; j <= nrow; j++)
            {
                for (i = 1; i <= ncol; i++)
                {
                    nrowt[j] += x[j, i];
                    ncolt[i] += x[j, i];
                }
            }

            int maxtot = MaximumSimulated;
            bool primed = false;

            double[] fact = new double[ncol + 1];
            int[] jwork = new int[ncol + 1];

            // the scores less their mean over the observations; and the T of the table observed, which the tables drawn take the
            // place of, without its sign, with the sum of its terms without theirs
            double[] rowTotals = new double[nrow + 1];
            for (j = 1; j <= nrow; j++)
                rowTotals[j] = nrowt[j];
            double[] score = Centred(wt, rowTotals, nrow);
            double FromExpected(int[,] table, out double terms)
            {
                double sum = 0.0;
                terms = 0.0;
                for (int row = 1; row <= nrow; row++)
                {
                    sum += score[row] * table[row, 1];
                    terms += Math.Abs(score[row]) * table[row, 1];
                }
                return Math.Abs(sum);
            }
            double observed = FromExpected(x, out double observedTerms);

            r = 0;
            for (i = 1; i <= iter; i++)
            {
                if (i % bootsDivisor == 0)
                {
                    if (progress.Update(i / (double)iter))
                    {
                        ierror = -1; //  Interrupted
                        break;
                    }
                }
                Rcont2(1, nrow, ncol, nrowt, ncolt, ref primed, ref x, ref fact, ref ntotal, ref maxtot, ref jwork, out ierror, ref rng);
                if (ierror != 0)
                    throw new InvalidDataException(NotDrawn(ierror));
                double drawn = FromExpected(x, out double drawnTerms);
                if (drawn >= observed - 1.0E-12 * Math.Max(observedTerms, drawnTerms))
                    r += 1;
            }
            actualIterations = i - 1;
        }

        ///  <summary>
        ///  Simulated exact P for R by C chi-square for independent, for trend, for equality and g-square
        ///  </summary>
        ///  <param name="host"></param>
        ///  <param name="o">(1..nrow,1..ncol) input 2 by k table</param>
        ///  <param name="rowScore">(1..nrow) row scores for trend test</param>
        ///  <param name="colScore">(1..nrow) column scores for trend test</param>
        ///  <param name="nrow">rows</param>
        ///  <param name="ncol">columns</param>
        ///  <param name="iter">Monte Carlo iterations</param>
        ///  <param name="rx2">Monte Carlo P numerator for independece chi-square</param>
        ///  <param name="rx2Eq">Monte Carlo P numerator for equality chi-square</param>
        ///  <param name="rx2Trend">Monte Carlo P numerator for trend chi-square</param>
        ///  <param name="rg2">Monte Carlo P numerator for g-square</param>
        ///  <param name="actualIterations">The number of Monte Carlo iterations actually performed</param>
        ///  <param name="iseed">RNG seed (0 for automatic)</param>
        ///  <param name="ierror">return non-zero if fault (-1 if interrupted)</param>
        ///  <remarks>
        ///  The table observed is that of the counts, rounded if they are not whole numbers.  What is compared of each statistic
        ///  (ChiRC) is a number that is nothing for a table that is what the totals expect, and that has nothing in it that is
        ///  the same for every table: the scores are the scores less their means over the observations (Centred), and
        ///  G-square is made of terms of which none is below 0.  A table is counted if its number is no less than that of the
        ///  table observed, or short of it by less than the rounding of its sums could account for: 1 part in 10^12 of the
        ///  statistic, for chi-square and G-square, and of the sum of its terms without their signs, for the two tests that have
        ///  scores.  Two tables that have the same statistic have it from sums that are rounded differently, and are both to be
        ///  counted; and a table whose statistic is less is not, however many subjects there are and whatever number the
        ///  scores have in common.
        ///  </remarks>
        public static void ChiRCResample(IProgressBarHost host, double[,] o, double[] rowScore, double[] colScore, int nrow, int ncol, int iter, out int rx2, out int rx2Eq, out int rx2Trend, out int rg2, out int actualIterations, int iseed, ref int ierror)
        {
            int[] ncolt = new int[ncol + 1];
            int[] nrowt = new int[nrow + 1];
            int[,] x = new int[nrow + 1, ncol + 1];
            int ntotal = 0;
            int i;
            int j;
            MersenneTwister rng = new();

            int bootsDivisor = Math.Max(1, iter / 1000);

            using IProgressBar progress = host.StartProgress("Simulating exact P", true);

            if (iseed != 0)
            {
                rng.Seed(iseed);
            }
            else { rng.Seed(); }

            for (j = 1; j <= nrow; j++)
            {
                for (i = 1; i <= ncol; i++)
                {
                    x[j, i] = Convert.ToInt32(o[j, i]);
                    nrowt[j] += x[j, i];
                    ncolt[i] += x[j, i];
                }
            }

            int maxtot = MaximumSimulated;
            bool primed = false;

            double[] fact = new double[ncol + 1];
            int[] jwork = new int[ncol + 1];

            rx2 = 0;
            rg2 = 0;
            rx2Eq = 0;
            rx2Trend = 0;
            actualIterations = 0;

            // the totals, which every table that is drawn has, and the scores less their means over the observations
            double[] rtot = new double[nrow + 1];
            double[] ctot = new double[ncol + 1];
            double gtot = 0.0;
            for (j = 1; j <= nrow; j++)
            {
                rtot[j] = nrowt[j];
                gtot += nrowt[j];
            }
            for (i = 1; i <= ncol; i++)
                ctot[i] = ncolt[i];
            double[] rowCentred = Centred(rowScore, rtot, nrow);
            double[] colCentred = Centred(colScore, ctot, ncol);

            // the table observed, which the tables drawn take the place of
            ChiRC(x, nrow, ncol, rowCentred, colCentred, rtot, ctot, gtot, out double x2, out double g2, out double cross, out double crossTerms, out double between, out double betweenTerms);

            for (i = 1; i <= iter; i++)
            {
                if (i % bootsDivisor == 0)
                {
                    if (progress.Update(i / (double)iter))
                    {
                        ierror = -1; //  Interrupted
                        break;
                    }
                }
                Rcont2(1, nrow, ncol, nrowt, ncolt, ref primed, ref x, ref fact, ref ntotal, ref maxtot, ref jwork, out ierror, ref rng);
                if (ierror != 0)
                    throw new InvalidDataException(NotDrawn(ierror));
                ChiRC(x, nrow, ncol, rowCentred, colCentred, rtot, ctot, gtot, out double x2rep, out double g2rep, out double crossRep, out double crossTermsRep, out double betweenRep, out double betweenTermsRep);
                actualIterations++;
                if (x2rep >= x2 - 1.0E-12 * x2)
                    rx2++;
                if (g2rep >= g2 - 1.0E-12 * g2)
                    rg2++;
                if (betweenRep >= between - 1.0E-12 * Math.Max(betweenTerms, betweenTermsRep))
                    rx2Eq++;
                if (Math.Abs(crossRep) >= Math.Abs(cross) - 1.0E-12 * Math.Max(crossTerms, crossTermsRep))
                    rx2Trend++;
            }
        }

        /// <summary>
        /// The P value of a statistic by simulation: the proportion r / its of the tables drawn whose statistic was no less than that
        /// of the table observed, with exact (Clopper-Pearson) confidence limits of the proportion at the level cco.
        /// </summary>
        /// <returns>The results, or nothing if the simulation failed (ierror other than 0, or -1 for one that was stopped and has
        /// the tables drawn so far).</returns>
        public static ParameterBag MCResults(int ierror, int r, int its, int seed, double cco)
        {
            if (ierror == 0 || ierror == -1 /* interrupted but partial results returned */ )
            {
                ParameterBag outputParameters = new();
                double p = r / (double)its;
                MathDbl.binci(r, its, out double ll, out double ul, cco, out string warn);
                outputParameters.AddOutput(new Dictionary<string, object>
                {
                    { "p", p },
                    { "pc", 100.0 * cco },
                    { "ll", ll },
                    { "ul", ul },
                    { "warn", warn },
                    { "its", its },
                    { "seed", seed }
                });
                return outputParameters;
            }
            else
                return null;
        }

        /// <summary>
        /// What is compared of a table that is drawn in the simulation and of the table observed, for each of the four statistics
        /// that Tables.SChi works out for the table observed: a number that is nothing for a table that is what the totals of the
        /// rows and columns expect, and that rises with the statistic.
        /// </summary>
        /// <param name="x">The table, from row 1 and column 1.</param>
        /// <param name="rows">The number of rows.</param>
        /// <param name="cols">The number of columns.</param>
        /// <param name="rowscore">The scores of the rows, less their mean over the observations (Centred).</param>
        /// <param name="colscore">The scores of the columns, less their mean over the observations.</param>
        /// <param name="rtot">The totals of the rows.</param>
        /// <param name="ctot">The totals of the columns.</param>
        /// <param name="gtot">The number of observations.</param>
        /// <param name="x2">On return, chi-square: the sum over the cells of (observed - expected)^2 / expected.</param>
        /// <param name="g2">On return, half of G-square: the sum over the cells of x log(x / expected) - (x - expected)
        /// (Deviance), of which no term is below 0; what is taken from each term adds to nothing over the cells.</param>
        /// <param name="cross">On return, the sum over the cells of the row score times the column score times the count.  The
        /// chi-square for trend is a constant times the square of this.</param>
        /// <param name="crossTerms">On return, the same sum with each term without its sign, to which the rounding of cross is
        /// in proportion.</param>
        /// <param name="between">On return, the sum over the columns of the square of the sum of the row scores of the
        /// observations of the column, over the total of the column.  The chi-square for the equality of the mean scores is a
        /// constant times this.</param>
        /// <param name="betweenTerms">On return, the sum over the columns of the sum of the row scores of the column, without
        /// its sign, times the same sum with each score without its sign, over the total of the column: the rounding of between
        /// is in proportion to it.</param>
        private static void ChiRC(int[,] x, int rows, int cols, double[] rowscore, double[] colscore, double[] rtot, double[] ctot, double gtot, out double x2, out double g2, out double cross, out double crossTerms, out double between, out double betweenTerms)
        {
            x2 = 0.0;
            g2 = 0.0;
            cross = 0.0;
            crossTerms = 0.0;
            between = 0.0;
            betweenTerms = 0.0;
            for (int c = 1; c <= cols; c++)
            {
                double xi = 0.0;
                double terms = 0.0;
                for (int r = 1; r <= rows; r++)
                {
                    xi += rowscore[r] * x[r, c];
                    terms += Math.Abs(rowscore[r]) * x[r, c];
                    double ef = rtot[r] * ctot[c] / gtot;
                    if (ef != 0.0)
                    {
                        x2 += Math.Pow(x[r, c] - ef, 2.0) / ef;
                        g2 += Deviance(x[r, c], ef);
                    }
                }
                cross += xi * colscore[c];
                crossTerms += terms * Math.Abs(colscore[c]);
                if (ctot[c] != 0.0)
                {
                    between += xi * xi / ctot[c];
                    betweenTerms += Math.Abs(xi) * terms / ctot[c];
                }
            }
        }

        /// <summary>
        /// Scores less their mean over the observations, each score having the weight of the total of its row or column.
        /// </summary>
        /// <remarks>
        /// A test that has scores is the same whatever number is added to all of them.  Its sums are made of the scores less
        /// their mean, so that what the scores have in common is gone before anything is squared or added up: it would
        /// take the figures of the sums from them, and it would make their rounding as great as what differs from table to
        /// table.  The first score is taken from each score before the mean is worked out, for the same reason.
        /// The scores are first made whole numbers, if they are decimal numbers (Whole): a test that has scores is the same
        /// whatever number the scores are multiplied by, too.
        /// </remarks>
        /// <param name="score">The scores, from element 1.</param>
        /// <param name="total">The totals of the rows or columns that the scores are of, from element 1.</param>
        /// <param name="n">The number of scores.</param>
        /// <returns>The scores, times a power of 10 if that makes whole numbers of them, less their mean; from element 1.  They
        /// are not less their mean if there are no observations.</returns>
        internal static double[] Centred(double[] score, double[] total, int n)
        {
            double[] whole = Whole(score, n);
            double[] centred = new double[n + 1];
            double sum = 0.0;
            double observations = 0.0;
            for (int i = 1; i <= n; i++)
            {
                sum += (whole[i] - whole[1]) * total[i];
                observations += total[i];
            }
            double mean = observations > 0.0 ? sum / observations : -whole[1];
            for (int i = 1; i <= n; i++)
                centred[i] = whole[i] - whole[1] - mean;
            return centred;
        }

        /// <summary>
        /// Scores that are decimal numbers, as whole numbers: each times the least power of 10 that makes whole numbers of them
        /// all.
        /// </summary>
        /// <remarks>
        /// A score such as 0.7 or 1000000.7 is held as the number nearest to it that the computer has, which is not the score
        /// to its last figure; and the differences of scores so held are not those of the scores, by a part that is the greater
        /// the more the scores have in common.  Tables that have the same statistic with the scores as they were typed then
        /// have statistics that differ.  A whole number is held as it is, up to 2^53.  A score is taken for a decimal number
        /// of so many places if the whole number that it makes, over the power of 10, is held as the same number as the score:
        /// that is the number that the score is held as when it is typed.
        /// </remarks>
        /// <param name="score">The scores, from element 1.</param>
        /// <param name="n">The number of scores.</param>
        /// <returns>The whole numbers, from element 1; or the scores as they are, if they are not decimal numbers of up to 15
        /// places whose whole numbers are below 2^53.</returns>
        private static double[] Whole(double[] score, int n)
        {
            const double most = 9007199254740992.0;
            double power = 1.0;
            for (int places = 0; places <= 15; places++)
            {
                double[] whole = new double[n + 1];
                bool all = true;
                for (int i = 1; i <= n && all; i++)
                {
                    whole[i] = Math.Round(score[i] * power);
                    all = Math.Abs(whole[i]) < most && whole[i] / power == score[i];
                }
                if (all)
                    return whole;
                power *= 10.0;
            }
            return score;
        }

        /// <summary>
        /// x log(x / e) - (x - e), for a count x whose expectation e is above 0: the part of half of G-square that is from a
        /// cell.  It is 0 if x is e, and above 0 if it is not.
        /// </summary>
        /// <remarks>
        /// If x is within a tenth of e the two parts are almost the same, and their difference would have few figures: it is
        /// worked out from the series e (t^2 / 2 - t^3 / 6 + t^4 / 12 - ...), where t is (x - e) / e and the term of t^k is
        /// over k (k - 1).
        /// </remarks>
        private static double Deviance(double x, double e)
        {
            if (x == 0.0)
                return e;
            double t = (x - e) / e;
            if (Math.Abs(t) >= 0.1)
                return x * Math.Log(x / e) - (x - e);
            double sum = 0.0;
            double power = -t;
            for (int k = 2; k <= 60; k++)
            {
                power *= -t;
                double term = power / (k * (k - 1.0));
                sum += term;
                if (Math.Abs(term) <= 1.0E-17 * Math.Abs(sum))
                    break;
            }
            return e * sum;
        }

        ///  <remarks>
        ///     WM Patefield,
        ///     Algorithm AS 159:
        ///     An Efficient Method of Generating RXC Tables with Given Row and Column Totals,
        ///     Applied Statistics, Volume 30, Number 1, 1981, pages 91-97.
        ///  </remarks>
        ///  <param name="lowerBound">0 for 0-based arrays (indices 0..nrow-1, 0..ncol-1); 1 for 1-based arrays (indices 1..nrow, 1..ncol).</param>
        /// <param name="matrix"></param>
        /// <param name="fact">1..(nrow+ncol) to store log-factorials</param>
        /// <param name="nrow"></param>
        /// <param name="ncol"></param>
        /// <param name="nrowt"></param>
        /// <param name="ncolt"></param>
        /// <param name="primed"></param>
        /// <param name="ntotal"></param>
        /// <param name="maxtot"></param>
        /// <param name="jwork"></param>
        /// <param name="ierror"></param>
        /// <param name="rng"></param>
        public static void Rcont2(int lowerBound, int nrow, int ncol, int[] nrowt, int[] ncolt, ref bool primed, ref int[,] matrix, ref double[] fact, ref int ntotal, ref int maxtot, ref int[] jwork, out int ierror, ref MersenneTwister rng)
        {
            ierror = 0;

            //   On user's signal, set up the factorial table.
            if (primed == false)
            {
                primed = true;
                if (nrow <= 1)
                {
                    ierror = 1;
                    return;
                }
                if (ncol <= 1)
                {
                    ierror = 2;
                    return;
                }
                for (int i = lowerBound; i < nrow + lowerBound; i++)
                {
                    if (nrowt[i] <= 0)
                    {
                        ierror = 3;
                        return;
                    }
                }
                for (int j = lowerBound; j < ncol + lowerBound; j++)
                {
                    if (ncolt[j] <= 0)
                    {
                        ierror = 4;
                        return;
                    }
                }
                int ncolsum = 0;
                int nrowsum = 0;
                for (int i = lowerBound; i < nrow + lowerBound; i++)
                    nrowsum += nrowt[i];
                for (int j = lowerBound; j < ncol + lowerBound; j++)
                    ncolsum += ncolt[j];
                if (ncolsum != nrowsum)
                {
                    ierror = 6;
                    return;
                }
                ntotal = nrowsum;
                if (maxtot < ntotal)
                {
                    ierror = 5;
                    return;
                }
                fact = new double[ntotal + 2];
                //   Calculate log-factorials.
                double x = 0.0;
                fact[1] = 0.0;
                for (int i = 1; i <= ntotal; i++)
                {
                    x += Math.Log(i);
                    fact[i + 1] = x;
                }
            }

            //   Construct a random matrix.

            for (int j = lowerBound; j <= ncol - 2 + lowerBound; j++)
                jwork[j] = ncolt[j];

            int jc = ntotal;
            int ib = 0;

            for (int l = lowerBound; l <= nrow - 2 + lowerBound; l++)
            {

                int nrowtl = nrowt[l];
                int ia = nrowtl;
                int ic = jc;
                jc -= nrowtl;

                for (int m = lowerBound; m <= ncol - 2 + lowerBound; m++)
                {

                    int id = jwork[m];
                    int ie = ic;
                    ic -= id;
                    ib = ie - ia;
                    int ii = ib - id;

                    //   Test for zero entries in matrix.

                    // the columns from this one on have nothing left: the rest of the row is nothing, and the counts of the row
                    // that have been drawn are as they are
                    if (ie == 0)
                    {
                        ia = 0;
                        for (int j = m; j < ncol + lowerBound; j++)
                            matrix[l, j] = 0;
                        break;
                    }

                    //   Generate a pseudo-random number.
                    double r = rng.NextDouble();

                    //   Compute the conditional expected value of MATRIX(L,M).

                    bool done1 = false;
                    bool done2 = false;

                    int nlm;
                    do
                    {
                        nlm = (int)Math.Floor((double)ia * id / ie + 0.5);   // as floating-point numbers: the product of two totals may be above what a whole number of 32 bits holds

                        int iap = ia + 1;
                        int idp = id + 1;
                        int igp = idp - nlm;
                        int ihp = iap - nlm;
                        int nlmp = nlm + 1;
                        int iip = ii + nlmp;
                        double x = Math.Exp(fact[iap] + fact[ib + 1] + fact[ic + 1] + fact[idp] - fact[ie + 1] - fact[nlmp] - fact[igp] - fact[ihp] - fact[iip]);

                        if (r <= x)
                            break;

                        double sumprb = x;
                        double y = x;
                        int nll = nlm;
                        bool lsp = false;
                        bool lsm = false;

                        //   Increment entry in row L, column M.

                        while (lsp == false)
                        {

                            double j = (double)(id - nlm) * (ia - nlm);

                            if (j == 0)
                                lsp = true;
                            else
                            {

                                nlm += 1;
                                x *= j / ((double)nlm * (ii + nlm));
                                sumprb += x;

                                if (r <= sumprb)
                                {
                                    done1 = true;
                                    break;
                                }

                            }

                            done2 = false;

                            while (lsm == false)
                            {

                                //   Decrement the entry in row L, column M.

                                j = (double)nll * (ii + nll);

                                if (j == 0)
                                {
                                    lsm = true;
                                    break;
                                }

                                nll -= 1;
                                y *= j / ((double)(id - nll) * (ia - nll));
                                sumprb += y;

                                if (r <= sumprb)
                                {
                                    nlm = nll;
                                    done2 = true;
                                    break;
                                }

                                if (lsp == false)
                                    break;
                            }

                            if (done2)
                                break;
                        }

                        if (done1 || done2)
                            break;

                        r = rng.NextDouble();
                        r = sumprb * r;

                    }
                    while (true);

                    matrix[l, m] = nlm;
                    ia -= nlm;
                    jwork[m] -= nlm;

                }

                matrix[l, ncol - 1 + lowerBound] = ia;
            }

            //   Compute the last row.
            for (int m = lowerBound; m <= ncol - 2 + lowerBound; m++)
                matrix[nrow - 1 + lowerBound, m] = jwork[m];
            matrix[nrow - 1 + lowerBound, ncol - 1 + lowerBound] = ib - matrix[nrow - 1 + lowerBound, ncol - 2 + lowerBound];
        }
    }
}
