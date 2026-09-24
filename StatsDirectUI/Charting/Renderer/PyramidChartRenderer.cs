using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using System;
using System.Drawing;

namespace StatsDirect.Charting.Renderer
{
    internal class PyramidChartRenderer : AbstractChartRenderer, IChartRenderer
    {
        public PyramidChartRenderer(ChartDefinition definition, ICanvasFactory canvasFactory)
            : base(definition, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            PyramidOptions pOptions = (PyramidOptions)Definition.ChartOptions;

            DataFrame maleFrame = pOptions.MaleFrame;
            DoubleVariable males = (DoubleVariable)maleFrame.Variables[0];
            double maxmale = males.Max;

            double maxfemale;
            if (pOptions.FemaleFrame != null)
            {
                //  Separate male and female values
                DataFrame femaleFrame = pOptions.FemaleFrame;
                DoubleVariable females = (DoubleVariable)femaleFrame.Variables[0];
                maxfemale = females.Max;
            }
            else
            {
                //  Combined male/female values - assume an even split
                maxmale /= 2.0;
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

        ParameterBag IChartRenderer.Plot(/* TODO: IPreferences*/ ITemplateHost _, bool isForReturnedParametersOnly)
        {
            if (isForReturnedParametersOnly)
                return new ParameterBag();

            PyramidOptions pOptions = (PyramidOptions)Definition.ChartOptions;

            DataFrame maleFrame = pOptions.MaleFrame;
            DoubleVariable males = (DoubleVariable)maleFrame.Variables[0];
            int nmale = males.Length;
            double maxmale = males.Max;

            //  A row with a missing count is left out; row[] keeps each drawn row's position in the data so that its label goes with it.
            int nfemale = 0;
            double[] female;
            double[] male;
            int[] row = new int[nmale];
            double maxfemale = 0;
            PyramidMode mode;
            if (pOptions.FemaleFrame != null)
            {
                //  Separate male and female values
                DataFrame femaleFrame = pOptions.FemaleFrame;
                DoubleVariable females = (DoubleVariable)femaleFrame.Variables[0];
                female = new double[nmale];
                male = new double[nmale];
                maxfemale = females.Max;

                for (int r = 0; r < nmale; r++)
                {
                    if (females.Data[r] != Constant.MISSING && males.Data[r] != Constant.MISSING)
                    {
                        female[nfemale] = females.Data[r];
                        male[nfemale] = males.Data[r];
                        row[nfemale] = r;
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
                for (int r = 0; r < nmale; r++)
                {
                    if (males.Data[r] != Constant.MISSING)
                    {
                        female[nfemale] = males.Data[r] / 2.0;
                        male[nfemale] = males.Data[r] / 2.0;
                        row[nfemale] = r;
                        nfemale += 1;
                    }
                }
                nmale = nfemale;
                mode = PyramidMode.Totals;
            }

            //  The label of each drawn row: none when the labels were skipped, "group n" (n the row's position in the data) for a blank label.
            string[] title = new string[nmale];
            Array.Fill(title, "");
            if (pOptions.LabelFrame != null)
            {
                StringVariable labels = (StringVariable)pOptions.LabelFrame.Variables[0];
                for (int i = 0; i < nmale; i++)
                    title[i] = MakeTitle(row[i] < labels.Length ? labels.Data[row[i]] : null, "group " + (row[i] + 1));
            }

            double tmax = maxfemale > maxmale ? maxfemale : maxmale;
            double tmx = pOptions.ScaleMaximum;

            double scaleMax = tmx;
            if (scaleMax < tmax)
                scaleMax = tmax;
            if (scaleMax <= 0)
                scaleMax = 1;   // counts that are all zero: an empty pyramid rather than a division by zero

            BrushDescriptor maleBrush = null;
            if (pOptions.MarkerTypes.Count >= 1)
                maleBrush = MarkerTypeToBrush(pOptions.MarkerTypes[0]);
            BrushDescriptor femaleBrush = null;
            if (pOptions.MarkerTypes.Count >= 2)
                femaleBrush = MarkerTypeToBrush(pOptions.MarkerTypes[1]);

            ScaleHeight(nmale);

            StartVectorPlot(pOptions);

            double xtra = 0;
            for (int i = 0; i < nmale; i++)
            {
                double w = AxisLabelWidthInCanvasCoordinates(title[i]);
                if (w > xtra)
                    xtra = w;
            }

            DefaultAxes(null, new Size((int)Math.Ceiling(xtra), 0));

            DrawTitle(pOptions.Title);

            double ystep = nmale > 0 ? YExtCanvas / nmale : 0;   // no rows (every count missing): nothing to draw but the frame
            if (pOptions.LabelFrame != null)
            {
                for (int i = 0; i < nmale; i++)
                {
                    double yc = YAxisCanvas + (nmale - i) * ystep - ystep / 2;
                    AxisDrawStringAtAngleRM(title[i], XAxisCanvas - 15, yc, LabelDirection.Across);
                }
            }

            double xstep = XExtCanvas / 2;
            double xc = XAxisCanvas + xstep;
            PenDescriptor blackPen = GetMarkerPen(ChartPreferences.MarkerTypes[10]);
            for (int i = 0; i < nmale; i++)
            {
                double yt = YAxisCanvas + (nmale - i) * ystep;
                double yb = YAxisCanvas + (nmale - i - 1) * ystep;
                double xl = XAxisCanvas + xstep - male[i] / scaleMax * xstep;
                double xr = XAxisCanvas + xstep + female[i] / scaleMax * xstep;
                if (mode == PyramidMode.Pairs)
                {
                    //  Male/female
                    DrawRectangleInCanvasCoordinates(blackPen, maleBrush, xl, yt, xc - xl, yt - yb);
                    DrawRectangleInCanvasCoordinates(blackPen, femaleBrush, xc, yt, xr - xc, yt - yb);
                }
                else
                {
                    //  Just the one
                    DrawRectangleInCanvasCoordinates(blackPen, maleBrush, xl, yt, xr - xl, yt - yb);
                }
            }

            if (mode == PyramidMode.Pairs)
            {
                DrawLineInCanvasCoordinates(blackPen, xc, YAxisCanvas, XAxisCanvas + xstep, YAxisCanvas + nmale * ystep);
                AxisDrawStringAtAngleCT("male", XExtCanvas / 4 + XAxisCanvas, YAxisCanvas - 12, LabelDirection.Across);
                AxisDrawStringAtAngleCT("female", XExtCanvas / 4 + XExtCanvas / 2 + XAxisCanvas, YAxisCanvas - 12, LabelDirection.Across);
            }

            AxisDrawStringAtAngleLT("Scale maximum = " + scaleMax, 40, YAxisCanvas - 40, LabelDirection.Across);

            EndVectorPlot();
            return new ParameterBag();
        }
    }
}
