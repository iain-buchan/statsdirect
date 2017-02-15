using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;
using System.Drawing;

namespace StatsDirect.Charting
{
    class PyramidChartRenderer: AbstractChartRenderer
    {
        public PyramidChartRenderer(ChartDefinition definition)
            : base(definition)
        {
        }

        public override ScaleParameters GetScaleParameters()
        {
            PyramidOptions pOptions = ((PyramidOptions)(definition.ChartOptions));

            DataFrame maleFrame = pOptions.MaleFrame;
            DoubleVariable males = maleFrame.Variables[0] as DoubleVariable;
            double maxmale = males.Max;

            double maxfemale;
            if (pOptions.FemaleFrame != null)
            {
                //  Separate male and female values
                DataFrame femaleFrame = pOptions.FemaleFrame;
                DoubleVariable females = femaleFrame.Variables[0] as DoubleVariable;
                maxfemale = females.Max;
            }
            else
            {
                //  Combined male/female values - assume an even split
                maxmale = maxmale / 2.0;
                maxfemale = maxmale;
            }

            double tmax = maxfemale > maxmale ? maxfemale : maxmale;

            return new ScaleParameters
            {
                X =
                {
                    AllowedScaleTypes = new[] { ScaleType.Linear },
                    Max = tmax,
                    Min = 0
                },
                Y =
                {
                    AllowedScaleTypes = new[] { ScaleType.Category },
                    Max = 0,
                    Min = 0
                }
            };
        }

        private enum PyramidMode
        {
            Totals,
            Pairs
        }

        public override ParameterBag Plot(ITemplateHost host)
        {
            const int MINIMUM_X_WHITESPACE = 30;

            PyramidOptions pOptions = ((PyramidOptions)(definition.ChartOptions));

            DataFrame maleFrame = pOptions.MaleFrame;
            DoubleVariable males = maleFrame.Variables[0] as DoubleVariable;
            int nmale = males.Length;
            double maxmale = males.Max;

            int nfemale = 0;
            double[] female;
            double[] male;
            double maxfemale = 0;
            PyramidMode mode;
            if (pOptions.FemaleFrame != null)
            {
                //  Separate male and female values
                DataFrame femaleFrame = pOptions.FemaleFrame;
                DoubleVariable females = femaleFrame.Variables[0] as DoubleVariable;
                female = new double[nmale];
                male = new double[nmale];
                maxfemale = females.Max;

                for (int r = 0; r < nmale; r++)
                {
                    if (females.Data[r] != Constant.MISSING && males.Data[r] != Constant.MISSING)
                    {
                        female[nfemale] = females.Data[r];
                        male[nfemale] = males.Data[r];
                        nfemale += 1;
                    }
                }
                nmale = nfemale;
                mode = PyramidMode.Pairs;
            }
            else
            {
                //  Combined male/female values - assume an even split
                female = new double[nmale];
                male = new double[nmale];
                for (int r = 0; r <= nmale - 1; r++)
                {
                    if (males.Data[r] != Constant.MISSING)
                    {
                        female[nfemale] = males.Data[r] / 2.0;
                        male[nfemale] = males.Data[r] / 2.0;
                        nfemale += 1;
                    }
                }
                nmale = nfemale;
                mode = PyramidMode.Totals;
            }

            string[] title = new string[nmale + 1];
            if (pOptions.LabelFrame != null)
            {
                StringVariable labels = pOptions.LabelFrame.Variables[0] as StringVariable;
                int i;
                for (i = labels.Length - 1; i >= 0; i--)
                {
                    if ((labels.Data[i] != null) && labels.Data[i].Length > 0)
                        break;
                }
                int lastrow = i;
                if (lastrow == nmale - 1)
                {
                    for (i = 0; i <= lastrow; i++)
                        title[i] = MakeTitle(labels.Data[i], "group " + (i + 1));
                }
            }

            double tmax = maxfemale > maxmale ? maxfemale : maxmale;
            double tmx = pOptions.ScaleMaximum;

            double ScaleMax = tmx;
            if (ScaleMax < tmax)
                ScaleMax = tmax;

            Brush maleBrush = null;
            if (pOptions.MarkerTypes.Count >= 1)
                maleBrush = MarkerTypeToBrush(pOptions.MarkerTypes[0]);
            Brush femaleBrush = null;
            if (pOptions.MarkerTypes.Count >= 2)
                femaleBrush = MarkerTypeToBrush(pOptions.MarkerTypes[1]);

            if (nmale > 10)
            {
                double scaleYAxis = 1 + (nmale - 10) / 20.0;
                if (scaleYAxis > 5)
                    scaleYAxis = 5;
                imageHeight = (int)Math.Ceiling(scaleYAxis * DEFAULT_METAFILE_HEIGHT);
            }

            StartVectorPlot();

            SetFontsAndThicknessesFromOptions(pOptions);

            double xtra = 0;
            for (int i = 0; i < nmale; i++)
            {
                double w = AxisLabelWidthInCanvasCoordinates(title[i]) + MINIMUM_X_WHITESPACE;
                if (w > xtra + xAxisCanvas)
                    xtra = w - xAxisCanvas - 5;
            }

            xAxisCanvas = xAxisCanvas + xtra;
            xExtCanvas = xExtCanvas - xtra;

            DrawTitle(pOptions.Title);

            using (StringFormat rightFormat = new StringFormat())
            {
                rightFormat.Alignment = StringAlignment.Far;
                double ystep = yExtCanvas / nmale;
                if (title[0].Length > 0)
                {
                    double txh = AxisLabelHeightInCanvasCoordinates(title[0]);
                    for (int i = 0; i < nmale; i++)
                    {
                        double yc = yAxisCanvas + (nmale - i) * ystep - ystep / 2;
                        DrawStringInCanvasCoordinates(title[i], axisLabelFont, Brushes.Black, xAxisCanvas - 15, yc + txh / 2, rightFormat);
                    }
                }

                double xstep = xExtCanvas / 2;
                double xc = xAxisCanvas + xstep;
                using (Pen blackPen = GetMarkerPen(MarkerTypes[10]))
                {
                    for (int i = 0; i < nmale; i++)
                    {
                        double yt = yAxisCanvas + (nmale - i) * ystep;
                        double yb = yAxisCanvas + (nmale - i - 1) * ystep;
                        double xl = xAxisCanvas + xstep - (male[i] / ScaleMax) * xstep;
                        double xr = xAxisCanvas + xstep + (female[i] / ScaleMax) * xstep;
                        if (mode == PyramidMode.Pairs)
                        {
                            //  Male/female
                            if (maleBrush != null)
                                FillRectangleInCanvasCoordinates(maleBrush, xl, yt, xc - xl, yt - yb);
                            if (femaleBrush != null)
                                FillRectangleInCanvasCoordinates(femaleBrush, xc, yt, xr - xc, yt - yb);
                        }
                        else
                        {
                            //  Just the one
                            if (maleBrush != null)
                                FillRectangleInCanvasCoordinates(maleBrush, xl, yt, xr - xl, yt - yb);
                        }
                        DrawRectangleInCanvasCoordinates(blackPen, xl, yt, xr - xl, yt - yb);
                    }
                    if (maleBrush != null)
                        maleBrush.Dispose();
                    if (femaleBrush != null)
                        femaleBrush.Dispose();

                    using (StringFormat leftFormat = new StringFormat())
                    {
                        leftFormat.Alignment = StringAlignment.Near;

                        if (mode == PyramidMode.Pairs)
                        {
                            DrawLineInCanvasCoordinates(blackPen, xc, yAxisCanvas, xAxisCanvas + xstep, yAxisCanvas + nmale * ystep);
                            DrawStringInCanvasCoordinates("male", axisLabelFont, Brushes.Black, (xExtCanvas / 4) + xAxisCanvas, yAxisCanvas - 12, leftFormat);
                            DrawStringInCanvasCoordinates("female", axisLabelFont, Brushes.Black, (xExtCanvas / 4) + (xExtCanvas / 2) + xAxisCanvas, yAxisCanvas - 12, leftFormat);
                        }

                        DrawStringInCanvasCoordinates("Scale maximum = " + ScaleMax, axisLabelFont, Brushes.Black, 40, yAxisCanvas - 40, leftFormat);

                        EndVectorPlot();
                        return new ParameterBag();
                    }
                }
            }
        }
    }
}
