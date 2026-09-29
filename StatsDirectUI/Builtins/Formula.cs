using System;
using System.Collections.Generic;

using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public static class Formula
    {
        private const string BigErr = "(err: number too big)";

        /// <summary>
        /// Puts x[low] to x[high] inclusive into a random order in which every order is equally likely (a Fisher-Yates shuffle), taking random numbers from mt.
        /// </summary>
        internal static void Shuffle<T>(MersenneTwister mt, T[] x, int low, int high)
        {
            for (int j = high; j > low; j--)
            {
                int k = low + (int)Math.Floor((j - low + 1) * mt.NextDouble());
                (x[j], x[k]) = (x[k], x[j]);
            }
        }

        // The name of a treatment from its number: A to Z, then AA, AB and so on, as the columns of a worksheet are named
        private static string TreatmentName(int number)
        {
            string name = string.Empty;
            while (number > 0)
            {
                number--;
                name = (char)('A' + number % 26) + name;
                number /= 26;
            }
            return name;
        }

        // The seed of a randomization: the one that was entered, or one from the clock if none was.  The report is given the
        // seed that was used, so that the allocation can be made again.
        private static int AutoSeed(ParameterBag parameters)
        {
            if (parameters.ContainsKey("seed") && parameters["seed"] != null && parameters["seed"].IsInt32)
                return parameters["seed"].AsInt32;

            return Base.DefaultSeed();
        }

        /// <summary>
        /// Random allocation in blocks: n subjects are given t treatments, in blocks in each of which every treatment comes as
        /// often as another, so that the numbers on the treatments are never far from one another.
        /// </summary>
        /// <remarks>
        /// A block is the treatments in their order, as often as the block has room for them, put into a random order in which
        /// every order is equally likely (Shuffle).  With a block size the blocks are of that size, and the last is of what is
        /// left if the number of subjects is not divisible by the block size, which the report then says.  Without one each
        /// block has 2, 3 or 4 times the number of treatments, each as likely; when fewer than 4 times the number of
        /// treatments are left, they are the last block.  The number of subjects and the block size are to be divisible by the
        /// number of treatments.
        /// </remarks>
        /// <param name="parameters">"n": the number of subjects; "b", which need not be there: the block size, for which a random
        /// size is taken if it is 0 or below, and 2 if it is 1; "t": the number of treatments, for which 2 is taken if it is
        /// below 2; "seed", which need not be there.</param>
        /// <returns>"seed_out": the seed that was used; "n_out", "t_out": the numbers of subjects and of treatments; "b_out": the
        /// block size, or what the report says of the random sizes; "*blockSizeWarn": a row with "warning" if the last block
        /// is smaller; "*subjects": for each subject "id", its number, and "rx", the name of its treatment (A, B, ...).</returns>
        public static StepOutput RptRandomBlock(ParameterBag parameters)
        {
            int seed = AutoSeed(parameters);
            MersenneTwister mt = new(seed);
            int n = parameters["n"].AsInt32;
            if (n < 2)
                throw new InvalidDataException("At least two subjects are needed");
            int blockSize = -1;
            if (parameters.ContainsKey("b"))
                blockSize = parameters["b"].AsInt32;
            bool blockSizeIsRandom = blockSize <= 0;
            if (!blockSizeIsRandom)
            {
                if (blockSize < 2)
                    blockSize = 2;
            }
            int t = parameters["t"].AsInt32;
            if (t < 2)
                t = 2;
            if (n / (double)t != Math.Floor(n / (double)t))
                throw new InvalidDataException("Number of subjects must be divisible by the number of treatments");

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("seed_out", seed);
            outputParameters.AddOutput("n_out", n);
            outputParameters.AddOutput("t_out", t);

            // the treatment of each subject, 1-based
            int[] rx = new int[n + 1];
            int ctr;
            if (!blockSizeIsRandom)
            {
                // FIXED BLOCK SIZE
                if (n / (double)blockSize != Math.Floor(n / (double)blockSize))
                {
                    string warning = $"The final block size will be {n % blockSize} not {blockSize} because the number of subjects is not divisible by the block size.";
                    outputParameters.AddOutput("*blockSizeWarn", new List<ParameterBag>() { new ParameterBag("warning", new FilledStringParameter(FilledParameterDirection.Output, warning)) });
                }

                if (blockSize / (double)t != Math.Floor(blockSize / (double)t))
                    throw new InvalidDataException("Block size must be divisible by the number of treatments");

                ctr = 0;
                do
                {
                    // curtail the block size if we are at the end of the allocation space
                    int bs = n - ctr < blockSize ? n - ctr : blockSize;

                    // for each block allocate the block pattern as treatments in alphanumeric order
                    for (int j = 1; j <= (int)Math.Floor((double)bs / t); j++)
                    {
                        for (int i = 1; i <= t; i++)
                        {
                            ctr += 1;
                            rx[ctr] = i;
                        }
                    }
                    // randomise the order of the block pattern: every order of the block is equally likely
                    Shuffle(mt, rx, ctr - bs + 1, ctr);

                    // exit the loop if all subjects have been allocated a block
                    if (ctr >= n)
                        break;
                }
                while (true);
                outputParameters.AddOutput("b_out", blockSize);
            }
            else
            {
                // RANDOM BLOCK SIZE
                int minBlockMult = 2;
                int maxBlockMult = 4;
                ctr = 0;
                do
                {
                    // allocate at random a block size of between 'low' and 'high' times the number of treatment groups
                    int bs;
                    if (n - ctr < maxBlockMult * t)
                    {
                        // curtail the random block size selection if we are at the end of the allocation space
                        bs = n - ctr;
                    }
                    else
                    {
                        // pick a multiplier at random between 'low' and 'high' if there is room in the allocation space
                        bs = t * (int)Math.Floor((maxBlockMult - minBlockMult + 1) * mt.NextDouble() + minBlockMult);
                    }
                    // for each block allocate the block pattern as treatments in alphanumeric order
                    for (int j = 1; j <= (int)Math.Floor((double)bs / t); j++)
                    {
                        for (int i = 1; i <= t; i++)
                        {
                            ctr += 1;
                            rx[ctr] = i;
                        }
                    }
                    // randomise the order of the block pattern: every order of the block is equally likely
                    Shuffle(mt, rx, ctr - bs + 1, ctr);

                    // exit the loop if all subjects have been allocated a block
                    if (ctr >= n)
                        break;
                }
                while (true);
                outputParameters.AddOutput("b_out", "random between " + minBlockMult * t + " and " + maxBlockMult * t);
            }

            List<ParameterBag> subjectsList = new();
            outputParameters.AddOutput("*subjects", subjectsList);
            for (int i = 1; i <= n; i++)
            {
                ParameterBag subjectsParameters = new();
                subjectsList.Add(subjectsParameters);
                subjectsParameters.AddOutput("id", i);
                subjectsParameters.AddOutput("rx", TreatmentName(rx[i]));
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Sample size for a study of a correlation coefficient: the smallest number of pairs with which a test of the
        /// coefficient r0 of the null hypothesis has the power asked if the coefficient is r1.
        /// </summary>
        /// <remarks>
        /// Fisher's z of a coefficient, half the logarithm of (1 + r) / (1 - r), is taken as normal with the variance
        /// 1 / (n - 3). The power with n pairs is that of both tails (x_rpower). The first estimate is the number at which one
        /// tail has the power; the smallest number is looked for from it, a pair at a time, or by halving above the whole
        /// numbers of 32 bits. The least that is given is 4 pairs.
        /// </remarks>
        /// <param name="parameters">"p": the power; "a": the two sided significance level; "r0": the coefficient of the null
        /// hypothesis, from 0 to below 1; "r1": that of the alternative hypothesis, above 0 and below 1.</param>
        /// <returns>"alpha", "power", "r0Fmt", "r1Fmt": what was entered; "size": the number of pairs.</returns>
        public static StepOutput RptSizeCorrelation(ParameterBag parameters)
        {
            double p = parameters["p"].AsDouble;
            double a = parameters["a"].AsDouble;
            double r0 = parameters["r0"].AsDouble;
            double r1 = parameters["r1"].AsDouble;

            const string caption = "Sample size for correlation study";
            x_checkPowerAlpha(p, a, caption);
            // Fisher's z is infinite at a coefficient of 1
            if (r0 < 0.0 || r0 >= 1.0 || r1 <= 0.0 || r1 >= 1.0)
                throw new TemplateOperationCancelledException("The correlation coefficient under the null hypothesis must be at least 0 and less than 1, and under the alternative hypothesis greater than 0 and less than 1.", caption);
            if (r0 == r1)
                throw new TemplateOperationCancelledException("The correlation coefficients under the null and alternative hypotheses must differ.", caption);

            double xsig = a / 2.0;
            double zsig = PDF.gauinv(1.0 - xsig, out int flt);
            if (flt != 0)
                throw new InvalidDataException();
            double zpow = PDF.gauinv(p, out flt);
            if (flt != 0)
                throw new InvalidDataException();

            double ztot = zpow + zsig;
            double dif = Math.Abs(Power.fisher_z1(r0) - Power.fisher_z1(r1));
            double sn = Math.Pow(ztot / dif, 2.0) + 3.0;
            // smallest integer n at which the Fisher z power (z ~ N(z(r), 1 / (n - 3))) reaches the target
            if (sn < int.MaxValue)
            {
                sn = Math.Max(Math.Ceiling(sn), 4.0);
                while (sn > 4.0 && x_rpower(dif, sn - 1.0, zsig) >= p)
                    sn -= 1.0;
                while (sn < int.MaxValue && x_rpower(dif, sn, zsig) < p)
                    sn += 1.0;
            }
            else
            {
                // Above the whole numbers of 32 bits the number is looked for by halving, as a search of a pair at a time would
                // be long: the first estimate has the power (it leaves out the second tail, which adds to the power), and 4
                // pairs have not. Where a double no longer holds every whole number the halving stops at the nearest it holds.
                double high = Math.Floor(sn) + 1;
                double low = 4.0;
                for (int i = 0; i < 200 && high - low > 1.0; i++)
                {
                    double mid = Math.Floor((low + high) / 2.0);
                    if (mid <= low || mid >= high)
                        break;
                    if (x_rpower(dif, mid, zsig) >= p)
                        high = mid;
                    else
                        low = mid;
                }
                sn = high;
            }

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("alpha", a);
            outputParameters.AddOutput("power", p);
            outputParameters.AddOutput("r0Fmt", r0);
            outputParameters.AddOutput("r1Fmt", r1);
            outputParameters.AddOutput("size", sn);
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// two sided power for a difference dif between Fisher z transformed correlation coefficients with n pairs
        /// </summary>
        private static double x_rpower(double dif, double n, double zsig)
        {
            double z = dif * Math.Sqrt(n - 3.0);
            return PDF.alnorm(z - zsig) + PDF.alnorm(-z - zsig);
        }

        /// <summary>
        /// Sample size for a comparison of survival times by the log-rank test: the number of experimental subjects, and of
        /// controls, with which a hazard ratio is detected with the power asked.
        /// </summary>
        /// <remarks>
        /// The number of experimental subjects is (z(alpha/2) + z(beta))^2 (1 + 1/m) / (p ln(hr)^2), where p is the chance that
        /// a subject is seen to die before the study ends. With exponential survival, subjects recruited evenly over the
        /// accrual time and followed for the additional time after it, p = 1 - pa exp(-ln(2) F / t), pa = (1 - exp(-ln(2) A / t))
        /// / (ln(2) A / t), where t is the mean of the two median survival times. The number is the whole part of the formula
        /// and 1; the controls are the next whole number to m times it. The note on the power is given as x_disclaim says.
        /// </remarks>
        /// <param name="parameters">"p": the power; "a": the two sided significance level; "ct": the median survival time of
        /// controls; "time-or-hr": "time" if "et", the median survival time of experimental subjects, is given, and if not
        /// "hr", the hazard of experimental subjects relative to controls, which is ct / et; "at": the accrual time; "fut":
        /// the additional follow-up time; "m": the controls per experimental subject.</param>
        /// <returns>"ctFmt", "etFmt", "hrFmt", "atFmt", "futFmt", "alpha", "power": what was entered, with the median or the
        /// hazard ratio that was worked out; "size": the experimental subjects; "controls": the controls; "*assumptions": a
        /// row with the note on the power, if it is given.</returns>
        public static StepOutput RptSizeSurvival(ParameterBag parameters)
        {
            double hr = 0;
            double et = 0;

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double beta = 1.0 - power;
            double ct = parameters["ct"].AsDouble;
            double at = parameters["at"].AsDouble;
            double fut = parameters["fut"].AsDouble;
            double M = parameters["m"].AsDouble;
            if (ct != 0.0)
            {
                bool hasEt = "time".Equals(parameters["time-or-hr"].AsString);
                // hr is the hazard of experimental subjects relative to controls, as the prompt asks for it. With exponential
                // survival the hazard is ln(2) / median, so the hazards are in the inverse ratio of the median survival times.
                if (hasEt)
                {
                    et = parameters["et"].AsDouble;
                    hr = ct / et;
                }
                else
                {
                    hr = parameters["hr"].AsDouble;
                    et = ct / hr;
                }
            }
            const string caption = "Sample size for comparing survival times";
            x_checkPowerAlpha(power, alpha, caption);
            if (!(ct > 0.0))
                throw new TemplateOperationCancelledException("The median survival time of the control group must be greater than 0.", caption);
            if (!(hr > 0.0) || double.IsInfinity(hr) || !(et > 0.0) || double.IsInfinity(et))
                throw new TemplateOperationCancelledException("The median survival time of the experimental group, or the hazard ratio, must be greater than 0.", caption);
            if (hr == 1.0)
                throw new TemplateOperationCancelledException("The median survival times of the two groups must differ: a hazard ratio of 1 gives no effect to detect.", caption);
            if (!(at >= 0.0) || !(fut >= 0.0) || !(at + fut > 0.0))
                throw new TemplateOperationCancelledException("The accrual time and the additional follow-up time must not be below 0, and one of them must be greater than 0.", caption);
            if (!(M > 0.0))
                throw new TemplateOperationCancelledException("The number of controls per experimental subject must be greater than 0.", caption);
            if (ct > 0.0 && et > 0.0 && power > 0.0 && power < 1.0 && alpha > 0.0 && alpha < 1.0 && hr != 1.0 && hr > 0.0 && !double.IsInfinity(hr) && at >= 0.0 && fut >= 0.0 && at + fut > 0.0)
            {
                double avt = (ct + et) / 2.0;
                // with no accrual period every subject is followed for the whole study
                double pa = at == 0.0 ? 1.0 : (1.0 - Math.Exp(-Math.Log(2.0) * at / avt)) / (Math.Log(2.0) * at / avt);
                double P = 1.0 - pa * Math.Exp(-Math.Log(2.0) * fut / avt);
                double zalpha = zcvalue(alpha / 2.0);
                double zbeta = zcvalue(beta);
                double n;
                try
                {
                    n = Math.Pow(zalpha + zbeta, 2.0) * ((1.0 + 1.0 / M) / P) / Math.Pow(Math.Log(hr), 2.0);
                    // rounded as the other sample size functions round. It used to add one and then round up, one subject more than they give.
                    n = Math.Floor(n) + 1;
                }
                catch (Exception)
                {
                    n = -1.0;
                }

                ParameterBag outputParameters = new();
                outputParameters.AddOutput("ctFmt", ct);
                // the experimental median is shown whichever way the inputs were given, so that the direction of the ratio is visible
                outputParameters.AddOutput("etFmt", et);
                outputParameters.AddOutput("hrFmt", hr);
                outputParameters.AddOutput("atFmt", at);
                outputParameters.AddOutput("futFmt", fut);
                outputParameters.AddOutput("alpha", alpha);
                outputParameters.AddOutput("power", power);
                if (n == -1.0)
                {
                    outputParameters.AddOutput("size", BigErr);
                    outputParameters.AddOutput("controls", BigErr);
                }
                else
                {
                    outputParameters.AddOutput("size", n);
                    outputParameters.AddOutput("controls", x_controls(M, n));
                }
                List<ParameterBag> assumptionsList = new();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2.0 * zalpha + zbeta <= 3.1)
                    assumptionsList.Add(x_disclaim(1.0 - beta, 1.0 - beta + alpha / 2.0, n));
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException();
        }

        // The equation of the approximate sample size of a t test, as what is to be 0: the number that the quantiles of
        // Student's t give with the degrees of freedom of n, less n. f = 1: the paired test, with n - 1 degrees of freedom;
        // f = 2: the unpaired test with M controls per subject, with n (M + 1) - 2. k is the difference in standard deviations.
        private static double x_f(int f, double n, double alpha, double beta, double k, double M)
        {
            double x_fReturn = 0;

            if (f == 1)
            {
                if (beta == 0.0)
                    return Constant.MISSING;

                double alpha_t = PDF.tfromp(alpha / 2.0, n - 1.0);
                //  Reproduce previous behaviour
                if (double.IsNaN(alpha_t))
                    alpha_t = Constant.MISSING;
                double beta_t = PDF.tfromp(beta, n - 1.0);
                //  Reproduce previous behaviour
                if (double.IsNaN(beta_t))
                    beta_t = Constant.MISSING;
                x_fReturn = Math.Pow(alpha_t + beta_t, 2.0) / Math.Pow(k, 2.0) - n;
                //  Reproduce previous behaviour
                if (double.IsInfinity(x_fReturn))
                    x_fReturn = 0;
            }
            else if (f == 2)
            {
                if (beta == 0.0)
                    return Constant.MISSING;

                double t1 = PDF.tfromp(alpha / 2.0, n * (M + 1.0) - 2.0);
                if (double.IsNaN(t1))
                    t1 = Constant.MISSING;
                double t2 = PDF.tfromp(beta, n * (M + 1.0) - 2.0);
                if (double.IsNaN(t2))
                    t2 = Constant.MISSING;
                x_fReturn = (1.0 + 1.0 / M) * Math.Pow(t1 + t2, 2.0) / Math.Pow(k, 2.0) - n;
                //  Reproduce previous behaviour
                if (double.IsInfinity(x_fReturn))
                    x_fReturn = 0;
            }
            return x_fReturn;
        }

        // The approximate sample size of a t test (typ 1 paired, 2 unpaired): the root of x_f, looked for by the secant method
        // from the number that the normal distribution gives. ifault is 0 if the root was found, 2 if it was not (xn is then
        // where the search was), 1 if a normal deviate could not be found.
        private static void x_tsample(double aa, double bb, double kk, double mm, int typ, ref double xn, out int ifault)
        {
            double n0;

            double alpha = aa;
            double beta = bb;
            int er;
            ifault = 1;
            double k = kk;
            double m = mm;
            if (typ == 1)
            {
                n0 = Math.Pow(x_zvalc(alpha / 2.0, out er) + x_zvalc(beta, out er), 2.0) / Math.Pow(k, 2.0); // TODO: This has always been unable to detect one of the errors in er on this line.
                if (er != 0)
                    return;
                ifault = 2;
                x_zroot(1, ref n0, 0.0001, ref xn, alpha, beta, k, m, ref er);
            }
            else
            {
                n0 = (1.0 + 1.0 / m) * Math.Pow(x_zvalc(alpha / 2.0, out er) + x_zvalc(beta, out er), 2.0) / Math.Pow(k, 2.0); // TODO: This has always been unable to detect one of the errors in er on this line.
                if (er != 0)
                    return;
                ifault = 2;
                x_zroot(2, ref n0, 0.0001, ref xn, alpha, beta, k, m, ref er);
            }
            if (er != 0)
                xn = n0;
            else
                ifault = 0;
        }

        // The root of x_f by the secant method from x0 and x0 + 1: xn is the root when x_f is within eps of 0; er is 1 if it
        // is not after 200 steps
        private static void x_zroot(int f, ref double x0, double eps, ref double xn, double alpha, double beta, double k, double m, ref int er)
        {
            const int imax = 200;
            double x1 = x0 + 1;
            int iter = 0;
            do
            {
                double fx0 = x_f(f, x0, alpha, beta, k, m);
                double fx1 = x_f(f, x1, alpha, beta, k, m);
                if (Math.Abs(fx0) <= eps)
                {
                    xn = x0;
                    break;
                }
                iter++;
                if (iter > imax)
                {
                    er = 1;
                    break;
                }
                double xnext = x1 - fx1 * (x1 - x0) / (fx1 - fx0);
                x0 = x1;
                x1 = xnext;
            }
            while (true);
        }

        private static double x_zvalc(double alpha, out int er) => -PDF.gauinv(alpha, out er);

        /// <summary>
        /// Randomization of intervention-control pairs: for each pair, which of the two comes first.
        /// </summary>
        /// <remarks>
        /// Each pair has the control first if the number drawn for it is a half or more, and the intervention first if not.
        /// With balanced allocation, which needs an even number of pairs, half of the pairs have the control first: the pairs
        /// are given the two orders in turn, and are then put into a random order in which every arrangement is equally
        /// likely (Shuffle).  If balance is asked for and the number of pairs is odd the pairs are allocated one by one, and
        /// the report has the seed without the note.
        /// </remarks>
        /// <param name="parameters">"pairs": the number of pairs; "balance": whether balanced allocation is asked for; "seed",
        /// which need not be there.</param>
        /// <returns>"seedAndNote": the seed that was used, with ",  balanced allocation" if the allocation is balanced;
        /// "*pairs": for each pair "index", its number as text, and "random", "Control - Intervention" or "Intervention -
        /// Control".</returns>
        public static StepOutput RptRandomPairs(ParameterBag parameters)
        {
            int seed = AutoSeed(parameters);
            MersenneTwister mt = new(seed);
            int pairs = parameters["pairs"].AsInt32;
            bool balance = pairs >= 1 && Math.Floor(pairs / 2.0) == pairs / 2.0 && parameters["balance"].AsBoolean;

            ParameterBag outputParameters = new();
            if (balance)
                outputParameters.AddOutput("seedAndNote", seed + ",  balanced allocation");
            else
                outputParameters.AddOutput("seedAndNote", seed);

            if (pairs >= 1)
            {
                bool[] rand = new bool[pairs + 1 /* VB to C# conversion */ ];
                if (balance)
                {
                    // half of the pairs one way and half the other, in an order in which every arrangement is equally likely
                    for (int n = 1; n <= pairs; n++)
                        rand[n] = !rand[n - 1];
                    Shuffle(mt, rand, 1, pairs);
                }
                else
                {
                    for (int n = 1; n <= pairs; n++)
                        rand[n] = mt.NextDouble() >= 0.5;
                }
                List<ParameterBag> pairsList = new();
                outputParameters.AddOutput("*pairs", pairsList);
                for (int n = 1; n <= pairs; n++)
                {
                    string f = n < 10000 ? "####  " : "#####  ";
                    ParameterBag pairsParameters = new();
                    pairsList.Add(pairsParameters);
                    pairsParameters.AddOutput("index", n.ToString(f));
                    pairsParameters.AddOutput("random", rand[n] ? "Control - Intervention" : "Intervention - Control");
                }
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException("At least one pair is needed");
        }

        /// <summary>
        /// Random allocation to two independent groups of one size: the subjects 1 to "high", an even number, are put into a
        /// random order, of which the first half are the intervention group and the second half the control group.
        /// </summary>
        /// <param name="parameters">"high": the number of subjects; "seed", which need not be there.</param>
        /// <returns>"seed_out": the seed that was used; "*allocations": rows with "case" and "control", a subject of each group,
        /// each group in the order of the numbers of its subjects.</returns>
        public static StepOutput RptRandomUnPaired(ParameterBag parameters)
        {
            int seed = AutoSeed(parameters);
            MersenneTwister mt = new(seed);
            const int low = 1;
            int high = parameters["high"].AsInt32;
            if (high >= 2 && high % 2 == 0)
            {
                int dimit = Math.Abs(high - low) + 1;
                int[] rand = new int[dimit + 1 /* VB to C# conversion */ ];
                int[] arand = new int[(int)Math.Floor((double)dimit / 2) + 1 ];
                int[] brand = new int[(int)Math.Floor((double)dimit / 2) + 1 ];
                for (int N = low; N <= high; N++)
                    rand[N] = N;
                // every order of the subjects is equally likely, so every split into two groups is equally likely
                Shuffle(mt, rand, low, high);
                int halfHigh = Convert.ToInt32(high / 2);
                for (int N = low; N <= halfHigh; N++)
                {
                    arand[N] = rand[N];
                    brand[N] = rand[halfHigh + N];
                }
                Array.Sort(arand, 1, halfHigh);
                Array.Sort(brand, 1, halfHigh);

                ParameterBag outputParameters = new();
                // the seed that was used, which is one from the clock if none was entered
                outputParameters.AddOutput("seed_out", seed);
                List<ParameterBag> allocationsList = new();
                outputParameters.AddOutput("*allocations", allocationsList);
                for (int N = 1; N <= halfHigh; N++)
                {
                    ParameterBag allocationsParameters = new();
                    allocationsList.Add(allocationsParameters);
                    allocationsParameters.AddOutput("case", arand[N]);
                    allocationsParameters.AddOutput("control", brand[N]);
                }
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException("The number of subjects must be even, so that the two groups are of equal size");
        }

        /// <summary>
        /// A series of whole numbers in a random order: the numbers from "low" to "high", or from "high" to "low" if that is the
        /// greater, in an order in which every order is equally likely (Shuffle).
        /// </summary>
        /// <param name="parameters">"low", "high": the ends of the series; "seed", which need not be there.</param>
        /// <returns>"seed_out": the seed that was used; "*allocations": for each place of the series "index", the number that
        /// the place has in the order of the numbers, as text, and "random", the number that it has in the random
        /// order.</returns>
        public static StepOutput RptRandomXY(ParameterBag parameters)
        {
            int seed = AutoSeed(parameters);
            MersenneTwister mt = new(seed);
            int low = parameters["low"].AsInt32;
            int high = parameters["high"].AsInt32;
            if (low > high)
                (high, low) = (low, high);
            if ((long)high - low + 1 > int.MaxValue - 1)
                throw new InvalidDataException("The series is too long to randomise");
            // the numbers low to high, 0-based, in a random order in which every order is equally likely
            int count = high - low + 1;
            int[] rand = new int[count];
            for (int i = 0; i < count; i++)
                rand[i] = low + i;
            Shuffle(mt, rand, 0, count - 1);

            ParameterBag outputParameters = new();
            // the seed that was used, which is one from the clock if none was entered
            outputParameters.AddOutput("seed_out", seed);
            List<ParameterBag> allocationsList = new();
            outputParameters.AddOutput("*allocations", allocationsList);
            for (int i = 0; i < count; i++)
            {
                ParameterBag allocationsParameters = new();
                allocationsList.Add(allocationsParameters);
                allocationsParameters.AddOutput("index", (low + i).ToString());
                allocationsParameters.AddOutput("random", rand[i]);
            }
            return new StepOutput(outputParameters);
        }

        /// <summary>
        /// Sample size for an independent case-control study: the number of cases, and of controls, with which a difference
        /// of the probabilities of exposure of cases and of controls is detected with the power asked.
        /// </summary>
        /// <remarks>
        /// The number of cases is (z(alpha/2) s0 + z(beta) sa)^2 / (p0 - p1)^2, where s0^2 = (1 + 1/m) pbar (1 - pbar), pbar =
        /// (p1 + m p0) / (m + 1), and sa^2 = p0 (1 - p0) / m + p1 (1 - p1); for the chi-square test with the correction for
        /// continuity, and Fisher's exact test, it is n/4 (1 + sqrt(1 + 2 (m + 1) / (n m |p0 - p1|)))^2. Each is the whole part
        /// of its formula and 1; the controls are the next whole number to m times the cases. If the odds ratio is given, the
        /// probability of exposure of cases is p0 r / (1 + p0 (r - 1)). The note on the power is given as x_disclaim says.
        /// </remarks>
        /// <param name="parameters">"p": the power; "a": the two sided significance level; "p0": the probability of exposure in
        /// controls; "prop-or-or": "prop" if "p1", the probability of exposure in cases, is given, and if not "r", the odds
        /// ratio; "m": the controls per case.</param>
        /// <returns>"pc", "ps": the two probabilities; "cpc", "alpha", "power": what was entered; "case", "controls": the
        /// cases and controls; "case_corr", "controls_corr": those of the corrected test; "*assumptions": a row with the note
        /// on the power, if it is given.</returns>
        public static StepOutput RptSizeIndCase(ParameterBag parameters)
        {
            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double beta = 1.0 - power;
            const string caption = "Sample size for independent case-control study";
            x_checkPowerAlpha(power, alpha, caption);
            double p0 = parameters["p0"].AsDouble;
            if (!(p0 >= 0.0 && p0 <= 1.0))
                throw new TemplateOperationCancelledException("The probability of exposure in controls must be from 0 to 1.", caption);
            bool hasP1 = "prop".Equals(parameters["prop-or-or"].AsString);
            double p1;
            if (hasP1)
            {
                p1 = parameters["p1"].AsDouble;
                if (!(p1 >= 0.0 && p1 <= 1.0))
                    throw new TemplateOperationCancelledException("The probability of exposure in cases must be from 0 to 1.", caption);
            }
            else
            {
                double r = parameters["r"].AsDouble;
                if (!(r > 0.0) || double.IsInfinity(r))
                    throw new TemplateOperationCancelledException("The odds ratio must be greater than 0.", caption);
                p1 = p0 * r / (1.0 + p0 * (r - 1.0));
            }
            double M = parameters["m"].AsDouble;
            if (M <= 0.0)
                throw new TemplateOperationCancelledException("The number of controls per case must be greater than 0.", caption);
            if (p1 == p0)
                throw new TemplateOperationCancelledException("The probabilities of exposure in cases and in controls must differ: an odds ratio of 1, or a probability of exposure in controls of 0 or 1, gives no effect to detect.", caption);
            if (p1 != p0 && M > 0)
            {
                double N;
                double ncor = 0;
                double zalpha = 0; double pbar = 0;

                try
                {
                    zalpha = zcvalue(alpha / 2.0);
                    pbar = (p1 + M * p0) / (M + 1.0);
                    double nx = Math.Pow(zalpha * Math.Sqrt((1.0 + 1.0 / M) * pbar * (1.0 - pbar)) + zcvalue(1.0 - power) * Math.Sqrt(p0 * (1.0 - p0) / M + p1 * (1.0 - p1)), 2.0) / Math.Pow(p0 - p1, 2.0);
                    N = Math.Floor(nx) + 1.0;
                    ncor = Math.Floor(nx / 4.0 * Math.Pow(1.0 + Math.Sqrt(1.0 + 2.0 * (M + 1.0) / (nx * M * Math.Abs(p0 - p1))), 2.0)) + 1.0;
                }
                catch (Exception)
                {
                    N = -1.0;
                }

                ParameterBag outputParameters = new();
                outputParameters.AddOutput("pc", p0);
                outputParameters.AddOutput("ps", p1);
                outputParameters.AddOutput("cpc", M);
                outputParameters.AddOutput("alpha", alpha);
                outputParameters.AddOutput("power", power);
                if (N == -1.0)
                {
                    outputParameters.AddOutput("case", BigErr);
                    outputParameters.AddOutput("controls", BigErr);
                    outputParameters.AddOutput("case_corr", BigErr);
                    outputParameters.AddOutput("controls_corr", BigErr);
                }
                else
                {
                    outputParameters.AddOutput("case", N);
                    // the controls are the next whole number, so that there are no fewer than the ratio asks for
                    outputParameters.AddOutput("controls", x_controls(M, N));
                    outputParameters.AddOutput("case_corr", ncor);
                    outputParameters.AddOutput("controls_corr", x_controls(M, ncor));
                }
                double sigmaa = Math.Sqrt(p0 * (1.0 - p0) / M + p1 * (1.0 - p1));
                double sigma0 = Math.Sqrt((1.0 + 1.0 / M) * pbar * (1.0 - pbar));
                double zbeta = zcvalue(1.0 - power);
                List<ParameterBag> assumptionsList = new();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2.0 * (sigma0 / sigmaa) * zalpha + zbeta <= 3.1)
                    assumptionsList.Add(x_disclaim(1.0 - beta, 1.0 - beta + alpha / 2.0, N));
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException();
        }

        /// <summary>
        /// Sample size for an independent cohort study: the number of experimental subjects, and of controls, with which a
        /// difference of the probabilities of the event is detected with the power asked.
        /// </summary>
        /// <remarks>
        /// The formulas are those of the independent case-control study (RptSizeIndCase). If the relative risk is given, the
        /// probability of the event in experimental subjects is p0 r, which is not to be above 1.
        /// </remarks>
        /// <param name="parameters">"p": the power; "a": the two sided significance level; "p0": the probability of the event
        /// in controls; "prop-or-or": "prop" if "p1", the probability in experimental subjects, is given, and if not "r", the
        /// relative risk; "m": the controls per experimental subject.</param>
        /// <returns>As RptSizeIndCase.</returns>
        public static StepOutput RptSizeIndProp(ParameterBag parameters)
        {
            double P1;
            double zalpha = 0; double pbar = 0;
            double ncor = 0;

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double beta = 1.0 - power;
            const string caption = "Sample size for independent cohort study";
            x_checkPowerAlpha(power, alpha, caption);
            double P0 = parameters["p0"].AsDouble;
            if (!(P0 >= 0.0 && P0 <= 1.0))
                throw new TemplateOperationCancelledException("The probability of the event in controls must be from 0 to 1.", caption);
            bool hasP1 = "prop".Equals(parameters["prop-or-or"].AsString);
            if (hasP1)
            {
                P1 = parameters["p1"].AsDouble;
                if (!(P1 >= 0.0 && P1 <= 1.0))
                    throw new TemplateOperationCancelledException("The probability of the event in experimental subjects must be from 0 to 1.", caption);
            }
            else
            {
                double r = parameters["r"].AsDouble;
                if (!(r > 0.0) || double.IsInfinity(r))
                    throw new TemplateOperationCancelledException("The relative risk must be greater than 0.", caption);
                P1 = P0 * r;
                if (P1 > 1.0)
                    throw new TemplateOperationCancelledException("The relative risk times the probability of the event in controls, which is the probability in experimental subjects, must not be above 1.", caption);
            }
            double M = parameters["m"].AsDouble;
            if (M <= 0.0)
                throw new TemplateOperationCancelledException("The number of controls per experimental subject must be greater than 0.", caption);
            if (P1 == P0)
                throw new TemplateOperationCancelledException("The probabilities of the event in experimental subjects and in controls must differ: a relative risk of 1, or a probability in controls of 0 or 1, gives no effect to detect.", caption);
            if (P1 != P0 && M > 0)
            {
                double N;
                try
                {
                    zalpha = zcvalue(alpha / 2.0);
                    pbar = (P1 + M * P0) / (M + 1.0);
                    double nx = Math.Pow(zalpha * Math.Sqrt((1.0 + 1.0 / M) * pbar * (1.0 - pbar)) + zcvalue(1.0 - power) * Math.Sqrt(P0 * (1.0 - P0) / M + P1 * (1.0 - P1)), 2.0) / Math.Pow(P0 - P1, 2.0);
                    N = Math.Floor(nx) + 1.0;
                    ncor = Math.Floor(nx / 4.0 * Math.Pow(1.0 + Math.Sqrt(1.0 + 2.0 * (M + 1.0) / (nx * M * Math.Abs(P0 - P1))), 2.0)) + 1.0;
                }
                catch (Exception)
                {
                    N = -1.0;
                }

                ParameterBag outputParameters = new();
                outputParameters.AddOutput("pc", P0);
                outputParameters.AddOutput("ps", P1);
                outputParameters.AddOutput("cpc", M);
                outputParameters.AddOutput("alpha", alpha);
                outputParameters.AddOutput("power", power);
                if (N == -1.0)
                {
                    outputParameters.AddOutput("case", BigErr);
                    outputParameters.AddOutput("controls", BigErr);
                    outputParameters.AddOutput("case_corr", BigErr);
                    outputParameters.AddOutput("controls_corr", BigErr);
                }
                else
                {
                    outputParameters.AddOutput("case", N);
                    // the controls are the next whole number, so that there are no fewer than the ratio asks for
                    outputParameters.AddOutput("controls", x_controls(M, N));
                    outputParameters.AddOutput("case_corr", ncor);
                    outputParameters.AddOutput("controls_corr", x_controls(M, ncor));
                }
                double sigmaa = Math.Sqrt(P0 * (1.0 - P0) / M + P1 * (1.0 - P1));
                double sigma0 = Math.Sqrt((1.0 + 1.0 / M) * pbar * (1.0 - pbar));
                double zbeta = zcvalue(1.0 - power);
                List<ParameterBag> assumptionsList = new();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2.0 * (sigma0 / sigmaa) * zalpha + zbeta <= 3.1)
                    assumptionsList.Add(x_disclaim(1.0 - beta, 1.0 - beta + alpha / 2.0, N));
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException();
        }

        /// <summary>
        /// Sample size for a matched case-control study: the number of cases, each matched with m controls, with which an
        /// odds ratio is detected with the power asked; and what part that number is of the number with one control.
        /// </summary>
        /// <param name="parameters">"p": the power; "a": the two sided significance level; "ph": the correlation of the
        /// exposures of a case and its control; "p0": the probability of exposure in controls; "ps": the odds ratio; "m": the
        /// controls per case, a whole number from 1 to 1000.</param>
        /// <returns>"corr", "pc", "odds", "cpc", "alpha", "power": what was entered; "size": the cases; "*reduction": with more
        /// than one control, a row with "controls" and "reduction", the number of cases as a part of that with one control;
        /// "*lower": a row if the power is 20% or less; "*assumptions": a row with the note on the power, if it is
        /// given.</returns>
        public static StepOutput RptSizeMatchCase(ParameterBag parameters)
        {

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double beta = 1.0 - power;
            double ph = parameters["ph"].AsDouble;
            double p0 = parameters["p0"].AsDouble;
            double ps = parameters["ps"].AsDouble;
            double M = parameters["m"].AsDouble;
            const string caption = "Sample size for matched case-control study";
            x_checkPowerAlpha(power, alpha, caption);
            // the calculation is for sets of a case and m controls
            if (M != Math.Floor(M))
                throw new TemplateOperationCancelledException("The number of controls per case must be a whole number.", caption);
            if (M < 1.0)
                throw new TemplateOperationCancelledException("There must be at least one control per case.", caption);
            if (M > 1000.0)
                throw new TemplateOperationCancelledException("The number of controls per case must not exceed 1000.", caption);
            if (p0 <= 0.0 || p0 >= 1.0)
                throw new TemplateOperationCancelledException("The probability of exposure in controls must be greater than 0 and less than 1.", caption);
            if (ps == 1.0)
                throw new TemplateOperationCancelledException("An odds ratio of 1 gives no effect to detect: the odds ratio must differ from 1.", caption);
            ssize(alpha, beta, ph, p0, M, ps, out double N, out double FM, out double sigmar, out int fault);
            if (fault == 2)
                throw new TemplateOperationCancelledException("The odds ratio must be greater than 0.", caption);
            if (fault == 1)
                throw new TemplateOperationCancelledException("A correlation of " + ph.ToString() + " between the exposures of a case and its control is not possible with this probability of exposure and odds ratio: a cell of the paired table would have a probability below 0 or above 1. Try a value nearer 0.", caption);

            ParameterBag outputParameters = new();
            outputParameters.AddOutput("corr", ph);
            outputParameters.AddOutput("pc", p0);
            outputParameters.AddOutput("odds", ps);
            outputParameters.AddOutput("cpc", M);
            outputParameters.AddOutput("alpha", alpha);
            outputParameters.AddOutput("power", power);
            List<ParameterBag> lowerList = new();
            outputParameters.AddOutput("*lower", lowerList);
            if (beta >= 0.8)
                lowerList.Add(new ParameterBag());
            if (fault == 0)
            {
                //  TODO: RTF_DeleteBlock() on the illegal piece, which is always removed in valid cases.
                outputParameters.AddOutput("size", N);
                List<ParameterBag> reductionList = new();
                outputParameters.AddOutput("*reduction", reductionList);
                if (M > 1)
                {
                    ParameterBag reductionParameters = new();
                    reductionList.Add(reductionParameters);
                    reductionParameters.AddOutput("controls", M);
                    reductionParameters.AddOutput("reduction", FM);
                }
                double zalpha = zcvalue(alpha / 2.0);
                double zbeta = zcvalue(beta);
                List<ParameterBag> assumptionsList = new();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2.0 * sigmar * zalpha + zbeta <= 3.1)
                    assumptionsList.Add(x_disclaim(1.0 - beta, 1.0 - beta + alpha / 2.0, N));
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException();
        }

        /// <summary>
        /// Sample size for a paired cohort study: the number of pairs of an experimental subject and a control with which a
        /// difference of the event rates is detected with the power asked.
        /// </summary>
        /// <remarks>
        /// With the correlation r of the events of the two subjects of a pair, the pairs of which only the experimental
        /// subject has the event have the probability py = p1 (1 - p0) - r g, and those of which only the control has it
        /// px = p0 (1 - p1) - r g, where g = sqrt(p1 (1 - p1) p0 (1 - p0)). With pa = py / (px + py) the number of pairs is
        /// (z(alpha/2) / 2 + z(beta) sqrt(pa (1 - pa)))^2 / ((pa - 1/2)^2 (px + py)): the whole part of it and 1. The note on
        /// the power is given as x_disclaim says.
        /// </remarks>
        /// <param name="parameters">"p": the power; "a": the two sided significance level; "p0": the event rate of controls;
        /// "ph": the correlation; "er-or-rr": "rr" if "rr", the relative risk, is given, and if not "p1", the event rate of
        /// experimental subjects.</param>
        /// <returns>"pc", "ps": the two event rates; "r", "alpha", "power": what was entered; "size": the pairs;
        /// "*assumptions": a row with the note on the power, if it is given.</returns>
        public static StepOutput RptSizeMatchProp(ParameterBag parameters)
        {
            double P1;
            double N = 0;
            const string caption = "Comparison of proportions for paired cohort study";

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double BETA = 1.0 - power;
            x_checkPowerAlpha(power, alpha, caption);
            double P0 = parameters["p0"].AsDouble;
            double ph = parameters["ph"].AsDouble;
            bool hasRr = "rr".Equals(parameters["er-or-rr"].AsString);
            if (hasRr)
            {
                double rr = parameters["rr"].AsDouble;
                P1 = rr * P0;
            }
            else
            {
                P1 = parameters["p1"].AsDouble;
            }
            // a rate of 0 or 1 leaves no discordant pairs on one side, so the calculation has no answer whatever the correlation
            if (P0 <= 0.0 || P0 >= 1.0)
                throw new TemplateOperationCancelledException("The event rate in the control group must be greater than 0 and less than 1.", caption);
            if (P1 <= 0.0 || P1 >= 1.0)
                throw new TemplateOperationCancelledException("The event rate in the experimental group" + (hasRr ? " (the relative risk times the control group rate)" : string.Empty) + " must be greater than 0 and less than 1.", caption);
            if (P1 == P0)
                throw new TemplateOperationCancelledException("The event rates in the two groups must differ.", caption);
            if (ph <= -1.0 || ph >= 1.0)
                throw new TemplateOperationCancelledException("The correlation coefficient must be greater than -1 and less than 1.", caption);
            if (P1 != P0 && ph > -1.0 && ph < 1.0)
            {
                ParameterBag outputParameters = new();
                outputParameters.AddOutput("pc", P0);
                outputParameters.AddOutput("ps", P1);
                outputParameters.AddOutput("r", ph);
                outputParameters.AddOutput("alpha", alpha);
                outputParameters.AddOutput("power", power);
                double zalpha = zcvalue(alpha / 2.0);
                double zbeta = zcvalue(BETA);
                double Q1 = 1.0 - P1;
                double Q0 = 1.0 - P0;
                double p10 = P1 * Q0 - ph * Math.Sqrt(P1 * Q1 * P0 * Q0);
                double p01 = Q1 * P0 - ph * Math.Sqrt(P1 * Q1 * P0 * Q0);
                double pa = p10 / (p01 + p10);
                double qa = 1.0 - pa;
                if (pa * qa <= 0.0)
                    throw new TemplateOperationCancelledException("Calculation not possible, try a smaller value for correlation.", caption);
                try
                {
                    N = Math.Floor(Math.Pow(zalpha * 0.5 + zbeta * Math.Sqrt(Math.Abs(pa * qa)), 2.0) / (Math.Pow(pa - 0.5, 2.0) * (p01 + p10))) + 1.0;
                    outputParameters.AddOutput("size", N);
                }
                catch (Exception)
                {
                    outputParameters.AddOutput("size", BigErr);
                }
                List<ParameterBag> assumptionsList = new();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2 * (0.5 / Math.Sqrt(Math.Abs(pa * qa))) * zalpha + zbeta <= 3.1)
                {
                    assumptionsList.Add(x_disclaim(1.0 - BETA, 1.0 - BETA + alpha / 2.0, N));
                }
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException();
        }


        /// <summary>
        /// the power and the two sided significance level of a sample size are probabilities above 0 and below 1: the form
        /// takes 0% and 100%, for which there is no sample size
        /// </summary>
        private static void x_checkPowerAlpha(double power, double alpha, string caption)
        {
            if (!(power > 0.0 && power < 1.0))
                throw new TemplateOperationCancelledException("The power must be greater than 0% and less than 100%.", caption);
            if (!(alpha > 0.0 && alpha < 1.0))
                throw new TemplateOperationCancelledException("Alpha must be greater than 0% and less than 100%.", caption);
        }

        // The normal deviate that has the probability alph above it
        private static double zcvalue(double alph) => -PDF.gauinv(alph);

        // The number of cases of a matched case-control study with M controls per case. The probability of exposure of a case,
        // P1, is the one with which the odds ratio of the pairs that differ is xspsi (MathDbl.pone); p01 and p00 are the
        // probabilities of exposure of a control whose case is exposed, and whose case is not. t[i] is the probability that
        // i of the case and its M controls are exposed. From these the mean and variance of the number of exposed cases in
        // sets with i exposed are summed under the null hypothesis (E1, v1) and with the odds ratio (epsi, vpsi), and the
        // number is (z(beta) sqrt(vpsi) + z(alpha/2) sqrt(v1))^2 / (epsi - E1)^2: the whole part of it and 1. The sums are
        // made for M controls and then for 1, of which FM is the ratio. sigmar is sqrt(v1 / vpsi), which the note on the
        // power needs. er is 2 if the odds ratio is not above 0, and 1 if there is no table of a pair with the correlation.
        private static void ssize(double salpha, double sBeta, double sr, double sp0, double M, double xspsi, out double N, out double FM, out double sigmar, out int er)
        {
            double n1 = 0;
            double nm = 0;

            double[] t = new double[1000 + 1];
            er = 0;
            double rm = M;
            double r = sr;
            double P0 = sp0;
            double dpsi = xspsi;
            double zalpha = zcvalue(salpha / 2.0);
            double zbeta = zcvalue(sBeta);
            N = 0;
            FM = 0;
            sigmar = 0;
            if (dpsi <= 0)
            {
                er = 2;
                return;
            }
            MathDbl.pone(P0, dpsi, r, out double P1, out bool impossible);
            if (impossible)
            {
                er = 1;
                return;
            }
            double Q1 = 1.0 - P1;
            double Q0 = 1.0 - P0;
            double p01 = P0 + r * Math.Sqrt(Q1 * P0 * Q0 / P1);
            double p00 = P0 - r * Math.Sqrt(P1 * P0 * Q0 / Q1);
            double q01 = 1.0 - p01;
            double q00 = 1.0 - p00;
            int im = (int)Math.Floor(M);
            do
            {
                double C1 = 1;
                double C2 = rm;
                for (int i = 1; i <= im; i++)
                {
                    t[i] = P1 * C1 * Math.Pow(p01, i - 1) * Math.Pow(q01, im - i + 1) + Q1 * C2 * Math.Pow(p00, i) * Math.Pow(q00, im - i);
                    C1 = C2;
                    C2 = C2 * (rm - i) / ((double)i + 1);
                }
                double E1 = 0;
                for (int i = 1; i <= im; i++)
                    E1 += i * t[i] / (rm + 1.0);
                double v1 = 0.0;
                for (int i = 1; i <= im; i++)
                    v1 += i * t[i] * (rm - i + 1.0) / Math.Pow(rm + 1.0, 2.0);
                double epsi = 0.0;
                for (int i = 1; i <= im; i++)
                    epsi += i * t[i] * dpsi / (i * dpsi + rm - i + 1.0);
                double vpsi = 0;
                for (int i = 1; i <= im; i++)
                    vpsi += i * t[i] * dpsi * (rm - i + 1.0) / Math.Pow(i * dpsi + rm - i + 1, 2.0);
                double S1 = Math.Sqrt(v1);
                double spsi = Math.Sqrt(vpsi);
                sigmar = S1 / spsi;
                if (rm > 1.0)
                {
                    nm = Math.Pow(zbeta * spsi + zalpha * S1, 2.0) / Math.Pow(epsi - E1, 2.0);
                    N = Math.Floor(nm) + 1.0;
                    rm = 1.0;
                    im = 1;
                }
                else if (rm == 1.0)
                {
                    n1 = Math.Pow(zbeta * spsi + zalpha * S1, 2.0) / Math.Pow(epsi - E1, 2.0);
                    break;
                }
                else
                {
                    break;
                }
            }
            while (true);
            if (M > 1)
            {
                // the ratio of the two sample sizes as they are reported, each rounded up to a whole number of cases
                FM = N / (Math.Floor(n1) + 1.0);
            }
            else
            {
                N = Math.Floor(n1) + 1.0;
                FM = 1.0;
            }
        }


        /// <summary>
        /// Sample size for a paired or single sample t test: the smallest number of pairs with which a mean difference is
        /// detected with the power asked.
        /// </summary>
        /// <remarks>
        /// The number is the smallest at which the power of the two sided test, from the non-central t distribution and with
        /// both tails, reaches what is asked (x_ncsize); the search starts from the solution of the approximate equation in
        /// Student's t (x_tsample, x_start). A difference below a ten thousandth of the standard deviation is taken as a ten
        /// thousandth of it: the number is then that of the approximate equation, and the report has a warning.
        /// </remarks>
        /// <param name="parameters">"p": the power; "a": the two sided significance level; "d": the mean difference; "sd": the
        /// standard deviation of the differences.</param>
        /// <returns>What x_tres gives; "*sample_size_warn": a row if the report is to have the warning.</returns>
        public static StepOutput RptSizePaired(ParameterBag parameters)
        {
            double N;
            double xn = 0;

            double P = parameters["p"].AsDouble;
            double a = parameters["a"].AsDouble;
            double D = parameters["d"].AsDouble;
            double sd = parameters["sd"].AsDouble;
            const string caption = "Sample size for a paired or 1 sample t test";
            x_checkPowerAlpha(P, a, caption);
            if (!(sd > 0.0))
                throw new TemplateOperationCancelledException("The standard deviation must be greater than 0.", caption);
            double k = D / sd;
            bool ok = true;
            const double omega = 0.0001;
            if (Math.Abs(k) < omega)
            {
                k = omega;
                ok = false;
            }
            double b = 1.0 - P;
            double M = 1.0;
            x_tsample(a, b, k, M, 1, ref xn, out int flt);
            xn = x_start(xn, false, a, b, k, M);
            if (xn < Convert.ToDouble(int.MaxValue))
            {
                N = Math.Floor(xn) + 1.0;
                if (ok)
                    N = x_ncsize(false, a, P, D, sd, M, N);
            }
            else
            {
                N = int.MaxValue;
                ok = false;
            }
            if (flt == 0 || flt == 2)
            {
                ParameterBag outputParameters = new();
                outputParameters.AddOutput("tt", "a paired or single sample");
                x_tres(outputParameters, false, a, b, P, M, N, D, sd);
                if (!ok)
                    outputParameters.AddOutput("*sample_size_warn", new List<ParameterBag>{ new ParameterBag() });
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException();
        }


        /// <summary>
        /// Sample size for a survey of a population: the number of subjects with which a rate is estimated within a deviation,
        /// with the confidence asked.
        /// </summary>
        /// <remarks>
        /// For a population without end the number is sn = z^2 p (1 - p) / d^2, where z is the normal deviate of the two sided
        /// confidence level; for a population of N it is sn / (1 + sn / N). The number is the whole part of that and 1.
        /// </remarks>
        /// <param name="parameters">"ps": the size of the population; "p": the rate, as a percentage; "xd": the deviation, as a
        /// percentage; "cco": the confidence level.</param>
        /// <returns>"estimate", "rate", "deviation", "level": what was entered, the level as a percentage; "size": the
        /// number of subjects.</returns>
        public static StepOutput RptSizePopSurvey(ParameterBag parameters)
        {
            // double af = 2; 
            double ps = parameters["ps"].AsDouble;
            double P = parameters["p"].AsDouble;
            double xd = parameters["xd"].AsDouble;
            double cco = parameters["cco"].AsDouble;
            const string caption = "Sample size for a population survey";
            if (!(cco > 0.0 && cco < 1.0))
                throw new TemplateOperationCancelledException("The confidence level must be greater than 0% and less than 100%.", caption);
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0, out int fault);
            if (fault == 0)
            {
                double xza = cit;
                // outside 0 to 100% the variance p(1 - p) is negative, and at 0% or 100% there is nothing to estimate
                if (P <= 0.0 || P >= 100.0)
                    throw new TemplateOperationCancelledException("The rate at which the characteristic occurs must be greater than 0% and less than 100%.", caption);
                P /= 100.0;
                xd /= 100.0;
                if (!(xd > 0.0))
                    throw new TemplateOperationCancelledException("The acceptable deviation must be greater than 0%.", caption);
                if (!(ps > 0.0))
                    throw new TemplateOperationCancelledException("The size of the population must be greater than 0.", caption);
                double sn = xza * xza * P * (1.0 - P) / (xd * xd);
                sn /= (1.0 + sn / ps);

                ParameterBag outputParameters = new();
                outputParameters.AddOutput("estimate", ps);
                outputParameters.AddOutput("rate", P * 100);
                outputParameters.AddOutput("deviation", xd * 100);
                outputParameters.AddOutput("level", 100 * cco);
                outputParameters.AddOutput("size", Math.Floor(sn) + 1);
                return new StepOutput(outputParameters);
            }
            return null;
        }


        /// <summary>
        /// Sample size for an unpaired t test: the smallest number of experimental subjects, with m controls for each, with
        /// which a difference of two means is detected with the power asked.
        /// </summary>
        /// <remarks>
        /// As RptSizePaired. The controls are the next whole number to m times the experimental subjects, and the power is
        /// that of the two numbers as they are reported.
        /// </remarks>
        /// <param name="parameters">"p": the power; "a": the two sided significance level; "d": the difference of the means;
        /// "sd": the standard deviation within a group; "m": the controls per experimental subject.</param>
        /// <returns>What x_tres gives; "*sample_size_warn": a row if the report is to have the warning.</returns>
        public static StepOutput RptSizeUnPaired(ParameterBag parameters)
        {
            double N; double xn = 0;

            double P = parameters["p"].AsDouble;
            double a = parameters["a"].AsDouble;
            double D = parameters["d"].AsDouble;
            double sd = parameters["sd"].AsDouble;
            double M = parameters["m"].AsDouble;
            const string caption = "Sample size for an unpaired t test";
            x_checkPowerAlpha(P, a, caption);
            if (!(sd > 0.0))
                throw new TemplateOperationCancelledException("The standard deviation must be greater than 0.", caption);
            if (!(M > 0.0))
                throw new TemplateOperationCancelledException("The number of controls per experimental subject must be greater than 0.", caption);

            double k = D / sd;
            bool ok = true;
            const double omega = 0.0001;
            if (Math.Abs(k) < omega)
            {
                k = omega;
                ok = false;
            }
            double b = 1.0 - P;
            x_tsample(a, b, k, M, 2, ref xn, out int fault);
            xn = x_start(xn, true, a, b, k, M);
            if (xn < Convert.ToDouble(int.MaxValue))
            {
                N = Math.Floor(xn) + 1L;
                if (ok)
                    N = x_ncsize(true, a, P, D, sd, M, N);
            }
            else
            {
                N = int.MaxValue;
                ok = false;
            }
            if (fault == 0 || fault == 2)
            {
                ParameterBag outputParameters = new();
                outputParameters.AddOutput("tt", "an unpaired two sample");
                x_tres(outputParameters, true, a, b, P, M, N, D, sd);
                if (!ok)
                    outputParameters.AddOutput("*sample_size_warn", new List<ParameterBag> { new ParameterBag() });
                return new StepOutput(outputParameters);
            }
            throw new InvalidDataException();
        }

        /// <summary>
        /// where the search for the smallest sample size of a t test starts: the solution xn of the approximate equation in
        /// Student's t, or the normal approximation if the secant search for that solution has not come back with a number
        /// within 100 of it. It does not where the number is small: the secant search then goes to numbers below 1, for
        /// which Student's t has no degrees of freedom (a difference of 3 standard deviations with a power of 50% at the
        /// 0.1% level gave 2,147,483,647 pairs for 6). The search gives the same number from any start.
        /// </summary>
        private static double x_start(double xn, bool unpaired, double alpha, double beta, double k, double m)
        {
            double nz = (unpaired ? 1.0 + 1.0 / m : 1.0) * Math.Pow(zcvalue(alpha / 2.0) + zcvalue(beta), 2.0) / (k * k);
            if (double.IsNaN(nz) || double.IsInfinity(nz))
                return xn;
            return Math.Abs(xn - nz) <= 100.0 ? xn : nz;
        }

        // The note on the power of a sample size. The formulas take the power from the tail of the effect alone. The other
        // tail, that of a significant result in the direction opposite to the effect, adds the normal probability beyond
        // 2 (s0 / sa) z(alpha/2) + z(beta), where s0 and sa are the standard deviations under the null and the alternative
        // hypothesis. The reports give the note if that deviate is 3.1 or less, which is a probability of 0.001 or more;
        // the note has the power asked (ll), and the power asked and alpha / 2 (ul), as the least and the most that the
        // true power is.
        private static ParameterBag x_disclaim(double ll, double ul, double N)
        {
            ParameterBag outputParameters = new();
            outputParameters.AddOutput("cases", N);
            outputParameters.AddOutput("no_less", ll);
            // the upper bound, power + alpha / 2, is a probability
            outputParameters.AddOutput("no_greater", Math.Min(ul, 1.0));
            return outputParameters;
        }

        /// <summary>
        /// two sided power of the t test with n pairs, or with n experimental subjects and the whole number of controls the report gives them
        /// </summary>
        private static double x_tpower(bool unpaired, double alpha, double delta, double sd, double m, double n) => unpaired ? Power.tstpower(alpha, delta, sd, n, x_controls(m, n) / n) : Power.ptpower(alpha, delta, sd, n);

        /// <summary>
        /// the number of controls for n experimental subjects at m controls per subject: a whole number, and at least one. The product is
        /// rounded before the ceiling is taken: 1.1 x 50 is 55.000000000000007 in floating point, and a whole-number design must not gain a control
        /// </summary>
        private static double x_controls(double m, double n) => Math.Ceiling(Math.Round(m * n, 9));

        /// <summary>
        /// smallest integer sample size (per experimental group if unpaired) whose two sided non-central t power reaches the target, searched from the approximation n0
        /// </summary>
        private static double x_ncsize(bool unpaired, double alpha, double power, double delta, double sd, double m, double n0)
        {
            double n = Math.Max(n0, 2.0);
            double pw = x_tpower(unpaired, alpha, delta, sd, m, n);
            if (pw != Constant.MISSING && pw >= power)
            {
                while (n > 2.0)
                {
                    pw = x_tpower(unpaired, alpha, delta, sd, m, n - 1.0);
                    if (pw == Constant.MISSING || pw < power)
                        break;
                    n -= 1.0;
                }
                return n;
            }
            for (int i = 0; i < 1000; i++)
            {
                n += 1.0;
                pw = x_tpower(unpaired, alpha, delta, sd, m, n);
                if (pw != Constant.MISSING && pw >= power)
                    return n;
            }
            return n0;
        }

        // What the report of a t test is given: "alpha", "power", "delta", "sd": what was entered; "mean": the words for the
        // difference; "size": the number of pairs or of experimental subjects; "df": the degrees of freedom of the test;
        // "*controls": for the unpaired test a row with "con_per", the controls per subject; "*subjects": for the unpaired
        // test a row with "con_tot", the controls; "*pairs": a row for the paired test; "*assumptions": a row with the note
        // on the power, which is given if twice the quantile of Student's t for alpha / 2 and the quantile for half of beta
        // come to 3.1 or less.
        private static void x_tres(ParameterBag outputParameters, bool unpaired, double alpha, double b, double power, double m, double n, double delta, double sd)
        {
            int ierr = 0;

            outputParameters.AddOutput("alpha", alpha);
            outputParameters.AddOutput("power", power);
            outputParameters.AddOutput("mean", unpaired ? "between means" : "of mean from zero");
            outputParameters.AddOutput("delta", delta);
            outputParameters.AddOutput("sd", sd);
            double df = n - 1.0;
            List<ParameterBag> controlsList = new();
            outputParameters.AddOutput("*controls", controlsList);
            if (unpaired)
            {
                ParameterBag controlsParameters = new();
                controlsList.Add(controlsParameters);
                controlsParameters.AddOutput("con_per", m);
                // the degrees of freedom of the design reported: n subjects and a whole number of controls
                df = n + x_controls(m, n) - 2.0;
            }
            outputParameters.AddOutput("size", n);

            List<ParameterBag> pairsList = new();
            outputParameters.AddOutput("*pairs", pairsList);
            List<ParameterBag> subjectsList = new();
            outputParameters.AddOutput("*subjects", subjectsList);
            if (unpaired)
            {
                ParameterBag subjectsParameters = new();
                subjectsList.Add(subjectsParameters);
                subjectsParameters.AddOutput("con_tot", x_controls(m, n));
            }
            else
            {
                pairsList.Add(new ParameterBag());
            }
            outputParameters.AddOutput("df", df);
            double ta = PDF.tfromp(alpha / 2.0, df);
            double tb = PDF.tfromp(b / 2.0, df);
            List<ParameterBag> assumptionsList = new();
            outputParameters.AddOutput("*assumptions", assumptionsList);
            if (ierr == 0 & 2.0 * ta + tb <= 3.1)
            {
                // the bounds on the true power are those of the approximate formula; the size reported is the smallest whose power from
                // the non-central t distribution reaches the target, and never below 2, so at that floor its power can exceed the upper bound
                double ll = 1.0 - b;
                double ul = 1.0 - b + alpha / 2.0;
                double pw = n < int.MaxValue ? x_tpower(unpaired, alpha, delta, sd, m, n) : Constant.MISSING;
                if (pw != Constant.MISSING)
                {
                    ll = Math.Min(ll, pw);
                    ul = Math.Max(ul, pw);
                }
                assumptionsList.Add(x_disclaim(ll, ul, n));
            }
        }
    }
}
