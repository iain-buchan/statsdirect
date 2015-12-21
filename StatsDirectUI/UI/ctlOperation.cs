using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using SpreadsheetGear.Advanced.Cells;
using StatsDirect.Builtins;
using StatsDirect.Data;
using StatsDirect.Numerics;
using StatsDirect.Templates;
using StatsDirect.Utilities;
using SpreadsheetGear.Windows.Forms;
using SpreadsheetGear;
using Color = System.Drawing.Color;
using SystemColors = System.Drawing.SystemColors;
using StatsDirect.Charting;

namespace StatsDirect.UI
{
    public partial class ctlOperation : UserControl
    {
        private static readonly char[] BAR = { '|' };
        private static readonly char[] EQUALS = { '=' };

        /// <summary>
        /// The minimum amount of other decoration that must be preserved above and below the operations panel.  Forces large panels to scroll.
        /// </summary>
        private const int HEIGHT_BREATHING_SPACE = 100;

        /// <summary>
        /// If true, a grid selection is in progress
        /// </summary>
        private bool selectingData /* = false */;
        /// <summary>
        /// If true, non-grid data entry is inprogress using the top bar
        /// </summary>
        private bool inputtingData /* = false */;
        private bool okPressed /* = false */;
        private bool cancelPressed /* = false */;

        /// <summary>
        /// A way of passing the ambient parameters into the visibility checks.
        /// This should be null except during a FillCombinedParameters call.
        /// </summary>
        private ParameterBag fillCombinedParametersContext;

        // Record the running scale factor used, for sizing controls we add dynamically where they don't do it themselves
        private SizeF currentScaleFactor = new SizeF(1f, 1f);

        /// <summary>
        /// Outside the debugger, the runtime cannot propagate exception through native code - the native handler gets them and fails.
        /// In two key places, exceptions are "punted" through the native code of a DoEvents loop.
        /// 1) SelectCells;
        /// 2) FillCombinedPanel.
        /// 
        /// Look for users of this variable for the gory details.
        /// Ideally the entire template system would be rebuilt to not steal the flow of control, at which point the system could be turned inside-out and there would be no need for this code (it would also work better in, say, an asp.net environment).
        /// </summary>
        private Exception puntedException;

        public ctlOperation()
        {
            InitializeComponent();
        }

        private IEnumerable<Parameter> GetAllOutstandingParameters()
        {
            List<Parameter> parameters = new List<Parameter>();
            TableLayoutPanel tlp = GetUserInputTable();
            if (null != tlp)
            {
                foreach (Control column in tlp.Controls)
                {
                    foreach (Control control in column.Controls)
                    {
                        if (null != control.Tag)
                        {
                            Parameter parameter = (Parameter)control.Tag;
                            if (!parameters.Contains(parameter))
                                parameters.Add(parameter);
                        }
                    }
                }
            }
            return parameters;
        }

        /// <summary>
        /// An unpleasant hack.  In debug mode, CLR exceptions propagate up the stack through sections of native code.
        /// In release code, they don't, hence this mechanism to avoid Windows exceptions being triggered.
        /// </summary>
        /// <param name="ex"></param>
        internal void PuntThroughEventLoop(Exception ex)
        {
            puntedException = ex;
            selectingData = false;
            inputtingData = false;
        }

        private bool HasUserInputTable()
        {
            return Controls.Count > 0;
        }

        internal bool SelectingData
        {
            get { return selectingData; }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="processor"></param>
        /// <param name="context"></param>
        /// <param name="outstandingParameters"></param>
        /// <returns></returns>
        /// <remarks>The return value may contain key->null for optional blank parameters.  It is up to the caller to handle this.</remarks>
        public ParameterBag FillAndValidateCombinedParameters(ITemplateHost host, ITemplateProcessor processor, ParameterBag context, IList<Parameter> outstandingParameters)
        {
            try
            {
                DrawingControl.SuspendDrawing(this);
                ParameterBag outputParameters = new ParameterBag();
                StartCombinedParameters();
                bool willDisplayAtLeastOneParameter = false;
                string cancelSkipsParameterString = null;
                bool atLeastOneNonCancel = false;
                foreach (Parameter parameter in outstandingParameters)
                {
                    if (null != parameter.RubricExpression)
                    {
                        string rubric = parameter.Rubric(processor, context);
                        if (null != rubric)
                        {
                            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

                            Label lbl = new Label
                            {
                                Tag = parameter,
                                Padding = new Padding(0, 3, 0, 3),
                                AutoSize = true,
                                MaximumSize = new Size(500, 500),
                                Text = rubric
                            };
                            tlp.Controls.Add(lbl);
                            tlp.SetColumnSpan(lbl, 2);
                        }
                    }

                    FilledParameter fp;
                    switch (parameter.Type)
                    {
                        case ParameterType.Boolean:
                            fp = PrepareCombinedParameter(processor, (BooleanParameter)parameter, context);
                            break;
                        case ParameterType.ConfidenceInterval:
                            fp = PrepareCombinedParameter(processor, (ConfidenceIntervalParameter)parameter, context);
                            break;
                        case ParameterType.Custom:
                            if (typeof(ChartOptionsParameter) == parameter.GetType())
                                fp = PrepareCombinedParameter((ChartOptionsParameter)parameter);
                            else if (typeof(FillableParameter) == parameter.GetType())
                                fp = PrepareCombinedParameter(host, (FillableParameter)parameter);
                            else
                                throw new ArgumentOutOfRangeException("processor", "Must be a ChartOptionsParameter or FillableParameter if it is a custom parameter");
                            break;
                        case ParameterType.Date:
                            fp = PrepareCombinedParameter(processor, (DateParameter)parameter, context);
                            break;
                        case ParameterType.Double:
                            fp = PrepareCombinedParameter(processor, (DoubleParameter)parameter, context);
                            break;
                        case ParameterType.Double2By2:
                            fp = PrepareCombinedParameter(processor, (Double2By2Parameter)parameter, context);
                            break;
                        case ParameterType.Double2By2ByK:
                            fp = PrepareCombinedParameter(processor, (Double2By2ByKParameter)parameter, context);
                            break;
                        case ParameterType.EditGrid:
                            fp = PrepareCombinedParameter(processor, (EditGridParameter)parameter, context);
                            break;
                        case ParameterType.Grid:
                            fp = PrepareCombinedParameter(processor, (GridParameter)parameter, context);
                            break;
                        case ParameterType.Integer:
                            fp = PrepareCombinedParameter(processor, (IntegerParameter)parameter, context);
                            break;
                        case ParameterType.Option:
                            fp = PrepareCombinedParameter(processor, (OptionParameter)parameter, context);
                            break;
                        case ParameterType.Options:
                            fp = PrepareCombinedParameter(processor, (OptionsParameter)parameter, context);
                            break;
                        case ParameterType.PickFromList:
                            fp = PrepareCombinedParameter(processor, (PickFromListParameter)parameter, context);
                            break;
                        case ParameterType.PickVariables:
                            fp = PrepareCombinedParameter(processor, (PickVariablesParameter)parameter, context);
                            break;
                        case ParameterType.Special:
                            fp = PrepareCombinedParameter(processor, (SpecialParameter)parameter, context);
                            break;
                        case ParameterType.String:
                            fp = PrepareCombinedParameter(processor, (StringParameter)parameter, context);
                            break;
                        default:
                            throw new Exception("parameter.Type: Only Boolean, ConfidenceInterval, Double, Integer parameters may be combined");
                    }
                    willDisplayAtLeastOneParameter |= null == fp;
                    if (null == fp)
                    {
                        // The parameter will be displayed.

                        // Check to see whether this parameter defines a value for skipping.  If so, set it.
                        if (null == parameter.CancelSkipsParameter)
                            atLeastOneNonCancel = true;
                        else
                        {
                            if (null == cancelSkipsParameterString)
                                cancelSkipsParameterString = parameter.CancelSkipsParameter;
                        }
                    }
                    else
                    {
                        // The parameter's been pre-filled; nothing is presented for this one.  Add it to the output.
                        outputParameters.Add(parameter.Name, fp);
                    }
                }
                IList<Parameter> parametersToValidate = new List<Parameter>(outstandingParameters);
                outstandingParameters.Clear();

                // Some parameters (notably CI parameters) may be defaulted - none will be shown.  If that's the case, don't show; just default them all!
                DrawingControl.ResumeDrawing(this);
                FillCombinedParameters(host, processor, context, willDisplayAtLeastOneParameter, atLeastOneNonCancel ? null : cancelSkipsParameterString, parametersToValidate, ref outputParameters);
                return outputParameters;
            }
            finally
            {
                // Make absolutely certain we haven't suspended layout on pnlUser and not fixed that.
                ResumeLayout();
                TableLayoutPanel tlp = GetUserInputTable();
                if (null != tlp)
                {
                    tlp.ResumeLayout(true);
                    foreach (Control col in tlp.Controls)
                        col.ResumeLayout();
                }

                // Make absolutely certain the window doesn't lock up and become unable to repaint
                DrawingControl.ResumeDrawing(this);
            }
        }

        /// <summary>
        /// Return the i'th column in the user input table, creating it and any prior columns if necessary.  The columns are created with layout suspended.
        /// </summary>
        /// <param name="column"></param>
        /// <returns></returns>
        private TableLayoutPanel GetUserInputTableForColumn(int column)
        {
            TableLayoutPanel tlp = GetUserInputTable();
            while (tlp.ColumnCount < column)
            {
                TableLayoutPanel colTlp = CreateUserInputColumn();
                colTlp.SuspendLayout();
                tlp.ColumnCount++;
                tlp.Controls.Add(colTlp);
                tlp.SetCellPosition(colTlp, new TableLayoutPanelCellPosition(tlp.ColumnCount - 1, 0));
                tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            }
            // Make use of the fact that we always create columns in the order 1..n, so controls[i-1] is the control that was created i'th in sequence and hence the control in column i.
            return (TableLayoutPanel)tlp.Controls[column - 1];
        }

        private void ClearCombinedParameters()
        {
            if (HasUserInputTable())
            {
                Control table = GetUserInputTable();
                Controls.Remove(table);
                table.Dispose();
            }
        }

        internal void StartCombinedParameters()
        {
            SuspendLayout();
            ClearCombinedParameters();
            TableLayoutPanel tlp = CreateUserInputTable();
            tlp.SuspendLayout();
            Controls.Add(tlp);
        }

        private static TableLayoutPanel CreateUserInputColumn()
        {
            TableLayoutPanel tlp = new TableLayoutPanel
            {
                ColumnCount = 2,
                GrowStyle = TableLayoutPanelGrowStyle.AddRows,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                AutoSize = true,
                Location = new Point(0, 0),
                Margin = new Padding(6, 0, 6, 6)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return tlp;
        }

        private TableLayoutPanel CreateUserInputTable()
        {
            TableLayoutPanel tlp = new TableLayoutPanel
            {
                ColumnCount = 1,
                GrowStyle = TableLayoutPanelGrowStyle.AddColumns,
                Width = this.Width,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Tag = "TopLevelUserTable",
                Location = new Point(0, 0),
                Margin = new Padding(0, 0, 0, 0)
            };
            tlp.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            // Expect to use at least one column if we're being created at all
            TableLayoutPanel col0 = CreateUserInputColumn();
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            tlp.Controls.Add(col0);
            tlp.SetCellPosition(col0, new TableLayoutPanelCellPosition(tlp.ColumnCount - 1, 0));

            return tlp;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, BooleanParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            CheckBox cb = new CheckBox
            {
                Padding = new Padding(3, 3, 3, 3),
                AutoSize = true,
                Tag = parameter
            };
            AddAppropriateEventHandlersTo(cb);
            if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsBoolean)
            {
                cb.Checked = context[parameter.Name].AsBoolean;
            }
            else
            {
                bool? defaultValue = parameter.DefaultValue(processor, context);
                if (defaultValue.HasValue)
                {
                    cb.Checked = defaultValue.Value;
                    context.AddInput(parameter.Name, defaultValue.Value);
                }
                else
                    cb.Checked = false;
            }
            cb.Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : string.Empty;
            MaybeAddHelpTip(cb, parameter);
            tlp.Controls.Add(cb);
            tlp.SetColumnSpan(cb, 2);

            return null;
        }

        private static void MaybeAddHelpTip(Control control, Parameter parameter)
        {
            if (null != parameter.Help)
            {
                ToolTip tt = new ToolTip();
                tt.SetToolTip(control, parameter.Help.Text);
            }
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
                    next = GetNextControl(next, true);
                    if (next == c)
                        break;
                } while (!IsUsefulControl(next, false));

                // If we've landed on anything outside our control, we should calculate.
                bool isInside = false;
                Control ancestor = next;
                while (null != ancestor)
                {
                    if (ancestor == this)
                    {
                        isInside = true;
                        break;
                    }
                    ancestor = ancestor.Parent;
                }
                if (isInside)
                {
                    // The Calculate button is sometimes visible in place of the OK button, notably when an operation is ready to be executed.  Deal with this by returning, which breaks out of the selection loop and runs the operation.
                    if (inputtingData)
                    {
                        selectingData = false;
                        inputtingData = false;
                        okPressed = true;
                        e.Handled = true;
                        return;
                    }
                    DoCalculate();
                }
                else
                {
                    // next.Focus();
                    // Select the entered text so it is ready to overwrite
                    if (next is TextBox)
                    {
                        next.Select();
                        // TextBox nText = (TextBox)next;
                        // nText.SelectionStart = 0;
                        // nText.SelectionLength = nText.TextLength;
                    }
                }
                e.Handled = true;
            }
            catch (Exception ex)
            {
                PuntThroughEventLoop(ex);
            }
        }

        private void DoCalculate()
        {
            throw new NotImplementedException();
        }

        private FilledParameter PrepareCombinedParameter(ChartOptionsParameter parameter)
        {
            ChartDefinition chartDefinition = parameter.ChartDefinition;
            ChartOptions chartOptions = chartDefinition.ChartOptions;
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            Control ctl;
            switch (chartOptions.OptionType)
            {
                case ChartOptionType.Bar:
                case ChartOptionType.BoxWhisker:
                case ChartOptionType.Control:
                case ChartOptionType.ErrorBars:
                case ChartOptionType.Forest:
                case ChartOptionType.Histogram:
                case ChartOptionType.Ladder:
                case ChartOptionType.Normal:
                case ChartOptionType.Pyramid:
                case ChartOptionType.ROC:
                case ChartOptionType.ScatterXY:
                case ChartOptionType.Spread:
                case ChartOptionType.Survival:
                    ctl = new ctlChartOptions(chartDefinition);
                    break;
                case ChartOptionType.Agreement:
                case ChartOptionType.Gini:
                case ChartOptionType.LinearRegression:
                    // Do nothing - there are no options to fill
                    return new FilledParameter(true, parameter.ChartDefinition);
                default:
                    throw new ArgumentOutOfRangeException("parameter", chartOptions.OptionType.ToString(), "ChartOptions.OptionType: Don't know how to ask the user for options for the specified chart type");
            }
            // At this point, ctl is always assigned.
            ctl.Tag = parameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, ConfidenceIntervalParameter parameter, ParameterBag context)
        {
            if (parameter.CanDefault && SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval)
                return new FilledParameter(true, SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval);

            // If this is a "standard" CI and the dedicated CI combo isn't in use, use it.  Otherwise, create one in the flow.
            ComboBox cbo;
                TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
                cbo = new ComboBox { Size = new Size(55, 18), FormattingEnabled = true };
                AddAppropriateEventHandlersTo(cbo);
                tlp.Controls.Add(cbo);
                MaybeAddHelpTip(cbo, parameter);

                Label lbl = new Label
                {
                    Tag = parameter,
                    Padding = new Padding(0, 6, 0, 3),
                    AutoSize = true,
                    Text =
                                        parameter.HasPrompt
                                            ? parameter.Prompt(processor, context)
                                            : "Confidence (%)"
                };
                tlp.Controls.Add(lbl);
                MaybeAddHelpTip(lbl, parameter);

            cbo.Tag = parameter;
            cbo.Items.Clear();
            for (int multiplier = 0; multiplier < 500; multiplier++)
            {
                double suggestedValue = parameter.MinimumSuggestedValue + (multiplier * parameter.SuggestedStep);
                if (suggestedValue > parameter.MaximumSuggestedValue)
                    break;
                cbo.Items.Add((suggestedValue * 100.0).ToString("##0.0"));
            }

            // If there's a specific default CI, force it.  If not, don't overwrite the CI combo's value, so that a user can persist CI values between operations.
            if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDouble)
            {
                cbo.Text = (context[parameter.Name].AsDouble * 100.0).ToString("##0.0");
            }
            else if (0.0 != parameter.DefaultValue)
            {
                cbo.Text = (parameter.DefaultValue * 100.0).ToString("##0.0");
            }
            else
            {
                    cbo.Text = SdApplication.SoleInstance.Preferences.CanDefaultConfidenceInterval ? (SdApplication.SoleInstance.Preferences.DefaultConfidenceInterval * 100.0).ToString("##0") : "95";
            }

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, DateParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(80, 18), Tag = parameter };
            if ((!parameter.ForceDefault) && context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsInt32)
            {
                txt.Text = context[parameter.Name].AsInt32.ToString();
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    txt.Text = parameter.DefaultValue(processor, context).ToString("d");
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
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : string.Empty
            };
            tlp.Controls.Add(lbl);
            MaybeAddHelpTip(lbl, parameter);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, DoubleParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(100, 18), Tag = parameter };
            if ((!parameter.ForceDefault) && context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDouble)
            {
                double defaultValue = context[parameter.Name].AsDouble;
                if ((!double.IsNaN(defaultValue)) && defaultValue != Constant.MISSING)
                    txt.Text = context[parameter.Name].AsDouble.ToString();
            }
            else
            {
                double? defaultValue = parameter.DefaultValue(processor, context);
                string defaultValueString = string.Empty;
                if (defaultValue.HasValue && (!double.IsNaN(defaultValue.Value)) && defaultValue.Value != Constant.MISSING)
                    defaultValueString = defaultValue.Value.ToString();
                txt.Text = defaultValueString;
            }
            AddAppropriateEventHandlersTo(txt);

            string suffix = string.Empty;
            if (parameter.ShowLimits)
            {
                double minimumValue = parameter.MinimumValue(processor, context);
                double maximumValue = parameter.MaximumValue(processor, context);
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

            Label lbl = new Label { Tag = parameter, Padding = new Padding(0, 6, 0, 3), AutoSize = true };
            if (parameter.HasPrompt)
                lbl.Text = parameter.Prompt(processor, context) + suffix;
            else
                lbl.Text = suffix;

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
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, Double2By2Parameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
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
            if (context.ContainsKey(parameter.TopLeftName) && null != context[parameter.TopLeftName] && context[parameter.TopLeftName].IsInputParameter && context[parameter.TopLeftName].IsDouble)
                txtTL.Text = context[parameter.TopLeftName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtTL);
            panel2By2.Controls.Add(txtTL, 0, 2);

            TextBox txtTR = new TextBox { Name = "txtTR", Size = new Size(100, 18) };
            if (context.ContainsKey(parameter.TopRightName) && null != context[parameter.TopRightName] && context[parameter.TopRightName].IsInputParameter && context[parameter.TopRightName].IsDouble)
                txtTR.Text = context[parameter.TopRightName].AsDouble.ToString();
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
            if (context.ContainsKey(parameter.BottomLeftName) && null != context[parameter.BottomLeftName] && context[parameter.BottomLeftName].IsInputParameter && context[parameter.BottomLeftName].IsDouble)
                txtBL.Text = context[parameter.BottomLeftName].AsDouble.ToString();
            AddAppropriateEventHandlersTo(txtBL);
            panel2By2.Controls.Add(txtBL, 0, 3);

            TextBox txtBR = new TextBox { Name = "txtBR", Size = new Size(100, 18) };
            if (context.ContainsKey(parameter.BottomRightName) && null != context[parameter.BottomRightName] && context[parameter.BottomRightName].IsInputParameter && context[parameter.BottomRightName].IsDouble)
                txtBR.Text = context[parameter.BottomRightName].AsDouble.ToString();
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

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, Double2By2ByKParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
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
            if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDataFrame)
            {
                DataFrame sourceFrame = context[parameter.Name].AsDataFrame;
                int tableCount = sourceFrame.MinRows / 2;
                if (sourceFrame.VariableCount == 2 && sourceFrame.Variables[0].IsDoubleVariable && sourceFrame.Variables[1].IsDoubleVariable)
                {
                    DoubleVariable var1 = sourceFrame.Variables[0].AsDoubleVariable;
                    DoubleVariable var2 = sourceFrame.Variables[1].AsDoubleVariable;
                    txtTL.Text = var1.Data[0].ToString();
                    txtTR.Text = var2.Data[0].ToString();
                    txtBL.Text = var1.Data[1].ToString();
                    txtBR.Text = var2.Data[1].ToString();
                    lblStratum.Text = "Stratum 1 of " + tableCount.ToString();
                    // Copy the data for maintenance and use by the controls
                    List<double>[] newData = (List<double>[])pnlNavigation.Tag;
                    newData[0].AddRange(var1.Data);
                    newData[1].AddRange(var2.Data);
                }
            }

            tlp.Controls.Add(panel2By2ByK);
            tlp.SetColumnSpan(panel2By2ByK, 2);

            return null;
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
                txtTl.BackColor = SystemColors.Window;
                txtTr.BackColor = SystemColors.Window;
                txtBl.BackColor = SystemColors.Window;
                txtBr.BackColor = SystemColors.Window;
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
                lblStratum.Text = "Stratum " + stratum + " of " + (Math.Max(strata, stratum));
                txtTL.BackColor = SystemColors.Window;
                txtTR.BackColor = SystemColors.Window;
                txtBL.BackColor = SystemColors.Window;
                txtBR.BackColor = SystemColors.Window;

                cmdPrevious.Enabled = true;
                cmdNext.Enabled = true; // Can always Next to create another stratum
            }
            catch (Exception ex)
            {
                SdApplication.SoleInstance.FriendlyError("Couldn't move to next stratum due to an internal error", ex, false);
            }
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, EditGridParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            DataGridView gridEditGrid = new DataGridView();
            ((ISupportInitialize)gridEditGrid).BeginInit();
            DataGridViewTextBoxColumn colKey = new DataGridViewTextBoxColumn();
            DataGridViewTextBoxColumn colValue = new DataGridViewTextBoxColumn();
            gridEditGrid.Tag = parameter;
            gridEditGrid.AllowUserToAddRows = false;
            gridEditGrid.AllowUserToDeleteRows = false;
            gridEditGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            gridEditGrid.ColumnHeadersVisible = false;
            gridEditGrid.Columns.AddRange(new DataGridViewColumn[] { colKey, colValue });
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
            DataFrame sourceFrame = context[egp.Source].AsDataFrame;
            StringVariable keyVariable = sourceFrame.FindVariable(egp.KeyVariable).AsStringVariable;
            StringVariable valueVariable = sourceFrame.FindVariable(egp.ValueVariable).AsStringVariable;
            gridEditGrid.Rows.Clear();
            for (int i = 0; i < keyVariable.Length; i++)
            {
                gridEditGrid.Rows.Add(keyVariable.Data[i], valueVariable.Data[i]);
            }
            gridEditGrid.Visible = true;

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : string.Empty
            };
            tlp.Controls.Add(lbl);
            return null;
        }

        private FilledParameter PrepareCombinedParameter(ITemplateHost host, FillableParameter parameter)
        {
            IFillable fillable = parameter.Fillable;
            string fillerToUse = fillable.FillerToUse;
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            Control ctl;
            switch (fillerToUse)
            {
                case "ChiSquareGoodnessOfFit":
                    ctl = new ctlChiGFOptions((ChiSquareGoodnessOfFitOptions)fillable);
                    break;
                case "ConvertUnits":
                    ctl = new ctlConvertUnits(host);
                    break;
                case "Distribution":
                    ctl = new ctlPDF((DistributionOptions)fillable, host);
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
                    throw new NotImplementedException();
                case "Scores":
                    ctl = new ctlScores((ScoresOptions)fillable);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("parameter", fillable.FillerToUse, "fillableParameter.Fillable.FillerToUse: Unknown option");
            }
            ctl.Tag = parameter;
            tlp.Controls.Add(ctl);
            tlp.SetColumnSpan(ctl, 2);
            return null;
        }

        /// <summary>
        /// If there is a grid presently displayed in the top bar, return it.  Otherwise return null.
        /// </summary>
        /// <returns></returns>
        private WorkbookView FindGridOrNull()
        {
            return FindGridOrNull(GetUserInputTable());
        }

        private WorkbookView FindGridOrNull(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (child is WorkbookView)
                    return (WorkbookView)child;
                if (child.Controls.Count > 0)
                {
                    WorkbookView found = FindGridOrNull(child);
                    if (null != found)
                        return found;
                }
            }
            // If we get here, there's no grid
            return null;
        }

        /// <summary>
        /// If this is called, we know we're acquiring "screen" data in the dialog area rather than data from a loaded worksheet.
        /// </summary>
        /// <param name="processor"></param>
        /// <param name="parameter"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, GridParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            WorkbookView grid = new WorkbookView
            {
                Tag = parameter,
                Name = "grid",
                Size = new Size((int)(494 * currentScaleFactor.Width), (int)(305 * currentScaleFactor.Height)),
                // ContextMenuStrip = contextMenuStrip
            };
            grid.ActiveWorkbookSet.GetLock();
            if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDataFrame)
            {
                IWorksheet sheet = grid.ActiveWorksheet;
                IRange usedRange = sheet.UsedRange;
                DataFrame frame = context[parameter.Name].AsDataFrame;
                for (int col = 0; col < frame.VariableCount; col++)
                {
                    DoubleVariable v = frame.Variables[col].AsDoubleVariable;
                    for (int row = 0; row < v.Length; row++)
                        usedRange.Cells[row, col].Value = v.Data[row];
                }
            }
            grid.ActiveWorksheet.WindowInfo.Zoom = 88; // percent
            int maximumColumns = parameter.MaximumColumns(processor, context);
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
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : string.Empty
            };
            tlp.Controls.Add(lbl);

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, IntegerParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
            TextBox txt = new TextBox { Size = new Size(100, 18), Tag = parameter };
            if ((!parameter.ForceDefault) && context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsInt32)
            {
                txt.Text = context[parameter.Name].AsInt32.ToString();
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    int? defaultValue = parameter.DefaultValue(processor, context);
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
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) + suffix : suffix
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
            return null;
        }

        private static void AutoSizeCombo(ComboBox cbo)
        {
            // There's no way of autosizing a combo... so we do it by hand!
            int width = cbo.DropDownWidth;
            Graphics g = cbo.CreateGraphics();
            Font font = cbo.Font;
            int vertScrollBarWidth = (cbo.Items.Count > cbo.MaxDropDownItems) ? SystemInformation.VerticalScrollBarWidth : 0;

            foreach (object item in cbo.Items)
            {
                string s = item.ToString();
                int newWidth = (int)g.MeasureString(s, font).Width + vertScrollBarWidth;
                if (width < newWidth)
                    width = newWidth;
            }
            cbo.DropDownWidth = width;
            cbo.Size = new Size(width + SystemInformation.VerticalScrollBarWidth, cbo.PreferredHeight); // Surprisingly, it appears the width of the drop-down arrow part of a ComboBox is the same as that of a vertical scrollbar.
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, OptionParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

            switch (parameter.OptionFormatType)
            {
                case OptionFormatType.Dropdown:
                    {
                        string defaultValue = null;
                        if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter)
                        {
                            defaultValue = context[parameter.Name].AsString;
                        }
                        else
                        {
                            if (null != parameter.DefaultValue)
                                defaultValue = processor.Evaluate(parameter.DefaultValue, context).ToString();
                        }

                        ComboBox cbo = new ComboBox { Tag = parameter, MaximumSize = new Size(250, 21) };
                        OptionOption defaultOption = parameter.Options[0];
                        foreach (OptionOption optionOption in parameter.Options)
                        {
                            cbo.Items.Add(optionOption);
                            if (null != defaultValue)
                                if (optionOption.Value.Equals(defaultValue))
                                    defaultOption = optionOption;
                        }
                        cbo.SelectedIndex = 0;
                        cbo.DropDownStyle = ComboBoxStyle.DropDownList;
                        AddAppropriateEventHandlersTo(cbo);
                        if (null != defaultOption)
                            cbo.SelectedItem = defaultOption;

                        // There's no way of autosizing a combo... so we do it by hand!
                        AutoSizeCombo(cbo);

                        Label lbl = new Label
                        {
                            Tag = parameter,
                            Padding = new Padding(0, 6, 0, 3),
                            AutoSize = true,
                            MaximumSize = new Size(500, 500),
                            Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : string.Empty
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
                        if (parameter.HasPrompt)
                        {
                            string prompt = parameter.Prompt(processor, context);
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
                        if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter)
                        {
                            defaultValue = context[parameter.Name].AsString;
                        }
                        else
                        {
                            if (null != parameter.DefaultValue)
                                defaultValue = processor.Evaluate(parameter.DefaultValue, context).ToString();
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
                    throw new ArgumentOutOfRangeException("parameter", parameter.OptionFormatType, "optionParameter.OptionFormatType: Only Dropdown and Radio are known");
            }

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, OptionsParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

            if (parameter.HasPrompt)
            {
                string prompt = parameter.Prompt(processor, context);
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
                if (context.ContainsKey(optionsOption.Name) && null != context[optionsOption.Name] && context[optionsOption.Name].IsInputParameter)
                    isChecked = context[optionsOption.Name].AsBoolean;

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

            return null;
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
                CheckCombinedParameterVisibilityAndMaybeResize((Control)sender);
        }

        void OptionParameter_CheckedChanged(object sender, EventArgs e)
        {
            try
            {
                CheckCombinedParameterVisibilityAndMaybeResize((Control)sender);
            }
            catch (Exception ex)
            {
                EatException(ex);
            }
        }

        private void CheckCombinedParameterVisibilityAndMaybeResize(Control sender)
        {
            while (null != sender)
            {
                if ("TopLevelUserTable".Equals(sender.Tag))
                    break;
                sender = sender.Parent;
            }
            if (null != sender)
            {
                ParameterBag ambientParameters = new ParameterBag();
                ParameterBag context = fillCombinedParametersContext;
                ExtractCurrentValues(new TemplateProcessor(SdApplication.SoleInstance), ambientParameters, context, false);
                if (null != context)
                {
                    // Add in ambient parameters; do not overwrite current parameters (which will include key->null for empty optional parameters)
                    foreach (KeyValuePair<string, FilledParameter> pair in context.Pairs)
                    {
                        if (!ambientParameters.ContainsKey(pair.Key))
                            ambientParameters.Add(pair.Key, pair.Value);
                    }
                }
                // Remove empty optional parameters
                List<string> keysToRemove = new List<string>();
                foreach (KeyValuePair<string, FilledParameter> pair in ambientParameters.Pairs)
                    if (null == pair.Value)
                        keysToRemove.Add(pair.Key);
                foreach (string keyToRemove in keysToRemove)
                    ambientParameters.Remove(keyToRemove);

                if (CheckCombinedParameterVisibility(ambientParameters))
                    ResizeContainer(true);
            }
        }

        private void ResizeContainer(bool v)
        {
            throw new NotImplementedException();
        }

        /// <returns>true if at least one control's visibility was changed (and hence the container might need to resize)</returns>
        private bool CheckCombinedParameterVisibility(ParameterBag ambientParameters)
        {
            TableLayoutPanel tlp = GetUserInputTable();
            TemplateProcessor processor = null;
            bool layoutSuspended = false;
            bool atLeastOneVisibilityChange = false;

            try
            {
                foreach (Control column in tlp.Controls)
                {
                    foreach (Control control in column.Controls)
                    {
                        if (null != control.Tag)
                        {
                            Parameter parameter = (Parameter)control.Tag;
                            if (parameter.HasAcquireIfTrue)
                            {
                                if (null == processor)
                                    processor = new TemplateProcessor(SdApplication.SoleInstance);
                                bool shouldAcquire = parameter.AcquireIfTrue(processor, ambientParameters);
                                if (control.Visible != shouldAcquire)
                                    atLeastOneVisibilityChange = true;
                                if (!layoutSuspended)
                                {
                                    tlp.SuspendLayout();
                                    foreach (Control col in tlp.Controls)
                                        col.SuspendLayout();
                                    layoutSuspended = true;
                                }
                                control.Visible = shouldAcquire;
                            }
                        }
                    }
                }
            }
            finally
            {
                if (layoutSuspended)
                {
                    tlp.ResumeLayout(true);
                    foreach (Control col in tlp.Controls)
                        col.ResumeLayout();
                }
            }
            return atLeastOneVisibilityChange;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, PickVariablesParameter parameter, ParameterBag context)
        {
            DataFrame frame = context[parameter.ParameterName].AsDataFrame;
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
                throw new ArgumentOutOfRangeException("parameter", parameter.MinimumVariables, "pickVariablesParameter.MinimumVariables: Must obtain values for at least one variable");
            if (parameter.MinimumVariables > parameter.MaximumVariables)
                throw new ArgumentException("minimumVariables must not be larger than maximumVariables");

            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

            // Label the parameter above it if required
            if (parameter.HasPrompt)
            {
                Label lbl = new Label
                {
                    Tag = parameter,
                    Padding = new Padding(0, 6, 0, 3),
                    AutoSize = true,
                    Text = parameter.Prompt(processor, context)
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
                    string rubric = (null == frame.Variables[i]) ? string.Empty : frame.Variables[i].Title;
                    cbo.Items.Add(rubric);
                }
                if (null != initialState)
                {
                    if (initialState.Length > v)
                        cbo.SelectedIndex = initialState[v];
                }
                AutoSizeCombo(cbo);
                holder.Controls.Add(cbo);
                Label l = new Label
                {
                    Padding = new Padding(3, 6, 3, 3),
                    AutoSize = true,
                    Text = parameter.LabelAs(processor, context, v)
                };
                holder.Controls.Add(l);
            }
            tlp.Controls.Add(holder);
            tlp.SetColumnSpan(holder, 2);

            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, PickFromListParameter parameter, ParameterBag context)
        {
            DataFrame sourceFrame = context[parameter.Source].AsDataFrame;
            string[] values;
            if (sourceFrame.Variables[0].IsStringVariable)
                values = sourceFrame.Variables[0].AsStringVariable.Data;
            else if (sourceFrame.Variables[0].IsClassifierVariable)
                values = sourceFrame.Variables[0].AsClassifierVariable.SortedCategoryNames;
            else
                throw new ArgumentException("A PickFromListParameter can only pick from string or classifier variables");

            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
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
                AutoSizeCombo(cbo);
                tlp.Controls.Add(cbo);
            }

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : string.Empty
            };
            tlp.Controls.Add(lbl);
            return null;
        }

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, SpecialParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);

            if (("chi-2-column".Equals(parameter.SpecialType))
                || ("chi-3-column".Equals(parameter.SpecialType))
                || ("rr-index".Equals(parameter.SpecialType))
                || ("person-time-size".Equals(parameter.SpecialType))
                || ("likelihood".Equals(parameter.SpecialType)))
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

                Panel colsPanel = new Panel { Padding = new Padding(0, 0, 0, 0), Margin = new Padding(0, 0, 0, 3), Size = new Size(300, 16), AutoSize = true, AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink };
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
                    // ContextMenuStrip = contextMenuStrip,
                    Padding = new Padding(0, 0, 0, 0),
                    Margin = new Padding(0, 0, 0, 0)
                };
                grid.GetLock();
                try
                {
                    if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDataFrame)
                    {
                        DataFrame sourceFrame = context[parameter.Name].AsDataFrame;
                        if (sourceFrame.VariableCount >= 2 && sourceFrame.Variables[0].IsDoubleVariable && sourceFrame.Variables[1].IsDoubleVariable)
                        {
                            DumpIntoSsg((IValues)grid.ActiveWorksheet, 0, sourceFrame.Variables[0].AsDoubleVariable);
                            DumpIntoSsg((IValues)grid.ActiveWorksheet, 1, sourceFrame.Variables[1].AsDoubleVariable);
                            if (has3Columns && sourceFrame.VariableCount >= 3 && sourceFrame.Variables[0].IsDoubleVariable)
                                DumpIntoSsg((IValues)grid.ActiveWorksheet, 2, sourceFrame.Variables[2].AsDoubleVariable);
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
                    int aHair = 3; // Fudge factor: Extra width in pixels for things like scrollbar edges and ensuring that the right-hand end of the last cell is visible
                    int overallWidthInPixels = (int)((rowHeaderWidthInPoints + visibleColumnsWidthInPoints) * pointsToPixels) + SystemInformation.VerticalScrollBarWidth + aHair;
                    grid.Size = new Size((int)(overallWidthInPixels * currentScaleFactor.Width), (int)(400 * currentScaleFactor.Height));
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
                        Location = new Point(rowHeaderWidthInPixels + (0 * oneColumnWidthInPixels) + fudge, 0)
                    };
                    colsPanel.Controls.Add(col1Label);

                    Label col2Label = new Label
                    {
                        Text =
                            (isPersonTimeSize || isRrIndex)
                                ? "Index Person-time"
                                : isLikelihood ? "- feature" : "- failure",
                        AutoSize = true,
                        Location = new Point(rowHeaderWidthInPixels + (1 * oneColumnWidthInPixels) + fudge, 0)
                    };
                    colsPanel.Controls.Add(col2Label);

                    if (has3Columns)
                    {
                        Label col3Label = new Label
                        {
                            Text = isPersonTimeSize ? "Reference size" : "score",
                            AutoSize = true,
                            Location = new Point(rowHeaderWidthInPixels + (2 * oneColumnWidthInPixels) + fudge, 0)
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

                return null;
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

                WorkbookView grid = new WorkbookView { Size = new Size((int)(450 * currentScaleFactor.Width), (int)(400 * currentScaleFactor.Height)) /*, ContextMenuStrip = contextMenuStrip */ };
                grid.GetLock();
                try
                {
                    if (context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsDataFrame)
                    {
                        DataFrame sourceFrame = context[parameter.Name].AsDataFrame;
                        for (int col = 0; col < sourceFrame.VariableCount; col++)
                            if (sourceFrame.Variables[col].IsDoubleVariable)
                                DumpIntoSsg((IValues)grid.ActiveWorksheet, col, sourceFrame.Variables[col].AsDoubleVariable);
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

                return null;
            }
            if ("addedConstant".Equals(parameter.SpecialType))
            {
                double minimumC;
                double suggestedC;
                DataFrame frame = context["data"].AsDataFrame;
                DoubleVariable dv = frame.Variables[0].AsDoubleVariable;
                Sheet.XConstant(dv.Length, 0, dv.Data, out minimumC, out suggestedC);
                context.AddOutput("a_min", minimumC);

                if (minimumC != Constant.MISSING)
                {
                    DoubleParameter dp = new DoubleParameter
                    {
                        Name = parameter.Name,
                        PromptExpression = parameter.PromptExpression,
                        MinimumValueExpression = new Expression(minimumC.ToString()),
                        DefaultValueExpression = new Expression(suggestedC.ToString()),
                        CancelSkipsParameter = "Skip"
                    };
                    return PrepareCombinedParameter(processor, dp, context);
                }
                return null;
            }
            if ("frame".Equals(parameter.SpecialType))
            {
                ctlPickAWindow ctl = new ctlPickAWindow(OutputType.Frame, parameter) { Tag = parameter };
                AddAppropriateEventHandlersTo(ctl);
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("report".Equals(parameter.SpecialType))
            {
                ctlPickAWindow ctl = new ctlPickAWindow(OutputType.Report, parameter) { Tag = parameter };
                AddAppropriateEventHandlersTo(ctl);
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("rubric".Equals(parameter.SpecialType))
            {
                Label ctl = new Label
                {
                    AutoSize = true,
                    Tag = parameter,
                    Text = parameter.Prompt(processor, context)
                };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("textToNumbers".Equals(parameter.SpecialType))
            {
                ctlTextToNumbers ctl = new ctlTextToNumbers(context) { Tag = parameter };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            if ("scores".Equals(parameter.SpecialType))
            {
                ctlScores ctl = new ctlScores(context) { Tag = parameter };
                tlp.Controls.Add(ctl);
                tlp.SetColumnSpan(ctl, 2);
                return null;
            }
            throw new ArgumentOutOfRangeException("parameter", parameter.SpecialType, "parameter.SpecialType: Unknown option");
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

        internal FilledParameter PrepareCombinedParameter(ITemplateProcessor processor, StringParameter parameter, ParameterBag context)
        {
            TableLayoutPanel tlp = GetUserInputTableForColumn(parameter.Column);
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
                txt.Size = new Size(6 + (int)Math.Ceiling(enWidth + ((enEnWidth - enWidth) * (parameter.MaxLength - 1))), 18);
            }
            txt.Tag = parameter;
            if ((!parameter.ForceDefault) && context.ContainsKey(parameter.Name) && null != context[parameter.Name] && context[parameter.Name].IsInputParameter && context[parameter.Name].IsString)
            {
                txt.Text = context[parameter.Name].AsString;
            }
            else
            {
                if (parameter.HasDefaultValue)
                {
                    txt.Text = parameter.DefaultValue(processor, context);
                }
            }
            AddAppropriateEventHandlersTo(txt);

            Label lbl = new Label
            {
                Tag = parameter,
                Padding = new Padding(0, 6, 0, 3),
                AutoSize = true,
                Text = parameter.HasPrompt ? parameter.Prompt(processor, context) : string.Empty
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
            return null;
        }

        /// <summary>
        /// A control is useful if a tab would stop on it, and it is not a label or panel (for some reason, the selection logic stops on those even though they are not TabStops).
        /// </summary>
        /// <param name="c">The control to be tested</param>
        /// <param name="outputControlsAreUseful"></param>
        /// <returns>true if the control is useful, false if not</returns>
        internal bool IsUsefulControl(Control c, bool outputControlsAreUseful)
        {
            if (c is Label
                || c is TableLayoutPanel
                || c is Panel
                || c is CheckBox
                || c is ComboBox
                || c is RadioButton
                || c is VerticalLabel
                || c is GroupBox)
                return false;

            if (!c.Visible)
                return false;

            if (outputControlsAreUseful)
                return true;

            return !(c.Tag is Parameter && ((Parameter)c.Tag).Type == ParameterType.Special && "report".Equals(((SpecialParameter)c.Tag).SpecialType));
        }

        internal void SelectFirstUsefulControlIn(Control c)
        {
            Control next = c;
            do
            {
                // Find the next useful control.
                // To prevent infinite loops, we also check for coming back to the control we tabbed from, and stop if so.
                next = GetNextControl(next, true);
                if (null == next)
                    break;
                if (next == c)
                    break;
            } while (!IsUsefulControl(next, true));
            if (null != next)
            {
                next.Select();
                next.Focus();
                if (next is DataGridView)
                {
                    SendKeys.Send("{tab}+{tab}");
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="host"></param>
        /// <param name="processor"></param>
        /// <param name="context"></param>
        /// <param name="shouldShow"></param>
        /// <param name="cancelSkipsParameterString"></param>
        /// <param name="parametersToValidate"></param>
        /// <param name="outputParameters"></param>
        /// <remarks>This may return key->null in outputParameters for optional empty parameters.  It is up to the caller to deal with this.</remarks>
        internal void FillCombinedParameters(ITemplateHost host, ITemplateProcessor processor, ParameterBag context, bool shouldShow, string cancelSkipsParameterString, ICollection<Parameter> parametersToValidate, ref ParameterBag outputParameters)
        {
            TableLayoutPanel tlp = GetUserInputTable();
            ResumeLayout();
            tlp.ResumeLayout(true);
            foreach (Control col in tlp.Controls)
                col.ResumeLayout();
            CheckCombinedParameterVisibility(context);

            if (!shouldShow)
            {
                bool allValid = ExtractCurrentValues(processor, outputParameters, context, true);
                if (!allValid)
                {
                    // TODO: How on earth do we get people to set valid parameters when the defaults are invalid and we're not supposed to show them anything?
                }
                ClearCombinedParameters();
                return;
            }

            // If we get here, at least one parameter should be shown.
            try
            {
                fillCombinedParametersContext = context;
                SelectFirstUsefulControlIn(tlp); // Must be performed once the operations panel is visible, as Select() only selects controls whose parents are all visible.
                using (new DefaultCursor())
                {
                    while (true)
                    {
                        inputtingData = true;
                        okPressed = false;
                        cancelPressed = false;
                        DoNestedEventLoop();

                        // Did we fall out of the loop?
                        if (!(okPressed || cancelPressed))
                        {
                            outputParameters = null;
                            return;
                        }

                        // Did the user cancel?
                        if (cancelPressed)
                        {
                            // Are all the parameters skippable?
                            bool allSkippable = CheckAllParametersSkippable(tlp);
                            outputParameters = allSkippable ? new ParameterBag() : null;
                            return;
                        }
                        bool allValid = ExtractCurrentValues(processor, outputParameters, context, true);
                        if (allValid)
                        {
                            string validationResult = null;
                            foreach (Parameter outstandingParameter in parametersToValidate)
                            {
                                if (null != outstandingParameter.Validators)
                                    foreach (Validator validator in outstandingParameter.Validators)
                                    {
                                        validationResult = TemplateProcessor.Validate(host, validator.ValidatorName, outstandingParameter, outputParameters, outstandingParameter.ValidationFailMessage);
                                        if (null != validationResult)
                                            break;
                                    }
                                if (null != validationResult)
                                    break;
                            }
                            allValid &= (null == validationResult);
                            if (!allValid)
                                SdApplication.SoleInstance.MsgboxX(validationResult, MessageBoxButtons.OK, MessageBoxIcon.Warning, "StatsDirect", false);
                        }
                        if (allValid)
                        {
                            break;
                        }

                        // Otherwise, at least one parameter's invalid and focus should already have been set to it.  Go round again.
                    }
                }
            }
            finally
            {
                fillCombinedParametersContext = null;
            }

            ClearCombinedParameters();
        }

        void DoNestedEventLoop()
        {
            // wait here until user presses OK or Cancel, or does something else suitable
            do
            {
                Application.DoEvents(); // HACK: Force an inner event loop
                Thread.Sleep(5);
            } while (inputtingData);
            if (null != puntedException)
            {
                Exception ex = puntedException;
                puntedException = null;
                throw ex;
            }
        }

        /// <remarks>Note that outputParameters will contain key->null for parameters that are optional and missing.  Callers must be able to deal with this.</remarks>
        /// <returns>true if doValidation is false, true if everything's valid, false if there are any validation errors</returns>
        bool ExtractCurrentValues(ITemplateProcessor processor, ParameterBag outputParameters, ParameterBag context, bool doValidation)
        {
            bool allValid = true;
            Control firstInvalidControl = null;
            TableLayoutPanel tlp = GetUserInputTable();
            if (null != tlp)
            {
                foreach (Control column in tlp.Controls)
                {
                    foreach (Control control in column.Controls)
                    {
                        Control invalidControlOrNull = ExtractCurrentValue(processor, control, outputParameters, context,
                                                                           doValidation);
                        if (null != invalidControlOrNull && null == firstInvalidControl)
                            firstInvalidControl = invalidControlOrNull;
                        allValid &= (null == invalidControlOrNull);
                    }
                }
            }
            if (!allValid /* && null != firstInvalidControl - always the case */)
            {
                // Set focus to the first invalid control.
                firstInvalidControl.BackColor = Color.FromArgb(192, 255, 255);
                firstInvalidControl.Focus();
            }
            return allValid;
        }

        private TableLayoutPanel GetUserInputTable()
        {
            return (TableLayoutPanel)Controls[0];
        }

        /// <returns>true if no parameters shown or all skippable, false if there are any required parameters</returns>
        bool CheckAllParametersSkippable(TableLayoutPanel tlp)
        {
            bool allSkippable = true;
            foreach (Control control in tlp.Controls)
            {
                bool skippable = CheckParameterSkippable(control);
                allSkippable &= skippable;
            }
            // The CI combo may also be in use
            return allSkippable;
        }

        /// <returns>true if the parameter is skippable, false if required.</returns>
        static bool CheckParameterSkippable(Control control)
        {
            // We're interested in non-label controls that have been tagged with parameters.
            // Labels are uninteresting as they'll never contain a useful user-entered value.
            if (control.Tag is Parameter && typeof(Label) != control.GetType())
            {
                Parameter parameter = (Parameter)control.Tag;
                return null != parameter.CancelSkipsParameter;
            }

            // Don't care... so it's OK.
            return true;
        }

        /// <returns>null if the parameter is valid (or has no validation or validation is disabled), the control to be selected if the parameter fails validation.</returns>
        static Control ExtractCurrentValue(ITemplateProcessor processor, Control control, ParameterBag outputParameters, ParameterBag context, bool doValidation)
        {
            // We're interested in non-label controls that have been tagged with parameters.
            // Labels are uninteresting as they'll never contain a useful user-entered value.
            if (control.Tag is Parameter && typeof(Label) != control.GetType())
            {
                // Hidden controls should never have their values extracted and are always OK.
                if (!control.Visible)
                    return null;

                Parameter parameter = (Parameter)control.Tag;

                switch (parameter.Type)
                {
                    case ParameterType.Boolean:
                        {
                            CheckBox cb = (CheckBox)control;
                            bool value = cb.Checked;
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.ConfidenceInterval:
                        {
                            ComboBox cbo = (ComboBox)control;
                            string raw = cbo.Text.Trim();
                            if (doValidation)
                            {
                                control.BackColor = SystemColors.Window;
                                // Missing or zero-length?
                                if (raw.Length == 0)
                                {
                                    // If the parameter should be filled in, this is an error
                                    if (null == parameter.CancelSkipsParameter)
                                        return cbo;
                                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                                    outputParameters[parameter.Name] = null;
                                    return null;
                                }
                            }
                            double value = Parsing.Cdbl_Txt(raw);
                            // Turn from percentage to fraction
                            if (value != Constant.MISSING)
                                value /= 100.0;
                            // In range?
                            if (value < 0.0 || value > 1.0)
                                return cbo;
                            // If we get here, it's OK.
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.Custom:
                        {
                            if (control is IOkable)
                            {
                                IOkable okable = (IOkable)control;
                                okable.OkClicked();
                            }
                            else if (control is IFillParameterBag)
                            {
                                return ((IFillParameterBag)control).Fill(outputParameters, true);
                            }
                            else
                                throw new ArgumentOutOfRangeException("control", "Couldn't request a custom parameter to fill itself in");
                        }
                        break;
                    case ParameterType.Date:
                        {
                            TextBox txt = (TextBox)control;
                            string raw = txt.Text.Trim();
                            if (doValidation)
                            {
                                control.BackColor = SystemColors.Window;
                                // Missing or zero-length?
                                if (raw.Length == 0)
                                {
                                    // If the parameter should be filled in, this is an error
                                    if (null == parameter.CancelSkipsParameter)
                                        return txt;
                                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                                    outputParameters[parameter.Name] = null;
                                    return null;
                                }
                            }
                            DateTime value = Parsing.Cdate_Txt(raw);
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.Double:
                        {
                            TextBox txt = (TextBox)control;
                            string raw = txt.Text.Trim();
                            bool isMissing = string.IsNullOrWhiteSpace(raw);
                            if (doValidation)
                            {
                                control.BackColor = SystemColors.Window;
                                // Missing or zero-length?
                                if (isMissing)
                                {
                                    // If the parameter should be filled in, this is an error
                                    if (null == parameter.CancelSkipsParameter)
                                        return txt;
                                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                                    outputParameters[parameter.Name] = null;
                                    return null;
                                }
                            }
                            double value = Parsing.Cdbl_Txt(raw);
                            if (doValidation)
                            {
                                // In range?
                                DoubleParameter dp = (DoubleParameter)parameter;
                                double minimumValue = dp.MinimumValue(processor, context);
                                double maximumValue = dp.MaximumValue(processor, context);
                                if (value < minimumValue || value > maximumValue)
                                    return txt;
                            }
                            // If we get here, it's OK.
                            outputParameters[parameter.Name] = isMissing ? null : new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.Double2By2:
                        {
                            Double2By2Parameter parm = (Double2By2Parameter)parameter;
                            TableLayoutPanel panel2By2 = (TableLayoutPanel)control;
                            TextBox txtTL = (TextBox)panel2By2.Controls["txtTL"];
                            TextBox txtTR = (TextBox)panel2By2.Controls["txtTR"];
                            TextBox txtBL = (TextBox)panel2By2.Controls["txtBL"];
                            TextBox txtBR = (TextBox)panel2By2.Controls["txtBR"];

                            double tl = Parsing.Cdbl_Txt(txtTL.Text);
                            double tr = Parsing.Cdbl_Txt(txtTR.Text);
                            double bl = Parsing.Cdbl_Txt(txtBL.Text);
                            double br = Parsing.Cdbl_Txt(txtBR.Text);

                            if (doValidation)
                            {
                                txtTL.BackColor = SystemColors.Window;
                                txtTR.BackColor = SystemColors.Window;
                                txtBL.BackColor = SystemColors.Window;
                                txtBR.BackColor = SystemColors.Window;
                                // Validate
                                if (tl == Constant.MISSING || tl < 0)
                                {
                                    txtTL.SelectAll();
                                    txtTL.Focus();
                                    return txtTL;
                                }
                                if (tr == Constant.MISSING || tr < 0)
                                {
                                    txtTR.SelectAll();
                                    txtTR.Focus();
                                    return txtTR;
                                }
                                if (bl == Constant.MISSING || bl < 0)
                                {
                                    txtBL.SelectAll();
                                    txtBL.Focus();
                                    return txtBL;
                                }
                                if (br == Constant.MISSING || br < 0)
                                {
                                    txtBR.SelectAll();
                                    txtBR.Focus();
                                    return txtBR;
                                }
                            }

                            if (tl != Constant.MISSING)
                                outputParameters[parm.TopLeftName] = new FilledParameter(true, tl);
                            if (tr != Constant.MISSING)
                                outputParameters[parm.TopRightName] = new FilledParameter(true, tr);
                            if (bl != Constant.MISSING)
                                outputParameters[parm.BottomLeftName] = new FilledParameter(true, bl);
                            if (br != Constant.MISSING)
                                outputParameters[parm.BottomRightName] = new FilledParameter(true, br);
                            return null;
                        }
                    case ParameterType.Double2By2ByK:
                        {
                            TableLayoutPanel pnl2By2ByK = (TableLayoutPanel)control;
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

                            if (doValidation)
                            {
                                txtTl.BackColor = SystemColors.Window;
                                txtTr.BackColor = SystemColors.Window;
                                txtBl.BackColor = SystemColors.Window;
                                txtBr.BackColor = SystemColors.Window;
                                // Validate - find the first missing value in the current stratum
                                if (tl == Constant.MISSING || tl < 0)
                                {
                                    txtTl.SelectAll();
                                    txtTl.Focus();
                                    return txtTl;
                                }
                                if (tr == Constant.MISSING || tr < 0)
                                {
                                    txtTr.SelectAll();
                                    txtTr.Focus();
                                    return txtTr;
                                }
                                if (bl == Constant.MISSING || bl < 0)
                                {
                                    txtBl.SelectAll();
                                    txtBl.Focus();
                                    return txtBl;
                                }
                                if (br == Constant.MISSING || br < 0)
                                {
                                    txtBr.SelectAll();
                                    txtBr.Focus();
                                    return txtBr;
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

                            if (doValidation)
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
                                            return txtTl;
                                        }
                                        if (tr == Constant.MISSING || tr < 0)
                                        {
                                            txtTr.SelectAll();
                                            txtTr.Focus();
                                            return txtTr;
                                        }
                                        if (bl == Constant.MISSING || bl < 0)
                                        {
                                            txtBl.SelectAll();
                                            txtBl.Focus();
                                            return txtBl;
                                        }
                                        if (br == Constant.MISSING || br < 0)
                                        {
                                            txtBr.SelectAll();
                                            txtBr.Focus();
                                            return txtBr;
                                        }
                                    }
                                }
                            }

                            DataFrame frame = new DataFrame();
                            DoubleVariable var1 = new DoubleVariable(var1Data.ToArray());
                            DoubleVariable var2 = new DoubleVariable(var2Data.ToArray());
                            frame.Variables.Add(var1);
                            frame.Variables.Add(var2);
                            outputParameters[parameter.Name] = new FilledParameter(true, frame);
                            return null;
                        }
                    case ParameterType.EditGrid:
                        {
                            DataGridView gridEditGrid = (DataGridView)control;
                            string[] data = new string[gridEditGrid.Rows.Count];
                            for (int i = 0; i < gridEditGrid.Rows.Count; i++)
                                data[i] = (string)gridEditGrid.Rows[i].Cells[1].Value;
                            StringVariable newValues = new StringVariable(data);
                            EditGridParameter egp = (EditGridParameter)parameter;
                            newValues.Title = egp.ValueVariable;
                            DataFrame oldFrame = context[egp.Source].AsDataFrame;
                            DataFrame newFrame = new DataFrame();
                            foreach (Variable v in oldFrame.Variables)
                                newFrame.Variables.Add(egp.ValueVariable.Equals(v.Title) ? newValues : v);
                            outputParameters[parameter.Name] = new FilledParameter(true, newFrame);
                            return null;
                        }
                    case ParameterType.Grid:
                        {
                            WorkbookView grid = (WorkbookView)control;
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

                            if (doValidation)
                            {
                                // TODO: Validate
                            }

                            // If we get here, it's valid.
                            outputParameters[parameter.Name] = new FilledParameter(true, frame);
                            return null;
                        }
                    case ParameterType.Integer:
                        {
                            TextBox txt = (TextBox)control;
                            string raw = txt.Text.Trim();
                            if (doValidation)
                            {
                                txt.BackColor = SystemColors.Window;
                                // Missing or zero-length?
                                if (raw.Length == 0)
                                {
                                    // If the parameter should be filled in, this is an error
                                    if (null == parameter.CancelSkipsParameter)
                                        return txt;
                                    // If the parameter is optional and also missing, note the missing in the output parameter bag.  It is up to the caller to deal with nulls in the output parameter bag.
                                    outputParameters[parameter.Name] = null;
                                    return null;
                                }
                            }
                            int value = Parsing.Cint_Txt(raw);
                            if (doValidation)
                            {
                                // In range?
                                IntegerParameter ip = (IntegerParameter)parameter;
                                if (value < ip.MinimumValue || value > ip.MaximumValue)
                                    return txt;
                            }
                            // If we get here, it's OK.
                            outputParameters[parameter.Name] = new FilledParameter(true, value);
                            return null;
                        }
                    case ParameterType.Option:
                        {
                            OptionParameter optionParameter = (OptionParameter)parameter;
                            switch (optionParameter.OptionFormatType)
                            {
                                case OptionFormatType.Dropdown:
                                    {
                                        ComboBox cbo = (ComboBox)control;
                                        OptionOption selectedOption = (OptionOption)cbo.SelectedItem;
                                        outputParameters[parameter.Name] = new FilledParameter(true, selectedOption.Value);
                                        return null;
                                    }
                                case OptionFormatType.Radio:
                                    {
                                        Control maybeGroup = control;
                                        if (maybeGroup is GroupBox)
                                            maybeGroup = maybeGroup.Controls[0];
                                        TableLayoutPanel optionPanel = (TableLayoutPanel)maybeGroup;
                                        bool atLeastOneChecked = false;
                                        foreach (Control c in optionPanel.Controls)
                                        {
                                            RadioButton rad = (RadioButton)c;
                                            if (rad.Checked)
                                            {
                                                outputParameters[parameter.Name] = new FilledParameter(true, rad.Tag);
                                                atLeastOneChecked = true;
                                                break;
                                            }
                                        }
                                        return ((null != optionParameter.CancelSkipsParameter) || atLeastOneChecked || !doValidation) ? null : optionPanel.Controls[0];
                                    }
                                default:
                                    throw new Exception("optionParameter.OptionFormatType: Only Dropdown and Radio are known");
                            }
                        }
                    case ParameterType.Options:
                        {
                            TableLayoutPanel optionsPanel = (TableLayoutPanel)control;
                            foreach (Control c in optionsPanel.Controls)
                            {
                                CheckBox chk = (CheckBox)c;
                                OptionsOption oo = (OptionsOption)chk.Tag;
                                outputParameters[oo.Name] = new FilledParameter(true, chk.Checked);
                            }
                            return null;
                        }
                    case ParameterType.PickFromList:
                        {
                            PickFromListParameter p = (PickFromListParameter)control.Tag;
                            if (p.AllowMultiple)
                            {
                                int offset = p.IncludeNoneEntry ? 1 : 0;
                                ListBox lstPickFromList = (ListBox)control;
                                bool[] selected = new bool[lstPickFromList.Items.Count - offset];
                                foreach (int i in lstPickFromList.SelectedIndices)
                                    selected[i - offset] = true;
                                outputParameters[parameter.Name] = new FilledParameter(true, selected);
                            }
                            else
                            {
                                int offset = p.IncludeNoneEntry ? 1 : 0;
                                ComboBox cbo = (ComboBox)control;
                                bool[] selected = new bool[cbo.Items.Count - offset];
                                if (cbo.SelectedIndex >= offset)
                                    selected[cbo.SelectedIndex - offset] = true;
                                if ((!p.IncludeNoneEntry) || cbo.SelectedIndex > 0)
                                    outputParameters[parameter.Name] = new FilledParameter(true, selected);
                            }
                            return null;
                        }
                    case ParameterType.PickVariables:
                        {
                            TableLayoutPanel pickPanel = (TableLayoutPanel)control;
                            int variables = pickPanel.RowCount;
                            int[] ary = new int[variables];
                            for (int v = 0; v < variables; v++)
                            {
                                ComboBox cbo = (ComboBox)pickPanel.Controls[2 * v];
                                ary[v] = cbo.SelectedIndex;
                            }
                            outputParameters[parameter.Name] = new FilledParameter(true, ary);
                            return null;
                        }
                    case ParameterType.Special:
                        {
                            SpecialParameter specialParameter = (SpecialParameter)parameter;
                            if ("chi-2-column".Equals(specialParameter.SpecialType)
                                || "likelihood".Equals(specialParameter.SpecialType)
                                || "rr-index".Equals(specialParameter.SpecialType)
                                || "chi-3-column".Equals(specialParameter.SpecialType)
                                || "person-time-size".Equals(specialParameter.SpecialType))
                            {
                                TableLayoutPanel ssgContainer = (TableLayoutPanel)control;
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
                                    outputParameters[parameter.Name] = new FilledParameter(true, frame);
                                }
                                return null;
                            }
                            if ("raters-2d".Equals(specialParameter.SpecialType))
                            {
                                TableLayoutPanel ssgContainer = (TableLayoutPanel)control;
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
                                    outputParameters[parameter.Name] = new FilledParameter(true, frame);
                                }
                                return null;
                            }
                            if ("report".Equals(specialParameter.SpecialType)
                                || "frame".Equals(specialParameter.SpecialType)
                                || "dummyVariables".Equals(specialParameter.SpecialType)
                                || "scores".Equals(specialParameter.SpecialType)
                                || "textToNumbers".Equals(specialParameter.SpecialType))
                            {
                                IFillParameterBag ifpb = (IFillParameterBag)control;
                                return ifpb.Fill(outputParameters, doValidation);
                            }
                            throw new Exception("specialParameter.SpecialType: Unknown value");
                        }
                    case ParameterType.String:
                        {
                            TextBox txt = (TextBox)control;
                            outputParameters[parameter.Name] = new FilledParameter(true, txt.Text);
                        }
                        break;
                    default:
                        throw new Exception("Unknown parameter type when parsing results");
                }
            }

            // If we get here, nothing about the control was invalid (but it may never have been a useful control at all!)
            return null;
        }

        /// <summary>
        /// An exception has occurred that we don't want to present to the user.  Silently discard it.  A future implementation might log it for later debug purposes.
        /// </summary>
        private void EatException(Exception ex)
        {
        }

        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);
            // Record the running scale factor used, for sizing controls we add dynamically where they don't do it themselves
            currentScaleFactor = new SizeF(currentScaleFactor.Width * factor.Width, currentScaleFactor.Height * factor.Height);
        }
    }
}