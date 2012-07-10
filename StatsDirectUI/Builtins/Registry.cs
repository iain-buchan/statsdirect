using System.Collections.Generic;
using StatsDirect.Templates;

namespace StatsDirect.Builtins
{
    public class Registry
    {
        /// <summary>
        /// Returns a mapping of function names to their bodies.
        /// This is allowed to be slow as callers will cache it.
        /// </summary>
        public static ICollection<Builtin> GetFunctionRegistry()
        {
            List<Builtin> functionRegistry = new List<Builtin>
                                                 {
                                                     new Builtin("fileExportWorksheet", ImportExport.FileExportWorksheet,
                                                                 InputDuringStep.Never),
                                                     new Builtin("fileImportWorksheet", ImportExport.FileImportWorksheet,
                                                                 InputDuringStep.Never),
                                                     new Builtin("setAnalysisOptions", Options.SetAnalysisOptions,
                                                                 InputDuringStep.Always),
                                                     new Builtin("showGraphicsOptions", Options.ShowGraphicsOptions,
                                                                 InputDuringStep.Always),
                                                     new Builtin("shtClearMissing", Sheet.ShtClearMissing,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtCombine", Sheet.ShtCombine, InputDuringStep.Never),
                                                     new Builtin("shtDates", Sheet.ShtDates, InputDuringStep.Never),
                                                     new Builtin("shtDummyVariables", Sheet.ShtDummyVariables,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtFillSeries", Sheet.ShtFillSeries,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtGroupCategorise", Sheet.ShtGroupCategorise,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtGroupExtract", Sheet.ShtGroupExtract,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtGroupSplit", Sheet.shtGroupSplit,
                                                                 InputDuringStep.Always),
                                                     new Builtin("shtLadderPowers", Sheet.ShtLadderPowers,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtNormal", Sheet.ShtNormal, InputDuringStep.Never),
                                                     new Builtin("shtPairDifferences", Sheet.ShtPairDifferences,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtPairMeans", Sheet.ShtPairMeans, InputDuringStep.Never),
                                                     new Builtin("shtPairSlopes", Sheet.ShtPairSlopes,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtRank", Sheet.ShtRank, InputDuringStep.Never),
                                                     new Builtin("shtRotate", Sheet.shtRotate, InputDuringStep.Never),
                                                     new Builtin("shtSort", Sheet.ShtSort, InputDuringStep.Never),
                                                     new Builtin("shtSortByExpression", Sheet.ShtSortByExpression, InputDuringStep.Never),
                                                     new Builtin("shtSortInPlace", Sheet.ShtSortInPlace,
                                                                 InputDuringStep.Always),
                                                     new Builtin("shtStandardize", Sheet.ShtStandardize,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformAngular", Sheet.ShtTransformAngular,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformCumulate", Sheet.ShtTransformCumulate,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformECDF", Sheet.ShtTransformECDF,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformLog", Sheet.ShtTransformLog,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformLog10", Sheet.ShtTransformLog10,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformLogit", Sheet.ShtTransformLogit,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformProbit", Sheet.ShtTransformProbit,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformZECDF", Sheet.ShtTransformZecdf,
                                                                 InputDuringStep.Never),
                                                     new Builtin("shtTransformZSD", Sheet.ShtTransformZsd,
                                                                 InputDuringStep.Never)
                                                 };
            return functionRegistry;
        }
    }
}
