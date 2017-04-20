using System;
using Layout;
using StatsDirect.Builtins;
using StatsDirect.Numerics;
using StatsDirect.Templates;

namespace StatsDirect.Charting.Renderer
{
    class NormalChartRenderer: AbstractChartRenderer, IChartRenderer
    {
        public NormalChartRenderer(ChartDefinition cd, ICanvasFactory canvasFactory)
            : base (cd, canvasFactory)
        {
        }

        ScaleParameters IChartRenderer.GetScaleParameters()
        {
            NormalOptions nOptions = (NormalOptions)Definition.ChartOptions;
            NormalOptions.ScoreMethod method = nOptions.Method;

            DoubleSeries xs0 = Definition.XSeries[0].AsDoubleSeries;
            int rows = xs0.Points;

            double[] x = new double[rows];
            ExFortran.Rank(xs0.Data, x, 0, rows, 0, out double xf);

            if (method == NormalOptions.ScoreMethod.ExpectedNormalOrder)
                if (rows > 4000)
                    method = NormalOptions.ScoreMethod.VanDerWaerden;

            int nn = xs0.Points;
            for (int j = 0; j < rows; j++)
            {
                int ifault;
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        //  van der Waerden, Conover P 396
                        x[j] = PDF.gauinv(x[j] / (Convert.ToDouble(nn) + 1.0), out ifault);
                        if (ifault != 0)
                            x[j] = Constant.MISSING;
                        break;
                    case NormalOptions.ScoreMethod.Blom:
                        //  Blom - Altman p143
                        x[j] = PDF.gauinv(x[j] / (Convert.ToDouble(nn) + 1.0), out ifault);
                        if (ifault != 0)
                            x[j] = Constant.MISSING;
                        break;
                    default:
                        //  expected normal order
                        x[j] = PDF.expnos(Convert.ToInt32(x[j]), nn);
                        break;
                }
            }

            Range xRange = GetMinMaxArray(x, ScaleType.Linear);
            Range yRange = GetMinMaxArray(xs0.Data, ScaleType.Linear);
            return new ScaleParameters
            {
                X = { AllowedScaleTypes = new[] { ScaleType.Linear }, ScaleType = ScaleType.Linear, Min = xRange.Min, Max = xRange.Max },
                Y = { AllowedScaleTypes = new[] { ScaleType.Linear }, ScaleType = ScaleType.Linear, Min = yRange.Min, Max = yRange.Max }
            };
        }


        ///  <summary>
        ///  Plot normal scores for a single variable in XSeries.
        ///  </summary>
        ///  <returns></returns>
        ///  <remarks></remarks>
        ParameterBag IChartRenderer.Plot(ITemplateHost host)
        {
            return PlotNormal(Definition.XSeries[0].AsDoubleSeries.Data);
        }

        ///  <summary>
        ///  Plot normal scores for a single variable in XSeries.
        ///  </summary>
        ///  <remarks></remarks>
        internal ParameterBag PlotNormal(double[] y)
        {
            NormalOptions nOptions = (NormalOptions)Definition.ChartOptions;
            NormalOptions.ScoreMethod method = nOptions.Method;
            bool shouldScaleZ = nOptions.ShouldScaleZ;

            int rows = y.Length;

            double sy = 0;
            for (int j = 0; j < rows; j++)
                sy += y[j];
            double ybar = sy / rows;
            double ssy = 0;
            for (int j = 0; j < rows; j++)
            {
                double d = y[j] - ybar;
                ssy += d * d;
            }
            double vary = ssy / rows;
            double sdy = Math.Sqrt(vary);

            double[] x = new double[rows];
            ExFortran.Rank(y, x, 0, rows, 0, out double scrap);

            if (method == NormalOptions.ScoreMethod.ExpectedNormalOrder)
                if (rows > 4000)
                    method = NormalOptions.ScoreMethod.VanDerWaerden;

            // Set label
            string lab;
            if (shouldScaleZ)
                lab = "Normal (" + Definition.XSeries[0].Title + ")";
            else
            {
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        lab = "Normal scores (van der Waerden)";
                        break;
                    case NormalOptions.ScoreMethod.Blom:
                        lab = "Normal scores (Blom)";
                        break;
                    default:
                        lab = "Expected normal order scores";
                        break;
                }
            }

            int nn = y.Length;
            for (int j = 0; j < rows; j++)
            {
                switch (method)
                {
                    case NormalOptions.ScoreMethod.VanDerWaerden:
                        {
                            //  van der Waerden, Conover P 396
                            x[j] = PDF.gauinv(x[j] / (nn + 1.0), out int ifault);
                            if (ifault != 0)
                                x[j] = Constant.MISSING;
                            break;
                        }
                    case NormalOptions.ScoreMethod.Blom:
                        {
                            //  Blom - Altman p143
                            x[j] = PDF.gauinv(x[j] / (nn + 1.0), out int ifault);
                            if (ifault != 0)
                                x[j] = Constant.MISSING;
                            break;
                        }
                    default:
                        //  expected normal order
                        x[j] = PDF.expnos(Convert.ToInt32(x[j]), nn);
                        break;
                }
                if (shouldScaleZ && x[j] != Constant.MISSING)
                    x[j] = x[j] * sdy + ybar;
            }

            StartVectorPlot();
            SetFontsAndThicknessesFromOptions(nOptions);
            AssignMarkersToSeries(Definition.XSeries, nOptions);

            DataMinMax Select_MinMaxY = DataMinMax.XCalc_YCalc;
            if (shouldScaleZ)
                Select_MinMaxY = DataMinMax.XY_CalcTogether;
            MarkerType mt = ChartPreferences.MarkerTypes[0];
            if (null != nOptions && null != nOptions.MarkerTypes && nOptions.MarkerTypes.Count >= 1)
                mt = nOptions.MarkerTypes[0];
            PlotXYInternal(x, y, lab, "Observed (" + Definition.XSeries[0].Title + ")", nOptions.Title, false, Select_MinMaxY, mt.MarkerSize, mt.MarkerShape, mt.IsMarkerFilled, GetMarkerPen(mt), true);
            if (shouldScaleZ)
                DrawLineInCanvasCoordinates(AxisPen, XAxisCanvas, YAxisCanvas, XAxisCanvas + XExtCanvas, YAxisCanvas + YExtCanvas);
            EndVectorPlot();

            // Regression results
            SimpleLinearRegressionContext context = new SimpleLinearRegressionContext(x, y);
            context.CalculateLeastSquaresMethod();
            return new ParameterBag("context", new FilledParameter(FilledParameterDirection.Output, context));
        }
    }
}
