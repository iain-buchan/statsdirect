using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    static class CellArrayProcessor
    {
        /// <summary>
        /// Turn the raw cell selection into a data frame with variables of the requested type selected in the requested way.
        /// </summary>
        /// <param name="cellSelection">A raw cell selection, which may very well be rectangular and with the bottom of it filled with cells that are in the sheet's used range but have never contained data.</param>
        /// <param name="mode"></param>
        /// <param name="rowLengthHint"></param>
        /// <param name="isRefill"></param>
        /// <param name="titleWasInData"></param>
        /// <param name="originGroup"></param>
        /// <param name="frameName"></param>
        /// <returns></returns>
        public static DataFrame ProcessCellArray(CellSelection cellSelection, DataAcquisitionMode mode, int rowLengthHint, bool isRefill, bool titleWasInData, int originGroup, string frameName)
        {
            // If we get here, the user selected some data.
            try
            {
                DataFrame frame;
                switch (mode)
                {
                    case DataAcquisitionMode.NumericSkipMissing:
                        frame = ProcessCellArrayNumericSkipMissing(cellSelection, mode, originGroup);
                        break;
                    case DataAcquisitionMode.NumericReplaceMissing:
                        frame = ProcessCellArrayNumericReplaceMissing(cellSelection, mode, originGroup);
                        break;
                    case DataAcquisitionMode.GroupIdentifiers:
                    case DataAcquisitionMode.CategoryReplaceMissing:
                    case DataAcquisitionMode.CategoryCombineAllColumns:
                    case DataAcquisitionMode.Text:
                        {
                            if (!PreprocessCellArrayGroupsOrText(cellSelection, rowLengthHint, isRefill, titleWasInData, out int topRow, out string[,] hold))
                                return null;

                            if (DataAcquisitionMode.CategoryReplaceMissing == mode)
                                PreprocessCellArrayCategoryReplaceMissing(topRow, hold);
                            if (DataAcquisitionMode.Text == mode)
                                frame = ProcessCellArrayText(cellSelection, mode, topRow, hold, originGroup);
                            else if (mode == DataAcquisitionMode.CategoryCombineAllColumns)
                                frame = ProcessCellArrayCategoryCombineAllColumns(cellSelection, mode, topRow, hold, originGroup);
                            else
                                frame = ProcessCellArrayCategoriesPerColumn(cellSelection, mode, topRow, hold, originGroup);
                        }
                        break;
                    case DataAcquisitionMode.DateReplaceMissing:
                        frame = ProcessCellArrayDateReplaceMissing(cellSelection, mode, originGroup);
                        break;
                    case DataAcquisitionMode.TextWithFormulae:
                        frame = ProcessCellArrayTextWithFormulae(cellSelection, mode, originGroup);
                        break;
                    case DataAcquisitionMode.Variant:
                        frame = ProcessCellArrayVariant(cellSelection, mode, originGroup);
                        break;
                    case DataAcquisitionMode.TextNoTitles:
                        frame = ProcessCellArrayTextNoTitles(cellSelection, mode, originGroup);
                        break;
                    case DataAcquisitionMode.NumericCodingTextToCategories:
                    case DataAcquisitionMode.NumericCodingTextToDummies:
                        frame = ProcessCellArrayNumericCodingTextToSomething(cellSelection, mode, originGroup);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mode), mode.ToString(), "Unknown mode");
                }
                if (null != frame)
                    frame.Name = frameName;

                // #1131: If the frame contains multiple variables, remove any that have no data.  Single variables are retained.
                // TODO: How does this affect minimum-column requirements?  Do they need checking later?
                if (null != frame && frame.VariableCount > 1)
                {
                    List<IVariable> toRemove = new List<IVariable>();
                    foreach (IVariable v in frame.Variables)
                        if (v.Length == 0)
                            toRemove.Add(v);
                    foreach (IVariable v in toRemove)
                        frame.Variables.Remove(v);
                }

                return frame;
            }
            catch (ArithmeticException ex)
            {
                SdApplication.SoleInstance.FriendlyError("Internal error reading data from worksheet", ex, false);
                throw; // TODO: What is the correct behaviour here?  Merely returning null causes a infinite loop
            }
        }

        private static DataFrame ProcessCellArrayNumericCodingTextToSomething(CellSelection cellSelection, DataAcquisitionMode mode, int originGroup)
        {
            DataFrame frame = new DataFrame();
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                double[] numericValues = ccs.GetDataValues();

                // If there's no data in the row (for example if it's hidden), ignore the row
                if (null == numericValues)
                    continue;

                // Find the last row
                int lastNumericRow;
                for (lastNumericRow = numericValues.GetUpperBound(0); lastNumericRow >= 0; --lastNumericRow)
                    if (numericValues[lastNumericRow] != Constant.MISSING)
                        break;
                // We obtained the DataValues of the numeric values in the row, which sets the title.  Obtain the texts (remembering that the first text might be a title) and compare.
                string[] textValues = ccs.GetTextValues();
                int lastTextRow;
                for (lastTextRow = textValues.GetUpperBound(0); lastTextRow >= 0; --lastTextRow)
                    if (!string.IsNullOrWhiteSpace(textValues[lastTextRow]))
                        break;
                int lastRow = Math.Max(lastNumericRow, lastTextRow);
                // If there are any numeric missing values where the text is not missing, treat the column as textual and code as a category
                bool isTextual = false;
                for (int row = 0; row <= lastRow; row++)
                {
                    if ((numericValues.Length <= row || numericValues[row] == Constant.MISSING) && !string.IsNullOrWhiteSpace(textValues[row]))
                    {
                        isTextual = true;
                        break;
                    }
                }
                if (isTextual)
                {
                    ClassifierVariable variable = new ClassifierVariable();
                    variable.EnsureLength(lastRow + 1);

                    // Code text categories as numbers
                    Dictionary<string, Group> groupsByLabel = new Dictionary<string, Group>();
                    int nextGroupNumber = 0;

                    for (int row = 0; row <= lastRow; row++)
                    {
                        string pattern = textValues[row];
                        // Ignore missing values
                        if (string.IsNullOrEmpty(pattern) || "*".Equals(pattern))
                            continue;

                        // Enumerate categories and put results in variable
                        if (!groupsByLabel.TryGetValue(pattern, out Group probe))
                        {
                            // New group
                            probe = new Group(pattern, nextGroupNumber++);
                            groupsByLabel.Add(probe.Label, probe);
                        }
                        // By now, we have always found or created the group for this row
                        probe.NBin++;
                        variable.Data[row] = probe.Id;
                    }

                    // Fill in groups
                    variable.EnsureGroups(groupsByLabel.Count);
                    foreach (Group group in groupsByLabel.Values)
                        variable.Groups[(int)group.Id] = group;

                    // Fill in column title
                    variable.Title = ccs.ColumnTitle;
                    variable.Origin = ccs.GetWorksheetOrigin(mode, ccs.TitleIsInData, originGroup);
                    if (mode == DataAcquisitionMode.NumericCodingTextToCategories)
                    {
                        frame.Variables.Add(variable);
                    }
                    else
                    {
                        // Dummies
                        DataFrame dummyFrame = Sheet.ToDummyVariables(SdApplication.SoleInstance, variable, true);
                        if (null != dummyFrame)
                        {
                            foreach (IVariable v in dummyFrame.Variables)
                                frame.Variables.Add(v);
                        }
                        else
                            frame.Variables.Add(variable);
                    }
                }
                else
                {
                    DoubleVariable variable = new DoubleVariable(lastRow + 1, ccs.ColumnTitle);
                    for (int row = 0; row <= lastRow; row++)
                    {
                        double v = numericValues[row];
                        if (Constant.MISSING * 10D == v)
                            v = Constant.MISSING;
                        variable.Data[row] = v;
                    }
                    variable.Origin = ccs.GetWorksheetOrigin(mode, ccs.TitleIsInData, originGroup);
                    if (mode == DataAcquisitionMode.NumericCodingTextToCategories)
                    {
                        frame.Variables.Add(variable);
                    }
                    else
                    {
                        // Dummies?
                        // Look for 2-12 non-unique integer values: if so, ask if categorical and dummy them
                        Dictionary<int, Group> groupsByLabel = new Dictionary<int, Group>();
                        bool allInteger = true;
                        int nextGroupNumber = 0;
                        double[] classifierData = new double[variable.Length];
                        for (int row = 0; row <= lastRow; row++)
                        {
                            double v = variable.Data[row];
                            if (v < int.MinValue || v > int.MaxValue || v != Math.Floor(v))
                            {
                                allInteger = false;
                                break;
                            }
                            int pattern = (int)v;
                            if (!groupsByLabel.TryGetValue(pattern, out Group probe))
                            {
                                // New group
                                probe = new Group(pattern.ToString(), nextGroupNumber++);
                                groupsByLabel.Add(pattern, probe);
                            }
                            // By now, we have always found or created the group for this row
                            probe.NBin++;
                            classifierData[row] = probe.Id;
                        }
                        if (!allInteger)
                        {
                            // Some non-integer values.  Can't be categorical.  Treat as a single variable.
                            frame.Variables.Add(variable);
                        }
                        else if (groupsByLabel.Count <= 2 || groupsByLabel.Count >= Math.Min(variable.Length, 12))
                        {
                            // All distinct, too many groups, or already binary.  Treat as a single variable.
                            frame.Variables.Add(variable);
                        }
                        else
                        {
                            ClassifierVariable cv = new ClassifierVariable { Title = variable.Title, Data = classifierData };
                            cv.EnsureLength(variable.Data.Length);
                            cv.EnsureGroups(groupsByLabel.Count);
                            foreach (Group group in groupsByLabel.Values)
                                cv.Groups[(int)group.Id] = group;
                            DataFrame dummyFrame;
                            try
                            {
                                dummyFrame = Sheet.ToDummyVariables(SdApplication.SoleInstance, cv, true);
                            }
                            catch (TemplateOperationCancelledException ex)
                            {
                                if (ex.ShouldShowError)
                                    SdApplication.TemplateHost.Error(ex.Message, ex.Caption);
                                dummyFrame = null;
                            }
                            if (null != dummyFrame)
                            {
                                foreach (IVariable v in dummyFrame.Variables)
                                    frame.Variables.Add(v);
                            }
                            else
                            {
                                frame.Variables.Add(variable);
                            }
                        }
                    }
                }
            }
            return frame;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cellSelection"></param>
        /// <param name="rowLengthHint"></param>
        /// <param name="isRefill"></param>
        /// <param name="titleWasInData"></param>
        /// <param name="topRow"></param>
        /// <param name="hold"></param>
        /// <returns>False if the user cancelled in response to a question (implying selection should not proceed), true otherwise.</returns>
        private static bool PreprocessCellArrayGroupsOrText(CellSelection cellSelection, int rowLengthHint, bool isRefill, bool titleWasInData, out int topRow, out string[,] hold)
        {
            // code text categories as numbers

            // is the top row unique (i.e. not duplicated in the column data) and long?  Works out min and max lengths as a side-effect
            bool isUnique = true;
            bool isUniqueLength = true;
            bool isShort = false;
            int minRowCount = int.MaxValue;
            int maxRowCount = int.MinValue;
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                int rc = ccs.RowCount;
                if (rc < minRowCount)
                    minRowCount = rc;
                if (rc > maxRowCount)
                    maxRowCount = rc;

                string[] columnArray = ccs.GetTexts();
                string qtitle = columnArray[0];
                int qtitleLength = qtitle.Length;
                if (qtitleLength < 3)
                    isShort = true;
                for (int i = 1; i < ccs.NonHiddenRowCount; i++)
                {
                    string candidate = columnArray[i];
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        candidate = candidate.Trim();
                        if (candidate.Equals(qtitle))
                        {
                            isUnique = false;
                            isUniqueLength = false;
                        }
                        else if (candidate.Length == qtitleLength)
                            isUniqueLength = false;
                        if (!(isUnique || isUniqueLength))
                            break;
                    }
                }
                // If we have duplicate names, there's no point looking further
                if (!isUnique)
                    break;
            }
            if (isRefill)
                topRow = titleWasInData ? 1 : 0;
            else if (minRowCount == maxRowCount && minRowCount == rowLengthHint)
                topRow = 0;
            else if (minRowCount == maxRowCount && minRowCount == rowLengthHint + 1)
                topRow = 1;
            else if (TopRowIsFormattedLikeTitles(cellSelection))
                topRow = 1;
            else if (isUnique)
            {
                if (isShort && !isUniqueLength)
                {
                    using (new DefaultCursor())
                    {
                        switch (
                            SdApplication.SoleInstance.MsgboxX(
                                "Does the top row of your selection contain titles?",
                                MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question,
                                "Worksheet Categorical Data Selection", true))
                        {
                            case DialogResult.Yes:
                                topRow = 1;
                                break;
                            case DialogResult.Cancel:
                                topRow = 0;
                                hold = null;
                                return false;
                            default:
                                topRow = 0;
                                break;
                        }
                    }
                }
                else
                {
                    topRow = 1;
                }
            }
            else
            {
                topRow = 0;
            }

            // At this point, we know whether we have titles or not.  Now obtain our data strings.
            hold = new string[cellSelection.LongestRowCount, cellSelection.TotalColumns];
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                // get text not entry because we want formulae translated
                string[] columnArray = ccs.GetTexts();
                int rx = 0;
                for (int i = 0; i < ccs.NonHiddenRowCount; i++)
                {
                    string t = columnArray[i];
                    string bufr = null == t ? string.Empty : t.Trim();
                    hold[rx++, c] = bufr;
                }
                // Fill in blanks for any rows that have been filtered out
                while (rx < cellSelection.LongestRowCount)
                    hold[rx++, c] = string.Empty;
            }
            return true;
        }

        private static void PreprocessCellArrayCategoryReplaceMissing(int topRow, string[,] hold)
        {

            // Include missing values
            // Updated to match DeJagger by PJC July 2008, from code by IEB Sep 2007
            int lrow = 0;
            for (int c = 0; c <= hold.GetUpperBound(1); c++)
            {
                int r;
                for (r = hold.GetUpperBound(0); r >= topRow; --r)
                {
                    if (hold[r, c].Length > 0 && !Formatting.ASTERISK.Equals(hold[r, c]))
                        break;
                }
                if (r > lrow)
                    lrow = r;
            }
            // At this point, lrow is the index of the last row that contains data.
            for (int c = 0; c <= hold.GetUpperBound(1); c++)
            {
                for (int r = lrow; r >= topRow; --r)
                {
                    if (hold[r, c].Length > 0)
                    {
                        if (Formatting.ASTERISK.Equals(hold[r, c]))
                            hold[r, c] = Formatting.MISSINGLABEL;
                    }
                    else
                    {
                        hold[r, c] = Formatting.MISSINGLABEL;
                    }
                }
            }
        }

        private static DataFrame ProcessCellArrayNumericSkipMissing(CellSelection cellSelection, DataAcquisitionMode mode, int originGroup)
        {
            DataFrame frame = new DataFrame();

            // read in the cells, skipping any missing data
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                double[] values = ccs.GetDataValues();

                // If there's no data in the row (for example if it's hidden), ignore the row
                if (null == values)
                    continue;

                DoubleVariable variable = new DoubleVariable(values.Length, ccs.ColumnTitle);

                // Eliminate MISSING values by copying, then truncating the array
                int size = 0;
                foreach (double v in values)
                    if (v != Constant.MISSING && v != Constant.MISSING * 10.0)
                        variable.Data[size++] = v;

                if (size < 1)
                {
                    using (new DefaultCursor())
                    {
                        SdApplication.SoleInstance.MsgboxX("You must select numerical data for this function", MessageBoxButtons.OK, MessageBoxIcon.Exclamation, "Worksheet Data Selection", true);
                    }
                    return null;
                }
                variable.Origin = ccs.GetWorksheetOrigin(mode, ccs.TitleIsInData, originGroup);
                variable.TruncateDataToLength(size);
                frame.Variables.Add(variable);
            }
            return frame;
        }

        private static DataFrame ProcessCellArrayText(CellSelection cellSelection, DataAcquisitionMode mode, int topRow, string[,] hold, int originGroup)
        {
            DataFrame frame = new DataFrame();
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                int totRows = ccs.RowCount;
                // Remove any trailing blanks from the selection
                while (totRows > topRow && hold[totRows - 1, c].Length == 0)
                    totRows--;

                string title = 0 == topRow
                                   ? ccs.GetColumnTitle()
                                   : ccs.GetFirstCellText().Trim();
                if (totRows > topRow || !string.IsNullOrEmpty(title))
                {
                    StringVariable variable = new StringVariable(totRows - topRow, title);
                    int size = 0;
                    for (int r = topRow; r < totRows; r++)
                        variable.Data[size++] = hold[r, c];

                    variable.Origin = ccs.GetWorksheetOrigin(mode, topRow > 0, originGroup);
                    frame.Variables.Add(variable);
                }
            }
            return frame;
        }

        private static DataFrame ProcessCellArrayCategoryCombineAllColumns(CellSelection cellSelection, DataAcquisitionMode mode, int topRow, string[,] hold, int originGroup)
        {
            // Categories combined across columns; output as row pattern
            int totRows = cellSelection.LongestRowCount;
            int totCols = cellSelection.TotalColumns;
            string[] foundwhat = new string[totRows];
            int[] nbin = new int[totRows];
            int found = 0;
            int size = 0;
            ClassifierVariable variable = new ClassifierVariable();
            for (int r = topRow; r < totRows; r++)
            {
                // Build the pattern for row r
                StringBuilder patternBuilder = new StringBuilder();
                for (int c = 0; c < totCols; c++)
                {
                    if (null == hold[r, c] || hold[r, c].Length == 0 || "*".Equals(hold[r, c]))
                    {
                        // Make the row pattern empty if any data are missing
                        patternBuilder.Length = 0;
                        break;
                    }
                    // Allow neat string pattern for later bin naming purposes
                    if (c > 0)
                        patternBuilder.Append(", ");
                    patternBuilder.Append(hold[r, c]);
                }
                string pattern = patternBuilder.ToString();

                // Enumerate categories and put results in variable
                if (pattern.Length > 0)
                {
                    bool wasFound = false;
                    for (int i = 0; i < found; i++)
                    {
                        if (pattern.Equals(foundwhat[i]))
                        {
                            wasFound = true;
                            size++;
                            variable.EnsureLength(size);
                            variable.Data[size - 1] = pattern.Contains(Formatting.MISSINGLABEL) ? Constant.MISSING : i;
                            nbin[i]++;
                            break;
                        }
                    }
                    if (!wasFound)
                    {
                        nbin[found] = 1;
                        foundwhat[found] = pattern;
                        size++;
                        variable.EnsureLength(size);
                        variable.Data[size - 1] = pattern.Contains(Formatting.MISSINGLABEL) ? Constant.MISSING : found;
                        found++;
                    }
                }
            }

            // fill in column details
            variable.EnsureGroups(found);
            for (int i = 0; i < found; i++)
            {
                Group group = new Group(foundwhat[i], i);
                group.NBin = nbin[i];
                variable.Groups[i] = group;
            }

            // column title is a hybrid of all columns
            StringBuilder titleBuilder = new StringBuilder();
            for (int c = 0; c < totCols; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                if (c > 0)
                    titleBuilder.Append(", ");
                titleBuilder.Append(0 == topRow
                                        ? ccs.GetColumnTitle()
                                        : ccs.GetFirstCellText().Trim());
            }
            variable.Title = titleBuilder.ToString();
            // TODO: This origin is incorrect; it should include all the columns that were combined, and it doesn't.
            variable.Origin = cellSelection.ColumnSelections[0].GetWorksheetOrigin(mode, topRow > 0, originGroup);
            return new DataFrame(variable);
        }

        private static DataFrame ProcessCellArrayCategoriesPerColumn(CellSelection cellSelection, DataAcquisitionMode mode, int topRow, string[,] hold, int originGroup)
        {
            DataFrame frame = new DataFrame();

            // Modes 3, 4 or 6: categories per column
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                int totRows = ccs.RowCount;
                string[] foundwhat = new string[totRows];
                int[] nbin = new int[totRows];
                int found = 0;
                int size = 0;
                ClassifierVariable variable = new ClassifierVariable();
                frame.Variables.Add(variable);
                for (int r = topRow; r < totRows; r++)
                {
                    string pattern = hold[r, c];
                    // Ignore missing values
                    if (string.IsNullOrEmpty(pattern) || "*".Equals(pattern))
                        continue;

                    // Enumerate categories and put results in variable
                    bool wasFound = false;
                    for (int i = 0; i < found; i++)
                    {
                        if (pattern.Equals(foundwhat[i]))
                        {
                            wasFound = true;
                            size++;
                            variable.EnsureLength(size);
                            if (pattern.Contains(Formatting.MISSINGLABEL))
                                variable.Data[size - 1] = Constant.MISSING;
                            else
                                variable.Data[size - 1] = i;
                            nbin[i]++;
                            break;
                        }
                    }
                    if (!wasFound)
                    {
                        foundwhat[found] = pattern;
                        size++;
                        variable.EnsureLength(size);
                        if (pattern.Contains(Formatting.MISSINGLABEL))
                            variable.Data[size - 1] = Constant.MISSING;
                        else
                            variable.Data[size - 1] = found;
                        nbin[found] = 1;
                        found++;
                    }
                }

                // Fill in column details
                variable.EnsureGroups(found);
                for (int i = 0; i < found; i++)
                {
                    Group group = new Group(foundwhat[i], i);
                    group.NBin = nbin[i];
                    variable.Groups[i] = group;
                }

                // Fill in column title
                variable.Title = 0 == topRow
                                     ? ccs.GetColumnTitle()
                                     : ccs.GetFirstCellText().Trim();
                variable.Origin = ccs.GetWorksheetOrigin(mode, topRow > 0, originGroup);
            }
            return frame;
        }

        private static DataFrame ProcessCellArrayNumericReplaceMissing(CellSelection cellSelection, DataAcquisitionMode mode, int originGroup)
        {
            DataFrame frame = new DataFrame();

            // Read in the cells replacing missing data with Constant.MISSING
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                double[] values = ccs.GetDataValues();

                // If there's no data in the row (for example if it's hidden), ignore the row
                if (null == values)
                    continue;

                // Find the last row
                int lrow;
                for (lrow = values.GetUpperBound(0); lrow >= 0; lrow--)
                    if (values[lrow] != Constant.MISSING)
                        break;

                DoubleVariable variable = new DoubleVariable(lrow + 1, ccs.ColumnTitle);
                frame.Variables.Add(variable);

                variable.Data = new double[lrow + 1];
                for (int row = 0; row <= lrow; row++)
                {
                    double v = values[row];
                    if (Constant.MISSING * 10D == v)
                        v = Constant.MISSING;
                    variable.Data[row] = v;
                }
                variable.Origin = ccs.GetWorksheetOrigin(mode, ccs.TitleIsInData, originGroup);
            }
            return frame;
        }

        private static DataFrame ProcessCellArrayDateReplaceMissing(CellSelection cellSelection, DataAcquisitionMode mode, int originGroup)
        {
            DataFrame frame = new DataFrame();
            // Read in the cells replacing missing data with Constant.MISSING
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];

                DateTime[] values = ccs.GetDateValues();

                // Find the last row
                int lrow;
                for (lrow = values.GetUpperBound(0); lrow >= 0; lrow--)
                    if (values[lrow] != DateTime.MinValue)
                        break;

                int r = 0;
                GenericVariable<DateTime> variable = new DateVariable(lrow + 1, ccs.ColumnTitle);
                for (int row = 0; row <= lrow; row++)
                    variable.Data[r++] = values[row];
                variable.Origin = ccs.GetWorksheetOrigin(mode, ccs.TitleIsInData, originGroup);
                frame.Variables.Add(variable);
            }
            return frame;
        }

        private static DataFrame ProcessCellArrayTextWithFormulae(CellSelection cellSelection, DataAcquisitionMode mode, int originGroup)
        {
            DataFrame frame = new DataFrame();
            int longestColumnLengthSoFar = 0;
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                string[] formulae = ccs.GetFormulae();
                // Trim any hidden rows before returning the variable - optimised for the very common case that there aren't any
                if (ccs.NonHiddenRowCount != formulae.Length)
                {
                    string[] temp = new string[ccs.NonHiddenRowCount];
                    Array.Copy(formulae, temp, ccs.NonHiddenRowCount);
                    formulae = temp;
                }
                int lastNonBlank = formulae.Length;
                while (lastNonBlank > 0 && string.IsNullOrWhiteSpace(formulae[lastNonBlank - 1]))
                    --lastNonBlank;
                longestColumnLengthSoFar = Math.Max(longestColumnLengthSoFar, lastNonBlank);
                StringVariable variable = new StringVariable(formulae, null);
                IOrigin origin = ccs.GetWorksheetOrigin(mode, false, originGroup);
                variable.Origin = origin;
                frame.Variables.Add(variable);
            }
            foreach (IVariable variable in frame.Variables)
                variable.TruncateDataToLength(longestColumnLengthSoFar);
            return frame;
        }

        private static DataFrame ProcessCellArrayVariant(CellSelection cellSelection, DataAcquisitionMode mode, int originGroup)
        {
            DataFrame frame = new DataFrame();
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];

                object[,] raw = ccs.GetObjects();
                int nonHiddenDataRowCount = ccs.NonHiddenDataRowCount;
                // Avoid copying titles if they've been identified as titles.
                int offset = ccs.NonHiddenRowCount - ccs.NonHiddenDataRowCount;
                while (nonHiddenDataRowCount > 0 && null == raw[nonHiddenDataRowCount - 1 + offset, 0])
                    --nonHiddenDataRowCount;
                bool allNumeric = true;
                for (int i = 0; i < nonHiddenDataRowCount; i++)
                {
                    object victim = raw[i + offset, 0];
                    if (!(null == victim || victim is double))
                        allNumeric = false;
                }
                IVariable variable;
                if (allNumeric)
                {
                    double[] cooked = new double[nonHiddenDataRowCount];
                    Array.Copy(ccs.GetDataValues(), cooked, cooked.Length);
                    variable = new DoubleVariable(cooked, ccs.ColumnTitle);
                }
                else
                {
                    object[] cooked = new object[nonHiddenDataRowCount];
                    for (int i = 0; i < nonHiddenDataRowCount; i++)
                        cooked[i] = raw[i + offset, 0];
                    variable = new VariantVariable(cooked, ccs.ColumnTitle);
                }
                IOrigin origin = ccs.GetWorksheetOrigin(mode, false, originGroup);
                variable.Origin = origin;
                frame.Variables.Add(variable);
            }
            return frame;
        }

        private static DataFrame ProcessCellArrayTextNoTitles(CellSelection cellSelection, DataAcquisitionMode mode, int originGroup)
        {
            DataFrame frame = new DataFrame();
            int longestColumnLengthSoFar = 0;
            for (int c = 0; c < cellSelection.TotalColumns; c++)
            {
                CellColumnSelection ccs = cellSelection.ColumnSelections[c];
                string[] texts = ccs.GetTexts();
                // Trim any hidden rows before returning the variable - optimised for the very common case that there aren't any
                if (ccs.NonHiddenRowCount != texts.Length)
                {
                    string[] temp = new string[ccs.NonHiddenRowCount];
                    Array.Copy(texts, temp, ccs.NonHiddenRowCount);
                    texts = temp;
                }
                int lastNonBlank = texts.Length;
                while (lastNonBlank > 0 && string.IsNullOrWhiteSpace(texts[lastNonBlank - 1]))
                    --lastNonBlank;
                longestColumnLengthSoFar = Math.Max(longestColumnLengthSoFar, lastNonBlank);
                StringVariable variable = new StringVariable(texts, null)
                {
                    Origin = ccs.GetWorksheetOrigin(mode, false, originGroup)
                };
                frame.Variables.Add(variable);
            }
            foreach (IVariable variable in frame.Variables)
                variable.TruncateDataToLength(longestColumnLengthSoFar);
            return frame;
        }

        private static bool TopRowIsFormattedLikeTitles(CellSelection cellSelection)
        {
            foreach (CellColumnSelection probe in cellSelection.ColumnSelections)
                if (!probe.TopIsFormattedLikeATitle())
                    return false;
            return true;
        }
    }
}
