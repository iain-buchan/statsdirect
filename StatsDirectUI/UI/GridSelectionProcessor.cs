using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// Handles higher-level data selection functions than the grid's GetCellArray.
    /// </summary>
    class GridSelectionProcessor
    {
        private IGrid grid;

        public GridSelectionProcessor(IGrid g)
        {
            grid = g;
        }

        internal ParameterBag FillGridParameter(Parameter parameter, ITemplateProcessor processor, ITemplateHost host, ParameterBag parameters)
        {
            GridParameter gridParameter = (GridParameter)parameter;
            if (gridParameter.ShouldClearSelectionFirst)
                ClearSelection();

            while (true)
            {
                bool userCancelled;
                bool wasPivoted;
                DataFrame frame;
                if (gridParameter.ColumnsAreSameLength || gridParameter.HasLength || null != gridParameter.SameLengthAsParameter)
                {
                    int requiredLength = 0;
                    if (gridParameter.HasLength)
                        requiredLength = gridParameter.Length(processor, parameters);
                    else if (null != gridParameter.SameLengthAsParameter)
                    {
                        if (parameters.ContainsKey(gridParameter.SameLengthAsParameter) && null != parameters[gridParameter.SameLengthAsParameter])
                        {
                            FilledParameter fp = parameters[gridParameter.SameLengthAsParameter];
                            if (fp.HasData && fp.IsDataFrame)
                            {
                                DataFrame f = fp.AsDataFrame;
                                requiredLength = f.MaxRows;
                            }
                        }
                    }
                    frame = GetCellEqual(requiredLength,
                        gridParameter.DataAcquisitionMode,
                        gridParameter.GroupIdentifierMode,
                        gridParameter.MinimumColumns(processor, parameters),
                        gridParameter.MaximumColumns(processor, parameters),
                        gridParameter.Prompt(processor, parameters),
                        gridParameter.CancelSkipsParameter,
                        gridParameter.ShouldAskForGroupId,
                        DataAcquisitionWidth.RespectPivotSetting,
                        false,
                        out userCancelled,
                        out wasPivoted,
                        processor.NextOriginGroup());
                }
                else
                {
                    int minimumColumns = gridParameter.MinimumColumns(processor, parameters);
                    int maximumColumns = gridParameter.MaximumColumns(processor, parameters);
                    string selectionMessage = gridParameter.Prompt(processor, parameters);
                    while (true)
                    {
                        if (gridParameter.ShouldAskForGroupId && SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier)
                        {
                            switch (gridParameter.GroupIdentifierMode)
                            {
                                case GroupIdentifierMode.GroupIdentifier:
                                    frame = Gidx1(gridParameter.DataAcquisitionMode, minimumColumns, maximumColumns, out userCancelled, out wasPivoted, processor.NextOriginGroup());
                                    break;
                                case GroupIdentifierMode.TreatmentAndBlock:
                                    frame = Gidx2(minimumColumns, maximumColumns, out userCancelled, out wasPivoted, processor.NextOriginGroup());
                                    break;
                                default:
                                    throw new Exception("Unknown mode when asking for group identifiers");
                            }
                        }
                        else
                        {
                            frame = grid.GetCellArray(0, gridParameter.DataAcquisitionMode, minimumColumns, maximumColumns, selectionMessage, gridParameter.CancelSkipsParameter, gridParameter.ShouldAskForGroupId, false, out userCancelled, out wasPivoted, processor.NextOriginGroup());
                        }
                        if (!wasPivoted)
                            break;
                    }
                }
                if (null != frame)
                {
                    ParameterBag outputParameters = new ParameterBag();
                    if (!string.IsNullOrEmpty(gridParameter.AppendToFrame))
                    {
                        DataFrame targetFrame;
                        if (parameters.ContainsKey(gridParameter.AppendToFrame))
                        {
                            targetFrame = parameters[gridParameter.AppendToFrame].AsDataFrame;
                        }
                        else
                        {
                            targetFrame = new DataFrame();
                            outputParameters.Add(gridParameter.AppendToFrame, new FilledParameter(true, targetFrame));
                        }
                        foreach (Variable v in frame.Variables)
                            targetFrame.Variables.Add(v);
                        // frame.Variables.Clear(); Removed as this prevents validation - the original frame's variables have to stay intact until after the validation phase.
                        // HACK: As an unpleasant side effect, this means that *both* frames share a pointer to the variable.
                    }
                    outputParameters.Add(parameter.Name, new FilledParameter(true, frame));
                    return outputParameters;
                }

                // No frame was returned, either because the user cancelled or because of an error.  Distinguish the two cases!
                if (userCancelled)
                {
                    if (null != gridParameter.CancelSkipsParameter)
                        return null;
                    throw new TemplateOperationCancelledException();
                }


                // User error - go round again, but clear the selection to avoid unbreakable loops!
                ClearSelection();
            }
        }

        internal DataFrame2D FillGridParameter2D(Parameter parameter, ITemplateProcessor processor, SdApplication sDApplication, ParameterBag parameters)
        {
            GridParameter2D gridParameter = (GridParameter2D)parameter;
            if (gridParameter.ShouldClearSelectionFirst)
                ClearSelection();
            while (true)
            {
                int originGroup = processor.NextOriginGroup();
                if (SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier)
                {
                    bool userCancelled;
                    bool wasPivoted;
                    DataFrame2D frame = Gidx3(gridParameter.MinimumColumns(processor, parameters),
                        gridParameter.MaximumColumns(processor, parameters),
                        0,
                        gridParameter.SubPrompt(processor, parameters),
                        gridParameter.DataAcquisitionMode,
                        out userCancelled,
                        out wasPivoted,
                        originGroup);
                    if (userCancelled)
                        throw new TemplateOperationCancelledException();
                    if (wasPivoted)
                        continue;
                    return frame;
                }
                switch (gridParameter.DataAcquisitionMode)
                {
                    case DataAcquisitionMode2D.GroupThenBlock:
                        {
                            DataFrame2D frame = new DataFrame2D();
                            bool userCancelled;
                            int groups = SdApplication.SoleInstance.GetInteger("Number of groups", gridParameter.Operation.ToString(), 1, out userCancelled);
                            if (userCancelled || groups < 1 || groups > 10)
                            {
                                // Cancelling the number of groups probably implies that the user wants to select by group
                                SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier = true;
                                continue;
                            }

                            // If we get here, we have some groups
                            bool ok = true;
                            for (int g = 0; g < groups; g++)
                            {
                                ClearSelection();
                                bool wasPivoted;
                                DataFrame subFrame = grid.GetCellArray(0, DataAcquisitionMode.NumericReplaceMissing, 1, 90, "Select subgroups for group " + (g + 1), parameter.CancelSkipsParameter, true, false, out userCancelled, out wasPivoted, originGroup);
                                if (userCancelled)
                                    throw new TemplateOperationCancelledException();
                                if (wasPivoted)
                                {
                                    // Go round again
                                    ok = false;
                                    break;
                                }

                                frame.EnsureVariablesJagged(g + 1, subFrame.VariableCount);
                                for (int s = 0; s < subFrame.VariableCount; s++)
                                {
                                    frame.Variables[g][s] = subFrame.Variables[s];
                                    subFrame.Variables[s] = null; // Paranoia, to ensure any future destructors don't operate on the copied variable.
                                }
                            }
                            if (ok)
                            {
                                if (gridParameter.ShouldSquare)
                                {
                                    // Good to return.  Square up the data before we do.
                                    int maxRows = frame.MaxRows;
                                    for (int g = 0; g < frame.VariableCount; g++)
                                        for (int s = 0; s < frame.VariableCountTheOtherWay; s++)
                                            if (null != frame.Variables[g][s])
                                                frame.Variables[g][s].EnsureLength(maxRows);
                                }
                                return frame;
                            }
                        }
                        break;
                    case DataAcquisitionMode2D.BlockThenGroup:
                        {
                            DataFrame2D frame = new DataFrame2D();
                            bool userCancelled;
                            int repeats = SdApplication.SoleInstance.GetInteger("Number of repeats", gridParameter.Operation.ToString(), 2, out userCancelled);
                            if (userCancelled || repeats <= 1)
                            {
                                // Cancelling the number of groups probably implies that the user wants to select by group
                                SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier = true;
                                continue;
                            }

                            // If we get here, we have some repeats
                            int cols = 0;
                            int rows = 0;
                            bool startAgain = false;
                            frame.Name = "";
                            for (int rpt = 1; rpt <= repeats; rpt++)
                            {
                                frame.Name += " (";
                                ClearSelection();
                                bool wasPivoted;
                                DataFrame repeatFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, GroupIdentifierMode.GroupIdentifier, 2, 200, "Select subject (row) by treatment (column) data for repeat " + rpt, parameter.CancelSkipsParameter, true, DataAcquisitionWidth.Wide, false, out userCancelled, out wasPivoted, originGroup);
                                // No frame was returned, either because the user cancelled or because of an error.  Distinguish the two cases!
                                if (userCancelled)
                                {
                                    if (null != gridParameter.CancelSkipsParameter)
                                        return null;
                                    throw new TemplateOperationCancelledException();
                                }
                                if (null == repeatFrame)
                                {
                                    if (wasPivoted)
                                        startAgain = true;
                                    break;
                                }

                                bool ok = true;
                                if (1 == rpt)
                                {
                                    // Wait for a selection then dim the array
                                    rows = repeatFrame.MinRows;
                                    cols = repeatFrame.VariableCount;
                                    frame.EnsureVariablesSquare(rows, cols);
                                    foreach (List<Variable> vl in frame.Variables)
                                    {
                                        for (int i = 0; i < vl.Count; i++)
                                        {
                                            DoubleVariable v = new DoubleVariable();
                                            v.EnsureLength(repeats);
                                            vl[i] = v;
                                        }
                                    }
                                }
                                else
                                {
                                    if (repeatFrame.MinRows != rows || repeatFrame.VariableCount != cols)
                                    {
                                        SdApplication.SoleInstance.MsgboxX("You must have the same number of subjects and treatments for each repeat, mark missing data with an asterisk if they are at the end of a column", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, gridParameter.Operation.ToString(), true);
                                        rpt--; // Try again
                                        ok = false;
                                    }
                                }
                                if (ok)
                                {
                                    for (int c = 0; c < cols; c++)
                                    {
                                        DoubleVariable v = repeatFrame.Variables[c].AsDoubleVariable;
                                        // 'CDAT2(rpt, c) = CDAT1(c)
                                        for (int r = 0; r < rows; r++)
                                            frame.Variables[r][c].AsDoubleVariable.Data[rpt - 1] = v.Data[r];
                                        if (0 == c)
                                            frame.Name += repeatFrame.Variables[c].Title;
                                        else
                                            frame.Name += ", " + repeatFrame.Variables[c].Title;
                                    }
                                }
                                frame.Name += ")";
                            }
                            if (!startAgain)
                                return frame;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("parameter", gridParameter.DataAcquisitionMode, "Only GroupThenBlock and BlockThenGroup are known");
                }
            }
        }

        public GroupedCovarianceData FillGroupedCovarianceParameter(ITemplateProcessor processor)
        {
            double[,,] y = null;
            int k = 0;
            int maxr = 0;
            int maxreps = 0;
            ColumnData[] cx = null;
            string xlab = "";
            MinMax minMax = null;

            ClearSelection();
            while (true)
            {
                double[,] xt;
                if (SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier)
                {
                    bool ok = Gidxyr(out xt, ref y, ref k, ref maxr, ref maxreps, ref cx, ref xlab, ref minMax);
                    if (ok)
                    {
                        double[] b = new double[k + 1];
                        double[] a = new double[k + 1];
                        string[] bnam = new string[k + 1];
                        double[] rssx = new double[k + 1];
                        double[] xmean = new double[k + 1];
                        double[] ymean = new double[k + 1];
                        int[] nxi = new int[k + 1];
                        int[,] ny = new int[k + 1, maxr + 1];
                        for (int g = 1; g <= k; g++)
                        {
                            nxi[g] = cx[g].Rows;
                            for (int i = 1; i <= nxi[g]; i++)
                            {
                                ny[g, i] = maxreps;
                            }
                        }
                        ITemplateHost host = SdApplication.SoleInstance;
                        ConfidenceIntervalParameter ciParam = new ConfidenceIntervalParameter { CanDefault = true, Name = "ci" };
                        ParameterBag filledCi = host.FillParameter(processor, ciParam, new ParameterBag(), false);

                        // If we get here, the operation acquired all its parameters successfully
                        GroupedCovarianceData gcd = new GroupedCovarianceData
                        {
                            a = a,
                            b = b,
                            bnam = bnam,
                            cx = cx,
                            GAMMA = filledCi["ci"].AsDouble,
                            k = k,
                            maxr = maxr,
                            maxreps = maxreps,
                            minMax = minMax,
                            nxi = nxi,
                            ny = ny,
                            rssx = rssx,
                            xlab = xlab,
                            xmean = xmean,
                            xt = xt,
                            y = y,
                            ymean = ymean
                        };
                        return gcd;
                    }
                }
                else
                {
                    minMax = new MinMax
                    {
                        MinX = Double.MaxValue,
                        MaxX = Double.MinValue,
                        MinY = Double.MaxValue,
                        MaxY = Double.MinValue
                    };
                    bool cancelled;
                    bool wasPivoted;
                    DataFrame predictorsFrame = grid.GetCellArray(0, DataAcquisitionMode.NumericSkipMissing, 2, 200, "Select data for PREDICTOR (x axis) SERIES", null, true, false, out cancelled, out wasPivoted, 0);
                    if (cancelled)
                        break;
                    if (wasPivoted)
                        continue;

                    k = predictorsFrame.VariableCount;
                    // Store the X Data temporarily
                    maxr = predictorsFrame.MaxRows;
                    xt = new double[k + 1, maxr + 1];
                    cx = new ColumnData[k + 1];
                    for (int c = 1; c <= k; c++)
                    {
                        DoubleVariable v = predictorsFrame.Variables[c - 1].AsDoubleVariable;
                        cx[c] = new ColumnData { Title = v.Title, Rows = v.Length, Sum = v.Sum };
                        xlab += v.Title + " ";
                        for (int r = 1; r <= cx[c].Rows; r++)
                        {
                            xt[c, r] = v.Data[r - 1];
                            if (xt[c, r] != Constant.MISSING)
                            {
                                if (xt[c, r] > minMax.MaxX)
                                    minMax.MaxX = xt[c, r];
                                if (xt[c, r] < minMax.MinX)
                                    minMax.MinX = xt[c, r];
                            }
                        }
                    }
                    if (xlab.Length > 70)
                        xlab = xlab.Substring(0, 70);

                    ITemplateHost host = SdApplication.SoleInstance;
                    bool yrep = host.GetBoolean("Use Y replicates", "Grouped linear covariance", false, out cancelled);
                    if (cancelled)
                        break;
                    double[] b = new double[k + 1];
                    double[] a = new double[k + 1];
                    string[] bnam = new string[k + 1];
                    double[] rssx = new double[k + 1];
                    double[] xmean = new double[k + 1];
                    double[] ymean = new double[k + 1];
                    int[] nxi = new int[k + 1];
                    int[,] ny = new int[k + 1, maxr + 1];
                    y = new double[k + 1, maxr + 1, 2];
                    for (int g = 1; g <= k; g++)
                    {
                        int nx = cx[g].Rows;
                        nxi[g] = nx;
                        if (yrep)
                        {
                            // Get Y replicates
                            ClearSelection();
                            DataFrame replicatesFrame = grid.GetCellArray(0, DataAcquisitionMode.NumericSkipMissing, nx, nx, "Select Data for OUTCOME (Y axis) REPLICATES for x SERIES " + g.ToString() + " LEVELS", null, false, false, out cancelled, out wasPivoted, 0);
                            if (cancelled || wasPivoted)
                                break; // This data selection cancelled, try again!

                            if (replicatesFrame.MaxRows > maxreps)
                            {
                                maxreps = replicatesFrame.MaxRows;
                                double[, ,] yNew = new double[k + 1, maxr + 1, maxreps + 1];
                                for (int i0 = 0; i0 <= y.GetUpperBound(0); i0++)
                                    for (int i1 = 0; i1 <= y.GetUpperBound(1); i1++)
                                        for (int i2 = 0; i2 <= y.GetUpperBound(2); i2++)
                                            yNew[i0, i1, i2] = y[i0, i1, i2];
                                y = yNew;
                            }
                            for (int j = 1; j <= nx; j++)
                            {
                                DoubleVariable v = replicatesFrame.Variables[j - 1].AsDoubleVariable;
                                ny[g, j] = v.Length;
                                for (int j2 = 1; j2 <= ny[g, j]; j2++)
                                {
                                    y[g, j, j2] = v.Data[j2 - 1];
                                    if (y[g, j, j2] != Constant.MISSING)
                                    {
                                        if (y[g, j, j2] > minMax.MaxY)
                                            minMax.MaxY = y[g, j, j2];
                                        if (y[g, j, j2] < minMax.MinY)
                                            minMax.MinY = y[g, j, j2];
                                    }
                                }
                            }
                        }
                        else
                        {
                            // Get single column values for y
                            double[, ,] yNew = new double[k + 1, maxr + 1, 2];
                            for (int i0 = 0; i0 <= y.GetUpperBound(0); i0++)
                                for (int i1 = 0; i1 <= y.GetUpperBound(1); i1++)
                                    for (int i2 = 0; i2 <= y.GetUpperBound(2); i2++)
                                        yNew[i0, i1, i2] = y[i0, i1, i2];
                            y = yNew;
                            ClearSelection();
                            DataFrame outcomeFrame = GetCellEqual(nx, DataAcquisitionMode.NumericSkipMissing, GroupIdentifierMode.GroupIdentifier, 1, 1, "Select Data for OUTCOME (Y) for PREDICTOR " + g + " {" + cx[g].Title.Substring(0, Math.Min(20, cx[g].Title.Length)) + "}", null, false, DataAcquisitionWidth.Wide, false, out cancelled, out wasPivoted, 0);
                            if (cancelled || wasPivoted)
                                break; // Failed selection, go round again
                            DoubleVariable outcomeVariable = outcomeFrame.Variables[0].AsDoubleVariable;
                            for (int j = 1; j <= nx; j++)
                            {
                                ny[g, j] = 1;
                                y[g, j, 1] = outcomeVariable.Data[j - 1];
                                if (y[g, j, 1] != Constant.MISSING)
                                {
                                    if (y[g, j, 1] > minMax.MaxY)
                                        minMax.MaxY = y[g, j, 1];
                                    if (y[g, j, 1] < minMax.MinY)
                                        minMax.MinY = y[g, j, 1];
                                }
                            }
                        }
                    }

                    ConfidenceIntervalParameter ciParam = new ConfidenceIntervalParameter { CanDefault = true, Name = "ci" };
                    ParameterBag filledCi = host.FillParameter(new TemplateProcessor(host), ciParam, new ParameterBag(), false);

                    // If we get here, the operation acquired all its parameters successfully
                    GroupedCovarianceData gcd = new GroupedCovarianceData
                    {
                        a = a,
                        b = b,
                        bnam = bnam,
                        cx = cx,
                        GAMMA = filledCi["ci"].AsDouble,
                        k = k,
                        maxr = maxr,
                        maxreps = maxreps,
                        minMax = minMax,
                        nxi = nxi,
                        ny = ny,
                        rssx = rssx,
                        xlab = xlab,
                        xmean = xmean,
                        xt = xt,
                        y = y,
                        ymean = ymean
                    };
                    return gcd;
                }
            }
            // If we get here, the user cancelled the operation
            throw new TemplateOperationCancelledException();
        }

        /// <summary>
        /// Return TRUE if all columns are equal length
        /// else returns FALSE (also if user cancels)
        /// </summary>
        /// <param name="requiredRows">If 0, no further requirement.  If non-zero, all rows must be requiredRows in length.</param>
        /// <param name="mode">The way in which the acquired data will be placed into the data structure</param>
        /// <param name="minimumColumns">The smallest acceptable number of columns</param>
        /// <param name="maximumColumns">The largest acceptable number of columns</param>
        /// <param name="selectionMessage">The prompt for the user</param>
        /// <param name="cancelButtonLabel">If non-null, the cancel button will take this label</param>
        /// <param name="allowUserToPivot">If true, the user is asked about grouping by identifier</param>
        /// <param name="width">If wide, always select columns.  If long, always select group IDs and values.  If RespectPivotSetting, use the user's current setting.</param>
        /// <param name="mightBeBatching">If true, we might be selecting data in a batch.  If batching, GetCellEqual should remember where its data came from and should re-select if such memory is present.  If not batching, GetCellEqual should clear memory and not pre-select.</param>
        /// <param name="userCancelled">Output. If true, the user explicitly cancelled the operation; if false, the data is valid or the user selected no data but did not explicitly cancel.</param>
        /// <param name="wasPivoted">If true, the user switched from selecting groups by column to by identifier or vice versa.</param>
        /// <returns></returns>
        private DataFrame GetCellEqual(int requiredRows, DataAcquisitionMode mode, GroupIdentifierMode groupIdentifierMode, int minimumColumns, int maximumColumns, string selectionMessage, string cancelButtonLabel, bool allowUserToPivot, DataAcquisitionWidth width, bool mightBeBatching, out bool userCancelled, out bool wasPivoted, int originGroup)
        {
            while (true)
            {
                bool isLong;
                switch (width)
                {
                    case DataAcquisitionWidth.RespectPivotSetting:
                        isLong = allowUserToPivot && SdApplication.SoleInstance.Preferences.SelectGroupsByIdentifier;
                        break;
                    case DataAcquisitionWidth.Wide:
                        isLong = false;
                        break;
                    case DataAcquisitionWidth.Long:
                        isLong = true;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException("width");
                }
                DataFrame frame;
                if (isLong)
                {
                    switch (groupIdentifierMode)
                    {
                        case GroupIdentifierMode.GroupIdentifier:
                            frame = Gidx1(mode, minimumColumns, maximumColumns, out userCancelled, out wasPivoted, originGroup);
                            break;
                        case GroupIdentifierMode.TreatmentAndBlock:
                            frame = Gidx2(minimumColumns, maximumColumns, out userCancelled, out wasPivoted, originGroup);
                            break;
                        default:
                            throw new Exception("Unknown group identifier mode");
                    }
                }
                else
                    frame = grid.GetCellArray(requiredRows, mode, minimumColumns, maximumColumns, selectionMessage, cancelButtonLabel, allowUserToPivot, mightBeBatching, out userCancelled, out wasPivoted, originGroup);
                if (wasPivoted)
                    return null;

                if (null == frame)
                {
                    // User cancelled selection or other issue - either way, userCancelled is already set correctly
                    return null;
                }
                // Find the index of the first non-matching column - if everything matches, c will equal frame.Variables
                int maxRows = frame.MaxRows;
                int c;
                for (c = 0; c < frame.VariableCount; c++)
                    if (frame.Variables[c].Length != maxRows)
                        break;
                bool allEqualLength = c == frame.VariableCount;

                if (0 == requiredRows)
                {
                    if (allEqualLength)
                    {
                        // They're all equal and we have no minimum - safe to return
                        return frame;
                    }
                    // Unequal - ask the user, if they accept then force all the data to maximum length, missing-padded
                    if (SdApplication.SoleInstance.MsgboxX("Warning: unequal length columns. If you select OK then the jagged ends of columns will be padded with missing data.", MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, "Worksheet Data Selection", true) == DialogResult.OK)
                    {
                        foreach (Variable v in frame.Variables)
                            v.EnsureLength(maxRows, true);
                        return frame;
                    }
                }
                else if (requiredRows == frame.MinRows && allEqualLength)
                {
                    return frame;
                }
                else
                {
                    // Defined minimum length, and we didn't hit it.
                    string xtra = "";
                    if (mode == DataAcquisitionMode.NumericSkipMissing || mode == DataAcquisitionMode.NumericReplaceMissing)
                    {
                        xtra = "\r\n\r\nThe rows must contain numeric data not text.";
                    }
                    SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "all columns selected must be the same length (" + requiredRows.ToString() + " rows)." + xtra, MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Worksheet Data Selection", true);
                }
                ClearSelection();
            }
        }

        /// <summary>
        /// Acquire a wide dataframe from long data with a single group selector.
        /// </summary>
        /// <param name="mode">NumericSkipMissing: Skip missing.  Anything else: include missing (but don't sum)</param>
        /// <param name="minimumColumns">Minimum number of variables</param>
        /// <param name="maximumColumns">Maximum number of variables</param>
        /// <param name="userCancelled"></param>
        /// <param name="wasPivoted"></param>
        /// <returns></returns>
        private DataFrame Gidx1(DataAcquisitionMode mode, int minimumColumns, int maximumColumns, out bool userCancelled, out bool wasPivoted, int originGroup)
        {
            string labd = "Select DATA";
            string labg = "Select GROUP IDENTIFIERS";

            while (true)
            {
                // call for group ID
                ClearSelection();
                DataFrame groupIdFrame = grid.GetCellArray(0, DataAcquisitionMode.CategoryCombineAllColumns, 1, 20, labg, null, true, false, out userCancelled, out wasPivoted, originGroup);
                if (userCancelled || wasPivoted)
                    return null;

                ClassifierVariable groupIdVariable = groupIdFrame.Variables[0].AsClassifierVariable;

                if (groupIdVariable.GroupCount < minimumColumns || groupIdVariable.GroupCount > maximumColumns)
                {
                    SelNumWarn(minimumColumns, maximumColumns, groupIdVariable.GroupCount, "StatsDirect Data Selection");
                    continue;
                }

                int mingn = int.MaxValue;
                int maxgn = int.MinValue;
                for (int i = 0; i < groupIdVariable.GroupCount; i++)
                {
                    if (groupIdVariable.Groups[i].NBin > maxgn)
                        maxgn = groupIdVariable.Groups[i].NBin;
                    if (groupIdVariable.Groups[i].NBin < mingn)
                        mingn = groupIdVariable.Groups[i].NBin;
                }

                // call for data
                ClearSelection();
                DataFrame dataFrame = GetCellEqual(groupIdVariable.Length, DataAcquisitionMode.NumericReplaceMissing, GroupIdentifierMode.GroupIdentifier, 1, 1, labd, null, true, DataAcquisitionWidth.Wide, true, out userCancelled, out wasPivoted, originGroup);
                if (userCancelled || wasPivoted)
                    return null;

                DoubleVariable dataVariable = dataFrame.Variables[0].AsDoubleVariable;

                DataFrame outputFrame = new DataFrame();
                for (int i = 0; i < groupIdVariable.GroupCount; i++)
                {
                    double thisGroupId = groupIdVariable.Groups[i].Id;
                    DoubleVariable v = new DoubleVariable();
                    outputFrame.Variables.Add(v);
                    v.EnsureLength(maxgn, false);

                    int cnt = 0;
                    if (DataAcquisitionMode.NumericSkipMissing == mode)
                    {
                        // skip missing data
                        for (int j = 0; j < groupIdVariable.Length; j++)
                        {
                            if (groupIdVariable.Data[j] == thisGroupId && dataVariable.Data[j] != Constant.MISSING)
                                v.Data[cnt++] = dataVariable.Data[j];
                        }
                    }
                    else
                    {
                        // include missing data but do not sum them
                        for (int j = 0; j < groupIdVariable.Length; j++)
                        {
                            if (groupIdVariable.Data[j] == thisGroupId)
                                v.Data[cnt++] = dataVariable.Data[j];
                        }
                    }
                    v.TruncateDataToLength(cnt);
                    if (groupIdVariable.GroupCount > 1)
                        v.Title = dataVariable.Title + "_" + groupIdVariable.Title + "_" + groupIdVariable.Groups[i].Label;
                    else
                        v.Title = dataVariable.Title;
                }
                return outputFrame;
            }
        }

        /// <summary>
        /// Acquire a wide dataframe from long data with two group selectors.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="userCancelled"></param>
        /// <param name="wasPivoted"></param>
        /// <returns></returns>
        private DataFrame Gidx2(int min, int max, out bool userCancelled, out bool wasPivoted, int originGroup)
        {
            string msg_ti = "StatsDirect Data Selection";
            string labd = "Select data";
            string labt = "Select treatment group/column identifier";
            string labb = "Select block/row identifier";

            while (true)
            {
                // BEWARE: Are group ID numbers being 0-based going to trip us up?
                // call for treatment ID
                ClearSelection();
                DataFrame treatmentFrame = grid.GetCellArray(0, DataAcquisitionMode.GroupIdentifiers, 1, 1, labt, null, true, false, out userCancelled, out wasPivoted, originGroup);
                if (userCancelled || wasPivoted)
                    return null;
                ClassifierVariable treatmentVariable = treatmentFrame.Variables[0].AsClassifierVariable;
                int treatment_cats = treatmentVariable.GroupCount;
                double[] treatment_gid = treatmentVariable.Data;
                string[] treatment_gcat = new string[treatment_cats];
                int[] treatment_gin = new int[treatment_cats];
                double[] treatment_g = new double[treatment_cats];
                int treatment_maxgn = 0;
                for (int i = 0; i < treatment_cats; i++)
                {
                    treatment_gcat[i] = treatmentVariable.Groups[i].Label;
                    treatment_gin[i] = treatmentVariable.Groups[i].NBin;
                    if (treatment_maxgn < treatment_gin[i])
                        treatment_maxgn = treatment_gin[i];
                    treatment_g[i] = i;
                }

                if (treatment_cats < min || treatment_cats > max)
                {
                    SelNumWarn(min, max, treatment_cats, msg_ti);
                    continue;
                }

                if (!EqGpWarn(-1, treatment_gin, 0, treatment_cats, msg_ti))
                    continue;

                // call for block ID
                ClearSelection();
                DataFrame blockFrame = grid.GetCellArray(0, DataAcquisitionMode.GroupIdentifiers, 1, 1, labb, null, true, false, out userCancelled, out wasPivoted, originGroup);
                ClassifierVariable blockVariable = blockFrame.Variables[0].AsClassifierVariable;
                if (userCancelled || wasPivoted)
                    return null;
                double[] block_gid = blockVariable.Data;
                int block_maxgn = 0;
                for (int i = 0; i < blockVariable.GroupCount; i++)
                    if (block_maxgn < blockVariable.Groups[i].NBin)
                        block_maxgn = blockVariable.Groups[i].NBin;

                if (block_maxgn > treatment_cats)
                {
                    SdApplication.SoleInstance.Error("Two way ANOVA requires only one observation per block - you entered " + (block_maxgn / (double)treatment_cats) + ".\r\n\r\nPlease use a repeated/replicate measures method or a regression model instead.", msg_ti);
                    continue;
                }

                int rows = (treatmentVariable.Length < blockVariable.Length) ? treatmentVariable.Length : blockVariable.Length;

                // call for data
                ClearSelection();
                DataFrame dataFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, GroupIdentifierMode.TreatmentAndBlock, 1, 1, labd, null, true, DataAcquisitionWidth.Wide, false, out userCancelled, out wasPivoted, originGroup);
                if (userCancelled || wasPivoted)
                    return null;

                DoubleVariable dataVariable = dataFrame.Variables[0].AsDoubleVariable;
                double[] dt = dataVariable.Data;

                DataFrame outputFrame = new DataFrame();
                // fill the transport matrix with data arranged in treatments (columns) and blocks (rows)
                for (int i = 0; i < treatment_cats; i++)
                {
                    double[] data = new double[treatment_maxgn];
                    for (int j = 0; j < treatment_maxgn; j++)
                        data[j] = Constant.MISSING;
                    for (int j = 0; j < rows; j++)
                        if (treatment_gid[j] == treatment_g[i])
                            data[(int)block_gid[j]] = dt[j];
                    string title = dataVariable.Title + ((treatment_cats > 1) ? "_" + treatmentVariable.Title + "_" + treatment_gcat[i] : "");
                    outputFrame.Variables.Add(new DoubleVariable(data, title));
                }
                return outputFrame;
            }
        }

        /// <summary>
        /// Acquire a "wide" 2D frame from long data with two explicit group selectors and an implicit third one for repeated observations.
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="neq"></param>
        /// <param name="subGroupSelectionLabel"></param>
        /// <param name="mode"></param>
        /// <param name="userCancelled"></param>
        /// <param name="wasPivoted"></param>
        /// <returns></returns>
        private DataFrame2D Gidx3(int min, int max, int neq, string subGroupSelectionLabel, DataAcquisitionMode2D mode, out bool userCancelled, out bool wasPivoted, int originGroup)
        {
            ClearSelection();
            string groupSelectionLabel = mode == DataAcquisitionMode2D.BlockThenGroup ? "Select treatment group/column identifier" : "Select GROUP identifier"; // Only BlockThenGroup uses medical terminology.
            DataFrame groupFrame = grid.GetCellArray(0, DataAcquisitionMode.GroupIdentifiers, 1, 1, groupSelectionLabel, null, true, false, out userCancelled, out wasPivoted, originGroup);
            if (userCancelled)
                throw new TemplateOperationCancelledException();
            if (wasPivoted)
                return null;

            ClassifierVariable groupVariable = groupFrame.Variables[0].AsClassifierVariable;
            int rows = groupVariable.Length;
            int cats = groupVariable.GroupCount;
            double[] gid = groupVariable.Data;
            string[] gcat = new string[cats];
            double[] g = new double[cats];
            int[] gin = new int[cats];

            for (int i = 0; i < cats; i++)
            {
                gcat[i] = groupVariable.Groups[i].Label;
                gin[i] = groupVariable.Groups[i].NBin;
                g[i] = i;
            }
            int numberOfGroups = cats;
            if (numberOfGroups < min || numberOfGroups > max)
            {
                SelNumWarn(min, max, numberOfGroups, "StatsDirect Data Selection");
                return null;
            }
            int mingn = int.MaxValue;
            int maxgn = int.MinValue;
            for (int i = 0; i < numberOfGroups; i++)
            {
                if (gin[i] > maxgn)
                    maxgn = gin[i];
                if (gin[i] < mingn)
                    mingn = gin[i];
            }

            if (!EqGpWarn(neq, gin, 0, numberOfGroups, "StatsDirect Data Selection"))
                return null;

            ClearSelection();
            if (null == subGroupSelectionLabel)
                subGroupSelectionLabel = "Select SUB-GROUP identifier";
            DataFrame subGroupFrame = GetCellEqual(rows, DataAcquisitionMode.GroupIdentifiers, GroupIdentifierMode.GroupIdentifier, 1, 1, subGroupSelectionLabel, null, true, DataAcquisitionWidth.Wide, false, out userCancelled, out wasPivoted, originGroup);
            if (userCancelled)
                throw new TemplateOperationCancelledException();
            ClassifierVariable subGroupVariable = subGroupFrame.Variables[0].AsClassifierVariable;

            int scats = subGroupVariable.GroupCount;
            double[] sgid = subGroupVariable.Data;
            string[] sgcat = new string[scats];
            double[] sg = new double[scats];

            int numberOfSubGroups = scats;
            int minsgn = int.MaxValue;
            int maxsgn = int.MinValue;
            for (int i = 0; i < scats; i++)
            {
                sgcat[i] = subGroupVariable.Groups[i].Label;
                sg[i] = (double)i;
                int sgin = subGroupVariable.Groups[i].NBin;
                if (sgin > maxsgn)
                    maxsgn = sgin;
                if (sgin < minsgn)
                    minsgn = sgin;
            }

            ClearSelection();
            DataFrame dataFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, GroupIdentifierMode.GroupIdentifier, 1, 1, "Select DATA column", null, true, DataAcquisitionWidth.Wide, false, out userCancelled, out wasPivoted, originGroup);
            if (userCancelled)
                throw new TemplateOperationCancelledException();
            DoubleVariable dataVariable = dataFrame.Variables[0].AsDoubleVariable;

            string dlab = dataVariable.Title;
            double[] dt = dataVariable.Data;

            switch (mode)
            {
                case DataAcquisitionMode2D.GroupThenBlock:
                    {
                        // Major axis: group.  Minor axis: subgroup.  Variable contents: Repeated data values, may be jagged.
                        DataFrame2D resultFrame = new DataFrame2D();
                        for (int groupNumber = 0; groupNumber < numberOfGroups; groupNumber++)
                        {
                            for (int subGroupNumber = 0; subGroupNumber < numberOfSubGroups; subGroupNumber++)
                            {
                                int cnt = 0;
                                double[] data = new double[maxgn];
                                for (int j = 0; j < rows; j++)
                                    if (gid[j] == g[groupNumber] && sgid[j] == sg[subGroupNumber])
                                        data[cnt++] = dt[j];
                                // Skip entering variables with no members - typically found at the end of jagged sources
                                if (cnt > 0)
                                {
                                    string title = groupVariable.Title + "_" + gcat[groupNumber] + " (" + subGroupVariable.Title + "_" + sgcat[subGroupNumber] + ")";
                                    Variable variable = new DoubleVariable(data, title); // TODO: Origin
                                    variable.TruncateDataToLength(cnt);
                                    resultFrame.EnsureVariablesJagged(groupNumber + 1, subGroupNumber + 1); // Results may not be rectangular, hence this is done in the inner loop.
                                    resultFrame.Variables[groupNumber][subGroupNumber] = variable;
                                }
                            }
                        }
                        return resultFrame;
                    }
                case DataAcquisitionMode2D.BlockThenGroup:
                    {
                        // Major axis: Block/subject.  Minor axis: group.  Variable contents: data values ordered by repeat, padded with MISSING.
                        DataFrame2D resultFrame = new DataFrame2D();
                        resultFrame.EnsureVariablesSquare(numberOfSubGroups, numberOfGroups);

                        int highestCnt = 0;
                        for (int group = 0; group < numberOfGroups; group++)
                        {
                            for (int subGroup = 0; subGroup < numberOfSubGroups; subGroup++)
                            {
                                int cnt = 0;
                                for (int row = 0; row < rows; row++)
                                {
                                    if (gid[row] == g[group] && sgid[row] == sg[subGroup])
                                    {
                                        if (null == resultFrame.Variables[subGroup][group])
                                        {
                                            DoubleVariable v = new DoubleVariable(); // TODO: Origin
                                            v.Title = groupVariable.Title + "_" + gcat[(int)g[group]] + " (" + subGroupVariable.Title + "_" + sgcat[(int)sg[subGroup]] + ")";
                                            v.EnsureLength(maxsgn, Constant.MISSING);
                                            resultFrame.Variables[subGroup][group] = v;
                                        }
                                        resultFrame.Variables[subGroup][group].AsDoubleVariable.Data[cnt++] = dt[row];
                                    }
                                }
                                highestCnt = Math.Max(highestCnt, cnt);
                            }
                        }
                        for (int group = 0; group < numberOfGroups; group++)
                            for (int subGroup = 0; subGroup < numberOfSubGroups; subGroup++)
                                resultFrame.Variables[subGroup][group].TruncateDataToLength(highestCnt);
                        resultFrame.Name = " " + dlab + " (data), " + groupVariable.Title + " (group), " + subGroupVariable.Title + " (sub-group)";
                        return resultFrame;
                    }
                default:
                    throw new Exception("Unknown 2D data acquisition mode");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="neq">If 0, this routine does nothing.  If -1, all elements in gin must be equal.  Otherwise, all elements in gin must equal neq.</param>
        /// <param name="gin"></param>
        /// <param name="lowerBound"></param>
        /// <param name="ng"></param>
        /// <param name="msg_ti"></param>
        /// <returns></returns>
        private static bool EqGpWarn(int neq, int[] gin, int lowerBound, int ng, string msg_ti)
        {
            if (0 == neq)
                return true;

            if (-1 == neq)
                neq = gin[lowerBound];

            for (int i = lowerBound; i < lowerBound + ng; i++)
            {
                if (gin[i] != neq)
                {
                    SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "all groups must be the same size.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, msg_ti, true);
                    return false;
                }
            }
            return true;
        }

        /*
                private DataFrame GIDXW(DataAcquisitionMode mode, int min, int max, int neq, string lab, out double[,] wt, ref string wtlab, ref double sumwt, out bool userCancelled)
                {
                    const string msg_ti = "StatsDirect Data Selection";
                    string labd = "Select DATA";
                    string labw = "Select WEIGHTS";
                    string labg = "Select GROUP IDENTIFIERS";
                    if (!string.IsNullOrEmpty(lab))
                    {
                        labd += " for " + lab;
                        labw += " for " + lab;
                        labg += " for " + lab;
                    }

                    do
                    {
                        bool proceed = true;

                        // call for group ID
                        ClearSelection();
                        DataFrame groupFrame = GetCellArray(0, DataAcquisitionMode.CategoryCombineAllColumns, 1, 10, labg, null, true, false, out userCancelled);
                        if (userCancelled || null == groupFrame)
                        {
                            // User cancelled
                            wt = null;
                            return null;
                        }
                        int rows = groupFrame.MinRows;
                        ClassifierVariable groupVariable = groupFrame.Variables[0].AsClassifierVariable;
                        int cats = groupVariable.GroupCount;
                        string catlab = groupVariable.Title;
                        double[] gid = groupVariable.Data;
                        string[] gcat = new string[cats];
                        int[] gin = new int[cats];
                        double[] g = new double[cats];
                        int mingn = int.MaxValue;
                        int maxgn = int.MinValue;
                        for (int i = 0; i < cats; i++)
                        {
                            gcat[i] = groupVariable.get_Group(i).Label;
                            gin[i] = groupVariable.get_Group(i).NBin;
                            if (gin[i] > mingn)
                                mingn = gin[i];
                            if (gin[i] < maxgn)
                                maxgn = gin[i];
                            g[i] = i;
                        }
                        int ng = cats;

                        if (ng < min || ng > max)
                        {
                            SelNumWarn(min, max, ng, msg_ti);
                            proceed = false;
                        }

                        EqGpWarn(neq, gin, ng, msg_ti);

                        if (proceed)
                        {
                            // call for data
                            ClearSelection();
                            DataFrame dataFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, 1, 1, labd, null, true, true, out userCancelled);
                            if (userCancelled || null == dataFrame)
                            {
                                // User cancelled
                                wt = null;
                                return null;
                            }
                            DoubleVariable dataVariable = dataFrame.Variables[0].AsDoubleVariable;
                            string datlab = dataVariable.Title;
                            double[] dt = dataVariable.Data;

                            // call for weights
                            ClearSelection();
                            DataFrame weightFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, 1, 1, labw, null, true, false, out userCancelled);
                            if (userCancelled || null == weightFrame)
                            {
                                // User cancelled
                                wt = null;
                                return null;
                            }
                            DoubleVariable weightVariable = weightFrame.Variables[0].AsDoubleVariable;
                            wtlab = weightVariable.Title;
                            double[] w = weightVariable.Data;

                            double[,] ARR2 = new double[ng, maxgn];
                            ColumnData[] CDAT1 = new ColumnData[ng];
                            wt = new double[ng, maxgn];
                            for (int i = 0; i < ng; i++)
                            {
                                for (int j = 0; j < maxgn; j++)
                                {
                                    ARR2[i, j] = Constant.MISSING;
                                    wt[i, j] = Constant.MISSING;
                                }
                            }
                            sumwt = 0;
                            for (int i = 0; i < ng; i++)
                            {
                                int cnt = 0;
                                CDAT1[i].Sum = 0;
                                if (DataAcquisitionMode.NumericSkipMissing == mode)
                                {
                                    // skip missing data
                                    for (int j = 0; j < rows; j++)
                                    {
                                        if (gid[j] == g[i] && dt[j] != Constant.MISSING && w[j] != Constant.MISSING)
                                        {
                                            ARR2[i, cnt] = dt[j];
                                            wt[i, cnt] = w[j];
                                            CDAT1[i].Sum += dt[j];
                                            sumwt += w[j];
                                            cnt++;
                                        }
                                    }
                                }
                                else
                                {
                                    // include missing data but do not sum them
                                    for (int j = 0; j < rows; j++)
                                    {
                                        if (gid[j] == g[i])
                                        {
                                            ARR2[i, cnt] = dt[j];
                                            wt[i, cnt] = w[j];
                                            if (dt[j] != Constant.MISSING)
                                                CDAT1[i].Sum += dt[j];
                                            if (w[j] != Constant.MISSING)
                                                sumwt += w[j];
                                            cnt++;
                                        }
                                    }
                                }
                                CDAT1[i].Rows = cnt;
                                if (ng > 1)
                                {
                                    CDAT1[i].Title = datlab + "_" + catlab + "_" + gcat[i];
                                }
                                else
                                {
                                    CDAT1[i].Title = datlab;
                                }
                            }
                            if (sumwt != 0)
                                sumwt = rows / sumwt;
                            DataFrame outputFrame = null;
                            // Complete me
                            return outputFrame;
                        }
                    } while (true);
                }
        */

        private bool Gidxyr(out double[,] x, ref double[, ,] y, ref int ng, ref int maxgn, ref int nrep, ref ColumnData[] cd, ref string xlab, ref MinMax minMax)
        {
            const string msgTi = "StatsDirect Data Selection";
            const string labd = "Select DATA";

            const int min = 2;
            const int max = 200;

            bool wasPivoted = true;
            while (wasPivoted)
            {
                // call for group ID
                ClearSelection();
                bool cancelled;
                DataFrame groupIdentifiers = grid.GetCellArray(0, DataAcquisitionMode.CategoryCombineAllColumns, 1, 10, "Select GROUP/SERIES IDENTIFIERS", null, true, false, out cancelled, out wasPivoted, 0);
                if (cancelled)
                    break;
                if (wasPivoted)
                    continue;

                minMax = new MinMax();
                ClassifierVariable groupIdentifierVariable = groupIdentifiers.Variables[0].AsClassifierVariable;
                int rows = groupIdentifierVariable.Length;
                int cats = groupIdentifierVariable.GroupCount;
                string catlab = groupIdentifierVariable.Title;
                double[] gid = new double[rows + 1];
                string[] gcat = new string[cats + 1];
                for (int i = 1; i <= rows; i++)
                {
                    gid[i] = groupIdentifierVariable.Data[i - 1];
                }
                for (int i = 1; i <= cats; i++)
                {
                    gcat[i] = groupIdentifierVariable.Groups[i - 1].Label;
                }

                // find number of categories
                ng = 1;
                double[] g = new double[rows + 1];
                int[] gin = new int[rows + 1];
                for (int j = 1; j <= rows; j++)
                {
                    if (gid[j] != Constant.MISSING)
                    {
                        g[1] = gid[j];
                        gin[1] = 1;
                        break;
                    }
                }
                for (int j = 2; j <= rows; j++)
                {
                    bool newa = true;
                    if (gid[j] == Constant.MISSING)
                    {
                        newa = false;
                    }
                    else
                    {
                        for (int i = 1; i <= ng; i++)
                        {
                            if (gid[j] == g[i])
                            {
                                newa = false;
                                gin[i]++;
                                break;
                            }
                        }
                    }
                    if (newa)
                    {
                        ng++;
                        g[ng] = gid[j];
                        gin[ng] = 1;
                    }
                }

                if (ng < min || ng > max)
                {
                    SelNumWarn(min, max, ng, msgTi);
                    continue;
                }

                maxgn = int.MinValue;
                for (int i = 1; i <= ng; i++)
                {
                    if (gin[i] > maxgn)
                        maxgn = gin[i];
                }

                // Call for y data
                minMax.MinY = double.MaxValue;
                minMax.MaxY = double.MinValue;
                ClearSelection();
                DataFrame replicatesFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, GroupIdentifierMode.GroupIdentifier, 1, 200, labd + " for Y (VERTICAL AXIS) REPLICATES", null, true, DataAcquisitionWidth.Wide, false, out cancelled, out wasPivoted, 0);
                if (cancelled || wasPivoted)
                    break;

                nrep = replicatesFrame.VariableCount;
                y = new double[ng + 1, maxgn + 1, nrep + 1];
                for (int k = 1; k <= nrep; k++)
                {
                    DoubleVariable v = replicatesFrame.Variables[k - 1].AsDoubleVariable;
                    for (int i = 1; i <= ng; i++)
                    {
                        int cnt = 0;
                        for (int j = 1; j <= rows; j++)
                        {
                            if (gid[j] == g[i])
                            {
                                cnt++;
                                double yz = v.Data[j - 1];
                                y[i, cnt, k] = yz;
                                if (yz != Constant.MISSING)
                                {
                                    if (yz > minMax.MaxY)
                                        minMax.MaxY = yz;
                                    if (yz < minMax.MinY)
                                        minMax.MinY = yz;
                                }
                            }
                        }
                    }
                }

                // Call for x data
                minMax.MinX = double.MaxValue;
                minMax.MaxX = double.MinValue;
                ClearSelection();
                DataFrame xFrame = GetCellEqual(rows, DataAcquisitionMode.NumericReplaceMissing, GroupIdentifierMode.GroupIdentifier, 1, 1, labd + " for X (HORIZONTAL AXIS)", null, true, DataAcquisitionWidth.Wide, false, out cancelled, out wasPivoted, 0);
                if (cancelled || wasPivoted)
                    break;
                DoubleVariable xVariable = xFrame.Variables[0].AsDoubleVariable;
                xlab = xVariable.Title;
                x = new double[ng + 1, maxgn + 1];
                cd = new ColumnData[ng + 1];
                for (int i = 1; i <= ng; i++)
                {
                    int cnt = 0;
                    cd[i] = new ColumnData { Sum = 0 };
                    for (int j = 1; j <= rows; j++)
                    {
                        if (gid[j] == g[i])
                        {
                            cnt++;
                            double xz = xVariable.Data[j - 1];
                            x[i, cnt] = xz;
                            if (xz != Constant.MISSING)
                            {
                                cd[i].Sum += xz;
                                if (xz > minMax.MaxX)
                                    minMax.MaxX = xz;
                                if (xz < minMax.MinX)
                                    minMax.MinX = xz;
                            }
                        }
                    }
                    cd[i].Rows = cnt;
                    if (ng > 1)
                        cd[i].Title = catlab + "_" + gcat[i];
                    else
                        cd[i].Title = catlab;
                }
                return true;
            }

            // If we get here, the user cancelled
            x = null;
            return false;
        }

        public static void SelNumWarn(int min, int max, int totcols, string msg_ti)
        {
            if (min == max)
                SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "you must select " + min.ToString() + " column" + (min > 1 ? "s" : "") + " but you selected " + totcols.ToString() + ".", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, msg_ti, true);
            else
            {
                if (totcols < min)
                    SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "you must select " + min.ToString() + " column" + (min > 1 ? "s" : "") + " or more but you selected " + totcols.ToString() + ".", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, msg_ti, true);
                else if (totcols > max)
                    SdApplication.SoleInstance.MsgboxX(Formatting.ERRCOLON + "you must select " + max.ToString() + " column" + (max > 1 ? "s" : "") + " or fewer but you selected " + totcols.ToString() + ".", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, msg_ti, true);
            }
        }

        private void ClearSelection()
        {
            grid.ClearSelection();
        }
    }
}
