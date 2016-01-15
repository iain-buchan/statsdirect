using SpreadsheetGear;
using SpreadsheetGear.Windows.Forms;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// Visitor pattern: extracts values for parameters that have been rendered by InlineParameterPreparer.
    /// </summary>
    internal class InlineParameterValueExtractor : IParameterVisitor
    {
        public ParameterBag Context { get; set; }
        public Control Control { get; set; }
        public bool DoValidation { get; set; }
        public Control FailedValidationControl { get; private set; }
        public ParameterBag OutputParameters { get; set; }
        public ITemplateProcessor Processor { get; set; }

        public void Visit(ConfidenceIntervalParameter parameter)
        {
            ComboBox cbo = (ComboBox)Control;
            string raw = cbo.Text.Trim();
            if (DoValidation)
            {
                Control.BackColor = System.Drawing.SystemColors.Window;
                // Missing or zero-length?
                if (raw.Length == 0)
                {
                    // If the parameter should be filled in, this is an error
                    if (null == parameter.CancelSkipsParameter)
                    {
                        FailedValidationControl = cbo;
                        return;
                    }
                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                    OutputParameters[parameter.Name] = null;
                    return;
                }
            }
            double value = Parsing.Cdbl_Txt(raw);
            // Turn from percentage to fraction
            if (value != Constant.MISSING)
                value /= 100.0;
            // In range?
            if (value < 0.0 || value > 1.0)
            {
                FailedValidationControl = cbo;
                return;
            }
            // If we get here, it's OK.
            OutputParameters[parameter.Name] = new FilledParameter(true, value);
        }

        public void Visit(Double2By2Parameter parameter)
        {
            Double2By2Parameter parm = parameter;
            TableLayoutPanel panel2By2 = (TableLayoutPanel)Control;
            TextBox txtTL = (TextBox)panel2By2.Controls["txtTL"];
            TextBox txtTR = (TextBox)panel2By2.Controls["txtTR"];
            TextBox txtBL = (TextBox)panel2By2.Controls["txtBL"];
            TextBox txtBR = (TextBox)panel2By2.Controls["txtBR"];

            double tl = Parsing.Cdbl_Txt(txtTL.Text);
            double tr = Parsing.Cdbl_Txt(txtTR.Text);
            double bl = Parsing.Cdbl_Txt(txtBL.Text);
            double br = Parsing.Cdbl_Txt(txtBR.Text);

            if (DoValidation)
            {
                txtTL.BackColor = System.Drawing.SystemColors.Window;
                txtTR.BackColor = System.Drawing.SystemColors.Window;
                txtBL.BackColor = System.Drawing.SystemColors.Window;
                txtBR.BackColor = System.Drawing.SystemColors.Window;
                // Validate
                if (tl == Constant.MISSING || tl < 0)
                {
                    txtTL.SelectAll();
                    txtTL.Focus();
                    FailedValidationControl = txtTL;
                    return;
                }
                if (tr == Constant.MISSING || tr < 0)
                {
                    txtTR.SelectAll();
                    txtTR.Focus();
                    FailedValidationControl = txtTR;
                    return;
                }
                if (bl == Constant.MISSING || bl < 0)
                {
                    txtBL.SelectAll();
                    txtBL.Focus();
                    FailedValidationControl = txtBL;
                    return;
                }
                if (br == Constant.MISSING || br < 0)
                {
                    txtBR.SelectAll();
                    txtBR.Focus();
                    FailedValidationControl = txtBR;
                    return;
                }
            }

            if (tl != Constant.MISSING)
                OutputParameters[parm.TopLeftName] = new FilledParameter(true, tl);
            if (tr != Constant.MISSING)
                OutputParameters[parm.TopRightName] = new FilledParameter(true, tr);
            if (bl != Constant.MISSING)
                OutputParameters[parm.BottomLeftName] = new FilledParameter(true, bl);
            if (br != Constant.MISSING)
                OutputParameters[parm.BottomRightName] = new FilledParameter(true, br);
        }

        public void Visit(DoubleParameter parameter)
        {
            TextBox txt = (TextBox)Control;
            string raw = txt.Text.Trim();
            bool isMissing = string.IsNullOrWhiteSpace(raw);
            if (DoValidation)
            {
                Control.BackColor = System.Drawing.SystemColors.Window;
                // Missing or zero-length?
                if (isMissing)
                {
                    // If the parameter should be filled in, this is an error
                    if (null == parameter.CancelSkipsParameter)
                    {
                        FailedValidationControl = txt;
                        return;
                    }
                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                    OutputParameters[parameter.Name] = null;
                    return;
                }
            }
            double value = Parsing.Cdbl_Txt(raw);
            if (DoValidation)
            {
                // In range?
                DoubleParameter dp = parameter;
                double minimumValue = dp.MinimumValue(Processor, Context);
                double maximumValue = dp.MaximumValue(Processor, Context);
                if (value < minimumValue || value > maximumValue)
                {
                    FailedValidationControl = txt;
                    return;
                }
            }
            // If we get here, it's OK.
            OutputParameters[parameter.Name] = isMissing ? null : new FilledParameter(true, value);
        }

        public void Visit(FillableParameter parameter)
        {
            if (Control is IOkable)
            {
                IOkable okable = (IOkable)Control;
                okable.OkClicked();
            }
            else if (Control is IFillParameterBag)
            {
                FailedValidationControl = ((IFillParameterBag)Control).Fill(OutputParameters, true);
            }
            else
                throw new ArgumentOutOfRangeException("control", "Couldn't request a custom parameter to fill itself in");
        }

        public void Visit(GridParameter parameter)
        {
            WorkbookView grid = (WorkbookView)Control;
            IWorksheet sheet = grid.ActiveWorksheet;
            // TODO: Force end edit if one is current
            grid.ActiveWorkbookSet.GetLock();
            IRange usedRange = sheet.UsedRange;
            DataFrame frame = new DataFrame();
            for (int col = 0; col < usedRange.ColumnCount; col++)
            {
                DoubleVariable v = new DoubleVariable(usedRange.RowCount, string.Empty);
                frame.Variables.Add(v);
                for (int row = 0; row < usedRange.RowCount; row++)
                {
                    object rawValue = usedRange.Cells[row, col].Value;
                    double parsedValue = frmSpreadsheetGear.ToCellValue(rawValue);
                    v.Data[row] = parsedValue;
                }
            }
            grid.ActiveWorkbookSet.ReleaseLock();

            if (DoValidation)
            {
                // TODO: Validate
            }

            // If we get here, it's valid.
            OutputParameters[parameter.Name] = new FilledParameter(true, frame);
        }

        public void Visit(IntegerParameter parameter)
        {
            TextBox txt = (TextBox)Control;
            string raw = txt.Text.Trim();
            if (DoValidation)
            {
                txt.BackColor = System.Drawing.SystemColors.Window;
                // Missing or zero-length?
                if (raw.Length == 0)
                {
                    // If the parameter should be filled in, this is an error
                    if (null == parameter.CancelSkipsParameter)
                    {
                        FailedValidationControl = txt;
                        return;
                    }
                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                    OutputParameters[parameter.Name] = null;
                    return;
                }
            }
            int value = Parsing.Cint_Txt(raw);
            if (DoValidation)
            {
                // In range?
                IntegerParameter ip = parameter;
                if (value < ip.MinimumValue || value > ip.MaximumValue)
                {
                    FailedValidationControl = txt;
                    return;
                }
            }
            // If we get here, it's OK.
            OutputParameters[parameter.Name] = new FilledParameter(true, value);
        }

        public void Visit(OptionsParameter parameter)
        {
            TableLayoutPanel optionsPanel = (TableLayoutPanel)Control;
            foreach (Control c in optionsPanel.Controls)
            {
                CheckBox chk = (CheckBox)c;
                OptionsOption oo = (OptionsOption)chk.Tag;
                OutputParameters[oo.Name] = new FilledParameter(true, chk.Checked);
            }
        }

        public void Visit(PickVariablesParameter parameter)
        {
            TableLayoutPanel pickPanel = (TableLayoutPanel)Control;
            int variables = pickPanel.RowCount;
            int[] ary = new int[variables];
            for (int v = 0; v < variables; v++)
            {
                ComboBox cbo = (ComboBox)pickPanel.Controls[2 * v];
                ary[v] = cbo.SelectedIndex;
            }
            OutputParameters[parameter.Name] = new FilledParameter(true, ary);
        }

        public void Visit(StringParameter parameter)
        {
            TextBox txt = (TextBox)Control;
            OutputParameters[parameter.Name] = new FilledParameter(true, txt.Text);
        }

        public void Visit(SpecialParameter parameter)
        {
            SpecialParameter specialParameter = parameter;
            if ("chi-2-column".Equals(specialParameter.SpecialType)
                || "likelihood".Equals(specialParameter.SpecialType)
                || "rr-index".Equals(specialParameter.SpecialType)
                || "chi-3-column".Equals(specialParameter.SpecialType)
                || "person-time-size".Equals(specialParameter.SpecialType))
            {
                TableLayoutPanel ssgContainer = (TableLayoutPanel)Control;
                WorkbookView grid = (WorkbookView)ssgContainer.GetControlFromPosition(1, 1);
                IWorksheet worksheet = grid.ActiveWorksheet;
                grid.GetLock();
                object value;
                try
                {
                    value = worksheet.UsedRange.Value;
                    if (null != value && !value.GetType().IsArray)
                        value = new[,] { { value } };
                }
                finally
                {
                    grid.ReleaseLock();
                }

                if (null != value)
                {
                    object[,] ary = (object[,])value;

                    DataFrame frame = new DataFrame();
                    for (int col = ary.GetLowerBound(1); col <= ary.GetUpperBound(1); col++)
                    {
                        DoubleVariable dv = new DoubleVariable(ary.GetUpperBound(0) - ary.GetLowerBound(0) + 1, "Column " + (col + 1).ToString());
                        for (int row = ary.GetLowerBound(0); row <= ary.GetUpperBound(0); row++)
                        {
                            dv.Data[row] = frmSpreadsheetGear.ToCellValue(ary[row, col]);
                        }
                        frame.Variables.Add(dv);
                    }
                    OutputParameters[parameter.Name] = new FilledParameter(true, frame);
                }
                return;
            }
            if ("raters-2d".Equals(specialParameter.SpecialType))
            {
                TableLayoutPanel ssgContainer = (TableLayoutPanel)Control;
                WorkbookView grid = (WorkbookView)ssgContainer.GetControlFromPosition(1, 1);
                IWorksheet worksheet = grid.ActiveWorksheet;
                grid.GetLock();
                object value;
                try
                {
                    value = worksheet.UsedRange.Value;
                    if (null != value && !value.GetType().IsArray)
                        value = new[,] { { value } };
                }
                finally
                {
                    grid.ReleaseLock();
                }
                if (null != value)
                {
                    object[,] ary = (object[,])value;

                    DataFrame frame = new DataFrame();
                    for (int col = ary.GetLowerBound(1); col <= ary.GetUpperBound(1); col++)
                    {
                        DoubleVariable dv = new DoubleVariable(ary.GetUpperBound(0) - ary.GetLowerBound(0) + 1, "R2(" + col.ToString() + ")");
                        for (int row = ary.GetLowerBound(0); row <= ary.GetUpperBound(0); row++)
                        {
                            dv.Data[row] = frmSpreadsheetGear.ToCellValue(ary[row, col]);
                        }
                        frame.Variables.Add(dv);
                    }
                    OutputParameters[parameter.Name] = new FilledParameter(true, frame);
                }
                return;
            }
            if ("report".Equals(specialParameter.SpecialType)
                || "frame".Equals(specialParameter.SpecialType)
                || "dummyVariables".Equals(specialParameter.SpecialType)
                || "scores".Equals(specialParameter.SpecialType)
                || "textToNumbers".Equals(specialParameter.SpecialType))
            {
                IFillParameterBag ifpb = (IFillParameterBag)Control;
                FailedValidationControl = ifpb.Fill(OutputParameters, DoValidation);
                return;
            }
            throw new Exception("specialParameter.SpecialType: Unknown value");
        }

        public void Visit(PickFromListParameter parameter)
        {
            PickFromListParameter p = (PickFromListParameter)Control.Tag;
            if (p.AllowMultiple)
            {
                int offset = p.IncludeNoneEntry ? 1 : 0;
                ListBox lstPickFromList = (ListBox)Control;
                bool[] selected = new bool[lstPickFromList.Items.Count - offset];
                foreach (int i in lstPickFromList.SelectedIndices)
                    selected[i - offset] = true;
                OutputParameters[parameter.Name] = new FilledParameter(true, selected);
            }
            else
            {
                int offset = p.IncludeNoneEntry ? 1 : 0;
                ComboBox cbo = (ComboBox)Control;
                bool[] selected = new bool[cbo.Items.Count - offset];
                if (cbo.SelectedIndex >= offset)
                    selected[cbo.SelectedIndex - offset] = true;
                if ((!p.IncludeNoneEntry) || cbo.SelectedIndex > 0)
                    OutputParameters[parameter.Name] = new FilledParameter(true, selected);
            }
        }

        public void Visit(OptionParameter parameter)
        {
            OptionParameter optionParameter = parameter;
            switch (optionParameter.OptionFormatType)
            {
                case OptionFormatType.Dropdown:
                    {
                        ComboBox cbo = (ComboBox)Control;
                        OptionOption selectedOption = (OptionOption)cbo.SelectedItem;
                        OutputParameters[parameter.Name] = new FilledParameter(true, selectedOption.Value);
                    }
                    break;
                case OptionFormatType.Radio:
                    {
                        Control maybeGroup = Control;
                        if (maybeGroup is GroupBox)
                            maybeGroup = maybeGroup.Controls[0];
                        TableLayoutPanel optionPanel = (TableLayoutPanel)maybeGroup;
                        bool atLeastOneChecked = false;
                        foreach (Control c in optionPanel.Controls)
                        {
                            RadioButton rad = (RadioButton)c;
                            if (rad.Checked)
                            {
                                OutputParameters[parameter.Name] = new FilledParameter(true, rad.Tag);
                                atLeastOneChecked = true;
                                break;
                            }
                        }
                        FailedValidationControl = ((null != optionParameter.CancelSkipsParameter) || atLeastOneChecked || !DoValidation) ? null : optionPanel.Controls[0];
                    }
                    break;
                default:
                    throw new Exception("optionParameter.OptionFormatType: Only Dropdown and Radio are known");
            }
        }

        public void Visit(GroupedCovarianceParameter parameter)
        {
            throw new Exception("Unknown parameter type when parsing results");
        }

        public void Visit(Grid2DParameter parameter)
        {
            throw new Exception("Unknown parameter type when parsing results");
        }

        public void Visit(EditGridParameter parameter)
        {
            DataGridView gridEditGrid = (DataGridView)Control;
            string[] data = new string[gridEditGrid.Rows.Count];
            for (int i = 0; i < gridEditGrid.Rows.Count; i++)
                data[i] = (string)gridEditGrid.Rows[i].Cells[1].Value;
            StringVariable newValues = new StringVariable(data);
            newValues.Title = parameter.ValueVariable;
            DataFrame oldFrame = Context[parameter.Source].AsDataFrame;
            DataFrame newFrame = new DataFrame();
            foreach (Variable v in oldFrame.Variables)
                newFrame.Variables.Add(parameter.ValueVariable.Equals(v.Title) ? newValues : v);
            OutputParameters[parameter.Name] = new FilledParameter(true, newFrame);
        }

        public void Visit(Double2By2ByKParameter parameter)
        {
            TableLayoutPanel pnl2By2ByK = (TableLayoutPanel)Control;
            FlowLayoutPanel pnlNavigation = (FlowLayoutPanel)pnl2By2ByK.GetControlFromPosition(0, 4);
            Button cmdPrevious = (Button)pnlNavigation.Controls[0];
            Label lblStratum = (Label)pnlNavigation.Controls[1];
            Button cmdNext = (Button)pnlNavigation.Controls[2];

            TextBox txtTl = (TextBox)pnl2By2ByK.GetControlFromPosition(0, 2);
            TextBox txtTr = (TextBox)pnl2By2ByK.GetControlFromPosition(1, 2);
            TextBox txtBl = (TextBox)pnl2By2ByK.GetControlFromPosition(0, 3);
            TextBox txtBr = (TextBox)pnl2By2ByK.GetControlFromPosition(1, 3);

            int stratum = (int)lblStratum.Tag;
            List<double>[] newData = (List<double>[])pnlNavigation.Tag;
            List<double> var1Data = newData[0];
            List<double> var2Data = newData[1];

            // Fill the stored data from the text boxes
            int offset = (stratum - 1) * 2;
            double tl = Parsing.Cdbl_Txt(txtTl.Text);
            double tr = Parsing.Cdbl_Txt(txtTr.Text);
            double bl = Parsing.Cdbl_Txt(txtBl.Text);
            double br = Parsing.Cdbl_Txt(txtBr.Text);

            if (DoValidation)
            {
                txtTl.BackColor = System.Drawing.SystemColors.Window;
                txtTr.BackColor = System.Drawing.SystemColors.Window;
                txtBl.BackColor = System.Drawing.SystemColors.Window;
                txtBr.BackColor = System.Drawing.SystemColors.Window;
                // Validate - find the first missing value in the current stratum
                if (tl == Constant.MISSING || tl < 0)
                {
                    txtTl.SelectAll();
                    txtTl.Focus();
                    FailedValidationControl = txtTl;
                    return;
                }
                if (tr == Constant.MISSING || tr < 0)
                {
                    txtTr.SelectAll();
                    txtTr.Focus();
                    FailedValidationControl = txtTr;
                    return;
                }
                if (bl == Constant.MISSING || bl < 0)
                {
                    txtBl.SelectAll();
                    txtBl.Focus();
                    FailedValidationControl = txtBl;
                    return;
                }
                if (br == Constant.MISSING || br < 0)
                {
                    txtBr.SelectAll();
                    txtBr.Focus();
                    FailedValidationControl = txtBr;
                    return;
                }
            }
            while (var1Data.Count < stratum * 2)
            {
                var1Data.Add(Constant.MISSING);
                var2Data.Add(Constant.MISSING);
            }
            var1Data[offset] = tl;
            var2Data[offset] = tr;
            var1Data[offset + 1] = bl;
            var2Data[offset + 1] = br;

            if (DoValidation)
            {
                // Validate - find the first missing value in *any* stratum, set up and highlight
                for (int i = 0; i < var1Data.Count; i++)
                {
                    if (var1Data[i] == Constant.MISSING || var2Data[i] == Constant.MISSING)
                    {
                        int failedStratum = i / 2; // Deliberately truncate - if i = 3, it's stratum (3/2) = 1.
                        tl = var1Data[failedStratum * 2];
                        tr = var2Data[failedStratum * 2];
                        bl = var1Data[failedStratum * 2 + 1];
                        br = var2Data[failedStratum * 2 + 1];
                        txtTl.Text = Formatting.XUnrounded(tl);
                        txtTr.Text = Formatting.XUnrounded(tr);
                        txtBl.Text = Formatting.XUnrounded(bl);
                        txtBr.Text = Formatting.XUnrounded(br);
                        lblStratum.Text = "Stratum " + (failedStratum + 1) + " of " + (var1Data.Count / 2);
                        lblStratum.Tag = failedStratum + 1;
                        cmdPrevious.Enabled = failedStratum > 0;
                        cmdNext.Enabled = true;
                        if (tl == Constant.MISSING || tl < 0)
                        {
                            txtTl.SelectAll();
                            txtTl.Focus();
                            FailedValidationControl = txtTl;
                            return;
                        }
                        if (tr == Constant.MISSING || tr < 0)
                        {
                            txtTr.SelectAll();
                            txtTr.Focus();
                            FailedValidationControl = txtTr;
                            return;
                        }
                        if (bl == Constant.MISSING || bl < 0)
                        {
                            txtBl.SelectAll();
                            txtBl.Focus();
                            FailedValidationControl = txtBl;
                            return;
                        }
                        if (br == Constant.MISSING || br < 0)
                        {
                            txtBr.SelectAll();
                            txtBr.Focus();
                            FailedValidationControl = txtBr;
                            return;
                        }
                    }
                }
            }

            DataFrame frame = new DataFrame();
            DoubleVariable var1 = new DoubleVariable(var1Data.ToArray());
            DoubleVariable var2 = new DoubleVariable(var2Data.ToArray());
            frame.Variables.Add(var1);
            frame.Variables.Add(var2);
            OutputParameters[parameter.Name] = new FilledParameter(true, frame);
        }

        public void Visit(DateParameter parameter)
        {
            TextBox txt = (TextBox)Control;
            string raw = txt.Text.Trim();
            if (DoValidation)
            {
                Control.BackColor = System.Drawing.SystemColors.Window;
                // Missing or zero-length?
                if (raw.Length == 0)
                {
                    // If the parameter should be filled in, this is an error
                    if (null == parameter.CancelSkipsParameter)
                    {
                        FailedValidationControl = txt;
                        return;
                    }
                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                    OutputParameters[parameter.Name] = null;
                    return;
                }
            }
            DateTime value = Parsing.Cdate_Txt(raw);
            OutputParameters[parameter.Name] = new FilledParameter(true, value);
        }

        public void Visit(ChartOptionsParameter parameter)
        {
            if (Control is IOkable)
            {
                IOkable okable = (IOkable)Control;
                okable.OkClicked();
            }
            else if (Control is IFillParameterBag)
            {
                FailedValidationControl = ((IFillParameterBag)Control).Fill(OutputParameters, true);
            }
            else
                throw new ArgumentOutOfRangeException("control", "Couldn't request a custom parameter to fill itself in");
        }

        public void Visit(BooleanParameter parameter)
        {
            CheckBox cb = (CheckBox)Control;
            bool value = cb.Checked;
            OutputParameters[parameter.Name] = new FilledParameter(true, value);
        }
    }
}
