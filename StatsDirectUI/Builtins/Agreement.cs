using StatsDirect.Numerics;
using StatsDirect.Data;
using StatsDirect.Templates;
using StatsDirect.Utilities;

using System;

namespace StatsDirect.Builtins
{
    public static class Agreement
    {

        /// <summary>
        /// The universal measure of agreement R between observers who each measure every one of a number of objects, in one dimension
        /// or in several.  What an observer measures of an object is a point, and delta is the mean distance between the points of two
        /// observers for the same object, over the objects and the pairs of observers.  If what the observers measured had nothing to do
        /// with which object it was, each observer's points could as well be dealt among the objects in any other order: the mean of
        /// delta over all such dealings is the delta expected by chance, and R is 1 - delta / (that mean), which is 1 when the observers
        /// agree in every measurement and about 0 when they agree no better than chance.  P is the probability of a delta as small as
        /// the delta observed, from the distribution that has the mean, variance and skewness of delta over the dealings (see Agree and
        /// Pgamt).  With one observer as a standard, each of the others is compared with the standard alone (see AgreeStandard).
        /// </summary>
        /// <param name="parameters">"data": the measurements, in one column; "raters", "objects" and "categories": the observer, the
        /// object and the dimension of each measurement ("categories" is left out when there is one dimension); "reference": a mark
        /// against the observer who is the standard, left out when there is none.</param>
        public static StepOutput RptUniversalAgreement(ParameterBag parameters)
        {
            int nobs = 0;
            string title = null;
            string refIdent = null;
            GatherUniversalAgreementData(parameters, out int n, out int b, out int c, out double[,,] data, out bool standard, ref nobs, ref title, ref refIdent);

            double delta;
            double edel;
            double var;
            double gam;
            double r;
            double p;

            if (standard)
                AgreeStandard(n, b, c, data, out delta, out edel, out var, out gam, out r, out p);
            else
                Agree(n, b, c, data, out delta, out edel, out var, out gam, out r, out p);

            ParameterBag outputParameters = new();

            outputParameters.AddOutput("name", title);
            outputParameters.AddOutput("nobs", nobs);
            outputParameters.AddOutput("n", n);
            outputParameters.AddOutput("b", b);
            outputParameters.AddOutput("c", c);
            outputParameters.AddOutput("ref", refIdent);
            outputParameters.AddOutput("delta", delta);
            outputParameters.AddOutput("edel", edel);
            outputParameters.AddOutput("vardel", var);
            outputParameters.AddOutput("skewdel", gam);
            outputParameters.AddOutput("R", r);
            outputParameters.AddOutput("p", p);
            return new StepOutput(outputParameters);
        }


        /// <summary>
        /// Reads the measurements into data[object, observer, dimension], each index from 1.  Every observer must have one measurement,
        /// and no more, of every object in every dimension.  With a standard, the numbers of the observers are changed round so that
        /// the standard is the first.
        /// </summary>
        /// <param name="parameters">As for RptUniversalAgreement.</param>
        /// <param name="n">On return, the number of objects.</param>
        /// <param name="b">On return, the number of observers.</param>
        /// <param name="c">On return, the number of dimensions.</param>
        /// <param name="data">On return, the measurements.</param>
        /// <param name="standard">On return, true if an observer is the standard.</param>
        /// <param name="nobs">On return, the number of measurements read.</param>
        /// <param name="title">On return, the title of the measurements, with the names of the dimensions if there are any.</param>
        /// <param name="refIdent">On return, the name of the standard for the report, or "None".</param>
        private static void GatherUniversalAgreementData(ParameterBag parameters, out int n, out int b, out int c, out double[,,] data, out bool standard, ref int nobs, ref string title, ref string refIdent)
        {
            DataFrame dataFrame = parameters["data"].AsDataFrame;
            DoubleVariable dataVariable = (DoubleVariable)dataFrame.Variables[0];
            DataFrame ratersFrame = parameters["raters"].AsDataFrame;
            ClassifierVariable ratersVariable = (ClassifierVariable)ratersFrame.Variables[0];
            DataFrame objectsFrame = parameters["objects"].AsDataFrame;
            ClassifierVariable objectsVariable = (ClassifierVariable)objectsFrame.Variables[0];
            bool hasCategories = parameters.ContainsKey("categories") && parameters["categories"] != null;
            ClassifierVariable categoriesVariable = null;
            if (hasCategories)
            {
                DataFrame categoriesFrame = parameters["categories"].AsDataFrame;
                categoriesVariable = (ClassifierVariable)categoriesFrame.Variables[0];
            }

            n = objectsVariable.GroupCount;
            b = ratersVariable.GroupCount;
            c = 1;
            if (hasCategories)
                c = categoriesVariable.GroupCount;

            data = new double[n + 1, b + 1, c + 1];
            for (int i = 1; i <= n; i++)
                for (int j = 1; j <= b; j++)
                    for (int k = 1; k <= c; k++)
                        data[i, j, k] = Constant.MISSING;

            //  In the XML we ask the user to select "Which observer is a reference standard (leave blank for none)?" then set the standard flag to true if there is a reference standard
            standard = parameters.ContainsKey("reference");
            if (standard)
            {
                //  Work out which group number is the reference.  This is a bit ugly as the parameter is a boolean array based on what was passed in - which in this case is an alpha-sorted list of the group names.
                string[] groupNames = ratersVariable.SortedCategoryNames;
                bool[] standardArray = (bool[])parameters["reference"].AsObject;
                string referenceName = null;
                for (int finder = 0; finder < groupNames.Length; finder++)
                {
                    if (standardArray[finder])
                    {
                        referenceName = groupNames[finder];
                        break;
                    }
                }
                if (referenceName == null)
                    throw new Exception("Could not match reference standard string");

                //  set the ref string to "Observer <name of reference category>" if there is a reference category
                refIdent = "Observer " + referenceName;

                //  Find the ID of the reference observer
                int referenceGroupNumber;
                for (referenceGroupNumber = 0; referenceGroupNumber < ratersVariable.GroupCount; referenceGroupNumber++)
                {
                    if (ratersVariable.Groups[referenceGroupNumber].Label == referenceName)
                        break;
                }
                if (referenceGroupNumber == ratersVariable.GroupCount)
                {
                    //  Should never happen - the earlier exception should have caught this case.  However, on the principle of belt and braces...
                    throw new Exception("Couldn't find reference standard in the group list");
                }
                //  rebase the coding of the observer variable so that the reference standard is the first observer.
                //  We do this the most obvious way: if the observer isn't id 0, swap its ID with 0.
                //  TODO: Is this always valid?  Is this proof against changes in the way group IDs are assigned?
                if (referenceGroupNumber > 0)
                {
                    Group oldZeroGroup = ratersVariable.Groups[0];
                    double oldZeroId = ratersVariable.Groups[0].Id;
                    double oldReferenceGroupId = ratersVariable.Groups[referenceGroupNumber].Id;
                    ratersVariable.Groups[0] = ratersVariable.Groups[referenceGroupNumber];
                    ratersVariable.Groups[referenceGroupNumber] = oldZeroGroup;
                    ratersVariable.Groups[0].Id = oldZeroId;
                    ratersVariable.Groups[referenceGroupNumber].Id = oldReferenceGroupId;
                    for (int i = 0; i < ratersVariable.Data.Length; i++)
                    {
                        int val = Convert.ToInt32(ratersVariable.Data[i]);
                        if (val == 0)
                        {
                            val = referenceGroupNumber;
                        }
                        else if (val == referenceGroupNumber)
                        {
                            val = 0;
                        }
                        ratersVariable.SetData(i, val);
                    }
                }
            }
            else
            {
                refIdent = "None";
            }

            nobs = 0;
            for (int row = 0; row < dataVariable.Length; row++)
            {
                double measurement = dataVariable.Data[row];
                double raterId = ratersVariable.Data[row];
                double objectId = objectsVariable.Data[row];
                double categoryId = 0;
                if (hasCategories)
                {
                    categoryId = categoriesVariable.Data[row];
                }
                if (!(measurement == Constant.MISSING || raterId == Constant.MISSING || objectId == Constant.MISSING || categoryId == Constant.MISSING))
                {
                    if (data[Convert.ToInt32(objectId) + 1, Convert.ToInt32(raterId) + 1, Convert.ToInt32(categoryId) + 1] != Constant.MISSING)
                    {
                        string specerr = hasCategories
                            ? "Object " + objectId + ", judge " + raterId + ", category " + categoryId + " has more than one measurement assigned."
                            : "Object " + objectId + ", judge " + raterId + " has more than one measurement assigned.";
                        throw new TemplateOperationCancelledException(specerr, "Universal agreement R");
                    }
                    data[Convert.ToInt32(objectId) + 1, Convert.ToInt32(raterId) + 1, Convert.ToInt32(categoryId) + 1] = measurement;
                    nobs += 1;
                }
            }

            //  Check for missing data and complain if any is found
            for (int i = 1; i <= n; i++)
            {
                for (int j = 1; j <= b; j++)
                {
                    for (int k = 1; k <= c; k++)
                    {
                        if (data[i, j, k] == Constant.MISSING)
                        {
                            string specerr = hasCategories
                                ? "A measurement must be specified for each judge, object and category."
                                : "A measurement must be specified for each judge and object.";
                            throw new TemplateOperationCancelledException(specerr, "Universal agreement R");
                        }
                    }
                }
            }

            if (hasCategories)
                title = dataVariable.Title + " (" + categoriesVariable.CommaSeparatedCategoryNames + ")";
            else
                title = dataVariable.Title;
        }

        /// <summary>
        /// Compares the values of R of two independent groups of observers, from what the report of each group gives: R, and the mean,
        /// variance and skewness of delta.  As R is 1 - delta / mean, the variance of R is that of delta over the square of the mean,
        /// and the third moment of R about its mean is minus that of delta over the cube of the mean.  The difference between the two
        /// values of R has the sum of their variances and the difference of their third moments; divided by its standard deviation, it
        /// is referred to the distribution with its skewness (see Pgamt) for a two sided P value.  The P value of each group's own R is
        /// given beside it.
        /// </summary>
        /// <param name="parameters">"r1_in", "mu1_in", "var1_in" and "gam1_in": R and the mean, variance and skewness of delta for the
        /// first group; "r2_in" and so on for the second.</param>
        public static StepOutput RptUniversalRCompare(ParameterBag parameters)
        {
            double r1 = parameters["r1_in"].AsDouble;
            double r2 = parameters["r2_in"].AsDouble;
            double mu1 = parameters["mu1_in"].AsDouble;
            double mu2 = parameters["mu2_in"].AsDouble;
            double var1 = parameters["var1_in"].AsDouble;
            double var2 = parameters["var2_in"].AsDouble;
            double gam1 = parameters["gam1_in"].AsDouble;
            double gam2 = parameters["gam2_in"].AsDouble;

            if (r1 == Constant.MISSING || r2 == Constant.MISSING || mu1 == Constant.MISSING || mu2 == Constant.MISSING || var1 == Constant.MISSING || var2 == Constant.MISSING || gam1 == Constant.MISSING || gam2 == Constant.MISSING)
                throw new TemplateOperationCancelledException("Please fill in all 8 values", "Compare two R values");

            double dr = r1 - r2;
            double dm = mu1 - mu2;
            //  the delta that was observed in each group: R is 1 - delta / mean, so delta is mean (1 - R)
            double d1 = mu1 * (1.0 - r1);
            double d2 = mu2 * (1.0 - r2);
            //  the variance of the difference, var1 / mu1^2 + var2 / mu2^2, and further on its skewness: its third moment about its
            //  mean, sig2^3 gam2 / mu2^3 - sig1^3 gam1 / mu1^3, over the cube of its standard deviation
            double vard = (Math.Pow(mu1, 2.0) * var2 + Math.Pow(mu2, 2.0) * var1) / (Math.Pow(mu1, 2.0) * Math.Pow(mu2, 2.0));
            double sig1 = Math.Sqrt(var1);
            double sig2 = Math.Sqrt(var2);
            double sigd = Math.Sqrt(vard);
            double gamd = (Math.Pow(mu1, 3.0) * Math.Pow(sig2, 3.0) * gam2 - Math.Pow(mu2, 3.0) * Math.Pow(sig1, 3.0) * gam1) / (Math.Pow(mu1, 3.0) * Math.Pow(mu2, 3.0) * Math.Pow(sigd, 3.0));
            double t = dr / sigd;
            double p1 = Pgamt((d1 - mu1) / sig1, gam1);
            double p2 = Pgamt((d2 - mu2) / sig2, gam2);
            //  two sided: twice the lesser of the two tails.  The upper tail at t is the lower tail at -t of the distribution with the
            //  skewness of the other sign
            double pd = Math.Min(1.0, 2.0 * Math.Min(Pgamt(t, gamd), Pgamt(-t, -gamd)));

            ParameterBag outputParameters = new();

            outputParameters.AddOutput("r1", r1);
            outputParameters.AddOutput("r2", r2);
            outputParameters.AddOutput("mu1", mu1);
            outputParameters.AddOutput("mu2", mu2);
            outputParameters.AddOutput("var1", var1);
            outputParameters.AddOutput("var2", var2);
            outputParameters.AddOutput("gam1", gam1);
            outputParameters.AddOutput("gam2", gam2);
            outputParameters.AddOutput("dr", dr);
            outputParameters.AddOutput("dm", dm);
            outputParameters.AddOutput("vard", vard);
            outputParameters.AddOutput("gamd", gamd);
            outputParameters.AddOutput("p1", p1);
            outputParameters.AddOutput("p2", p2);
            outputParameters.AddOutput("pd", pd);
            return new StepOutput(outputParameters);
        }


        ///  <summary>
        ///  calculates the value for delta and the value for the coefficient of agreement, r.
        ///  exact values for the mean (edel), variance (var), and skewness (gam) of the delta distribution are computed.
        ///  The distribution of delta is over all the ways of dealing each observer's measurements among the objects: n factorial ways
        ///  for each observer, every one as likely as any other.  Its moments are not found by going through the dealings but from
        ///  sums of the distances between the points, of their squares and cubes, and of their products.
        ///  </summary>
        ///  <param name="n">number of objects observed</param>
        ///  <param name="b">number of observers/judges</param>
        ///  <param name="c">number of dimensions/responses</param>
        ///  <param name="data">matrix (n,b,c) containing the raw score values</param>
        ///  <param name="delta">observed (realized) value of delta</param>
        ///  <param name="edel">expected (mean) value of delta</param>
        ///  <param name="var">variance of the delta distribution</param>
        ///  <param name="gam">skewness of the delta distribution</param>
        ///  <param name="r">delta-based agreement coefficient</param>
        ///  <param name="p">probability of agreement coefficient</param>
        public static void Agree(int n, int b, int c, double[,,] data, out double delta, out double edel, out double var, out double gam, out double r, out double p)
        {
            int i, j, k;
            int ix, ir, irr;
            int jss, iss;
            double[,] d = new double[n * b + 1, n * b + 1];
            double[,,] sj = new double[n + 1, b + 1, b + 1];
            double[,,] sj2 = new double[n + 1, b + 1, b + 1];
            double[,] vi = new double[b + 1, b + 1];
            double[,,] sj3 = new double[n + 1, b + 1, b + 1];
            double[,,] uj = new double[n + 1, b + 1, b + 1];
            double[,,] wi = new double[b + 1, b + 1, b + 1];
            double[,,] yij = new double[b + 1, b + 1, b + 1];
            double[,] uij = new double[b + 1, b + 1];
            double[,,] zijk = new double[b + 1, b + 1, b + 1];
            double[,] sij = new double[b + 1, b + 1];
            double[,] sij2 = new double[b + 1, b + 1];
            double[,] sij3 = new double[b + 1, b + 1];
            double[,] tij2 = new double[b + 1, b + 1];
            double[,] tij3 = new double[b + 1, b + 1];

            const double zero = 0.0;
            //  d holds the distance between every two points.  The point of object i and observer j is number b (i - 1) + j
            for (i = 1; i <= n; i++)
            {
                for (j = 1; j <= b; j++)
                {
                    for (k = i; k <= n; k++)
                    {
                        int lo = 1;
                        if (i == k)
                        {
                            lo = j;
                        }
                        int l;
                        for (l = lo; l <= b; l++)
                        {
                            int ij = b * (i - 1) + j;
                            int kl = b * (k - 1) + l;
                            d[ij, kl] = zero;
                            int m;
                            for (m = 1; m <= c; m++)
                            {
                                d[ij, kl] = d[ij, kl] + Math.Pow(data[i, j, m] - data[k, l, m], 2.0);
                            }
                            d[ij, kl] = Math.Pow(d[ij, kl], 0.5);
                            d[kl, ij] = d[ij, kl];
                        }
                    }
                }
            }
            //  For each two observers ir and ix, taken in that order: sj[i, ir, ix] is the sum of the distances from the point that ir
            //  has for object i to the points that ix has for all the objects, and sj2 and sj3 are the sums of the squares and of the
            //  cubes of those distances.  sij, sij2 and sij3 are their totals over the objects: sums over all n^2 pairs of a point of
            //  ir with a point of ix
            for (ix = 1; ix <= b; ix++)
            {
                for (ir = 1; ir <= b; ir++)
                {
                    if (ir != ix)
                    {
                        sij[ir, ix] = zero;
                        sij2[ir, ix] = zero;
                        sij3[ir, ix] = zero;
                        for (i = 1; i <= n; i++)
                        {
                            sj[i, ir, ix] = zero;
                            sj2[i, ir, ix] = zero;
                            sj3[i, ir, ix] = zero;
                            for (j = 1; j <= n; j++)
                            {
                                irr = (i - 1) * b + ir;
                                jss = (j - 1) * b + ix;
                                sj[i, ir, ix] = sj[i, ir, ix] + d[irr, jss];
                                sj2[i, ir, ix] = sj2[i, ir, ix] + Math.Pow(d[irr, jss], 2.0);
                                sj3[i, ir, ix] = sj3[i, ir, ix] + Math.Pow(d[irr, jss], 3.0);
                            }
                            sij[ir, ix] = sij[ir, ix] + sj[i, ir, ix];
                            sij2[ir, ix] = sij2[ir, ix] + sj2[i, ir, ix];
                            sij3[ir, ix] = sij3[ir, ix] + sj3[i, ir, ix];
                        }
                    }
                }
            }
            //  t2 is the part of the third moment of delta that comes from three observers at a time.  The deltas of two pairs of
            //  observers are uncorrelated, which is why the variance below has a term for each pair and no more, but the deltas of
            //  the three pairs that can be made of three observers are not independent of each other
            double t2 = zero;
            if (b > 2)
            {
                int it;
                for (it = 3; it <= b; it++)
                {
                    for (ix = 2; ix < it; ix++)
                    {
                        for (ir = 1; ir < ix; ir++)
                        {
                            wi[ir, ix, it] = zero;
                            wi[ix, ir, it] = zero;
                            wi[it, ir, ix] = zero;
                            yij[ir, ix, it] = zero;
                            zijk[ir, ix, it] = zero;
                            for (i = 1; i <= n; i++)
                            {
                                wi[ir, ix, it] = wi[ir, ix, it] + sj[i, ir, ix] * sj[i, ir, it];
                                wi[ix, ir, it] = wi[ix, ir, it] + sj[i, ix, ir] * sj[i, ix, it];
                                wi[it, ir, ix] = wi[it, ir, ix] + sj[i, it, ir] * sj[i, it, ix];
                                for (j = 1; j <= n; j++)
                                {
                                    irr = b * (i - 1) + ir;
                                    jss = b * (j - 1) + ix;
                                    int jtt = b * (j - 1) + it;
                                    iss = b * (i - 1) + ix;
                                    yij[ir, ix, it] = yij[ir, ix, it] + d[irr, jss] * sj[i, ir, it] * sj[j, ix, it] + d[irr, jtt] * sj[i, ir, ix] * sj[j, it, ix] + d[iss, jtt] * sj[i, ix, ir] * sj[j, it, ir];
                                    for (k = 1; k <= n; k++)
                                    {
                                        int ktt = (k - 1) * b + it;
                                        zijk[ir, ix, it] = zijk[ir, ix, it] + d[irr, jss] * d[irr, ktt] * d[jss, ktt];
                                    }
                                }
                            }
                            t2 = t2 + sij[ir, ix] * sij[ir, it] * sij[ix, it] - sij[ix, it] * wi[ir, ix, it] * n - sij[ir, it] * wi[ix, ir, it] * n - sij[ir, ix] * wi[it, ir, ix] * n + yij[ir, ix, it] * n * n - zijk[ir, ix, it] * n * n * n;
                        }
                    }
                }
                t2 = 6.0 * t2 / (n - 1);
            }
            //  For each pair of observers: tij2 and tij3 are the sums, over the points of both, of the square and of the cube of a
            //  point's sum of distances to the points of the other observer; vi is the sum of a point's sum of distances times its
            //  sum of squared distances; and uij is the sum, over the pairs of a point of one with a point of the other, of their
            //  distance times the two points' sums of distances
            for (ix = 2; ix <= b; ix++)
            {
                for (ir = 1; ir < ix; ir++)
                {
                    tij2[ir, ix] = zero;
                    tij3[ir, ix] = zero;
                    vi[ir, ix] = zero;
                    uij[ir, ix] = zero;
                    for (i = 1; i <= n; i++)
                    {
                        tij2[ir, ix] = tij2[ir, ix] + Math.Pow(sj[i, ir, ix], 2.0) + Math.Pow(sj[i, ix, ir], 2.0);
                        tij3[ir, ix] = tij3[ir, ix] + Math.Pow(sj[i, ir, ix], 3.0) + Math.Pow(sj[i, ix, ir], 3.0);
                        vi[ir, ix] = vi[ir, ix] + sj[i, ir, ix] * sj2[i, ir, ix] + sj[i, ix, ir] * sj2[i, ix, ir];
                        uj[i, ir, ix] = zero;
                        for (j = 1; j <= n; j++)
                        {
                            irr = b * (i - 1) + ir;
                            jss = b * (j - 1) + ix;
                            uj[i, ir, ix] = uj[i, ir, ix] + d[irr, jss] * sj[i, ir, ix] * sj[j, ix, ir];
                        }
                        uij[ir, ix] = uij[ir, ix] + uj[i, ir, ix];
                    }
                }
            }
            //  The mean of delta is the mean of all the n^2 distances between a point of one observer and a point of another,
            //  averaged over the pairs of observers.  Its variance is the sum over the pairs of observers of
            //  S^2 - n T + n^2 Q (S being sij, T tij2 and Q sij2), over n - 1 and over the square of n times the number of
            //  distances that make up delta (fac).  t1 is the part of the third moment that comes from each pair of observers
            //  by itself
            edel = zero;
            var = zero;
            double t1 = zero;
            for (ix = 2; ix <= b; ix++)
            {
                for (ir = 1; ir < ix; ir++)
                {
                    if (n > 2)
                        t1 = t1 + 4.0 * Math.Pow(sij[ir, ix], 3.0) - sij[ir, ix] * tij2[ir, ix] * 6.0 * n + uij[ir, ix] * 6.0 * n * n + tij3[ir, ix] * 2.0 * n * n + sij[ir, ix] * sij2[ir, ix] * 3.0 * n * n - vi[ir, ix] * 3.0 * n * n * n + sij3[ir, ix] * Math.Pow(n, 4.0);
                    edel += sij[ir, ix];
                    var = var + sij[ir, ix] * sij[ir, ix] - tij2[ir, ix] * n + sij2[ir, ix] * n * n;
                }
            }
            if (n > 2)
            {
                t1 /= (n - 2);
            }
            double fac = n * b * (b - 1) / 2.0;
            double con = 1.0 / (fac * n);
            var = var * con * con / (n - 1);
            gam = Math.Pow(con, 3.0) * (t1 - t2) / (n - 1) / Math.Sqrt(Math.Pow(var, 3.0));
            edel = con * edel;
            //  delta itself: the mean distance between the two points of the same object, over the objects and the pairs of observers
            delta = zero;
            for (ix = 2; ix <= b; ix++)
            {
                for (ir = 1; ir < ix; ir++)
                {
                    for (i = 1; i <= n; i++)
                    {
                        irr = (i - 1) * b + ir;
                        iss = (i - 1) * b + ix;
                        delta += d[irr, iss];
                    }
                }
            }
            delta /= fac;
            double t = (delta - edel) / Math.Sqrt(var);
            p = Pgamt(t, gam);
            r = 1.0 - delta / edel;
        }


        ///  <summary>
        ///  Calculates the probability of a value of t being less than or equal to the observed value of t.
        ///  The distribution is the Pearson type III with mean 0, variance 1 and the skewness given: a gamma distribution, moved
        ///  along and, for a negative skewness, turned about.
        ///  </summary>
        ///  <param name="t">standardized test statistic</param>
        ///  <param name="gam">skewness of the delta distribution</param>
        ///  <return>probability of the test statistic</return>
        private static double Pgamt(double t, double gam)
        {
            //  with next to no skewness the distribution is the normal distribution: the gamma distribution that stands for it would
            //  have a shape too great for t to be told from its mean
            if (Math.Abs(gam) < 1.0E-07)
                return PDF.alnorm(t);

            //  the distribution with mean 0, variance 1 and skewness gam is that of (G - shape) / r when gam is positive and of
            //  (shape - G) / r when it is negative, G having the gamma distribution with the shape r squared, r being 2 / |gam|
            double r = 2.0 / Math.Abs(gam);
            double shape = r * r;
            double x = gam > 0.0 ? shape + r * t : shape - r * t;
            //  beyond the end of the distribution, which has no values below -r when gam is positive and none above r when it is negative
            if (x <= 0.0)
                return gam > 0.0 ? 0.0 : 1.0;
            //  the lower tail of G when gam is positive, and the upper tail when it is negative
            return PDF.gammad(x, shape, gam < 0.0, out int _);
        }

        ///  <summary>
        ///  multivariate measure of agreement between a set of raters and a standard (or correct set) of responses.
        ///  The first observer is the standard.  Each of the others is taken with the standard as a pair of observers (AgreeStdCalc),
        ///  and the deltas of the pairs are added up.  So are their means, their variances and their third moments about the mean,
        ///  which is right because each observer's measurements are dealt among the objects independently of the others'.
        ///  </summary>
        ///  <param name="kn">number of objects observed</param>
        ///  <param name="km">number of observers/judges</param>
        ///  <param name="kr">number of dimensions/responses</param>
        ///  <param name="tdata">matrix (n,b,c) containing the raw score values</param>
        ///  <param name="delta">observed (realized) value of delta</param>
        ///  <param name="edel">expected (mean) value of delta</param>
        ///  <param name="var">variance of the delta distribution</param>
        ///  <param name="gam">skewness of the delta distribution</param>
        ///  <param name="rho">delta-based agreement coefficient</param>
        ///  <param name="prob">probability of agreement coefficient</param>
        private static void AgreeStandard(int kn, int km, int kr, double[,,] tdata, out double delta, out double edel, out double var, out double gam, out double rho, out double prob)
        {
            double[] c1 = new double[km + 1];
            double[] c2 = new double[km + 1];
            double[] c3 = new double[km + 1];
            double[,] d = new double[2 * kn + 1, 2 * kn + 1];
            double[,,] data = new double[kn + 1, 3, kr + 1];
            double[] del = new double[km + 1];
            double[,,] sj1 = new double[kn + 1, 3, 3];
            double[,,] sj2 = new double[kn + 1, 3, 3];
            double[,,] sj3 = new double[kn + 1, 3, 3];
            double[,,] uj = new double[kn + 1, 3, 3];

            for (int i = 1; i <= kn; i++)
                for (int j = 1; j <= kr; j++)
                    data[i, 1, j] = tdata[i, 1, j];
            for (int i = 2; i <= km; i++)
            {
                for (int j = 1; j <= kn; j++)
                    for (int k = 1; k <= kr; k++)
                        data[j, 2, k] = tdata[j, i, k];

                AgreeStdCalc(kn, kr, d, data, sj1, sj2, sj3, uj, out double cum1, out double cum2, out double cum3, out delta);

                del[i] = delta;
                c1[i] = cum1;
                c2[i] = cum2;
                c3[i] = cum3;
            }
            delta = 0.0;
            double c11 = 0.0;
            double c22 = 0.0;
            double c33 = 0.0;
            for (int i = 2; i <= km; i++)
            {
                delta += del[i];
                c11 += c1[i];
                c22 += c2[i];
                c33 += c3[i];
            }
            edel = c11;
            var = c22;
            gam = c33 / Math.Sqrt(Math.Pow(var, 3.0));
            double t = (delta - edel) / Math.Sqrt(var);
            rho = 1.0 - delta / edel;

            prob = Pgamt(t, gam);
        }

        /// <summary>
        /// The delta of one observer with the standard, and the mean, variance and third moment about the mean of that delta over all the
        /// dealings of the observer's measurements among the objects: the calculation of Agree for two observers.
        /// </summary>
        /// <param name="kn">The number of objects.</param>
        /// <param name="kr">The number of dimensions.</param>
        /// <param name="d">Room for the distances between the points.</param>
        /// <param name="data">The measurements: data[object, 1, dimension] of the standard and data[object, 2, dimension] of the observer.</param>
        /// <param name="sj1">Room for each point's sum of distances to the points of the other of the two.</param>
        /// <param name="sj2">Room for each point's sum of squared distances.</param>
        /// <param name="sj3">Room for each point's sum of cubed distances.</param>
        /// <param name="uj">Room for the sums of products of distances (see Agree).</param>
        /// <param name="cum1">On return, the mean of delta.</param>
        /// <param name="cum2">On return, the variance of delta.</param>
        /// <param name="cum3">On return, the third moment of delta about its mean.</param>
        /// <param name="delta">On return, the delta observed.</param>
        private static void AgreeStdCalc(int kn, int kr, double[,] d, double[,,] data, double[,,] sj1, double[,,] sj2, double[,,] sj3, double[,,] uj, out double cum1, out double cum2, out double cum3, out double delta)
        {
            int i;
            int irr, ix, j, jss;
            double[,] sij1 = new double[3, 3];
            double[,] sij2 = new double[3, 3];
            double[,] sij3 = new double[3, 3];
            double[,] tij2 = new double[3, 3];
            double[,] tij3 = new double[3, 3];
            double[,] uij = new double[3, 3];
            double[,] vi = new double[3, 3];

            for (i = 1; i <= 2 * kn; i++)
                for (j = 1; j <= 2 * kn; j++)
                    d[i, j] = 0.0;
            for (i = 1; i <= kn; i++)
            {
                for (j = 1; j <= 2; j++)
                {
                    int k;
                    for (k = i; k <= kn; k++)
                    {
                        int lo = 1;
                        if (i == k)
                            lo = j;
                        int l;
                        for (l = lo; l <= 2; l++)
                        {
                            int ij = 2 * (i - 1) + j;
                            int kl = 2 * (k - 1) + l;
                            d[ij, kl] = 0.0;
                            int m;
                            for (m = 1; m <= kr; m++)
                                d[ij, kl] = d[ij, kl] + Math.Pow(data[i, j, m] - data[k, l, m], 2.0);
                            d[ij, kl] = Math.Pow(d[ij, kl], 0.5);
                            d[kl, ij] = d[ij, kl];
                        }
                    }
                }
            }
            for (ix = 1; ix <= 2; ix++)
            {
                int ir;
                for (ir = 1; ir <= 2; ir++)
                {
                    if (ir != ix)
                    {
                        sij1[ir, ix] = 0.0;
                        sij2[ir, ix] = 0.0;
                        sij3[ir, ix] = 0.0;
                        for (i = 1; i <= kn; i++)
                        {
                            sj1[i, ir, ix] = 0.0;
                            sj2[i, ir, ix] = 0.0;
                            sj3[i, ir, ix] = 0.0;
                            for (j = 1; j <= kn; j++)
                            {
                                irr = (i - 1) * 2 + ir;
                                jss = (j - 1) * 2 + ix;
                                sj1[i, ir, ix] = sj1[i, ir, ix] + d[irr, jss];
                                sj2[i, ir, ix] = sj2[i, ir, ix] + Math.Pow(d[irr, jss], 2.0);
                                sj3[i, ir, ix] = sj3[i, ir, ix] + Math.Pow(d[irr, jss], 3.0);
                            }
                            sij1[ir, ix] = sij1[ir, ix] + sj1[i, ir, ix];
                            sij2[ir, ix] = sij2[ir, ix] + sj2[i, ir, ix];
                            sij3[ir, ix] = sij3[ir, ix] + sj3[i, ir, ix];
                        }
                    }
                }
            }
            const double t2 = 0.0;
            tij2[1, 2] = 0.0;
            tij3[1, 2] = 0.0;
            vi[1, 2] = 0.0;
            uij[1, 2] = 0.0;
            for (i = 1; i <= kn; i++)
            {
                tij2[1, 2] = tij2[1, 2] + Math.Pow(sj1[i, 1, 2], 2.0) + Math.Pow(sj1[i, 2, 1], 2.0);
                tij3[1, 2] = tij3[1, 2] + Math.Pow(sj1[i, 1, 2], 3.0) + Math.Pow(sj1[i, 2, 1], 3.0);
                vi[1, 2] = vi[1, 2] + sj1[i, 1, 2] * sj2[i, 1, 2] + sj1[i, 2, 1] * sj2[i, 2, 1];
                uj[i, 1, 2] = 0.0;
                for (j = 1; j <= kn; j++)
                {
                    irr = 2 * (i - 1) + 1;
                    jss = 2 * (j - 1) + 2;
                    uj[i, 1, 2] = uj[i, 1, 2] + d[irr, jss] * sj1[i, 1, 2] * sj1[j, 2, 1];
                }
                uij[1, 2] = uij[1, 2] + uj[i, 1, 2];
            }
            double t1 = 0.0;
            if (kn > 2)
            {
                t1 = t1 + 4.0 * Math.Pow(sij1[1, 2], 3.0) - sij1[1, 2] * tij2[1, 2] * 6.0 * kn + uij[1, 2] * 6.0 * kn * kn + tij3[1, 2] * 2.0 * kn * kn + sij1[1, 2] * sij2[1, 2] * 3.0 * kn * kn - vi[1, 2] * 3.0 * kn * kn * kn + sij3[1, 2] * Math.Pow(kn, 4.0);
                t1 /= Convert.ToDouble(kn - 2);
            }
            double c1 = 1.0 / Convert.ToDouble(kn * kn);
            double c2 = c1 * c1;
            double c3 = c2 * c1;
            cum1 = c1 * sij1[1, 2];
            cum2 = (sij1[1, 2] * sij1[1, 2] - tij2[1, 2] * kn + sij2[1, 2] * kn * kn) * c2 / Convert.ToDouble(kn - 1);
            cum3 = c3 * (t1 - t2) / Convert.ToDouble(kn - 1);
            delta = 0.0;
            for (i = 1; i <= kn; i++)
            {
                irr = (i - 1) * 2 + 1;
                int iss = (i - 1) * 2 + 2;
                delta += d[irr, iss];
            }
            delta /= Convert.ToDouble(kn);
        }

        /// <summary>
        /// The P value of the universal measure of agreement by simulation: the proportion, among dealings of the observers' measurements
        /// among the objects that are made at random, of those with a delta no greater than the delta observed, with the confidence
        /// interval of that proportion.  It is for observers without a standard.
        /// </summary>
        /// <param name="host">Where progress is shown.</param>
        /// <param name="parameters">As for RptUniversalAgreement, with "iterations", the number of dealings; "seed", the start of the
        /// random numbers (0 for one taken from the clock); and "ci", the confidence level of the interval.</param>
        public static StepOutput RptUniversalAgreementSimulateExactP(IProgressBarHost host, ParameterBag parameters)
        {
            int nobs = 0;
            string title = null;
            string refIdent = null;
            GatherUniversalAgreementData(parameters, out int n, out int b, out int c, out double[,,] data, out bool _, ref nobs, ref title, ref refIdent);

            // Agree(n, b, c, data, out double delta, out double edel, out double var, out double gam, out double r, out double t);
            // double prob = Pgamt(t, gam);

            int iterations = parameters["iterations"].AsInt32;
            double ci = parameters["ci"].AsDouble;
            int seed = parameters["seed"].AsInt32;

            Rmrbp(host, 1.0, n, b, c, 0, 0, 0, data, 0, seed, iterations, out int ir, out int mpd);
            ParameterBag outputParameters = new();
            double p = Convert.ToDouble(ir) / Convert.ToDouble(mpd);
            outputParameters.AddOutput("p", p);
            //  CI
            MathDbl.binci(Convert.ToDouble(ir), Convert.ToDouble(mpd), out double ll, out double ul, ci, out string warn);
            outputParameters.AddOutput("pc", 100.0 * ci);
            outputParameters.AddOutput("ll", ll);
            outputParameters.AddOutput("ul", ul);
            outputParameters.AddOutput("warn", warn);
            outputParameters.AddOutput("k", mpd);
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// The permutation test of an experiment in randomised blocks, by simulation.  For the measure of agreement the objects are its
        /// groups and the observers its blocks; it is called for the distance as it is (v = 1) and without alignment, commensuration
        /// or ranks, so that the measurements go to Calc as they are.
        /// </summary>
        /// <param name="host">Where progress is shown.</param>
        /// <param name="v">The power to which the distance between two points is raised.</param>
        /// <param name="kg">The number of groups (objects).</param>
        /// <param name="kb">The number of blocks (observers).</param>
        /// <param name="kr">The number of responses (dimensions).</param>
        /// <param name="ia">1 to align the blocks: the median of each block, in each dimension, is taken from its values.</param>
        /// <param name="ic">1 to make the dimensions commensurate: each is divided by a measure of its spread.</param>
        /// <param name="lr">1 to put ranks in place of the values (see Rank).</param>
        /// <param name="data">The values: data[group, block, response], each index from 1.  They are changed.</param>
        /// <param name="h">The power for the ranks.</param>
        /// <param name="iseed">The start of the random numbers, or 0 for one taken from the clock.</param>
        /// <param name="ms">The number of dealings to make.</param>
        /// <param name="mp">On return, the number of dealings with a delta no greater than the delta observed.</param>
        /// <param name="mpd">On return, the number of dealings made.</param>
        private static void Rmrbp(IProgressBarHost host, double v, int kg, int kb, int kr, int ia, int ic, int lr, double[,,] data, int h, int iseed, int ms, out int mp, out int mpd)
        {

            //          THIS FORTRAN PROGRAM COMPUTES THE TEST STATISTIC AND ASSOCIATED
            //          P-VALUE FOR AN ANALYSIS OF A RANDOMIZED BLOCK EXPERIMENT (MRBP3).
            //          THE CORRESPONDENCE BETWEEN A CORRELATION ANALYSIS AND A
            //          RANDOMIZED BLOCK EXPERIMENT CAN BE USED TO GET A CORRELATION
            //          COEFFICIENT AS WELL. THE MAXIMUM VALUES OF G, B AND R CAN BE
            //          CHANGED FOR ANY EXAMPLE.  THE PRESENT MAXIMUM VALUES OF G, B
            //          AND R IN THIS PROGRAM ARE RESPECTIVELY 10, 12 AND 15.

            //        THIS PROGRAM IS CAPABLE OF PERFORMING (1) REPEATED MRBP ANALYSES
            //        IN 1.0 OPERATION, (2) ALIGNMENT WITHIN BLOCKS, (3) DISTANCE
            //        FUNCTION COMMENSURATION, AND (4) C(G,H) RANKS TEST.

            //          PROGRAM MODIFIED 2/8/2003

            //        THE DATA MATRIX MUST BE IN THE FOLLOWING SEQUENCE WITH EACH
            //        OBJECT'S R RESPONSE VALUES ON A SEPARATE LINE AS FOLLOWS:

            //          A(1,1,1),A(1,1,2),...,A(1,1,R)
            //          A(1,2,1),A(1,2,2),...,A(1,2,R)
            //           ...
            //          A(1,B,1),A(1,B,2),...,A(1,B,R)
            //          A(2,1,1),A(2,1,2),...,A(2,1,R)
            //           ...
            //          A(2,B,1),A(2,B,2),...,A(2,B,R)
            //           ...
            //          A(G,1,1),A(G,1,2),...,A(G,1,R)
            //           ...
            //          A(G,B,1),A(G,B,2),...,A(G,B,R)

            //    THE INPUT DATA ARE IN FREE FORMAT.  SPECIFICALLY, V IS DISTANCE EXP1.0NT,
            //    G IS # OF GROUPS, B IS # OF BLOCKS, R IS # OF RESPONSES, IA = 1 IMPLIES
            //    ALIGNMENT, IC = 1 IMPLIES COMMENSURATION, AND LR = 1 IMPLIES C(G,H)
            //    RANKS TEST.   NOTE: ASSOCIATE G, B AND R WITH KG, KB AND KR IN PROGRAM.

            double[] ad = new double[kr + 1];
            double[,] xm = new double[kb + 1, kr + 1];
            double[,,] x = new double[kg + 1, kb + 1, kr + 1];
            double dm1 = 0;
            double dm2 = 0;
            double a2 = 0;

            if (lr == 1)
            {
                // ia = 0; Redundant assignment PJC 2012/04/09
                // ic = 0; Redundant assignment PJC 2012/04/09 
                Rank(kg, kb, kr, h, ref data);
            }
            else
            {

                if (ia != 0)
                {
                    for (int i = 1; i <= kg; i++)
                        for (int j = 1; j <= kb; j++)
                            for (int k = 1; k <= kr; k++)
                                x[i, j, k] = data[i, j, k];
                    for (int j = 1; j <= kb; j++)
                    {
                        for (int k = 1; k <= kr; k++)
                        {
                            double a1 = 1.0E+200;
                            double sum1 = a1;
                            for (int i1 = 1; i1 <= kg; i1++)
                            {
                                double sum = 0.0;
                                for (int i2 = 1; i2 <= kg; i2++)
                                    sum += Math.Abs(data[i2, j, k] - x[i1, j, k]);
                                if (sum < a1)
                                {
                                    dm1 = x[i1, j, k];
                                    a1 = sum;
                                    a2 = a1 * 1.0000000001;
                                }
                                if (sum < a2 & dm1 != x[i1, j, k])
                                {
                                    dm2 = x[i1, j, k];
                                    sum1 = sum;
                                }
                            }
                            if (sum1 > a2)
                                dm2 = dm1;
                            xm[j, k] = (dm1 + dm2) / 2.0;
                        }
                    }
                    for (int i = 1; i <= kg; i++)
                        for (int j = 1; j <= kb; j++)
                            for (int k = 1; k <= kr; k++)
                                data[i, j, k] = data[i, j, k] - xm[j, k];
                }
                if (ic != 0 & kr != 1)
                {
                    for (int k = 1; k <= kr; k++)
                    {
                        ad[k] = 0.0;
                        for (int i1 = 1; i1 <= kg; i1++)
                            for (int i2 = 1; i2 <= kg; i2++)
                                for (int j1 = 2; j1 <= kb; j1++)
                                    for (int j2 = 1; j2 < j1; j2++)
                                        ad[k] += Math.Pow(Math.Abs(data[i1, j1, k] - data[i2, j2, k]), v);
                        ad[k] = Math.Pow(ad[k], 1.0 / v);
                    }
                    for (int i = 1; i <= kg; i++)
                        for (int j = 1; j <= kb; j++)
                            for (int k = 1; k <= kr; k++)
                                data[i, j, k] = data[i, j, k] / ad[k];
                }

            }
            Calc(host, v, kg, kb, kr, iseed, ms, data, out mp, out mpd);
        }

        /// <summary>
        /// Puts in place of the values of each block, in each dimension, their ranks less the mean rank, (kg + 1) / 2, raised to the power
        /// h with the sign kept.  Values that are equal share the mean of the ranks that they would have between them.
        /// </summary>
        /// <param name="kg">The number of groups.</param>
        /// <param name="kb">The number of blocks.</param>
        /// <param name="kr">The number of responses.</param>
        /// <param name="h">The power.</param>
        /// <param name="data">The values: data[group, block, response], each index from 1.</param>
        private static void Rank(int kg, int kb, int kr, int h, ref double[,,] data)
        {

            double[] rks = new double[kg + 1];

            double ym = 1.0 * (kg + 1) / 2;
            for (int j = 1; j <= kb; j++)
            {
                for (int k = 1; k <= kr; k++)
                {
                    double cl = 1.0E+30;
                    for (int i = 1; i <= kg; i++)
                        if (data[i, j, k] < cl)
                            cl = data[i, j, k];
                    for (int i = 1; i <= kg; i++)
                        data[i, j, k] = data[i, j, k] - cl + 1.0;
                    const double phi = 1.0 + 0.000000000001;
                    double a1 = 0.0;
                    double a2 = 0.0;
                    double a3 = 1.0 * kg - 0.1;
                    double b1 = 0.0 - 1.0E+30;
                    double b2 = 1.0E+30;
                    while (a2 <= a3)
                    {
                        for (int i = 1; i <= kg; i++)
                            if (data[i, j, k] > b1 & data[i, j, k] < b2)
                                b2 = data[i, j, k] * phi;
                        for (int i = 1; i <= kg; i++)
                        {
                            double w = Math.Abs(1.0 - data[i, j, k] / b2);
                            if (w < 0.00000000001)
                                a1 += 1;
                        }
                        double a4 = a2 + (a1 + 1) / 2;
                        for (int i = 1; i <= kg; i++)
                        {
                            double w = Math.Abs(1.0 - data[i, j, k] / b2);
                            if (w < 0.00000000001)
                                rks[i] = a4;
                        }
                        a2 += a1;
                        a1 = 0.0;
                        b1 = b2;
                        b2 = 1.0E+30;
                    }
                    for (int i = 1; i <= kg; i++)
                    {
                        double w = Math.Abs(rks[i] - ym);
                        if (w < 0.001)
                            data[i, j, k] = 0.0;
                        else
                            data[i, j, k] = (rks[i] - ym) * Math.Pow(Math.Abs(rks[i] - ym), h - 1);
                    }
                }
            }
        }


        /// <summary>
        /// Delta for the values as they are, and for ms dealings of them made at random: mp counts the dealings with a delta no greater
        /// than the delta of the values as they are.
        /// </summary>
        /// <param name="host">Where progress is shown.</param>
        /// <param name="v">The power to which the distance between two points is raised.</param>
        /// <param name="kg">The number of groups (objects).</param>
        /// <param name="kb">The number of blocks (observers).</param>
        /// <param name="kr">The number of responses (dimensions).</param>
        /// <param name="iseed">The start of the random numbers, or 0 for one taken from the clock.</param>
        /// <param name="ms">The number of dealings to make.</param>
        /// <param name="data">The values: data[group, block, response], each index from 1.  They are left as the last dealing.</param>
        /// <param name="mp">On return, the number of dealings with a delta no greater than the delta observed.</param>
        /// <param name="mpd">On return, the number of dealings made: ms, or fewer if the user stopped the simulation.</param>
        private static void Calc(IProgressBarHost host, double v, int kg, int kb, int kr, int iseed, int ms, double[,,] data, out int mp, out int mpd)
        {

            double[,] d = new double[kb * (kg - 1) + kb + 1, kb * (kg - 1) + kb + 1];
            // double[,] dt = new double[kg + 1, kr + 1]; Array never referenced.  PJC 2012/04/09.
            int lo, l, ij, kl, is0, is1, irr, iss, i, j, k, iw, m;
            MersenneTwister rng = new();
            int trigger = Convert.ToInt32(ms / 1000) + 1;

            if (iseed != 0)
                rng.Seed(iseed);
            else
                rng.Seed();

            double y = v / 2.0;
            double bc2 = 1.0 * kb * (kb - 1) / 2.0;
            int kbg = kb * kg;
            for (i = 1; i <= kbg; i++)
                for (j = 1; j <= kbg; j++)
                    d[i, j] = 0.0;
            for (i = 1; i <= kg; i++)
            {
                for (j = 1; j <= kb; j++)
                {
                    for (k = i; k <= kg; k++)
                    {
                        lo = 1;
                        if (i == k)
                            lo = j;
                        for (l = lo; l <= kb; l++)
                        {
                            ij = kb * (i - 1) + j;
                            kl = kb * (k - 1) + l;
                            d[ij, kl] = 0.0;
                            for (m = 1; m <= kr; m++)
                                d[ij, kl] = d[ij, kl] + Math.Pow(data[i, j, m] - data[k, l, m], 2.0);
                            d[ij, kl] = Math.Pow(d[ij, kl], y);
                            d[kl, ij] = d[ij, kl];
                        }
                    }
                }
            }
            double delta = 0.0;
            for (is0 = 2; is0 <= kb; is0++)
            {
                is1 = is0 - 1;
                for (int ir = 1; ir <= is1; ir++)
                {
                    for (i = 1; i <= kg; i++)
                    {
                        irr = (i - 1) * kb + ir;
                        iss = (i - 1) * kb + is0;
                        delta += d[irr, iss];
                    }
                }
            }
            double c0 = bc2 * kg;
            delta /= c0;
            //  a little above the delta observed, so that a delta that differs from it by rounding alone counts as equal to it
            double dx = delta * 1.000000000001;
            mp = 0;

            using IProgressBar progress = host.StartProgress("Simulating exact P", true);
            int ctr = 0;

            mpd = ms;
            for (iw = 1; iw <= ms; iw++)
            {
                //  For each block but the first (the first can stay as it is, as only the order of one block beside another matters):
                //  the values of each group in turn change places with those of a group picked at random.  A dealing starts from the
                //  one before it and not from the values as they were, so that after the first few dealings every order is very
                //  nearly as likely as any other
                for (j = 2; j <= kb; j++)
                {
                    for (i = 1; i <= kg; i++)
                    {
                        int ix = rng.NextInteger(1, kg);
                        for (k = 1; k <= kr; k++)
                        {
                            double tmp = data[i, j, k];
                            data[i, j, k] = data[ix, j, k];
                            data[ix, j, k] = tmp;
                        }
                    }
                }
                for (i = 1; i <= kbg; i++)
                    for (j = 1; j <= kbg; j++)
                        d[i, j] = 0.0;
                for (i = 1; i <= kg; i++)
                {
                    for (j = 1; j <= kb; j++)
                    {
                        for (k = i; k <= kg; k++)
                        {
                            lo = 1;
                            if (i == k)
                                lo = j;
                            for (l = lo; l <= kb; l++)
                            {
                                ij = kb * (i - 1) + j;
                                kl = kb * (k - 1) + l;
                                d[ij, kl] = 0.0;
                                for (m = 1; m <= kr; m++)
                                    d[ij, kl] = d[ij, kl] + Math.Pow(data[i, j, m] - data[k, l, m], 2.0);
                                d[ij, kl] = Math.Pow(d[ij, kl], y);
                                d[kl, ij] = d[ij, kl];
                            }
                        }
                    }
                }
                double dz = 0.0;
                for (is0 = 2; is0 <= kb; is0++)
                {
                    is1 = is0 - 1;
                    for (int ir = 1; ir <= is1; ir++)
                    {
                        for (i = 1; i <= kg; i++)
                        {
                            irr = (i - 1) * kb + ir;
                            iss = (i - 1) * kb + is0;
                            dz += d[irr, iss];
                        }
                    }
                }
                dz /= c0;
                //  a delta equal to the delta observed is counted: with perfect agreement the delta observed is nothing, and none is below it
                if (dz <= dx)
                    mp += 1;

                ctr += 1;
                if (ctr > trigger)
                {
                    bool bailout = progress.Update(Convert.ToDouble(iw) / Convert.ToDouble(ms));
                    ctr = 0;
                    if (bailout)
                    {
                        mpd = iw;
                        break;
                    }
                }

            }
        }
    }
}
