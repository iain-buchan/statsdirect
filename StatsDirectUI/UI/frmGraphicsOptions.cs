using System;
using System.Windows.Forms;

namespace StatsDirect.UI
{
    public partial class frmGraphicsOptions : Form
    {
        private SelectablePictureBox[] picMarkerTypes;
        private SelectablePictureBox[] picStyles;
        private SelectablePictureBox[] picWidths;
        private RadioButton[] rdoSeries;
        private Charting.MarkerType[] workingMarkerTypes;

        public frmGraphicsOptions()
        {
            InitializeComponent();
            SetupArrays();
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
            {
                pb.SelectedChanged += MarkerType_SelectedChanged;
            }

            foreach (SelectablePictureBox pb in picStyles)
            {
                pb.SelectedChanged += Style_SelectedChanged;
            }

            foreach (SelectablePictureBox pb in picWidths)
            {
                pb.SelectedChanged += Width_SelectedChanged;
            }

            foreach (RadioButton rdo in rdoSeries)
            {
                rdo.CheckedChanged += rdoSeries_CheckedChanged;
            }
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

                Charting.MarkerType mt = GetSelectedMarkerType();
                if (null != mt)
                {
                    for (int i = 0; i < picMarkerTypes.Length; i++)
                        if (sender == picMarkerTypes[i])
                            mt.Shape = (Charting.MarkerShape)(i + 1);
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

                Charting.MarkerType mt = GetSelectedMarkerType();
                if (null != mt)
                {
                    for (int i = 0; i < picStyles.Length; i++)
                        if (sender == picStyles[i])
                            mt.Style = (System.Drawing.Drawing2D.DashStyle)i;
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

                Charting.MarkerType mt = GetSelectedMarkerType();
                if (null != mt)
                {
                    for (int i = 0; i < picWidths.Length; i++)
                        if (sender == picWidths[i])
                            mt.Width = i + 1;
                }
            }
        }

        private void cmdCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void cmdOK_Click(object sender, EventArgs e)
        {
            SaveOptions();
            Close();
        }

        private void frmGraphicsOptions_Shown(object sender, EventArgs e)
        {
            LoadOptions();
        }

        private void SaveOptions()
        {
            SDApplication.SoleInstance.Preferences.ShouldUseColour = !chkAllBlack.Checked;
            Charting.ChartRenderer.DefaultBoxAxes = chkBoxAxes.Checked;
            Charting.ChartRenderer.SaveFlags();

            for (int i = 0; i < 10; i++)
            {
                Charting.ChartRenderer.MarkerTypes[i] = workingMarkerTypes[i];
            }
            Charting.ChartRenderer.SaveMarkerTypes();

            Charting.ChartRenderer.DefaultAxisLabelFont = lblAxisLabelFont.Font;
            Charting.ChartRenderer.DefaultAxisTitleFont = lblAxisLabelFont.Font;
            Charting.ChartRenderer.DefaultLabelFont = lblAxisLabelFont.Font;
            Charting.ChartRenderer.DefaultLegendFont = lblAxisLabelFont.Font;
            Charting.ChartRenderer.DefaultTitleFont = lblTitleFont.Font;
            Charting.ChartRenderer.SaveFonts();
        }

        private void LoadOptions()
        {
            lblAxisLabelFont.Font = Charting.ChartRenderer.DefaultLabelFont;
            lblTitleFont.Font = Charting.ChartRenderer.DefaultTitleFont;

            chkAllBlack.Checked = !SDApplication.SoleInstance.Preferences.ShouldUseColour;
            chkBoxAxes.Checked = Charting.ChartRenderer.DefaultBoxAxes;

            workingMarkerTypes = new Charting.MarkerType[10];
            for (int i = 0; i < 10; i++)
            {
                workingMarkerTypes[i] = Charting.ChartRenderer.MarkerTypes[i].Clone();
            }
            rdoSeries1.Checked = true;
            LoadSeriesOptions();
        }

        private void LoadSeriesOptions()
        {
            Charting.MarkerType mt = GetSelectedMarkerType();
            if (null != mt)
            {
                picMarkerTypes[(int)mt.Shape - 1].Selected = true;
                picWidths[(int)mt.Width - 1].Selected = true;
                picStyles[(int)mt.Style].Selected = true;
                colorPanel.Color = mt.Color;
            }
        }

        private void colorPanel_ColorChanged(object sender, PJLControls.ColorChangedEventArgs e)
        {
            Charting.MarkerType mt = GetSelectedMarkerType();
            if (null != mt)
                mt.Color = colorPanel.Color;
        }

        private Charting.MarkerType GetSelectedMarkerType()
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
    }
}
