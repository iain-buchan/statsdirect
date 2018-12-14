namespace StatsDirect.Charting
{
    public interface IChartOptionVisitor
    {
        void Visit(AgreementOptions options);
        void Visit(BiasMAOptions options);
        void Visit(BarOptions options);
        void Visit(BoxWhiskerOptions options);
        void Visit(ControlOptions options);
        void Visit(CorrelationOptions options);
        void Visit(CoxSurvivalOrHazardOptions options);
        void Visit(ErrorBarOptions options);
        void Visit(ForestOptions options);
        void Visit(GiniOptions options);
        void Visit(HistogramOptions options);
        void Visit(LadderOptions options);
        void Visit(LinearRegressionOptions options);
        void Visit(MHOptions options);
        void Visit(NormalOptions options);
        void Visit(PyramidOptions options);
        void Visit(ROCOptions options);
        void Visit(ScatterXYOptions options);
        void Visit(SpreadOptions options);
        void Visit(SurvivalOptions options);
        void Visit(TiesOptions options);
    }
}
