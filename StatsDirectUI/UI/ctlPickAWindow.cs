using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Forms;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlPickAWindow : UserControl, IFillParameterBag
    {
        private OutputType outputType;
        private readonly string parameterName;
        private RelativePosition writePosition;

        /// <summary>
        /// For use when this control is embedded in a designer-generated form.
        /// </summary>
        public ctlPickAWindow()
        {
            InitializeComponent();
            WritePosition = RelativePosition.AfterSelection;
        }

        public ctlPickAWindow(OutputType outputType, Parameter parameter)
        {
            InitializeComponent();
            parameterName = parameter.Name;
            this.outputType = outputType;
            bool preferBefore = parameter is SpecialParameter && null != ((SpecialParameter) parameter).ExtraData &&
                                (bool) ((object[])((SpecialParameter) parameter).ExtraData)[0];
            WritePosition = preferBefore ? RelativePosition.BeforeSelection : RelativePosition.AfterSelection;
        }

        [Browsable(true)]
        public RelativePosition WritePosition
        {
            get { return writePosition; }
            set
            {
                switch (value)
                {
                    case RelativePosition.FirstColumn:
                        rdoFirstColumn.Checked = true;
                        break;
                    case RelativePosition.BeforeSelection:
                        rdoBeforeSelection.Checked = true;
                        break;
                    case RelativePosition.AfterSelection:
                        rdoAfterSelection.Checked = true;
                        break;
                    case RelativePosition.LastColumn:
                        rdoLastColumn.Checked = true;
                        break;
                }
                writePosition = value;
            }
        }

        private void SetPanesAndLocations()
        {
            // We don't have the sole instance in design mode, so fake it
            if (DesignMode)
            {
                cboWindows.Items.Add("... destinations go here...");
                return;
            }

            IList<PaneAndPosition> panesAndPositions;
            string newName;
            Pane defaultSelection = null;
            switch (outputType)
            {
                case OutputType.Frame:
                    panesAndPositions = SDApplication.SoleInstance.AvailableFramePanesAndPositions();
                    newName = "New workbook";
                    if (null != SDApplication.SoleInstance.ActiveGrid)
                        defaultSelection = SDApplication.SoleInstance.ActiveGrid.Window.SelectedPane;
                    break;
                case OutputType.Report:
                    panesAndPositions = SDApplication.SoleInstance.AvailableReportPanesAndPositions();
                    newName = "New report";
                    if (null != SDApplication.SoleInstance.MostRecentlySelectedReport)
                        defaultSelection = SDApplication.SoleInstance.MostRecentlySelectedReport.Pane;
                    rdoAfterSelection.Visible = false;
                    rdoBeforeSelection.Visible = false;
                    rdoFirstColumn.Visible = false;
                    rdoLastColumn.Visible = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("outputType", outputType, "Only Frame and Report known");
            }
            SetWindows(panesAndPositions, newName, defaultSelection);
        }

        [Browsable(true)]
        public bool ShowLabel
        {
            get { return lblSelectWindow.Visible; }
            set
            {
                lblSelectWindow.Visible = value;
            }
        }

        [Browsable(true)]
        public OutputType OutputType
        {
            get { return outputType; }
            set { outputType = value; }
        }

        internal void SetWindows(IList<PaneAndPosition> info, string NewName, Pane defaultSelection)
        {
            int indexToSelect = -1;
            foreach (PaneAndPosition paneAndLocation in info)
            {
                cboWindows.Items.Add(paneAndLocation.Pane);
                if (paneAndLocation.Pane.Equals(defaultSelection))
                {
                    indexToSelect = cboWindows.Items.Count - 1;
                }
            }
            if (null != NewName)
                cboWindows.Items.Add(new Pane(NewName, null, null));
            if (indexToSelect >= 0)
                cboWindows.SelectedIndex = indexToSelect;

            if (cboWindows.Items.Count > 0 && cboWindows.SelectedIndex < 0)
                cboWindows.SelectedIndex = 0;
        }

        internal PaneAndPosition SelectedPaneAndPosition()
        {
            if (null == cboWindows.SelectedItem)
                return null;
            return new PaneAndPosition((Pane)cboWindows.SelectedItem, writePosition);
        }

        public event KeyPressEventHandler InsideKeyPress
        {
            add
            {
                cboWindows.KeyPress += value;
            }
            remove
            {
                cboWindows.KeyPress -= value;
            }
        }

        protected override void Select(bool directed, bool forward)
        {
            cboWindows.Select();
        }

        #region IFillParameterBag Members

        public Control Fill(ParameterBag outputParameters, bool doValidation)
        {
            PaneAndPosition selectedPaneAndPosition = SelectedPaneAndPosition();
            outputParameters.SetOutput(parameterName, selectedPaneAndPosition);
            switch (outputType)
            {
                case OutputType.Frame:
                    // No longer required, as grids are selected more dynamically - and if you output to a different one, we don't want to break where the input's coming from.
                    // SDApplication.SoleInstance.MostRecentlySelectedGrid = selectedPane;
                    break;
                case OutputType.Report:
                    SDApplication.SoleInstance.MostRecentlySelectedReport = selectedPaneAndPosition;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("outputType", outputType, "Only Frame and Report known");
            }
            return null;
        }

        #endregion

        private void ctlPickAWindow_Load(object sender, EventArgs e)
        {
            SetPanesAndLocations();
        }

        private void rdoFirstColumn_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoFirstColumn.Checked)
                writePosition = RelativePosition.FirstColumn;
        }

        private void rdoBeforeSelection_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoBeforeSelection.Checked)
                writePosition = RelativePosition.BeforeSelection;
        }

        private void rdoAfterSelection_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoAfterSelection.Checked)
                writePosition = RelativePosition.AfterSelection;
        }

        private void rdoLastColumn_CheckedChanged(object sender, EventArgs e)
        {
            if (rdoLastColumn.Checked)
                writePosition = RelativePosition.LastColumn;
        }
    }

    public enum OutputType
    {
        Frame,
        Report
    }
}