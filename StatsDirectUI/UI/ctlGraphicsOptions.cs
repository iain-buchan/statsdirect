using System;
using System.Windows.Forms;
using StatsDirect.Charting;
using ChartRenderer = StatsDirect.Charting.Renderer.ChartRenderer;

namespace StatsDirect.UI
{
    public partial class ctlGraphicsOptions : UserControl, IOkable
    {
        private SelectablePictureBox[] picMarkerTypes;
        private SelectablePictureBox[] picStyles;
        private SelectablePictureBox[] picWidths;
        private RadioButton[] rdoSeries;
        private MarkerType[] workingMarkerTypes;

        public ctlGraphicsOptions()
        {
            InitializeComponent();
            SetupArrays();
            LoadOptions();
        }

        private void SetupArrays()
        {
            picMarkerTypes = new SelectablePictureBox[8];
            picMarkerTypes[0] = picCircle;
            picMarkerTypes[1] = picSquare;
            picMarkerTypes[2] = picTriangle;
            picMarkerTypes[3] = picPlus;
            picMarkerTypes[4] = picCross;
            picMarkerTypes[5] = picPhi;
            picMarkerTypes[6] = picSquare1;
            picMarkerTypes[7] = picSquare2;

            picStyles = new SelectablePictureBox[5];
            picStyles[0] = picStyle1;
            picStyles[1] = picStyle2;
            picStyles[2] = picStyle3;
            picStyles[3] = picStyle4;
            picStyles[4] = picStyle5;

            picWidths = new SelectablePictureBox[4];
            picWidths[0] = picWidth1;
            picWidths[1] = picWidth2;
            picWidths[2] = picWidth3;
            picWidths[3] = picWidth4;

            rdoSeries = new RadioButton[10];
            rdoSeries[0] = rdoSeries1;
            rdoSeries[1] = rdoSeries2;
            rdoSeries[2] = rdoSeries3;
            rdoSeries[3] = rdoSeries4;
            rdoSeries[4] = rdoSeries5;
            rdoSeries[5] = rdoSeries6;
            rdoSeries[6] = rdoSeries7;
            rdoSeries[7] = rdoSeries8;
            rdoSeries[8] = rdoSeries9;
            rdoSeries[9] = rdoSeries10;

            foreach (SelectablePictureBox pb in picMarkerTypes)
                pb.SelectedChanged += MarkerType_SelectedChanged;

            foreach (SelectablePictureBox pb in picStyles)
                pb.SelectedChanged += Style_SelectedChanged;

            foreach (SelectablePictureBox pb in picWidths)
                pb.SelectedChanged += Width_SelectedChanged;

            foreach (RadioButton rdo in rdoSeries)
                rdo.CheckedChanged += rdoSeries_CheckedChanged;
        }

        void rdoSeries_CheckedChanged(object sender, EventArgs e)
        {
            LoadSeriesOptions();
        }

        void MarkerType_SelectedChanged(object sender, EventArgs e)
        {
            if (((SelectablePictureBox)sender).Selected)
            {
                // A box has been selected.  Deselect anything else in the same array
                foreach (SelectablePictureBox pb in picMarkerTypes)
                    if (pb.Selected && pb != sender)
                        pb.Selected = false;

                MarkerType mt = GetSelectedMarkerType();
                if (null != mt)
                {
                    for (int i = 0; i < picMarkerTypes.Length; i++)
                        if (sender == picMarkerTypes[i])
                            mt.MarkerShape = (MarkerShape)(i + 1);
                }
            }
        }

        void Style_SelectedChanged(object sender, EventArgs e)
        {
            if (((SelectablePictureBox)sender).Selected)
            {
                // A box has been selected.  Deselect anything else in the same array
                foreach (SelectablePictureBox pb in picStyles)
                    if (pb.Selected && pb != sender)
                        pb.Selected = false;

                MarkerType mt = GetSelectedMarkerType();
                if (null != mt)
                {
                    for (int i = 0; i < picStyles.Length; i++)
                        if (sender == picStyles[i])
                            mt.LineDashStyle = (System.Drawing.Drawing2D.DashStyle)i;
                }
            }
        }

        void Width_SelectedChanged(object sender, EventArgs e)
        {
            if (((SelectablePictureBox)sender).Selected)
            {
                // A box has been selected.  Deselect anything else in the same array
                foreach (SelectablePictureBox pb in picWidths)
                    if (pb.Selected && pb != sender)
                        pb.Selected = false;

                MarkerType mt = GetSelectedMarkerType();
                if (null != mt)
                {
                    for (int i = 0; i < picWidths.Length; i++)
                        if (sender == picWidths[i])
                            mt.Width = i + 1;
                }
            }
        }

        private void SaveOptions()
        {
            SdApplication.SoleInstance.Preferences.ShouldUseColour = !chkAllBlack.Checked;
            ChartRenderer.DefaultBoxAxes = chkBoxAxes.Checked;
            ChartRenderer.SaveFlags();

            for (int i = 0; i < 10; i++)
            {
                ChartRenderer.MarkerTypes[i] = workingMarkerTypes[i];
            }
            ChartRenderer.SaveMarkerTypes();

            ChartRenderer.DefaultAxisLabelFont = ChartRenderer.SaveStringFromFont(lblAxisLabelFont.Font);
            ChartRenderer.DefaultAxisTitleFont = ChartRenderer.SaveStringFromFont(lblAxisLabelFont.Font);
            ChartRenderer.DefaultLabelFont = ChartRenderer.SaveStringFromFont(lblAxisLabelFont.Font);
            ChartRenderer.DefaultLegendFont = ChartRenderer.SaveStringFromFont(lblAxisLabelFont.Font);
            ChartRenderer.DefaultTitleFont = ChartRenderer.SaveStringFromFont(lblTitleFont.Font);
            ChartRenderer.SaveFonts();
        }

        private void LoadOptions()
        {
            lblAxisLabelFont.Font = ChartRenderer.FontFromSaveString(ChartRenderer.DefaultLabelFont);
            lblTitleFont.Font = ChartRenderer.FontFromSaveString(ChartRenderer.DefaultTitleFont);

            chkAllBlack.Checked = !SdApplication.SoleInstance.Preferences.ShouldUseColour;
            chkBoxAxes.Checked = ChartRenderer.DefaultBoxAxes;

            workingMarkerTypes = new MarkerType[10];
            for (int i = 0; i < 10; i++)
            {
                workingMarkerTypes[i] = ChartRenderer.MarkerTypes[i].Clone();
            }
            rdoSeries1.Checked = true;
            LoadSeriesOptions();
        }

        private void LoadSeriesOptions()
        {
            MarkerType mt = GetSelectedMarkerType();
            if (null != mt)
            {
                picMarkerTypes[(int)mt.MarkerShape - 1].Selected = true;
                picWidths[(int)mt.Width - 1].Selected = true;
                picStyles[(int)mt.LineDashStyle].Selected = true;
                colorPanel.Color = mt.MarkerColor;
            }
        }

        private void colorPanel_ColorChanged(object sender, PJLControls.ColorChangedEventArgs e)
        {
            MarkerType mt = GetSelectedMarkerType();
            if (null != mt)
            {
                mt.MarkerColor = colorPanel.Color;
                mt.LineColor = colorPanel.Color;
            }
        }

        private MarkerType GetSelectedMarkerType()
        {
            if (null != workingMarkerTypes)
            {
                for (int i = 0; i < 10; i++)
                {
                    if (rdoSeries[i].Checked)
                    {
                        return workingMarkerTypes[i];
                    }
                }
            }
            return null;
        }

        private void cmdChangeTitleFont_Click(object sender, EventArgs e)
        {
            using (FontDialog dlg = new FontDialog())
            {
                dlg.Font = lblTitleFont.Font;
                dlg.ShowColor = false;
                dlg.ShowApply = false;
                dlg.ShowEffects = false;
                dlg.ShowHelp = false;
                DialogResult result = dlg.ShowDialog(this);
                if (DialogResult.OK == result)
                {
                    lblTitleFont.Font = dlg.Font;
                }
            }
        }

        private void cmdChangeAxisLabelFont_Click(object sender, EventArgs e)
        {
            using (FontDialog dlg = new FontDialog())
            {
                dlg.Font = lblAxisLabelFont.Font;
                dlg.ShowColor = false;
                dlg.ShowApply = false;
                dlg.ShowEffects = false;
                dlg.ShowHelp = false;
                DialogResult result = dlg.ShowDialog(this);
                if (DialogResult.OK == result)
                {
                    lblAxisLabelFont.Font = dlg.Font;
                }
            }
        }

        void IOkable.OkClicked()
        {
            SaveOptions();
        }

    }
}
