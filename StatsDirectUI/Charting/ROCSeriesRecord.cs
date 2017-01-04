namespace StatsDirect.Charting
{
    ///  <summary>
    ///  A way of carrying ROC series values around the system.
    ///  </summary>
    public class ROCSeriesRecord
    {
        public double[] pdata;
        public double[] adata;
        public double[] tdata;
        public double pmn;
        public double amn;
        public double min;
        public double max;
        public int a;
        public int b;
        public int c;
        public int d;
        public double cutoff;
        public double sens;
        public double spec;
        public double auc;
        public ComparisonValue comp;

        public void ReCut()
        {
            a = 0;
            b = 0;
            c = 0;
            d = 0;
            for (int j = 0; j < pdata.Length; j++)
            {
                switch (comp)
                {
                    case ComparisonValue.LT:
                        if (pdata[j] < cutoff)
                            a += 1;
                        break;
                    case ComparisonValue.LE:
                        if (pdata[j] <= cutoff)
                            a += 1;
                        break;
                    case ComparisonValue.GT:
                        if (pdata[j] > cutoff)
                            a += 1;
                        break;
                    default:
                        if (pdata[j] >= cutoff)
                            a += 1;
                        break;
                }
            }
            c = pdata.Length - a;
            for (int j = 0; j < adata.Length; j++)
            {
                switch (comp)
                {
                    case ComparisonValue.LT:
                        if (adata[j] < cutoff)
                            b += 1;
                        break;
                    case ComparisonValue.LE:
                        if (adata[j] <= cutoff)
                            b += 1;
                        break;
                    case ComparisonValue.GT:
                        if (adata[j] > cutoff)
                            b += 1;
                        break;
                    default:
                        if (adata[j] >= cutoff)
                            b += 1;
                        break;
                }

            }
            d = adata.Length - b;
            sens = a / (double)(a + c);
            spec = d / (double)(b + d);
        }


        ///  <summary>
        ///  Copy the values so that an original record can be changed.
        ///  The arrays never change, so a shallow copy is sufficient.
        ///  </summary>
        ///  <returns>The copied object</returns>
        public ROCSeriesRecord Clone()
        {
            return new ROCSeriesRecord
            {
                a = a,
                b = b,
                c = c,
                d = d,
                pdata = pdata,
                adata = adata,
                tdata = tdata,
                pmn = pmn,
                amn = amn,
                min = min,
                max = max,
                cutoff = cutoff,
                spec = spec,
                sens = sens,
                auc = auc,
                comp = comp
            };
        }
    }
}
