using System;
using System.Collections.Generic;
using System.Windows.Forms;
using StatsDirect.Charting;

namespace StatsDirect.UI
{
    public partial class ctlSeriesOptions : UserControl
    {
        private IList<SeriesOptionsDescriptor> seriesOptionsDescriptors;
        private IList<MarkerType> markerTypes;
        private SeriesOptionsDescriptor selectedDescriptor;

        public ctlSeriesOptions()
        {
            InitializeComponent();
        }

        void rdoSeries_CheckedChanged(object sender, EventArgs e)
        {
            if (((RadioButton)sender).Checked)
            {
                LoadSeriesOptions();
            }
            else
            {
                SaveSeriesOptions();
            }
        }

        public void Save()
        {
            SaveSeriesOptions();
        }

        private void SaveSeriesOptions()
        {
            ctlOneSeriesOptions1.Save();
        }

        /// <summary>
        /// Series option descriptors are now known.  Create enough options that they're set up for each series.
        /// </summary>
        private void LoadOptions()
        {
            // If we don't yet have any marker types passed in by an outside party, fake it from the saved details.
            if (null == markerTypes && null != seriesOptionsDescriptors)
                markerTypes = Charting.Renderer.AbstractChartRenderer.MarkersFromDescriptors(seriesOptionsDescriptors, ShouldForceIsFilled, ForcedIsFilled, ShouldForceFillStyle, ForcedFillStyle);

            cboSeries.Items.Clear();
            if (null != seriesOptionsDescriptors)
            {
                for (int i = 0; i < seriesOptionsDescriptors.Count; i++)
                {
                    string seriesName = seriesOptionsDescriptors[i].SeriesName ?? "Series " + (i + 1).ToString();
                    cboSeries.Items.Add(seriesName);
                }
                if (cboSeries.Items.Count > 0)
                    cboSeries.SelectedIndex = 0;
                // LoadSeriesOptions();
            }
        }

        private void LoadSeriesOptions()
        {
            if (null != selectedDescriptor)
            {
                SeriesOptionsDescriptor sod = selectedDescriptor;
                int markerIndex = sod.MarkerIndex;
                MarkerType mt = markerTypes[markerIndex];
                ctlOneSeriesOptions1.ShowDashStyle = sod.AllowChangeToDashStyle;
                ctlOneSeriesOptions1.ShowLineThickness = sod.AllowChangeToLineThickness;
                ctlOneSeriesOptions1.ShowMarkerColour = sod.AllowChangeToMarkerColour;
                ctlOneSeriesOptions1.ShowLineColour = sod.AllowChangeToLineColour;
                ctlOneSeriesOptions1.ShowMarkerSize = sod.AllowChangeToMarkerSize;
                ctlOneSeriesOptions1.ShowMarkerStyle = sod.AllowChangeToMarkerType;
                ctlOneSeriesOptions1.ShowFillStyle = sod.AllowChangeToFill;
                ctlOneSeriesOptions1.MarkerType = mt;
            }
        }

        public IList<MarkerType> MarkerTypes
        {
            get { return markerTypes; }
            set { markerTypes = value; }
        }

        public IList<SeriesOptionsDescriptor> SeriesOptionsDescriptors
        {
            get { return seriesOptionsDescriptors; }
            set
            {
                seriesOptionsDescriptors = value;
                LoadOptions();
            }
        }

        public bool ShouldForceIsFilled { get; set; }
        public bool ForcedIsFilled { get; set; }
        public bool ShouldForceFillStyle { get; set; }
        public FillStyle ForcedFillStyle { get; set; }

        private void cboSeries_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (null != selectedDescriptor)
                SaveSeriesOptions();
            if (cboSeries.SelectedIndex < 0)
                selectedDescriptor = null;
            else
            {
                selectedDescriptor = seriesOptionsDescriptors[cboSeries.SelectedIndex];
                LoadSeriesOptions();
            }
        }

        public void SetColour(bool useColour)
        {
            ctlOneSeriesOptions1.SetColour(useColour);
        }

        public void SetShowLineOptions(bool showLineOptions)
        {
            ctlOneSeriesOptions1.SetShowLineOptions(showLineOptions);
        }

        internal void SetShowMarkerOptions(bool showMarkerOptions)
        {
            ctlOneSeriesOptions1.SetShowMarkerOptions(showMarkerOptions);
        }
    }
}
