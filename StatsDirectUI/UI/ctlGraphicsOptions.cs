using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Windows.Forms;
using StatsDirect.Charting;
using StatsDirect.Templates;

namespace StatsDirect.UI
{
    public partial class ctlGraphicsOptions : UserControl, IOkable
    {
        private SelectablePictureBox[] picMarkerTypes;
        private SelectablePictureBox[] picStyles;
        private SelectablePictureBox[] picWidths;
        private RadioButton[] rdoSeries;
        private MarkerType[] workingMarkerTypes;

        private IChartPreferences ChartPreferences { get; }

        public ctlGraphicsOptions(IChartPreferences chartPreferences)
        {
            ChartPreferences = chartPreferences;
            InitializeComponent();
            SetupArrays();
            LoadOptions();
        }

        [MemberNotNull(nameof(picMarkerTypes))]
        [MemberNotNull(nameof(picStyles))]
        [MemberNotNull(nameof(picWidths))]
        [MemberNotNull(nameof(rdoSeries))]
        private void SetupArrays()
        {
            picMarkerTypes = new[]
            {
                picCircle,
                picSquare,
                picTriangle,
                picPlus,
                picCross,
                picPhi,
                picSquare1,
                picSquare2
            };

            picStyles = new[]
            {
                picStyle1,
                picStyle2,
                picStyle3,
                picStyle4,
                picStyle5
            };

            picWidths = new[]
            {
                picWidth1,
                picWidth2,
                picWidth3,
                picWidth4
            };

            rdoSeries = new[]
            {
                rdoSeries1,
                rdoSeries2,
                rdoSeries3,
                rdoSeries4,
                rdoSeries5,
                rdoSeries6,
                rdoSeries7,
                rdoSeries8,
                rdoSeries9,
                rdoSeries10
            };

            foreach (SelectablePictureBox pb in picMarkerTypes)
                pb.SelectedChanged += MarkerType_SelectedChanged;

            foreach (SelectablePictureBox pb in picStyles)
                pb.SelectedChanged += Style_SelectedChanged;

            foreach (SelectablePictureBox pb in picWidths)
                pb.SelectedChanged += Width_SelectedChanged;

            foreach (RadioButton rdo in rdoSeries)
                rdo.CheckedChanged += rdoSeries_CheckedChanged;
        }

        void rdoSeries_CheckedChanged(object? sender, EventArgs e)
        {
            LoadSeriesOptions();
        }

        void MarkerType_SelectedChanged(object? sender, EventArgs e)
        {
            if (((SelectablePictureBox)sender).Selected)
            {
                // A box has been selected.  Deselect anything else in the same array
                foreach (SelectablePictureBox pb in picMarkerTypes)
                    if (pb.Selected && pb != sender)
                        pb.Selected = false;

                MarkerType? mt = GetSelectedMarkerType();
                if (null != mt)
                    for (int i = 0; i < picMarkerTypes.Length; i++)
                        if (sender == picMarkerTypes[i])
                            SetSelectedMarkerType(new MarkerType(mt, markerShape: (MarkerShape)(i + 1)));
            }
        }

        void Style_SelectedChanged(object? sender, EventArgs e)
        {
            if (((SelectablePictureBox)sender).Selected)
            {
                // A box has been selected.  Deselect anything else in the same array
                foreach (SelectablePictureBox pb in picStyles)
                    if (pb.Selected && pb != sender)
                        pb.Selected = false;

                MarkerType? mt = GetSelectedMarkerType();
                if (null != mt)
                    for (int i = 0; i < picStyles.Length; i++)
                        if (sender == picStyles[i])
                            SetSelectedMarkerType(new MarkerType(mt, lineDashStyle: (DashStyleDescriptor)i));
            }
        }

        void Width_SelectedChanged(object? sender, EventArgs e)
        {
            if (((SelectablePictureBox)sender).Selected)
            {
                // A box has been selected.  Deselect anything else in the same array
                foreach (SelectablePictureBox pb in picWidths)
                    if (pb.Selected && pb != sender)
                        pb.Selected = false;

                MarkerType? mt = GetSelectedMarkerType();
                if (null != mt)
                    for (int i = 0; i < picWidths.Length; i++)
                        if (sender == picWidths[i])
                            SetSelectedMarkerType(new MarkerType(mt, width: i + 1));
            }
        }

        private void SaveOptions()
        {
            ChartPreferences.UseColour = !chkAllBlack.Checked;
            ChartPreferences.BoxAxes = chkBoxAxes.Checked;

            for (int i = 0; i < 10; i++)
                ChartPreferences.MarkerTypes[i] = workingMarkerTypes[i];

            ChartPreferences.AxisLabelFont = FontCache.DescriptorFromFont(lblAxisLabelFont.Font);
            ChartPreferences.AxisTitleFont = FontCache.DescriptorFromFont(lblAxisLabelFont.Font);
            ChartPreferences.LabelFont = FontCache.DescriptorFromFont(lblAxisLabelFont.Font);
            ChartPreferences.LegendFont = FontCache.DescriptorFromFont(lblAxisLabelFont.Font);
            ChartPreferences.TitleFont = FontCache.DescriptorFromFont(lblTitleFont.Font);

            ChartPreferences.Save();
        }

        [MemberNotNull(nameof(workingMarkerTypes))]
        private void LoadOptions()
        {
            lblAxisLabelFont.Font = FontCache.FontFromDescriptor(ChartPreferences.LabelFont);
            lblTitleFont.Font = FontCache.FontFromDescriptor(ChartPreferences.TitleFont);

            chkAllBlack.Checked = !ChartPreferences.UseColour;
            chkBoxAxes.Checked = ChartPreferences.BoxAxes;

            workingMarkerTypes = new MarkerType[10];
            for (int i = 0; i < 10; i++)
                workingMarkerTypes[i] = ChartPreferences.MarkerTypes[i];
            rdoSeries1.Checked = true;
            LoadSeriesOptions();
        }

        private void LoadSeriesOptions()
        {
            MarkerType? mt = GetSelectedMarkerType();
            if (null != mt)
            {
                picMarkerTypes[(int)mt.MarkerShape - 1].Selected = true;
                picWidths[(int)mt.Width - 1].Selected = true;
                picStyles[(int)mt.LineDashStyle].Selected = true;
                colorPanel.Color = ToColor(mt.MarkerColor);
            }
        }

        private static Color ToColor(ColorDescriptor color) => Color.FromArgb(color.R, color.G, color.B);

        private static ColorDescriptor ToColorDescriptor(Color color) => new ColorDescriptor(color.R, color.G, color.B);

        private void colorPanel_ColorChanged(object? sender, PJLControls.ColorChangedEventArgs e)
        {
            MarkerType? mt = GetSelectedMarkerType();
            if (null != mt)
            {
                ColorDescriptor colorDescriptor = ToColorDescriptor(colorPanel.Color);
                mt = new(mt, 
                    markerColor: colorDescriptor,
                    lineColor: colorDescriptor);
                SetSelectedMarkerType(mt);
            }
        }

        private MarkerType? GetSelectedMarkerType()
        {
            int? index = GetSelectedMarkerIndex();
            return index.HasValue
                ? workingMarkerTypes[index.Value]
                : null;
        }

        private void SetSelectedMarkerType(MarkerType mt)
        {
            int? index = GetSelectedMarkerIndex();
            if (index.HasValue)
                workingMarkerTypes[index.Value] = mt;
        }

        private int? GetSelectedMarkerIndex()
        {
            if (null != workingMarkerTypes)
                for (int i = 0; i < 10; i++)
                    if (rdoSeries[i].Checked)
                        return i;
            return default;
        }

        private void cmdChangeTitleFont_Click(object? sender, EventArgs e)
        {
            using FontDialog dlg = new();
            dlg.Font = lblTitleFont.Font;
            dlg.ShowColor = false;
            dlg.ShowApply = false;
            dlg.ShowEffects = false;
            dlg.ShowHelp = false;
            DialogResult result = dlg.ShowDialog(this);
            if (DialogResult.OK == result)
                lblTitleFont.Font = dlg.Font;
        }

        private void cmdChangeAxisLabelFont_Click(object? sender, EventArgs e)
        {
            using FontDialog dlg = new();
            dlg.Font = lblAxisLabelFont.Font;
            dlg.ShowColor = false;
            dlg.ShowApply = false;
            dlg.ShowEffects = false;
            dlg.ShowHelp = false;
            DialogResult result = dlg.ShowDialog(this);
            if (DialogResult.OK == result)
                lblAxisLabelFont.Font = dlg.Font;
        }

        void IOkable.OkClicked()
        {
            SaveOptions();
        }
    }
}
