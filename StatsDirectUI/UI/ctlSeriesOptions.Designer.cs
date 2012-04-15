namespace StatsDirect.UI
{
    partial class ctlSeriesOptions
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.grpSeries = new System.Windows.Forms.GroupBox();
            this.tlpSeriesAndDetail = new System.Windows.Forms.TableLayoutPanel();
            this.ctlOneSeriesOptions1 = new StatsDirect.UI.ctlOneSeriesOptions();
            this.pnlSeries = new System.Windows.Forms.Panel();
            this.lblSeries = new System.Windows.Forms.Label();
            this.cboSeries = new System.Windows.Forms.ComboBox();
            this.grpSeries.SuspendLayout();
            this.tlpSeriesAndDetail.SuspendLayout();
            this.pnlSeries.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpSeries
            // 
            this.grpSeries.AutoSize = true;
            this.grpSeries.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.grpSeries.Controls.Add(this.tlpSeriesAndDetail);
            this.grpSeries.Location = new System.Drawing.Point(0, 0);
            this.grpSeries.Name = "grpSeries";
            this.grpSeries.Size = new System.Drawing.Size(194, 195);
            this.grpSeries.TabIndex = 0;
            this.grpSeries.TabStop = false;
            this.grpSeries.Text = "Variable";
            // 
            // tlpSeriesAndDetail
            // 
            this.tlpSeriesAndDetail.AutoSize = true;
            this.tlpSeriesAndDetail.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpSeriesAndDetail.ColumnCount = 1;
            this.tlpSeriesAndDetail.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpSeriesAndDetail.Controls.Add(this.ctlOneSeriesOptions1, 0, 1);
            this.tlpSeriesAndDetail.Controls.Add(this.pnlSeries, 0, 0);
            this.tlpSeriesAndDetail.Location = new System.Drawing.Point(6, 19);
            this.tlpSeriesAndDetail.Name = "tlpSeriesAndDetail";
            this.tlpSeriesAndDetail.RowCount = 2;
            this.tlpSeriesAndDetail.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpSeriesAndDetail.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpSeriesAndDetail.Size = new System.Drawing.Size(182, 157);
            this.tlpSeriesAndDetail.TabIndex = 0;
            // 
            // ctlOneSeriesOptions1
            // 
            this.ctlOneSeriesOptions1.AutoSize = true;
            this.ctlOneSeriesOptions1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ctlOneSeriesOptions1.Location = new System.Drawing.Point(0, 27);
            this.ctlOneSeriesOptions1.Margin = new System.Windows.Forms.Padding(0);
            this.ctlOneSeriesOptions1.MarkerType = null;
            this.ctlOneSeriesOptions1.Name = "ctlOneSeriesOptions1";
            this.ctlOneSeriesOptions1.ShowDashStyle = true;
            this.ctlOneSeriesOptions1.ShowFillStyle = true;
            this.ctlOneSeriesOptions1.ShowLineThickness = true;
            this.ctlOneSeriesOptions1.ShowMarkerColour = true;
            this.ctlOneSeriesOptions1.ShowMarkerSize = true;
            this.ctlOneSeriesOptions1.ShowMarkerStyle = true;
            this.ctlOneSeriesOptions1.Size = new System.Drawing.Size(182, 130);
            this.ctlOneSeriesOptions1.TabIndex = 1;
            // 
            // pnlSeries
            // 
            this.pnlSeries.AutoSize = true;
            this.pnlSeries.Controls.Add(this.lblSeries);
            this.pnlSeries.Controls.Add(this.cboSeries);
            this.pnlSeries.Location = new System.Drawing.Point(0, 0);
            this.pnlSeries.Margin = new System.Windows.Forms.Padding(0);
            this.pnlSeries.Name = "pnlSeries";
            this.pnlSeries.Size = new System.Drawing.Size(178, 27);
            this.pnlSeries.TabIndex = 0;
            // 
            // lblSeries
            // 
            this.lblSeries.AutoSize = true;
            this.lblSeries.Location = new System.Drawing.Point(3, 6);
            this.lblSeries.Name = "lblSeries";
            this.lblSeries.Size = new System.Drawing.Size(45, 13);
            this.lblSeries.TabIndex = 1;
            this.lblSeries.Text = "Variable";
            // 
            // cboSeries
            // 
            this.cboSeries.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboSeries.FormattingEnabled = true;
            this.cboSeries.Location = new System.Drawing.Point(54, 3);
            this.cboSeries.Name = "cboSeries";
            this.cboSeries.Size = new System.Drawing.Size(121, 21);
            this.cboSeries.TabIndex = 0;
            this.cboSeries.SelectedIndexChanged += new System.EventHandler(this.cboSeries_SelectedIndexChanged);
            // 
            // ctlSeriesOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.grpSeries);
            this.Name = "ctlSeriesOptions";
            this.Size = new System.Drawing.Size(197, 198);
            this.grpSeries.ResumeLayout(false);
            this.grpSeries.PerformLayout();
            this.tlpSeriesAndDetail.ResumeLayout(false);
            this.tlpSeriesAndDetail.PerformLayout();
            this.pnlSeries.ResumeLayout(false);
            this.pnlSeries.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpSeries;
        private System.Windows.Forms.TableLayoutPanel tlpSeriesAndDetail;
        private ctlOneSeriesOptions ctlOneSeriesOptions1;
        private System.Windows.Forms.Panel pnlSeries;
        private System.Windows.Forms.Label lblSeries;
        private System.Windows.Forms.ComboBox cboSeries;
    }
}