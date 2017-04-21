using SpreadsheetGear;
using SpreadsheetGear.Advanced.Cells;
using SpreadsheetGear.Windows.Forms;
using StatsDirect.Builtins;
using StatsDirect.Charting;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    /// <summary>
    /// Visitor pattern: Renders inline controls (in frmMain's pnlOperations) in such a way that the user can fill them in and values can be extracted by InlineParameterValueExtractor.
    /// </summary>
    internal class InlineParameterPreparer : IParameterVisitor
    {
        public ParameterBag Context { get; set; }
        public frmMain Form { get; set; }
        public ITemplateHost Host { get; set; }
        public ITemplateProcessor Processor { get; set; }
        public FilledParameter FilledParameter { get; private set; }

        public void Visit(ConfidenceIntervalParameter parameter)
        {
            if (parameter.CanDefault && SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
            {
                FilledParameter = new FilledParameter(FilledParameterDirection.Input, SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval);
                return;
            }

            // If this is a "standard" CI and the dedicated CI combo isn't in use, use it.  Otherwise, create one in the flow.
            ComboBox cbo;
            bool useSingle = parameter.CanUseStandard && null == Form.IntegratedConfidenceIntervalControl.Tag && parameter.MinimumSuggestedValue == 0.9 && parameter.MaximumSuggestedValue == 0.99;
            if (useSingle)
            {
                cbo = Form.IntegratedConfidenceIntervalControl;
                Form.IntegratedConfidenceIntervalControlVisible = true;
            }
            else
            {
                TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
                cbo = new ComboBox { Size = new Size(55, 18), FormattingEnabled = true };
                AddAppropriateEventHandlersTo(cbo);
                tlp.Controls.Add(cbo);
                MaybeAddHelpTip(cbo, parameter);

                Label lbl = new Label
                {
                    Tag = parameter,
                    Padding = new Padding(0, 6, 0, 3),
                    AutoSize = true,
                    Text = parameter.Prompt(Processor, Context, "Confidence (%)")
                };
                tlp.Controls.Add(lbl);
                MaybeAddHelpTip(lbl, parameter);
            }

            cbo.Tag = parameter;
            cbo.Items.Clear();
            for (int multiplier = 0; multiplier < 500; multiplier++)
            {
                double suggestedValue = parameter.MinimumSuggestedValue + multiplier * parameter.SuggestedStep;
                if (suggestedValue > parameter.MaximumSuggestedValue)
                    break;
                cbo.Items.Add((suggestedValue * 100.0).ToString("##0.0"));
            }

            // If there's a specific default CI, force it.  If not, don't overwrite the CI combo's value, so that a user can persist CI values between operations.
            if (Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsDouble)
            {
                cbo.Text = (Context[parameter.Name].AsDouble * 100.0).ToString("##0.0");
            }
            else
            {
                double? defaultValue = parameter.DefaultValue(Processor, Context);

                if (defaultValue.HasValue && 0.0 != defaultValue.Value)
                    cbo.Text = (defaultValue.Value * 100.0).ToString("##0.0");
                else
                {
                    // Don't force a CI if there's already one set on the singleton
                    if (!useSingle || string.IsNullOrEmpty(Form.IntegratedConfidenceIntervalControl.Text))
                        cbo.Text = SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval ? (SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval * 100.0).ToString("##0") : "95";
                }
            }
            cbo.AutoSizeToList();
        }

        public void Visit(Double2By2Parameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            TableLayoutPanel panel2By2 = new TableLayoutPanel { Tag = parameter, RowCount = 4, ColumnCount = 3, AutoSize = true };
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label lblColumnsPrompt = new Label
            {
                Padding = new Padding(3, 3, 3, 3),
                AutoSize = true,
                Text = parameter.ColumnsPrompt
            };
            panel2By2.Controls.Add(lblColumnsPrompt, 0, 0);
            panel2By2.SetColumnSpan(lblColumnsPrompt, 3);

            Label lblLeftColumnPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.LeftColumnPrompt
            };
            panel2By2.Controls.Add(lblLeftColumnPrompt, 0, 1);

            Label lblRightColumnPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.RightColumnPrompt
            };
            panel2By2.Controls.Add(lblRightColumnPrompt, 1, 1);

            Label lblRowsPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.RowsPrompt
            };
            panel2By2.Controls.Add(lblRowsPrompt, 2, 1);

            TextBox txtTL = new TextBox { Name = "txtTL", Size = new Size(100, 18) };
            if (Context.ContainsKey(parameter.TopLeftName) && null != Context[parameter.TopLeftName] && Context[parameter.TopLeftName].IsInputParameter && Context[parameter.TopLeftName].IsDouble)
                txtTL.Text = Context[parameter.TopLeftName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtTL);
            panel2By2.Controls.Add(txtTL, 0, 2);

            TextBox txtTR = new TextBox { Name = "txtTR", Size = new Size(100, 18) };
            if (Context.ContainsKey(parameter.TopRightName) && null != Context[parameter.TopRightName] && Context[parameter.TopRightName].IsInputParameter && Context[parameter.TopRightName].IsDouble)
                txtTR.Text = Context[parameter.TopRightName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtTR);
            panel2By2.Controls.Add(txtTR, 1, 2);

            Label lblTopRowPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.TopRowPrompt
            };
            panel2By2.Controls.Add(lblTopRowPrompt, 2, 2);

            TextBox txtBL = new TextBox { Name = "txtBL", Size = new Size(100, 18) };
            if (Context.ContainsKey(parameter.BottomLeftName) && null != Context[parameter.BottomLeftName] && Context[parameter.BottomLeftName].IsInputParameter && Context[parameter.BottomLeftName].IsDouble)
                txtBL.Text = Context[parameter.BottomLeftName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtBL);
            panel2By2.Controls.Add(txtBL, 0, 3);

            TextBox txtBR = new TextBox { Name = "txtBR", Size = new Size(100, 18) };
            if (Context.ContainsKey(parameter.BottomRightName) && null != Context[parameter.BottomRightName] && Context[parameter.BottomRightName].IsInputParameter && Context[parameter.BottomRightName].IsDouble)
                txtBR.Text = Context[parameter.BottomRightName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtBR);
            panel2By2.Controls.Add(txtBR, 1, 3);

            Label lblBottomRowPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = parameter.BottomRowPrompt
            };
            panel2By2.Controls.Add(lblBottomRowPrompt, 2, 3);

            tlp.Controls.Add(panel2By2);
            tlp.SetColumnSpan(panel2By2, 2);
        }

        public void Visit(DoubleParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(100, 18), Tag = parameter };
            if (!parameter.ForceDefault && Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsDouble)
            {
                double defaultValue = Context[parameter.Name].AsDouble;
                if (!double.IsNaN(defaultValue) && defaultValue != Constant.MISSING)
                    txt.Text = Context[parameter.Name].AsDouble.ToString();
            }
            else
            {
                double? defaultValue = parameter.DefaultValue(Processor, Context);
                string defaultValueString = string.Empty;
                if (defaultValue.HasValue && !double.IsNaN(defaultValue.Value) && defaultValue.Value != Constant.MISSING)
                    defaultValueString = defaultValue.Value.ToString();
                txt.Text = defaultValueString;
            }
            AddAppropriateEventHandlersTo(txt);

            string suffix = string.Empty;
            if (parameter.ShowLimits)
            {
                double minimumValue = parameter.MinimumValue(Processor, Context);
                double maximumValue = parameter.MaximumValue(Processor, Context);
                if (minimumValue > double.MinValue)
                {
                    if (maximumValue < double.MaxValue)
                        suffix = " (" + minimumValue.ToString() + " to " + maximumValue.ToString() + ")";
                    else
                        suffix = " (>= " + minimumValue.ToString() + ")";
                }
                else if (maximumValue < double.MaxValue)
                {
                    suffix = " (<= " + maximumValue.ToString() + ")";
                }
            }

            Label lbl = new Label { Tag = parameter, Padding = new Padding(0, 6, 0, 3), AutoSize = true, Text = parameter.Prompt(Processor, Context, string.Empty) + suffix };

            MaybeAddHelpTip(lbl, parameter);
            MaybeAddHelpTip(txt, parameter);

            if (parameter.PromptPrecedesParameter)
            {
                tlp.Controls.Add(lbl);
                tlp.Controls.Add(txt);
            }
            else
            {
                tlp.Controls.Add(txt);
                tlp.Controls.Add(lbl);
            }
        }

        public void Visit(FillableParameter parameter)
        {
            IFillable fillable = parameter.Fillable;
            string fillerToUse = fillable.FillerToUse;
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            Control ctl;
            switch (fillerToUse)
            {
                case "ChiSquareGoodnessOfFit":
                    ctl = new ctlChiGFOptions((ChiSquareGoodnessOfFitOptions)fillable);
                    break;
                case "ConvertUnits":
                    ctl = new ctlConvertUnits();
                    break;
                case "Distribution":
                    ctl = new ctlPDF((DistributionOptions)fillable, Host);
                    break;
                case "Dummy":
                    ctl = new ctlDummyOptions((DummyOptions)fillable);
                    break;
                case "Extraction":
                    ExtractionOptions f = (ExtractionOptions)fillable;
                    if (null == f.IdentifiersFrame)
                        ctl = new ctlFindAndReplaceData(f);
                    else
                        ctl = new ctlExtraction(f);
                    break;
                case "GraphicsOptions":
                    ctl = new ctlGraphicsOptions();
                    break;
                case "ROCCutoff":
                    {
                        ROCCutoff rc = (ROCCutoff)fillable;
                        ctl = new ctlROCCutoff(rc.SeriesRecord, rc.Weight, rc.Title);
                        break;
                    }
                case "SortInPlace":
                    // HACK: Break layering completely
                    frmSpreadsheetGear gearForm = (frmSpreadsheetGear)Form.ActiveMdiChild;
                    if (null == gearForm)
                        return;
                    IRange range = gearForm.workbookView.RangeSelection.Areas[0];
                    ctl = new ctlSort(range, gearForm.workbookView);
                    break;
                case "Scores":
                    ctl = new ctlScores((ScoresOptions)fillable);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(parameter), fillable.FillerToUse, "fillableParameter.Fillable.FillerToUse: Unknown option");
            }
            ctl.Tag = parameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
        }

        /// <summary>
        /// If this is called, we know we're acquiring "screen" data in the dialog area rather than data from a loaded worksheet.
        /// </summary>
        public void Visit(FrameParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            WorkbookView grid = new WorkbookView
            {
                Tag = parameter,
                Name = "grid",
                Size = new Size((int)(494 * Form.currentScaleFactor.Width), (int)(305 * Form.currentScaleFactor.Height)),
                ContextMenuStrip = Form.InlineGridContextMenuStrip
            };
            grid.ActiveWorkbookSet.GetLock();
            if (Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsDataFrame)
            {
                IWorksheet sheet = grid.ActiveWorksheet;
                IRange usedRange = sheet.UsedRange;
                DataFrame frame = Context[parameter.Name].AsDataFrame;
                for (int col = 0; col < frame.VariableCount; col++)
                {
                    DoubleVariable v = (DoubleVariable) frame.Variables[col];
                    for (int row = 0; row < v.Length; row++)
                        usedRange.Cells[row, col].Value = v.Data[row];
                }
            }
            grid.ActiveWorksheet.WindowInfo.Zoom = 88; // percent
            int maximumColumns = parameter.MaximumColumns(Processor, Context);
            if (maximumColumns > 0)
                grid.ActiveWorksheet.Cells[0, maximumColumns, 0, grid.ActiveWorksheet.Cells.ColumnCount - 1].EntireColumn.Hidden = true;
            grid.ActiveWorkbook.WindowInfo.DisplayWorkbookTabs = false;
            grid.ActiveWorkbookSet.ReleaseLock();
            grid.AllowChartExplorer = false;
            grid.AllowRangeExplorer = false;
            grid.AllowShapeExplorer = false;
            grid.AllowWorkbookDesigner = false;
            grid.AllowWorkbookExplorer = false;
            tlp.Controls.Add(grid);

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.Prompt(Processor, Context, string.Empty)
            };
            tlp.Controls.Add(lbl);
        }

        public void Visit(IntegerParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(100, 18), Tag = parameter };
            if (!parameter.ForceDefault && Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsInt32)
            {
                txt.Text = Context[parameter.Name].AsInt32.ToString();
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    int? defaultValue = parameter.DefaultValue(Processor, Context);
                    if (defaultValue.HasValue)
                        txt.Text = defaultValue.Value.ToString();
                }
            }
            AddAppropriateEventHandlersTo(txt);

            string suffix = string.Empty;
            if (parameter.ShowLimits)
            {
                int minimumValue = parameter.MinimumValue;
                int maximumValue = parameter.MaximumValue;
                if (minimumValue > int.MinValue)
                {
                    if (maximumValue < int.MaxValue)
                        suffix = " (" + minimumValue.ToString() + " to " + maximumValue.ToString() + ")";
                    else
                        suffix = " (>= " + minimumValue.ToString() + ")";
                }
                else if (maximumValue < int.MaxValue)
                {
                    suffix = " (<= " + maximumValue.ToString() + ")";
                }
            }

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.Prompt(Processor, Context, string.Empty) + suffix
            };

            MaybeAddHelpTip(lbl, parameter);
            MaybeAddHelpTip(txt, parameter);

            if (parameter.PromptPrecedesParameter)
            {
                tlp.Controls.Add(lbl);
                tlp.Controls.Add(txt);
            }
            else
            {
                tlp.Controls.Add(txt);
                tlp.Controls.Add(lbl);
            }
        }

        public void Visit(OptionsParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);

            string prompt = parameter.Prompt(Processor, Context);
            if (!string.IsNullOrEmpty(prompt))
            {
                Label lbl = new Label
                {
                    Tag = parameter,
                    Padding = new Padding(0, 6, 0, 3),
                    AutoSize = true,
                    MaximumSize = new Size(500, 500),
                    Text = prompt
                };
                tlp.Controls.Add(lbl);
                tlp.SetColumnSpan(lbl, 2);
            }

            TableLayoutPanel panelOptions = new TableLayoutPanel
            {
                Tag = parameter,
                RowCount = (parameter.Options.Count + 1) / 2,
                ColumnCount = parameter.Columns,
                AutoSize = true
            };
            for (int column = 0; column < parameter.Columns; column++)
            {
                panelOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }

            foreach (OptionsOption optionsOption in parameter.Options)
            {
                bool isChecked = optionsOption.Selected;
                if (Context.ContainsKey(optionsOption.Name) && null != Context[optionsOption.Name] && Context[optionsOption.Name].IsInputParameter)
                    isChecked = Context[optionsOption.Name].AsBoolean;

                CheckBox chk = new CheckBox
                {
                    AutoSize = true,
                    Text = optionsOption.Label,
                    Checked = isChecked,
                    Tag = optionsOption,
                    UseVisualStyleBackColor = true
                };
                panelOptions.Controls.Add(chk);
            }

            tlp.Controls.Add(panelOptions);
            tlp.SetColumnSpan(panelOptions, 2);
        }

        public void Visit(PickVariablesParameter parameter)
        {
            DataFrame frame = Context[parameter.ParameterName].AsDataFrame;
            int[] initialState = null;
            if (parameter.PreSelectVariables)
            {
                // Set up at least the minimum variables
                int variableCount = Math.Min(frame.VariableCount, parameter.MinimumVariables);
                initialState = new int[variableCount];
                for (int i = 0; i < initialState.Length; i++)
                    initialState[i] = i;
            }
            if (parameter.MinimumVariables < 1)
                throw new ArgumentOutOfRangeException(nameof(parameter), parameter.MinimumVariables, "pickVariablesParameter.MinimumVariables: Must obtain values for at least one variable");
            if (parameter.MinimumVariables > parameter.MaximumVariables)
                throw new ArgumentException("minimumVariables must not be larger than maximumVariables");

            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);

            // Label the parameter above it if required
            if (parameter.HasPrompt)
            {
                Label lbl = new Label
                {
                    Tag = parameter,
                    Padding = new Padding(0, 6, 0, 3),
                    AutoSize = true,
                    Text = parameter.Prompt(Processor, Context)
                };
                tlp.Controls.Add(lbl);
                tlp.SetColumnSpan(lbl, 2);
            }

            TableLayoutPanel holder = new TableLayoutPanel
            {
                AutoSize = true,
                ColumnCount = 2,
                RowCount = parameter.MaximumVariables,
                Tag = parameter
            };
            for (int v = 0; v < parameter.MaximumVariables; v++)
            {
                ComboBox cbo = new ComboBox { FormattingEnabled = true };
                for (int i = 0; i < frame.VariableCount; i++)
                {
                    string rubric = null == frame.Variables[i] ? string.Empty : frame.Variables[i].Title;
                    cbo.Items.Add(rubric);
                }
                if (initialState?.Length > v)
                    cbo.SelectedIndex = initialState[v];
                cbo.AutoSizeToList();
                holder.Controls.Add(cbo);
                Label l = new Label
                {
                    Padding = new Padding(3, 6, 3, 3),
                    AutoSize = true,
                    Text = parameter.LabelAs(Processor, Context, v)
                };
                holder.Controls.Add(l);
            }
            tlp.Controls.Add(holder);
            tlp.SetColumnSpan(holder, 2);
        }

        public void Visit(StringParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox();
            if (parameter.MaxLength <= 0)
                txt.Size = new Size(250, 18);
            else
            {
                // Windows kerns fonts; so we measure two different strings to give us an idea of the kerning.
                float enWidth;
                float enEnWidth;
                using (Graphics tapeMeasure = txt.CreateGraphics())
                {
                    enWidth = tapeMeasure.MeasureString("n", txt.Font).Width;
                    enEnWidth = tapeMeasure.MeasureString("nn", txt.Font).Width;
                }
                txt.MaxLength = parameter.MaxLength;
                txt.Size = new Size(6 + (int)Math.Ceiling(enWidth + (enEnWidth - enWidth) * (parameter.MaxLength - 1)), 18);
            }
            txt.Tag = parameter;
            if (!parameter.ForceDefault && Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsString)
            {
                txt.Text = Context[parameter.Name].AsString;
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    txt.Text = parameter.DefaultValue(Processor, Context);
                }
            }
            AddAppropriateEventHandlersTo(txt);

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.Prompt(Processor, Context, string.Empty)
            };

            MaybeAddHelpTip(lbl, parameter);
            MaybeAddHelpTip(txt, parameter);

            if (parameter.PromptPrecedesParameter)
            {
                tlp.Controls.Add(lbl);
                tlp.Controls.Add(txt);
            }
            else
            {
                tlp.Controls.Add(txt);
                tlp.Controls.Add(lbl);
            }
        }

        public void Visit(SpecialParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);

            if ("chi-2-column".Equals(parameter.SpecialType)
                || "chi-3-column".Equals(parameter.SpecialType)
                || "rr-index".Equals(parameter.SpecialType)
                || "person-time-size".Equals(parameter.SpecialType)
                || "likelihood".Equals(parameter.SpecialType))
            {
                bool isLikelihood = "likelihood".Equals(parameter.SpecialType);
                bool isRrIndex = "rr-index".Equals(parameter.SpecialType);
                bool isPersonTimeSize = "person-time-size".Equals(parameter.SpecialType);
                bool has3Columns = "chi-3-column".Equals(parameter.SpecialType) || isPersonTimeSize;

                TableLayoutPanel ssgContainer = new TableLayoutPanel
                {
                    Tag = parameter,
                    RowCount = 2,
                    ColumnCount = 2,
                    AutoSize = true
                };

                Panel colsPanel = new Panel { Padding = new Padding(0, 0, 0, 0), Margin = new Padding(0, 0, 0, 3), Size = new Size(300, 16), AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
                ssgContainer.Controls.Add(colsPanel, 1, 0);

                if (isLikelihood)
                {
                    VerticalLabel rowsLabel = new VerticalLabel
                    {
                        Text = "Level",
                        AutoSize = true
                    };
                    ssgContainer.Controls.Add(rowsLabel, 0, 1);
                }

                WorkbookView grid = new WorkbookView
                {
                    ContextMenuStrip = Form.InlineGridContextMenuStrip,
                    Padding = new Padding(0, 0, 0, 0),
                    Margin = new Padding(0, 0, 0, 0)
                };
                grid.GetLock();
                try
                {
                    if (Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsDataFrame)
                    {
                        DataFrame sourceFrame = Context[parameter.Name].AsDataFrame;
                        if (sourceFrame.VariableCount >= 2 && sourceFrame.Variables[0] is DoubleVariable && sourceFrame.Variables[1] is DoubleVariable)
                        {
                            DumpIntoSsg((IValues)grid.ActiveWorksheet, 0, (DoubleVariable) sourceFrame.Variables[0]);
                            DumpIntoSsg((IValues)grid.ActiveWorksheet, 1, (DoubleVariable) sourceFrame.Variables[1]);
                            if (has3Columns && sourceFrame.VariableCount >= 3)
                                DumpIntoSsg((IValues)grid.ActiveWorksheet, 2, sourceFrame.Variables[2] as DoubleVariable);
                        }
                    }
                    grid.ActiveWorksheet.WindowInfo.SplitColumns = has3Columns ? 3 : 2;
                    grid.ActiveWorksheet.WindowInfo.FreezePanes = true;
                    grid.ActiveWorksheet.Cells[0, has3Columns ? 3 : 2, 0, grid.ActiveWorksheet.Cells.ColumnCount - 1].EntireColumn.Hidden = true;
                    grid.ActiveWorkbook.WindowInfo.DisplayWorkbookTabs = false;
                    grid.ActiveWorkbook.WindowInfo.DisplayHorizontalScrollBar = false;

                    // Figure out the width of the row header
                    grid.ActiveWorksheet.Cells[0, 0, 0, 0].EntireColumn.ColumnWidth = 3; // characters - used to simulate row header, which defaults to 3 character width until 1,000th row visible
                    double rowHeaderWidthInPoints = grid.ActiveWorksheet.Cells[0, 0, 0, 0].EntireColumn.Width; // Simulated row header

                    // Reset column widths to a more useful number (11 characters) and get their visible width
                    grid.ActiveWorksheet.Cells[0, 0, 0, has3Columns ? 2 : 1].EntireColumn.ColumnWidth = 11; // characters
                    double visibleColumnsWidthInPoints = grid.ActiveWorksheet.Cells[0, 0, 0, has3Columns ? 2 : 1].EntireColumn.Width; // Visible columns excluding row header and scrollbar
                    double oneColumnWidthInPoints = grid.ActiveWorksheet.Cells[0, 0, 0, 0].EntireColumn.Width; // One column

                    // Set the control size
                    double pointsToPixels = 2; // TODO: HACK: Fudge factor.  How do we get this to be saner?
                    const int aHair = 3; // Fudge factor: Extra width in pixels for things like scrollbar edges and ensuring that the right-hand end of the last cell is visible
                    int overallWidthInPixels = (int)((rowHeaderWidthInPoints + visibleColumnsWidthInPoints) * pointsToPixels) + SystemInformation.VerticalScrollBarWidth + aHair;
                    grid.Size = new Size((int)(overallWidthInPixels * Form.currentScaleFactor.Width), (int)(400 * Form.currentScaleFactor.Height));
                    int rowHeaderWidthInPixels = (int)(rowHeaderWidthInPoints * pointsToPixels);
                    int oneColumnWidthInPixels = (int)(oneColumnWidthInPoints * pointsToPixels);
                    int fudge = (int)(3 * pointsToPixels); // Offset of labels from nominal column start, in pixels.  Ideally this should closely match SSG's internal offset.

                    Label col1Label = new Label
                    {
                        Text =
                            isPersonTimeSize
                                ? "Index events"
                                : isRrIndex
                                    ? "Reference rate"
                                    : isLikelihood ? "+ feature" : "+ success",
                        AutoSize = true,
                        Location = new Point(rowHeaderWidthInPixels + 0 * oneColumnWidthInPixels + fudge, 0)
                    };
                    colsPanel.Controls.Add(col1Label);

                    Label col2Label = new Label
                    {
                        Text =
                            isPersonTimeSize || isRrIndex
                                ? "Index Person-time"
                                : isLikelihood ? "- feature" : "- failure",
                        AutoSize = true,
                        Location = new Point(rowHeaderWidthInPixels + 1 * oneColumnWidthInPixels + fudge, 0)
                    };
                    colsPanel.Controls.Add(col2Label);

                    if (has3Columns)
                    {
                        Label col3Label = new Label
                        {
                            Text = isPersonTimeSize ? "Reference size" : "score",
                            AutoSize = true,
                            Location = new Point(rowHeaderWidthInPixels + 2 * oneColumnWidthInPixels + fudge, 0)
                        };
                        colsPanel.Controls.Add(col3Label);
                    }

                }
                finally
                {
                    grid.ReleaseLock();
                }
                ssgContainer.Controls.Add(grid, 1, 1);

                tlp.Controls.Add(ssgContainer);
                tlp.SetColumnSpan(ssgContainer, 2);

                grid.Focus();

                return;
            }
            if ("raters-2d".Equals(parameter.SpecialType))
            {
                TableLayoutPanel ssgContainer = new TableLayoutPanel
                {
                    Tag = parameter,
                    RowCount = 2,
                    ColumnCount = 2,
                    AutoSize = true
                };

                Label colsLabel = new Label { Text = "Rater 2", AutoSize = true };
                ssgContainer.Controls.Add(colsLabel, 1, 0);

                VerticalLabel rowsLabel = new VerticalLabel { Text = "Rater 1", AutoSize = true, TabStop = false };
                ssgContainer.Controls.Add(rowsLabel, 0, 1);

                WorkbookView grid = new WorkbookView { Size = new Size((int)(450 * Form.currentScaleFactor.Width), (int)(400 * Form.currentScaleFactor.Height)), ContextMenuStrip = Form.InlineGridContextMenuStrip };
                grid.GetLock();
                try
                {
                    if (Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsDataFrame)
                    {
                        DataFrame sourceFrame = Context[parameter.Name].AsDataFrame;
                        for (int col = 0; col < sourceFrame.VariableCount; col++)
                            if (sourceFrame.Variables[col] is DoubleVariable)
                                DumpIntoSsg((IValues)grid.ActiveWorksheet, col, (DoubleVariable) sourceFrame.Variables[col]);
                    }
                    grid.ActiveWorksheet.WindowInfo.Zoom = 88; // percent
                    grid.ActiveWorkbook.WindowInfo.DisplayWorkbookTabs = false;
                }
                finally
                {
                    grid.ReleaseLock();
                }
                ssgContainer.Controls.Add(grid, 1, 1);

                tlp.Controls.Add(ssgContainer);
                tlp.SetColumnSpan(ssgContainer, 2);

                grid.Focus();

                return;
            }
            if ("addedConstant".Equals(parameter.SpecialType))
            {
                DataFrame frame = Context["data"].AsDataFrame;
                DoubleVariable dv = (DoubleVariable) frame.Variables[0];
                double minimumC = Sheet.XConstant(dv.Data);
                Context.AddOutput("a_min", minimumC);

                if (minimumC != Constant.MISSING)
                {
                    DoubleParameter dp = new DoubleParameter
                    {
                        Name = parameter.Name,
                        PromptExpression = parameter.PromptExpression,
                        MinimumValueExpression = new Expression(minimumC.ToString()),
                        DefaultValueExpression = new Expression(minimumC.ToString()),
                        CancelSkipsParameter = "Skip"
                    };
                    // Safe to delegate to another Visit rather than going through the Accept.
                    Visit(dp);
                }
                return;
            }
            if ("frame".Equals(parameter.SpecialType))
            {
                ctlPickAWindow ctl = new ctlPickAWindow(OutputType.Frame, parameter) { Tag = parameter };
                AddAppropriateEventHandlersTo(ctl);
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return;
            }
            if ("report".Equals(parameter.SpecialType))
            {
                ctlPickAWindow ctl = new ctlPickAWindow(OutputType.Report, parameter) { Tag = parameter };
                AddAppropriateEventHandlersTo(ctl);
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return;
            }
            if ("rubric".Equals(parameter.SpecialType))
            {
                Label ctl = new Label
                {
                    AutoSize = true,
                    Tag = parameter,
                    Text = parameter.Prompt(Processor, Context)
                };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return;
            }
            if ("textToNumbers".Equals(parameter.SpecialType))
            {
                ctlTextToNumbers ctl = new ctlTextToNumbers(Context) { Tag = parameter };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return;
            }
            if ("scores".Equals(parameter.SpecialType))
            {
                ctlScores ctl = new ctlScores(Context) { Tag = parameter };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return;
            }
            throw new ArgumentOutOfRangeException(nameof(parameter), parameter.SpecialType, "parameter.SpecialType: Unknown option");
        }

        public void Visit(PickFromListParameter parameter)
        {
            DataFrame sourceFrame = Context[parameter.Source].AsDataFrame;
            string[] values;
            if (sourceFrame.Variables[0] is StringVariable)
                values = ((StringVariable) sourceFrame.Variables[0]).Data;
            else if (sourceFrame.Variables[0] is ClassifierVariable)
                values = ((ClassifierVariable) sourceFrame.Variables[0]).SortedCategoryNames;
            else
                throw new ArgumentException("A PickFromListParameter can only pick from string or classifier variables");

            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            if (parameter.AllowMultiple)
            {
                ListBox lstPickFromList = new ListBox
                {
                    Tag = parameter,
                    FormattingEnabled = true,
                    Name = "lstPickFromList",
                    Size = new Size(250, 48)
                };
                foreach (string value in values)
                    lstPickFromList.Items.Add(value);
                lstPickFromList.SelectionMode = parameter.AllowMultiple ? SelectionMode.MultiSimple : SelectionMode.One;
                tlp.Controls.Add(lstPickFromList);
            }
            else
            {
                ComboBox cbo = new ComboBox { Tag = parameter, MaximumSize = new Size(250, 21) };
                if (parameter.IncludeNoneEntry)
                    cbo.Items.Add("(none)");
                foreach (string value in values)
                    cbo.Items.Add(value);
                cbo.SelectedIndex = 0;
                cbo.DropDownStyle = ComboBoxStyle.DropDownList;

                // There's no way of autosizing a combo... so we do it by hand!
                cbo.AutoSizeToList();
                tlp.Controls.Add(cbo);
            }

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.Prompt(Processor, Context, string.Empty)
            };
            tlp.Controls.Add(lbl);
        }

        public void Visit(OptionParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);

            switch (parameter.OptionFormatType)
            {
                case OptionFormatType.Dropdown:
                    {
                        string defaultValue = null;
                        if (Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter)
                        {
                            defaultValue = Context[parameter.Name].AsString;
                        }
                        else
                        {
                            if (null != parameter.DefaultValueExpression)
                                defaultValue = Processor.Evaluate(parameter.DefaultValueExpression, Context).ToString();
                        }

                        ComboBoxEx cbo = new ComboBoxEx { Tag = parameter, MaximumSize = new Size(250, 21) };
                        ComboBoxExItem defaultItem = null;
                        foreach (OptionOption optionOption in parameter.Options)
                        {
                            ComboBoxExItem cbi = new ComboBoxExItem { Tag = optionOption, Text = optionOption.Label };
                            cbo.Items.Add(cbi);
                            if (null != defaultValue)
                                if (optionOption.Value.Equals(defaultValue))
                                    defaultItem = cbi;
                        }
                        cbo.SelectedIndex = 0;
                        cbo.DropDownStyle = ComboBoxStyle.DropDownList;
                        AddAppropriateEventHandlersTo(cbo);
                        if (null != defaultItem)
                            cbo.SelectedItem = defaultItem;

                        // There's no way of autosizing a combo... so we do it by hand!
                        cbo.AutoSizeToList();

                        Label lbl = new Label
                        {
                            Tag = parameter,
                            Padding = new Padding(0, 6, 0, 3),
                            AutoSize = true,
                            MaximumSize = new Size(500, 500),
                            Text = parameter.Prompt(Processor, Context, string.Empty)
                        };

                        if (parameter.PromptPrecedesParameter)
                        {
                            tlp.Controls.Add(lbl);
                            tlp.Controls.Add(cbo);
                            tlp.SetColumn(lbl, 0);
                            tlp.SetColumn(cbo, 1);
                        }
                        else
                        {
                            tlp.Controls.Add(cbo);
                            tlp.Controls.Add(lbl);
                            tlp.SetColumn(cbo, 0);
                            tlp.SetColumn(lbl, 1);
                        }
                    }
                    break;
                case OptionFormatType.Radio:
                    {
                        GroupBox groupBox = null;
                        string prompt = parameter.Prompt(Processor, Context);
                        if (!string.IsNullOrEmpty(prompt))
                        {
                            groupBox = new SDGroupBox
                            {
                                Tag = parameter,
                                Padding = new Padding(3, 3, 3, 3),
                                AutoSize = true,
                                Text = prompt
                            };
                            // Add later so that autosizing can size the contained controls as well
                        }

                        TableLayoutPanel panelOptions = new TableLayoutPanel
                        {
                            Tag = parameter,
                            RowCount = (parameter.Options.Count + 1) / 2,
                            ColumnCount = parameter.Columns,
                            AutoSize = true
                        };
                        for (int column = 0; column < parameter.Columns; column++)
                        {
                            panelOptions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                        }

                        string defaultValue = null;
                        if (Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter)
                        {
                            defaultValue = Context[parameter.Name].AsString;
                        }
                        else
                        {
                            if (null != parameter.DefaultValueExpression)
                                defaultValue = Processor.Evaluate(parameter.DefaultValueExpression, Context).ToString();
                        }

                        foreach (OptionOption optionOption in parameter.Options)
                        {
                            RadioButton rad = new RadioButton
                            {
                                AutoSize = true,
                                Text = optionOption.Label,
                                Tag = optionOption.Value,
                                UseVisualStyleBackColor = true
                            };
                            AddAppropriateEventHandlersTo(rad);
                            panelOptions.Controls.Add(rad);
                            if (null != defaultValue)
                                rad.Checked = optionOption.Value.Equals(defaultValue);
                        }

                        if (null == groupBox)
                        {
                            tlp.Controls.Add(panelOptions);
                            tlp.SetColumnSpan(panelOptions, 2);
                        }
                        else
                        {
                            panelOptions.Height = panelOptions.PreferredSize.Height;
                            panelOptions.Location = new Point(7, 20);
                            groupBox.Controls.Add(panelOptions);
                            tlp.Controls.Add(groupBox);
                            tlp.SetColumnSpan(groupBox, 2);
                        }
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(parameter), parameter.OptionFormatType, "optionParameter.OptionFormatType: Only Dropdown and Radio are known");
            }
        }

        public void Visit(GroupedCovarianceParameter parameter)
        {
            throw new Exception("Grouped covariance parameters cannot be combined");
        }

        public void Visit(Frame2DParameter parameter)
        {
            throw new Exception("Grid 2D parameters cannot be combined");
        }

        public void Visit(EditGridParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            DataGridView gridEditGrid = new DataGridView();
            ((ISupportInitialize)gridEditGrid).BeginInit();
            DataGridViewTextBoxColumn colKey = new DataGridViewTextBoxColumn();
            DataGridViewTextBoxColumn colValue = new DataGridViewTextBoxColumn();
            gridEditGrid.Tag = parameter;
            gridEditGrid.AllowUserToAddRows = false;
            gridEditGrid.AllowUserToDeleteRows = false;
            gridEditGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridEditGrid.ColumnHeadersVisible = false;
            gridEditGrid.Columns.AddRange(colKey, colValue);
            gridEditGrid.EditMode = DataGridViewEditMode.EditOnEnter;
            gridEditGrid.MultiSelect = false;
            gridEditGrid.Name = "gridEditGrid";
            gridEditGrid.RowHeadersVisible = false;
            gridEditGrid.SelectionMode = DataGridViewSelectionMode.CellSelect;
            gridEditGrid.Size = new Size(250, 48);
            colKey.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            colKey.HeaderText = "Key";
            colKey.Name = "colKey";
            colKey.ReadOnly = true;
            colKey.SortMode = DataGridViewColumnSortMode.NotSortable;
            colKey.Width = 5;
            colValue.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            colValue.HeaderText = "Value";
            colValue.Name = "colValue";
            colValue.SortMode = DataGridViewColumnSortMode.NotSortable;
            tlp.Controls.Add(gridEditGrid);
            ((ISupportInitialize)gridEditGrid).EndInit();
            EditGridParameter egp = parameter;
            DataFrame sourceFrame = Context[egp.Source].AsDataFrame;
            StringVariable keyVariable = (StringVariable) sourceFrame.FindVariable(egp.KeyVariable);
            StringVariable valueVariable = (StringVariable) sourceFrame.FindVariable(egp.ValueVariable);
            gridEditGrid.Rows.Clear();
            for (int i = 0; i < keyVariable.Length; i++)
                gridEditGrid.Rows.Add(keyVariable.Data[i], valueVariable.Data[i]);
            gridEditGrid.Visible = true;

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.Prompt(Processor, Context, string.Empty)
            };
            tlp.Controls.Add(lbl);
        }

        public void Visit(Double2By2ByKParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            TableLayoutPanel panel2By2ByK = new TableLayoutPanel
            {
                Tag = parameter,
                RowCount = 5,
                ColumnCount = 3,
                AutoSize = true
            };
            panel2By2ByK.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2ByK.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            panel2By2ByK.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            Label lblColumnsPrompt = new Label
            {
                Padding = new Padding(3, 6, 3, 3),
                AutoSize = true,
                Text = "Characteristic (press F1 for an example)"
            };
            panel2By2ByK.Controls.Add(lblColumnsPrompt, 0, 0);
            panel2By2ByK.SetColumnSpan(lblColumnsPrompt, 3);

            Label lblLeftColumnPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Present" };
            panel2By2ByK.Controls.Add(lblLeftColumnPrompt, 0, 1);

            Label lblRightColumnPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Absent" };
            panel2By2ByK.Controls.Add(lblRightColumnPrompt, 1, 1);

            Label lblRowsPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Outcome:" };
            panel2By2ByK.Controls.Add(lblRowsPrompt, 2, 1);

            TextBox txtTL = new TextBox { Name = "txtTL", Size = new Size(100, 18) };
            AddAppropriateEventHandlersTo(txtTL);
            panel2By2ByK.Controls.Add(txtTL, 0, 2);

            TextBox txtTR = new TextBox { Name = "txtTR", Size = new Size(100, 18) };
            AddAppropriateEventHandlersTo(txtTR);
            panel2By2ByK.Controls.Add(txtTR, 1, 2);

            Label lblTopRowPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Present" };
            panel2By2ByK.Controls.Add(lblTopRowPrompt, 2, 2);

            TextBox txtBL = new TextBox { Name = "txtBL", Size = new Size(100, 18) };
            AddAppropriateEventHandlersTo(txtBL);
            panel2By2ByK.Controls.Add(txtBL, 0, 3);

            TextBox txtBR = new TextBox { Name = "txtBR", Size = new Size(100, 18) };
            AddAppropriateEventHandlersTo(txtBR);
            panel2By2ByK.Controls.Add(txtBR, 1, 3);

            FlowLayoutPanel pnlNavigation = new FlowLayoutPanel { AutoSize = true, Tag = new[] { new List<double>(), new List<double>() } };
            panel2By2ByK.Controls.Add(pnlNavigation, 0, 4);
            panel2By2ByK.SetColumnSpan(pnlNavigation, 3);

            Button cmdPrevious = new Button { Name = "cmdPrevious", Text = "<", Width = 20 };
            cmdPrevious.Click += cmdPrevious_KeyPress;
            cmdPrevious.Enabled = false;
            pnlNavigation.Controls.Add(cmdPrevious);

            Label lblStratum = new Label { Padding = new Padding(3, 9, 3, 3), AutoSize = true, Text = "Stratum 1 of 1" };
            pnlNavigation.Controls.Add(lblStratum);
            lblStratum.Tag = 1;

            Button cmdNext = new Button { Name = "cmdNext", Text = ">", Width = 20 };
            cmdNext.Click += cmdNext_KeyPress;
            pnlNavigation.Controls.Add(cmdNext);

            Label lblBottomRowPrompt = new Label { Padding = new Padding(3, 6, 3, 3), AutoSize = true, Text = "Absent" };
            panel2By2ByK.Controls.Add(lblBottomRowPrompt, 2, 3);

            // Fill in data for stratum 1 if present; set number of strata if present
            if (Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsDataFrame)
            {
                DataFrame sourceFrame = Context[parameter.Name].AsDataFrame;
                int tableCount = sourceFrame.MinRows / 2;
                if (sourceFrame.VariableCount == 2 && sourceFrame.Variables[0] is DoubleVariable && sourceFrame.Variables[1] is DoubleVariable)
                {
                    DoubleVariable var1 = (DoubleVariable) sourceFrame.Variables[0];
                    DoubleVariable var2 = (DoubleVariable) sourceFrame.Variables[1];
                    txtTL.Text = var1.Data[0].ToString();
                    txtTR.Text = var2.Data[0].ToString();
                    txtBL.Text = var1.Data[1].ToString();
                    txtBR.Text = var2.Data[1].ToString();
                    lblStratum.Text = "Stratum 1 of " + tableCount;
                    // Copy the data for maintenance and use by the controls
                    List<double>[] newData = (List<double>[])pnlNavigation.Tag;
                    newData[0].AddRange(var1.Data);
                    newData[1].AddRange(var2.Data);
                }
            }

            tlp.Controls.Add(panel2By2ByK);
            tlp.SetColumnSpan(panel2By2ByK, 2);
        }

        public void Visit(DateParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(80, 18), Tag = parameter };
            if (!parameter.ForceDefault && Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsInt32)
            {
                txt.Text = Context[parameter.Name].AsInt32.ToString();
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    txt.Text = parameter.DefaultValue(Processor, Context).ToString("d");
                }
            }
            AddAppropriateEventHandlersTo(txt);
            MaybeAddHelpTip(txt, parameter);
            tlp.Controls.Add(txt);

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.Prompt(Processor, Context, string.Empty)
            };
            tlp.Controls.Add(lbl);
            MaybeAddHelpTip(lbl, parameter);
        }

        public void Visit(ChartOptionsParameter parameter)
        {
            ChartDefinition chartDefinition = parameter.ChartDefinition;
            ChartOptions chartOptions = chartDefinition.ChartOptions;
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            ChartOptionToControlVisitor visitor = new ChartOptionToControlVisitor(chartDefinition);
            chartOptions.Accept(visitor);
            Control ctl = visitor.Control;
            if (null == ctl)
            {
                // Do nothing - there are no options to fill
                FilledParameter = new FilledParameter(FilledParameterDirection.Input, parameter.ChartDefinition);
                return;
            }
            // At this point, ctl is always assigned.
            ctl.Tag = parameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
        }

        public void Visit(BooleanParameter parameter)
        {
            TableLayoutPanel tlp = Form.GetUserInputTableForColumn(parameter.Column);
            CheckBox cb = new CheckBox
            {
                Padding = new Padding(3, 3, 3, 3),
                AutoSize = true,
                Tag = parameter,
                Text = parameter.Prompt(Processor, Context, string.Empty)
            };
            AddAppropriateEventHandlersTo(cb);
            if (Context.ContainsKey(parameter.Name) && null != Context[parameter.Name] && Context[parameter.Name].IsInputParameter && Context[parameter.Name].IsBoolean)
            {
                cb.Checked = Context[parameter.Name].AsBoolean;
            }
            else
            {
                bool? defaultValue = parameter.DefaultValue(Processor, Context);
                if (defaultValue.HasValue)
                {
                    cb.Checked = defaultValue.Value;
                    Context.AddInput(parameter.Name, defaultValue.Value);
                }
                else
                    cb.Checked = false;
            }
            MaybeAddHelpTip(cb, parameter);
            tlp.Controls.Add(cb);
            tlp.SetColumnSpan(cb, 2);
        }

        private static void MaybeAddHelpTip(Control control, Parameter parameter)
        {
            if (null != parameter.Help)
            {
                ToolTip tt = new ToolTip();
                tt.SetToolTip(control, parameter.Help.Text);
            }
        }

        /// <summary>
        /// Assuming control is used for data entry in the SD3 dialog area, add appropriate handlers to enable global behaviours for such controls.
        /// </summary>
        void AddAppropriateEventHandlersTo(Control control)
        {
            if (control is CheckBox)
                ((CheckBox)control).CheckedChanged += OptionParameter_CheckedChanged;
            if (control is RadioButton)
                ((RadioButton)control).CheckedChanged += OptionParameter_CheckedChanged;
            if (control is ComboBox)
                ((ComboBox)control).SelectedIndexChanged += OptionParameter_CheckedChanged;
            if (control is ComboBox || control is TextBox)
                control.KeyPress += EnterMovesDown;
            if (control is ctlPickAWindow)
                ((ctlPickAWindow)control).InsideKeyPress += EnterMovesDown;
            control.LostFocus += RunChecksAfterLostFocus;
        }

        void EnterMovesDown(object sender, KeyPressEventArgs e)
        {
            try
            {
                // Only interested in ENTER - ignore others
                if ('\r' != e.KeyChar)
                    return;

                Control c = (Control)sender;

                Control next = c;
                do
                {
                    if (null == next)
                        break;
                    // Find the next useful control.  A control is useful if a tab would stop on it, and it is not a label or panel (for some reason, the selection logic stops on those even though they are not TabStops), and it is not a combo box (business logic).
                    // To prevent infinite loops, we also check for coming back to the control we tabbed from, and stop if so.
                    next = Form.GetNextControl(next, true);
                    if (next == c)
                        break;
                } while (!frmMain.IsUsefulControl(next, false));

                // If we've landed on the Calculate, we should calculate.
                if (Form.IsCalculateButton(next))
                {
                    // The Calculate button is sometimes visible in place of the OK button, notably when an operation is ready to be executed.  Deal with this by returning, which breaks out of the selection loop and runs the operation.
                    if (Form.IsSelecting || Form.IsInputtingData)
                    {
                        Form.NoteEndOfSelection(true);
                        e.Handled = true;
                        return;
                    }
                    Form.DoCalculate();
                }
                else
                {
                    // Select the entered text so it is ready to overwrite
                    if (next is TextBox)
                        next.Select();
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                Form.PuntThroughEventLoop(ex);
            }
        }

        static void cmdPrevious_KeyPress(object sender, EventArgs e)
        {
            try
            {
                // Find our control and get tag data
                Button cmdPrevious = (Button)sender;
                FlowLayoutPanel pnlNavigation = (FlowLayoutPanel)cmdPrevious.Parent;
                Label lblStratum = (Label)pnlNavigation.Controls[1];
                Button cmdNext = (Button)pnlNavigation.Controls[2];
                TableLayoutPanel panel2By2ByK = (TableLayoutPanel)pnlNavigation.Parent;
                TextBox txtTl = (TextBox)panel2By2ByK.GetControlFromPosition(0, 2);
                TextBox txtTr = (TextBox)panel2By2ByK.GetControlFromPosition(1, 2);
                TextBox txtBl = (TextBox)panel2By2ByK.GetControlFromPosition(0, 3);
                TextBox txtBr = (TextBox)panel2By2ByK.GetControlFromPosition(1, 3);

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
                while (var1Data.Count < stratum * 2)
                {
                    var1Data.Add(Constant.MISSING);
                    var2Data.Add(Constant.MISSING);
                }
                var1Data[offset] = tl;
                var2Data[offset] = tr;
                var1Data[offset + 1] = bl;
                var2Data[offset + 1] = br;

                // #744: Test for all-missing in the end stratum; delete it if so
                int strata = newData[0].Count / 2;
                int lastOffset = (strata - 1) * 2;
                if (var1Data[lastOffset] == Constant.MISSING && var2Data[lastOffset] == Constant.MISSING && var1Data[lastOffset + 1] == Constant.MISSING && var2Data[lastOffset + 1] == Constant.MISSING)
                {
                    --strata;
                    var1Data.RemoveAt(lastOffset + 1);
                    var1Data.RemoveAt(lastOffset);
                    var2Data.RemoveAt(lastOffset + 1);
                    var2Data.RemoveAt(lastOffset);
                }


                if (stratum > 1)
                    --stratum;
                lblStratum.Tag = stratum;

                // Fill the text boxes from the stored data
                offset = (stratum - 1) * 2;
                txtTl.BackColor = System.Drawing.SystemColors.Window;
                txtTr.BackColor = System.Drawing.SystemColors.Window;
                txtBl.BackColor = System.Drawing.SystemColors.Window;
                txtBr.BackColor = System.Drawing.SystemColors.Window;
                txtTl.Text = Formatting.XUnrounded(var1Data[offset]);
                txtTr.Text = Formatting.XUnrounded(var2Data[offset]);
                txtBl.Text = Formatting.XUnrounded(var1Data[offset + 1]);
                txtBr.Text = Formatting.XUnrounded(var2Data[offset + 1]);
                lblStratum.Text = "Stratum " + stratum + " of " + strata;

                cmdPrevious.Enabled = stratum > 1;
                cmdNext.Enabled = true;
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't move to previous stratum due to an internal error", ex, false);
            }
        }

        static void cmdNext_KeyPress(object sender, EventArgs e)
        {
            try
            {
                // Find our control and get tag data
                Button cmdNext = (Button)sender;
                FlowLayoutPanel pnlNavigation = (FlowLayoutPanel)cmdNext.Parent;
                Label lblStratum = (Label)pnlNavigation.Controls[1];
                Button cmdPrevious = (Button)pnlNavigation.Controls[0];
                TableLayoutPanel panel2By2ByK = (TableLayoutPanel)pnlNavigation.Parent;
                TextBox txtTL = (TextBox)panel2By2ByK.GetControlFromPosition(0, 2);
                TextBox txtTR = (TextBox)panel2By2ByK.GetControlFromPosition(1, 2);
                TextBox txtBL = (TextBox)panel2By2ByK.GetControlFromPosition(0, 3);
                TextBox txtBR = (TextBox)panel2By2ByK.GetControlFromPosition(1, 3);

                int stratum = (int)lblStratum.Tag;
                List<double>[] newData = (List<double>[])pnlNavigation.Tag;
                List<double> var1Data = newData[0];
                List<double> var2Data = newData[1];

                // Fill the stored data from the text boxes
                int offset = (stratum - 1) * 2;
                int strata = newData[0].Count / 2;
                double tl = Parsing.Cdbl_Txt(txtTL.Text);
                double tr = Parsing.Cdbl_Txt(txtTR.Text);
                double bl = Parsing.Cdbl_Txt(txtBL.Text);
                double br = Parsing.Cdbl_Txt(txtBR.Text);
                while (var1Data.Count < stratum * 2)
                {
                    var1Data.Add(Constant.MISSING);
                    var2Data.Add(Constant.MISSING);
                }
                var1Data[offset] = tl;
                var2Data[offset] = tr;
                var1Data[offset + 1] = bl;
                var2Data[offset + 1] = br;

                stratum++;
                lblStratum.Tag = stratum;

                // Fill the text boxes from the stored data
                offset = (stratum - 1) * 2;
                if (offset < var1Data.Count)
                {
                    txtTL.Text = Formatting.XUnrounded(var1Data[offset]);
                    txtTR.Text = Formatting.XUnrounded(var2Data[offset]);
                    txtBL.Text = Formatting.XUnrounded(var1Data[offset + 1]);
                    txtBR.Text = Formatting.XUnrounded(var2Data[offset + 1]);
                }
                else
                {
                    // New stratum
                    txtTL.Clear();
                    txtTR.Clear();
                    txtBL.Clear();
                    txtBR.Clear();
                }
                lblStratum.Text = "Stratum " + stratum + " of " + Math.Max(strata, stratum);
                txtTL.BackColor = System.Drawing.SystemColors.Window;
                txtTR.BackColor = System.Drawing.SystemColors.Window;
                txtBL.BackColor = System.Drawing.SystemColors.Window;
                txtBR.BackColor = System.Drawing.SystemColors.Window;

                cmdPrevious.Enabled = true;
                cmdNext.Enabled = true; // Can always Next to create another stratum
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't move to next stratum due to an internal error", ex, false);
            }
        }

        void RunChecksAfterLostFocus(object sender, EventArgs e)
        {
            // If we're no longer attached to a window, don't run any checks; they're not relevant and we'll be missing our data anyway.
            Control probe = (Control)sender;
            while (null != probe)
            {
                if (probe is Form)
                    break; // It's still attached
                probe = probe.Parent;
            }
            if (null != probe)
                Form.CheckCombinedParameterVisibilityAndMaybeResize((Control)sender);
        }

        void OptionParameter_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                Form.CheckCombinedParameterVisibilityAndMaybeResize((Control)sender);
            }
            catch (Exception ex)
            {
                frmMain.EatException(ex);
            }
        }

        static void DumpIntoSsg(IValues values, int column, DoubleVariable variable)
        {
            double[] data = variable.Data;
            if (null != data)
            {
                for (int i = 0; i < data.Length; i++)
                    if (Constant.MISSING == data[i])
                        values.SetText(i, column, Formatting.ASTERISK);
                    else
                        values.SetNumber(i, column, data[i]);
            }
        }

        private class ChartOptionToControlVisitor : IChartOptionVisitor
        {
            private readonly ChartDefinition chartDefinition;
            public Control Control { get; private set; }

            public ChartOptionToControlVisitor(ChartDefinition definition)
            {
                chartDefinition = definition;
            }

            void IChartOptionVisitor.Visit(AgreementOptions options)
            {
                Control = null;
            }

            void IChartOptionVisitor.Visit(BarOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(BoxWhiskerOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(ControlOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(ErrorBarOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(ForestOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(GiniOptions options)
            {
                Control = null;
            }

            void IChartOptionVisitor.Visit(HistogramOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(LadderOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(LinearRegressionOptions options)
            {
                Control = null;
            }

            void IChartOptionVisitor.Visit(NormalOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(PyramidOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(ROCOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(ScatterXYOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(SpreadOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }

            void IChartOptionVisitor.Visit(SurvivalOptions options)
            {
                Control = new ctlChartOptions(chartDefinition);
            }
        }
    }
}
