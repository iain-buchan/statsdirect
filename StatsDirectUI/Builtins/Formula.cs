using System;
using System.Collections.Generic;

using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;

namespace StatsDirect.Builtins
{
    public class Formula
    {
        private struct TwoLng : IComparable<TwoLng>
        {
            public int Id;
            public int Rx;

            private int CompareTo(TwoLng other)
            {
                if (Id < other.Id)
                    return -1;
                return Id == other.Id ? 0 : 1;
            }

            // interface methods implemented by CompareTo
            int IComparable<TwoLng>.CompareTo(TwoLng other)
            {
                return CompareTo(other);
            }

        }


        private const string BigErr = "(err: number too big)";


        private static int AutoSeed(ParameterBag parameters)
        {
            if (parameters.ContainsKey("seed") && parameters["seed"] != null && parameters["seed"].IsInt32)
            {
                return parameters["seed"].AsInt32;
            }
            return Base.DefaultSeed();
        }


        public static StepResult RptRandomBlock(ITemplateHost host, ParameterBag parameters)
        {
            int i; int j; int ctr; int low; int high; int bs; int bks; int minBlockMult = 0; int maxBlockMult = 0;
            TwoLng[] x;
            bool rb;
            const string caption = "Allocate subjects in blocks";

            int seed = AutoSeed(parameters);
            MersenneTwister mt = new MersenneTwister(seed);
            int N = parameters["n"].AsInt32;
            if (N < 4)
            {
                N = 4;
            }
            int b = -1;
            if (parameters.ContainsKey("b"))
                b = parameters["b"].AsInt32;
            if (b <= 0)
            {
                rb = true;
            }
            else
            {
                rb = false;
                if (b < 2)
                {
                    b = 2;
                }
            }
            int t = parameters["t"].AsInt32;
            if (t < 2)
            {
                t = 2;
            }
            if (N / (double)t != Math.Floor(N / (double)t))
            {
                throw new InvalidDataException("Number of subjects must be divisible by the number of treatments");
            }
            // bool incomplete = false; 
            if (rb == false)
            {
                // FIXED BLOCK SIZE
                if (N / (double)b != Math.Floor(N / (double)b))
                {
                    host.Warning("The final block size will be " + (N % b).ToString() + " not " + b.ToString() + " because" + "\r\n" + "the number of subjects is not divisible by the block size.", caption);
                    // incomplete = true; 
                }
                bks = ((int)(Math.Floor((double)N / b)));
                if (b / (double)t != Math.Floor(b / (double)t))
                {
                    throw new InvalidDataException("Block size must be divisible by the number of treatments");
                }
                x = new TwoLng[b * bks + 1 /* VB to C# conversion */ ];
                ctr = 0;
                do
                {
                    if (N - ctr < b * t)
                    {
                        // curtail the random block size selection if we are at the end of the allocation space
                        bs = N - ctr;
                    }
                    else
                    {
                        bs = b;
                    }
                    // for each block allocate the block pattern as treatments in alphanumeric order
                    for (j = 1; j <= ((int)(Math.Floor((double)bs / t))); j++)
                    {
                        for (i = 1; i <= t; i++)
                        {
                            ctr = ctr + 1;
                            x[ctr].Rx = i;
                        }
                    }
                    // randomise the order of the block pattern by allocating an order number at random for each element then bubble sort the array
                    high = ctr;
                    low = ctr - bs + 1;
                    for (j = high; j >= low; j--)
                    {
                        x[j].Id = ((int)(Math.Floor((high - low + 1) * mt.NextDouble() + low)));
                    }
                    // exit the loop if all subjects have been allocated a block
                    if (ctr >= N)
                    {
                        break;
                    }
                }
                while (true);
            }
            else
            {
                // RANDOM BLOCK SIZE
                minBlockMult = 2;
                maxBlockMult = 4;
                //  allocate a two-element array: treatment element & subject/order element
                x = new TwoLng[N + 1 /* VB to C# conversion */ ];
                ctr = 0;
                bks = 0;
                do
                {
                    bks = bks + 1;
                    // allocate at random a block size of between 'low' and 'high' times the number of treatment groups
                    if (N - ctr < maxBlockMult * t)
                    {
                        // curtail the random block size selection if we are at the end of the allocation space
                        bs = N - ctr;
                    }
                    else
                    {
                        // pick a multiplier at random between 'low' and 'high' if there is room in the allocation space
                        bs = t * ((int)(Math.Floor((maxBlockMult - minBlockMult + 1) * mt.NextDouble() + minBlockMult)));
                    }
                    // for each block allocate the block pattern as treatments in alphanumeric order
                    for (j = 1; j <= ((int)(Math.Floor((double)bs / t))); j++)
                    {
                        for (i = 1; i <= t; i++)
                        {
                            ctr = ctr + 1;
                            x[ctr].Rx = i;
                        }
                    }
                    // randomise the order of the block pattern by allocating an order number at random for each element then bubble sort the array
                    high = ctr;
                    low = ctr - bs + 1;
                    for (j = high; j >= low; j--)
                    {
                        x[j].Id = ((int)(Math.Floor((high - low + 1) * mt.NextDouble() + low)));
                    }
                    // exit the loop if all subjects have been allocated a block
                    if (ctr >= N)
                    {
                        break;
                    }
                }
                while (true);
            }

            //  sort the id numbers within blocks
            Array.Sort(x, 1, ctr);

            //  RTF_LoadTemplate("r_block.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("seed", seed.ToString());
            outputParameters.AddOutput("n", N.ToString());
            if (rb == false)
            {
                outputParameters.AddOutput("b", b.ToString());
            }
            else
            {
                outputParameters.AddOutput("b", "random between " + (minBlockMult * t).ToString() + " and " + (maxBlockMult * t).ToString());
            }
            outputParameters.AddOutput("t", t.ToString());
            List<ParameterBag> subjectsList = new List<ParameterBag>();
            outputParameters.AddOutput("*subjects", subjectsList);
            for (i = 1; i <= N; i++)
            {
                ParameterBag subjectsParameters = new ParameterBag();
                subjectsList.Add(subjectsParameters);
                subjectsParameters.AddOutput("id", i.ToString());
                subjectsParameters.AddOutput("rx", Convert.ToChar(64 + x[i].Rx));
            }
            return new StepResult(StepSuccess.Success, outputParameters);
        }

        public static StepResult RptSizeCorrelation(ITemplateHost host, ParameterBag parameters)
        {
            double P = parameters["p"].AsDouble;
            double a = parameters["a"].AsDouble;
            double r0 = parameters["r0"].AsDouble;
            double r1 = parameters["r1"].AsDouble;
            if (P >= 1.0 || P < 0.000001)
                P = 0.8;
            if (a >= 1.0 || P < 0.000001)
                P = 0.05;

            if (r0 < 0.0 || r0 > 1.0 || r1 <= 0.0 || r1 >= 1.0)
                throw new InvalidDataException();

            double dif = Math.Abs(Power.fisher_z1(r0) - Power.fisher_z1(r1));
            double xsig = a / 2.0;
            int flt;
            double zsig = PDF.gauinv(1.0 - xsig, out flt);
            double zpow = 0;
            if (flt == 0)
                zpow = PDF.gauinv(P, out flt);
            if (flt != 0)
                throw new InvalidDataException();

            double ztot = zpow + zsig;
            double sn = Math.Pow((ztot / dif), 2.0) + 3.0;
            // get precise (to 0.001) result by monotone bisection
            const double acc = 0.0000001;
            double stp = sn / 10.0;
            double xp = sn;
            int ctr = 0;
            double lastDelta = 0;
            do
            {
                ctr = ctr + 1;
                double delta = P - Power.rpower(r0, r1, xp, a);
                if (Math.Abs(delta) < acc)
                {
                    sn = xp;
                    break;
                }
                if (ctr > 500)
                {
                    break;
                }
                if (Math.Abs(delta) > Math.Abs(lastDelta))
                {
                    stp = stp / 2.0;
                }
                if (delta > 0.0)
                {
                    stp = Math.Abs(stp);
                }
                else
                {
                    stp = -Math.Abs(stp);
                }
                lastDelta = delta;
                xp = xp + stp;
            }
            while (true);

            //  RTF_LoadTemplate("s_corr.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("alpha", host.RoundU(a));
            outputParameters.AddOutput("power", host.RoundU(P));
            outputParameters.AddOutput("r0Fmt", host.RoundU(r0));
            outputParameters.AddOutput("r1Fmt", host.RoundU(r1));
            outputParameters.AddOutput("size", (Math.Floor(sn) + 1).ToString());
            return new StepResult(StepSuccess.Success, outputParameters);
        }


        public static StepResult RptSizeSurvival(ITemplateHost host, ParameterBag parameters)
        {
            double hr = 0;
            double et = 0;

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double BETA = 1.0 - power;
            double ct = parameters["ct"].AsDouble;
            double at = parameters["at"].AsDouble;
            double fut = parameters["fut"].AsDouble;
            double M = parameters["m"].AsDouble;
            if (ct != 0.0)
            {
                bool hasEt = "time".Equals(parameters["time-or-hr"].AsString);
                if (hasEt)
                {
                    et = parameters["et"].AsDouble;
                    hr = et / ct;
                }
                else
                {
                    hr = parameters["hr"].AsDouble;
                    et = hr * ct;
                }
            }
            if (ct > 0.0 & power > 0.0 & power < 1.0 & alpha > 0.0 & alpha < 1.0 & hr != 1.0 & hr > 0.0 & at >= 0.0 & fut >= 0.0)
            {
                if (at == 0.0)
                {
                    at = fut * 0.00004;
                }
                if (M <= 0.0)
                {
                    M = 1;
                }
                double avt = (ct + et) / 2.0;
                double pa = (1.0 - Math.Exp(-Math.Log(2.0) * at / avt)) / (Math.Log(2.0) * at / avt);
                double P = 1.0 - pa * Math.Exp(-Math.Log(2.0) * fut / avt);
                double zalpha = zcvalue(alpha / 2.0);
                double zbeta = zcvalue(BETA);
                double N;
                try
                {
                    N = Math.Pow((zalpha + zbeta), 2.0) * ((1.0 + 1.0 / M) / P) / Math.Pow((Math.Log(hr)), 2.0) + 1.0;
                    if (N != Math.Floor(N))
                    {
                        N = Math.Floor(N) + 1;
                    }
                }
                catch (Exception)
                {
                    N = -1.0;
                }

                //  RTF_LoadTemplate("s_survival.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("ctFmt", host.RoundU(ct));
                outputParameters.AddOutput("hrFmt", host.RoundU(hr));
                outputParameters.AddOutput("atFmt", host.RoundU(at));
                outputParameters.AddOutput("futFmt", host.RoundU(fut));
                outputParameters.AddOutput("alpha", host.RoundU(alpha));
                outputParameters.AddOutput("power", host.RoundU(power));
                if (N == -1.0)
                {
                    outputParameters.AddOutput("size", BigErr);
                    outputParameters.AddOutput("controls", BigErr);
                }
                else
                {
                    outputParameters.AddOutput("size", N.ToString());
                    outputParameters.AddOutput("controls", (N * M).ToString());
                }
                List<ParameterBag> assumptionsList = new List<ParameterBag>();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2.0 * zalpha + zbeta <= 3.1)
                {
                    assumptionsList.Add(x_disclaim(1.0 - BETA, 1.0 - BETA + alpha / 2.0, N));
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        private static double x_f(int f, double N, double alpha, double BETA, double k, double M)
        {
            double x_fReturn = 0;

            if (f == 1)
            {
                if (BETA == 0.0)
                    return Constant.MISSING;

                double alpha_t = PDF.tfromp(alpha / 2.0, N - 1.0);
                //  Reproduce previous behaviour
                if (double.IsNaN(alpha_t))
                    alpha_t = Constant.MISSING;
                double beta_t = PDF.tfromp(BETA, N - 1.0);
                //  Reproduce previous behaviour
                if (double.IsNaN(beta_t))
                    beta_t = Constant.MISSING;
                x_fReturn = Math.Pow((alpha_t + beta_t), 2.0) / Math.Pow(k, 2.0) - N;
                //  Reproduce previous behaviour
                if (double.IsInfinity(x_fReturn))
                    x_fReturn = 0;
            }
            else if (f == 2)
            {
                if (BETA == 0.0)
                    return Constant.MISSING;

                double t1 = PDF.tfromp(alpha / 2.0, N * (M + 1.0) - 2.0);
                if (double.IsNaN(t1))
                    t1 = Constant.MISSING;
                double t2 = PDF.tfromp(BETA, N * (M + 1.0) - 2.0);
                if (double.IsNaN(t2))
                    t2 = Constant.MISSING;
                x_fReturn = (1.0 + 1.0 / M) * Math.Pow((t1 + t2), 2.0) / Math.Pow(k, 2.0) - N;
                //  Reproduce previous behaviour
                if (double.IsInfinity(x_fReturn))
                    x_fReturn = 0;
            }
            return x_fReturn;
        }

        private static void x_tsample(double aa, double bb, double kk, double MM, int typ, ref double xn, out int ifault)
        {
            double n0;

            double alpha = aa;
            double BETA = bb;
            int er;
            ifault = 1;
            double k = kk;
            double M = MM;
            if (typ == 1)
            {
                n0 = (Math.Pow((x_zvalc(alpha / 2.0, out er) + x_zvalc(BETA, out er)), 2.0)) / (Math.Pow(k, 2.0)); // TODO: This has always been unable to detect one of the errors in er on this line.
                if (er != 0)
                {
                    return;
                }
                ifault = 2;
                x_zroot(1, ref n0, 0.0001, ref xn, alpha, BETA, k, M, ref er);
            }
            else
            {
                n0 = ((1.0 + 1.0 / M) * Math.Pow((x_zvalc(alpha / 2.0, out er) + x_zvalc(BETA, out er)), 2.0)) / (Math.Pow(k, 2.0));
                if (er != 0)
                {
                    return;
                }
                ifault = 2;
                x_zroot(2, ref n0, 0.0001, ref xn, alpha, BETA, k, M, ref er);
            }
            if (er != 0)
            {
                xn = n0;
            }
            else
            {
                ifault = 0;
            }
        }

        private static void x_zroot(int f, ref double x0, double eps, ref double xn, double alpha, double BETA, double k, double M, ref int er)
        {
            const int imax = 200;
            double x1 = x0 + 1;
            int iter = 0;
            do
            {
                double fx0 = x_f(f, x0, alpha, BETA, k, M);
                double fx1 = x_f(f, x1, alpha, BETA, k, M);
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
                double xnext = x1 - ((fx1 * (x1 - x0)) / (fx1 - fx0));
                x0 = x1;
                x1 = xnext;
            }
            while (true);
        }

        private static double x_zvalc(double alpha, out int er)
        {
            return -PDF.gauinv(alpha, out er);
        }

        public static StepResult RptRandomPairs(ITemplateHost host, ParameterBag parameters)
        {
            int Seed = AutoSeed(parameters);
            MersenneTwister mt = new MersenneTwister(Seed);
            int pairs = parameters["pairs"].AsInt32;
            bool balance = pairs >= 1 && (Math.Floor(pairs / 2.0) == pairs / 2.0) && parameters["balance"].AsBoolean;
            //  RTF_LoadTemplate("r_pair.rtf")
            ParameterBag outputParameters = new ParameterBag();
            if (balance)
                outputParameters.AddOutput("seed", Seed.ToString() + ",  balanced allocation");
            else
                outputParameters.AddOutput("seed", Seed.ToString());

            if (pairs >= 1)
            {
                bool[] rand = new bool[pairs + 1 /* VB to C# conversion */ ];
                int N;
                if (balance)
                {
                    for (N = 1; N <= pairs; N++)
                    {
                        rand[N] = !(rand[N - 1]);
                    }
                    int tn;
                    for (tn = 1; tn <= 3; tn++)
                    {
                        for (N = 1; N <= pairs; N++)
                        {
                            int nrp = Convert.ToInt32(Math.Floor(pairs * mt.NextDouble()) + 1);
                            bool tmpBool = rand[N];
                            rand[N] = rand[nrp];
                            rand[nrp] = tmpBool;
                        }
                    }
                }
                else
                {
                    for (N = 1; N <= pairs; N++)
                    {
                        rand[N] = mt.NextDouble() >= 0.5;
                    }
                }
                List<ParameterBag> pairsList = new List<ParameterBag>();
                outputParameters.AddOutput("*pairs", pairsList);
                for (N = 1; N <= pairs; N++)
                {
                    string f = N < 10000 ? "####  " : "#####  ";
                    ParameterBag pairsParameters = new ParameterBag();
                    pairsList.Add(pairsParameters);
                    pairsParameters.AddOutput("index", N.ToString(f));
                    pairsParameters.AddOutput("random", rand[N] ? "Control - Intervention" : "Intervention - Control");
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        public static StepResult RptRandomUnPaired(ITemplateHost host, ParameterBag parameters)
        {
            int seed = AutoSeed(parameters);
            MersenneTwister mt = new MersenneTwister(seed);
            const int low = 1;
            int high = parameters["high"].AsInt32;
            if (high >= 2 && high % high / 2 == 0)
            {
                int dimit = Math.Abs(high - low) + 1;
                int[] rand = new int[dimit + 1 /* VB to C# conversion */ ];
                int[] arand = new int[((int)(Math.Floor((double)dimit / 2))) + 1 /* for VB to C# conversion */ ];
                int[] brand = new int[((int)(Math.Floor((double)dimit / 2))) + 1 /* for VB to C# conversion */ ];
                for (int N = low; N <= high; N++)
                {
                    rand[N] = N;
                }
                for (int N = low; N <= high; N++)
                {
                    int nrp = ((int)(Math.Floor((high - low + 1) * mt.NextDouble() + low)));
                    int tmp = rand[N];
                    rand[N] = rand[nrp];
                    rand[nrp] = tmp;
                }
                int halfHigh = Convert.ToInt32(high / 2);
                for (int N = low; N <= halfHigh; N++)
                {
                    arand[N] = rand[N];
                    brand[N] = rand[halfHigh + N];
                }
                Array.Sort(arand, 1, halfHigh);
                Array.Sort(brand, 1, halfHigh);

                //  RTF_LoadTemplate("r_unpair.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("seed", seed.ToString());
                List<ParameterBag> allocationsList = new List<ParameterBag>();
                outputParameters.AddOutput("*allocations", allocationsList);
                for (int N = 1; N <= halfHigh; N++)
                {
                    ParameterBag allocationsParameters = new ParameterBag();
                    allocationsList.Add(allocationsParameters);
                    allocationsParameters.AddOutput("case", arand[N].ToString());
                    allocationsParameters.AddOutput("control", brand[N].ToString());
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        public static StepResult RptRandomXY(ITemplateHost host, ParameterBag parameters)
        {
            int seed = AutoSeed(parameters);
            MersenneTwister mt = new MersenneTwister(seed);
            int low = parameters["low"].AsInt32;
            int high = parameters["high"].AsInt32;
            if (low > high)
            {
                int t = low;
                low = high;
                high = t;
            }
            if (low >= 0 & high >= 1)
            {
                int[] rand = new int[high + 1 + 1 /* for VB to C# conversion */ ];
                int N;
                for (N = low; N <= high; N++)
                {
                    rand[N] = N;
                }
                int tn;
                for (tn = 1; tn <= 3; tn++)
                {
                    for (N = low; N <= high; N++)
                    {
                        int nrp = ((int)(Math.Floor((high - low + 1) * mt.NextDouble() + low)));
                        int tmp = rand[N];
                        rand[N] = rand[nrp];
                        rand[nrp] = tmp;
                    }
                }
                //  RTF_LoadTemplate("r_xy.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("seed", seed.ToString());
                List<ParameterBag> allocationsList = new List<ParameterBag>();
                outputParameters.AddOutput("*allocations", allocationsList);
                for (N = low; N <= high; N++)
                {
                    ParameterBag allocationsParameters = new ParameterBag();
                    allocationsList.Add(allocationsParameters);
                    allocationsParameters.AddOutput("index", N.ToString("#####"));
                    allocationsParameters.AddOutput("random", rand[N].ToString());
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        public static StepResult RptSizeIndCase(ITemplateHost host, ParameterBag parameters)
        {
            double P1;
            double zalpha = 0; double pbar = 0;
            double ncor = 0;

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double BETA = 1.0 - power;
            double P0 = parameters["p0"].AsDouble;
            if (P0 > 1.0)
            {
                P0 = 1.0;
            }
            if (P0 < 0.0)
            {
                P0 = 0.0;
            }
            bool hasP1 = "prop".Equals(parameters["prop-or-or"].AsString);
            if (hasP1)
            {
                P1 = parameters["p1"].AsDouble;
            }
            else
            {
                double r = parameters["r"].AsDouble;
                P1 = P0 * r / (1.0 + P0 * (r - 1.0));
            }
            if (P1 > 1.0)
            {
                P1 = 1.0;
            }
            if (P1 < 0.0)
            {
                P1 = 0.0;
            }
            double M = parameters["m"].AsDouble;
            if (P1 != P0 & M > 0)
            {
                double N;
                try
                {
                    zalpha = zcvalue(alpha / 2.0);
                    pbar = (P1 + M * P0) / (M + 1.0);
                    double nx = Math.Pow((zalpha * Math.Sqrt((1.0 + 1.0 / M) * pbar * (1.0 - pbar)) + zcvalue(1.0 - power) * Math.Sqrt(P0 * (1.0 - P0) / M + P1 * (1.0 - P1))), 2.0) / Math.Pow((P0 - P1), 2.0);
                    N = Math.Floor(nx) + 1.0;
                    ncor = Math.Floor(N / 4.0 * Math.Pow((1.0 + Math.Sqrt(1.0 + 2.0 * (M + 1.0) / (N * M * Math.Abs(P0 - P1)))), 2.0)) + 1.0;
                }
                catch (Exception)
                {
                    N = -1.0;
                }
                //  RTF_LoadTemplate("s_incase.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("pc", host.RoundU(P0));
                outputParameters.AddOutput("ps", host.RoundU(P1));
                outputParameters.AddOutput("cpc", M.ToString());
                outputParameters.AddOutput("alpha", host.RoundU(alpha));
                outputParameters.AddOutput("power", host.RoundU(power));
                if (N == -1.0)
                {
                    outputParameters.AddOutput("case", BigErr);
                    outputParameters.AddOutput("controls", BigErr);
                    outputParameters.AddOutput("case_corr", BigErr);
                    outputParameters.AddOutput("controls_corr", BigErr);
                }
                else
                {
                    outputParameters.AddOutput("case", N.ToString());
                    outputParameters.AddOutput("controls", Math.Floor(M * N).ToString());
                    outputParameters.AddOutput("case_corr", ncor.ToString());
                    outputParameters.AddOutput("controls_corr", Math.Floor(M * ncor).ToString());
                }
                double sigmaa = Math.Sqrt(P0 * (1.0 - P0) / M + P1 * (1.0 - P1));
                double sigma0 = Math.Sqrt((1.0 + 1.0 / M) * pbar * (1.0 - pbar));
                double zbeta = zcvalue(1.0 - power);
                List<ParameterBag> assumptionsList = new List<ParameterBag>();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2.0 * (sigma0 / sigmaa) * zalpha + zbeta <= 3.1)
                {
                    assumptionsList.Add(x_disclaim(1.0 - BETA, 1.0 - BETA + alpha / 2.0, N));
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        public static StepResult RptSizeIndProp(ITemplateHost host, ParameterBag parameters)
        {
            double P1;
            double zalpha = 0; double pbar = 0;
            double ncor = 0;

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double BETA = 1.0 - power;
            double P0 = parameters["p0"].AsDouble;
            if (P0 > 1.0)
            {
                P0 = 1.0;
            }
            if (P0 < 0.0)
            {
                P0 = 0.0;
            }
            bool hasP1 = "prop".Equals(parameters["prop-or-or"].AsString);
            if (hasP1)
            {
                P1 = parameters["p1"].AsDouble;
            }
            else
            {
                double r = parameters["r"].AsDouble;
                P1 = P0 * r;
            }
            if (P1 > 1.0)
            {
                P1 = 1.0;
            }
            if (P1 < 0.0)
            {
                P1 = 0.0;
            }
            double M = parameters["m"].AsDouble;
            if (P1 != P0 && M > 0)
            {
                double N;
                try
                {
                    zalpha = zcvalue(alpha / 2.0);
                    pbar = (P1 + M * P0) / (M + 1.0);
                    double nx = Math.Pow((zalpha * Math.Sqrt((1.0 + 1.0 / M) * pbar * (1.0 - pbar)) + zcvalue(1.0 - power) * Math.Sqrt(P0 * (1.0 - P0) / M + P1 * (1.0 - P1))), 2.0) / Math.Pow((P0 - P1), 2.0);
                    N = Math.Floor(nx) + 1.0;
                    ncor = Math.Floor(N / 4.0 * Math.Pow((1.0 + Math.Sqrt(1.0 + 2.0 * (M + 1.0) / (N * M * Math.Abs(P0 - P1)))), 2.0)) + 1.0;
                }
                catch (Exception)
                {
                    N = -1.0;
                }
                //  RTF_LoadTemplate("s_ind.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("pc", host.RoundU(P0));
                outputParameters.AddOutput("ps", host.RoundU(P1));
                outputParameters.AddOutput("cpc", M.ToString());
                outputParameters.AddOutput("alpha", host.RoundU(alpha));
                outputParameters.AddOutput("power", host.RoundU(power));
                if (N == -1.0)
                {
                    outputParameters.AddOutput("case", BigErr);
                    outputParameters.AddOutput("controls", BigErr);
                    outputParameters.AddOutput("case_corr", BigErr);
                    outputParameters.AddOutput("controls_corr", BigErr);
                }
                else
                {
                    outputParameters.AddOutput("case", N.ToString());
                    outputParameters.AddOutput("controls", Math.Floor(M * N).ToString());
                    outputParameters.AddOutput("case_corr", ncor.ToString());
                    outputParameters.AddOutput("controls_corr", Math.Floor(M * ncor).ToString());
                }
                double sigmaa = Math.Sqrt(P0 * (1.0 - P0) / M + P1 * (1.0 - P1));
                double sigma0 = Math.Sqrt((1.0 + 1.0 / M) * pbar * (1.0 - pbar));
                double zbeta = zcvalue(1.0 - power);
                List<ParameterBag> assumptionsList = new List<ParameterBag>();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2.0 * (sigma0 / sigmaa) * zalpha + zbeta <= 3.1)
                {
                    assumptionsList.Add(x_disclaim(1.0 - BETA, 1.0 - BETA + alpha / 2.0, N));
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        public static StepResult RptSizeMatchCase(ITemplateHost host, ParameterBag parameters)
        {
            double sigmar = 0; double FM = 0; double N = 0;
            int fault;

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double BETA = 1.0 - power;
            double ph = parameters["ph"].AsDouble;
            double P0 = parameters["p0"].AsDouble;
            double ps = parameters["ps"].AsDouble;
            double M = parameters["m"].AsDouble;
            ssize(ref alpha, ref BETA, ref ph, ref P0, ref M, ref ps, ref N, ref FM, ref sigmar, out fault);
            //  RTF_LoadTemplate("s_macase.rtf")
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("corr", host.RoundU(ph));
            outputParameters.AddOutput("pc", host.RoundU(P0));
            outputParameters.AddOutput("odds", host.RoundU(ps));
            outputParameters.AddOutput("cpc", host.RoundU(M));
            outputParameters.AddOutput("alpha", host.RoundU(alpha));
            outputParameters.AddOutput("power", host.RoundU(power));
            List<ParameterBag> lowerList = new List<ParameterBag>();
            outputParameters.AddOutput("*lower", lowerList);
            if (BETA >= 0.8)
            {
                lowerList.Add(new ParameterBag());
            }
            if (fault == 0)
            {
                //  TODO: RTF_DeleteBlock() on the illegal piece, which is always removed in valid cases.
                outputParameters.AddOutput("size", N.ToString());
                List<ParameterBag> reductionList = new List<ParameterBag>();
                outputParameters.AddOutput("*reduction", reductionList);
                if (M > 1)
                {
                    ParameterBag reductionParameters = new ParameterBag();
                    reductionList.Add(reductionParameters);
                    reductionParameters.AddOutput("controls", M.ToString());
                    reductionParameters.AddOutput("reduction", FM.ToString());
                }
                double zalpha = zcvalue(alpha / 2.0);
                double zbeta = zcvalue(BETA);
                List<ParameterBag> assumptionsList = new List<ParameterBag>();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2.0 * sigmar * zalpha + zbeta <= 3.1)
                {
                    assumptionsList.Add(x_disclaim(1.0 - BETA, 1.0 - BETA + alpha / 2.0, N));
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        public static StepResult RptSizeMatchProp(ITemplateHost host, ParameterBag parameters)
        {
            double P1;
            double N = 0;
            const string caption = "Comparision of proportions for paired cohort study";

            double power = parameters["p"].AsDouble;
            double alpha = parameters["a"].AsDouble;
            double BETA = 1.0 - power;
            double P0 = parameters["p0"].AsDouble;
            if (P0 > 1.0)
            {
                P0 = 1.0;
            }
            if (P0 < 0.0)
            {
                P0 = 0.0;
            }
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
            if (P1 > 1.0)
            {
                P1 = 1.0;
            }
            if (P1 < 0.0)
            {
                P1 = 0.0;
            }
            if (P1 != P0 & ph > -1.0 & ph < 1.0)
            {
                //  RTF_LoadTemplate("s_maprop.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("pc", host.RoundU(P0));
                outputParameters.AddOutput("ps", host.RoundU(P1));
                outputParameters.AddOutput("r", host.RoundU(ph));
                outputParameters.AddOutput("alpha", host.RoundU(alpha));
                outputParameters.AddOutput("power", host.RoundU(power));
                double zalpha = zcvalue(alpha / 2.0);
                double zbeta = zcvalue(BETA);
                double Q1 = 1.0 - P1;
                double Q0 = 1.0 - P0;
                double p10 = P1 * Q0 - ph * Math.Sqrt(P1 * Q1 * P0 * Q0);
                double p01 = Q1 * P0 - ph * Math.Sqrt(P1 * Q1 * P0 * Q0);
                double pa = p10 / (p01 + p10);
                double qa = 1.0 - pa;
                if (pa * qa <= 0.0)
                {
                    host.Error("Calculation not possible, try a smaller value for correlation.", caption);
                    return null;
                }
                try
                {
                    N = Math.Floor(Math.Pow((zalpha * 0.5 + zbeta * Math.Sqrt(Math.Abs(pa * qa))), 2.0) / (Math.Pow((pa - 0.5), 2.0) * (p01 + p10))) + 1.0;
                    outputParameters.AddOutput("size", N.ToString());
                }
                catch (Exception)
                {
                    outputParameters.AddOutput("size", BigErr);
                }
                List<ParameterBag> assumptionsList = new List<ParameterBag>();
                outputParameters.AddOutput("*assumptions", assumptionsList);
                if (2 * (0.5 / Math.Sqrt(Math.Abs(pa * qa))) * zalpha + zbeta <= 3.1)
                {
                    assumptionsList.Add(x_disclaim(1.0 - BETA, 1.0 - BETA + alpha / 2.0, N));
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        private static double zcvalue(double alph)
        {
            int fault;
            return -PDF.gauinv(alph, out fault);
        }


        private static void ssize(ref double salpha, ref double SBeta, ref double sr, ref double sp0, ref double M, ref double xspsi, ref double N, ref double FM, ref double sigmar, out int er)
        {
            int imposs;
            double n1 = 0;
            double P1;
            double nm = 0;

            double[] t = new double[1000 + 1 /* for VB to C# conversion */];
            er = 0;
            double rm = M;
            double r = sr;
            double P0 = sp0;
            double dpsi = xspsi;
            double zalpha = zcvalue(salpha / 2.0);
            double zbeta = zcvalue(SBeta);
            if (dpsi <= 0)
            {
                er = 2;
                N = 0.0;
                return;
            }
            MathDbl.pone(P0, dpsi, r, out P1, out imposs);
            if (imposs == 1)
            {
                er = 1;
                FM = 0.0;
                N = 0.0;
                return;
            }
            double Q1 = 1.0 - P1;
            double Q0 = 1.0 - P0;
            double p01 = P0 + r * Math.Sqrt(Q1 * P0 * Q0 / P1);
            double p00 = P0 - r * Math.Sqrt(P1 * P0 * Q0 / Q1);
            double q01 = 1.0 - p01;
            double q00 = 1.0 - p00;
            int im = ((int)(Math.Floor(M)));
            do
            {
                double C1 = 1;
                double C2 = rm;
                int i;
                for (i = 1; i <= im; i++)
                {
                    t[i] = P1 * C1 * Math.Pow(p01, Convert.ToDouble(i - 1)) * Math.Pow(q01, Convert.ToDouble(im - i + 1)) + Q1 * C2 * Math.Pow(p00, Convert.ToDouble(i)) * Math.Pow(q00, Convert.ToDouble(im - i));
                    C1 = C2;
                    C2 = C2 * (rm - Convert.ToDouble(i)) / (Convert.ToDouble(i) + 1);
                }
                double E1 = 0;
                for (i = 1; i <= im; i++)
                {
                    E1 = E1 + (Convert.ToDouble(i) * t[i] / (rm + 1.0));
                }
                double v1 = 0.0;
                for (i = 1; i <= im; i++)
                {
                    v1 = v1 + (Convert.ToDouble(i) * t[i] * (rm - Convert.ToDouble(i) + 1.0) / Math.Pow((rm + 1.0), 2.0));
                }
                double epsi = 0.0;
                for (i = 1; i <= im; i++)
                {
                    epsi = epsi + (Convert.ToDouble(i) * t[i] * dpsi / (Convert.ToDouble(i) * dpsi + rm - Convert.ToDouble(i) + 1.0));
                }
                double vpsi = 0;
                for (i = 1; i <= im; i++)
                {
                    vpsi = vpsi + (Convert.ToDouble(i) * t[i] * dpsi * (rm - Convert.ToDouble(i) + 1.0) / Math.Pow((Convert.ToDouble(i) * dpsi + rm - Convert.ToDouble(i) + 1), 2.0));
                }
                double S1 = Math.Sqrt(v1);
                double spsi = Math.Sqrt(vpsi);
                sigmar = S1 / spsi;
                if (rm > 1.0)
                {
                    nm = Math.Pow((zbeta * spsi + zalpha * S1), 2.0) / Math.Pow((epsi - E1), 2.0);
                    N = Math.Floor(nm) + 1.0;
                    rm = 1.0;
                    im = 1;
                }
                else if (rm == 1.0)
                {
                    n1 = Math.Pow((zbeta * spsi + zalpha * S1), 2.0) / Math.Pow((epsi - E1), 2.0);
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
                else
                {
                    break; /* TRANSWARNING: check that break is in correct scope */
                }
            }
            while (true);
            if (M > 1)
            {
                FM = Convert.ToDouble(Convert.ToInt64(nm)) / Convert.ToDouble(Convert.ToInt64(n1));
            }
            else
            {
                N = Math.Floor(n1) + 1.0;
                FM = 1.0;
            }
        }


        public static StepResult RptSizePaired(ITemplateHost host, ParameterBag parameters)
        {
            double N;
            double xn = 0;
            int flt;
            const string caption = "Comparision of means for paired or single sample t test";

            double P = parameters["p"].AsDouble;
            double a = parameters["a"].AsDouble;
            double D = parameters["d"].AsDouble;
            double sd = parameters["sd"].AsDouble;
            if (P >= 1.0 || P < 0.000001)
            {
                P = 0.8;
            }
            if (a >= 1.0 || P < 0.000001)
            {
                P = 0.05;
            }
            double k = D / sd;
            bool OK = true;
            const double omega = 0.0001;
            if (Math.Abs(k) < omega)
            {
                k = omega;
                OK = false;
            }
            double b = 1.0 - P;
            double M = 1.0;
            x_tsample(a, b, k, M, 1, ref xn, out flt);
            if (xn < Convert.ToDouble(Int32.MaxValue))
            {
                N = Math.Floor(xn) + 1.0;
            }
            else
            {
                N = Int32.MaxValue;
                OK = false;
            }
            if (flt == 0 || flt == 2)
            {
                //  RTF_LoadTemplate("s_t.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("tt", "a paired or single sample");
                x_tres(host, outputParameters, false, a, b, P, M, N, D, sd);
                if (!(OK))
                {
                    host.Error("Estimate of sample size is greater than StatsDirect can display: a very large but lower value is given", caption);
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        public static StepResult RptSizePopSurvey(ITemplateHost host, ParameterBag parameters)
        {
            // double af = 2; 
            double ps = parameters["ps"].AsDouble;
            double P = parameters["p"].AsDouble;
            double xd = parameters["xd"].AsDouble;
            double cco = parameters["cco"].AsDouble;
            if (cco <= 0.0 | cco >= 1.0)
            {
                cco = 0.95;
            }
            int fault;
            double cit = PDF.gauinv(cco + (1.0 - cco) / 2.0, out fault);
            if (fault == 0)
            {
                double xza = cit;
                P = P / 100.0;
                xd = xd / 100.0;
                if (xd <= 0.0 | ps <= 0.0)
                {
                    throw new InvalidDataException();
                }
                double sn = xza * xza * P * (1.0 - P) / (xd * xd);
                sn = sn / (1.0 + sn / ps);
                //  If RTF_LoadTemplate("s_survey.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("estimate", host.RoundU(ps));
                outputParameters.AddOutput("rate", host.RoundU(P * 100));
                outputParameters.AddOutput("deviation", host.RoundU(xd * 100));
                outputParameters.AddOutput("level", host.RoundU(100 * cco));
                outputParameters.AddOutput("size", (Math.Floor(sn) + 1).ToString());
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            return null;
        }


        public static StepResult RptSizeUnPaired(ITemplateHost host, ParameterBag parameters)
        {
            double N; double xn = 0;
            int fault;
            const string caption = "Comparision of means for unpaired two sample t test";

            double P = parameters["p"].AsDouble;
            double a = parameters["a"].AsDouble;
            double D = parameters["d"].AsDouble;
            double sd = parameters["sd"].AsDouble;
            double M = parameters["m"].AsDouble;
            if (P >= 1 || P < 0.000001)
            {
                P = 0.8;
            }
            if (a >= 1 || P < 0.000001)
            {
                P = 0.05;
            }
            double k = D / sd;
            bool ok = true;
            const double omega = 0.0001;
            if (Math.Abs(k) < omega)
            {
                k = omega;
                ok = false;
            }
            double b = 1.0 - P;
            if (M <= 0)
            {
                M = 1;
            }
            x_tsample(a, b, k, M, 2, ref xn, out fault);
            if (xn < Convert.ToDouble(Int32.MaxValue))
            {
                N = Math.Floor(xn) + 1L;
            }
            else
            {
                N = Int32.MaxValue;
                ok = false;
            }
            if (fault == 0 | fault == 2)
            {
                //  RTF_LoadTemplate("s_t.rtf")
                ParameterBag outputParameters = new ParameterBag();
                outputParameters.AddOutput("tt", "an unpaired two sample");
                x_tres(host, outputParameters, true, a, b, P, M, N, D, sd);
                if (!(ok))
                {
                    host.Error("Estimate of sample size is greater than StatsDirect can display: a very large but lower value is given", caption);
                }
                return new StepResult(StepSuccess.Success, outputParameters);
            }
            throw new InvalidDataException();
        }


        private static ParameterBag x_disclaim(double ll, double ul, double N)
        {

            //  RTF_UseBlock()
            ParameterBag outputParameters = new ParameterBag();
            outputParameters.AddOutput("cases", N.ToString());
            outputParameters.AddOutput("no_less", ll.ToString());
            outputParameters.AddOutput("no_greater", ul.ToString());
            return outputParameters;
        }


        private static void x_tres(ITemplateHost Host, ParameterBag outputParameters, bool unpaired, double a, double b, double P, double M, double N, double D, double sd)
        {
            int ierr = 0;

            outputParameters.AddOutput("alpha", Host.RoundU(a));
            outputParameters.AddOutput("power", Host.RoundU(P));
            outputParameters.AddOutput("mean", unpaired ? "between means" : "of mean from zero");
            outputParameters.AddOutput("delta", Host.RoundU(D));
            outputParameters.AddOutput("sd", Host.RoundU(sd));
            double df = N - 1.0;
            List<ParameterBag> controlsList = new List<ParameterBag>();
            outputParameters.AddOutput("*controls", controlsList);
            if (unpaired)
            {
                ParameterBag controlsParameters = new ParameterBag();
                controlsList.Add(controlsParameters);
                controlsParameters.AddOutput("con_per", M.ToString());
                df = N * (M + 1) - 2.0;
            }
            outputParameters.AddOutput("size", N.ToString());

            List<ParameterBag> pairsList = new List<ParameterBag>();
            outputParameters.AddOutput("*pairs", pairsList);
            List<ParameterBag> subjectsList = new List<ParameterBag>();
            outputParameters.AddOutput("*subjects", subjectsList);
            if (unpaired)
            {
                ParameterBag subjectsParameters = new ParameterBag();
                subjectsList.Add(subjectsParameters);
                subjectsParameters.AddOutput("con_tot", Math.Floor(M * N).ToString());
            }
            else
            {
                pairsList.Add(new ParameterBag());
            }
            outputParameters.AddOutput("df", df.ToString());
            double ta = PDF.tfromp(a / 2.0, df);
            double tb = PDF.tfromp(b / 2.0, df);
            List<ParameterBag> assumptionsList = new List<ParameterBag>();
            outputParameters.AddOutput("*assumptions", assumptionsList);
            if (ierr == 0 & 2.0 * ta + tb <= 3.1)
            {
                assumptionsList.Add(x_disclaim(1.0 - b, 1.0 - b + a / 2.0, N));
            }
        }
    }
}
