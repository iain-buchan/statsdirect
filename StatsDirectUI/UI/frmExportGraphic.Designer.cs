namespace StatsDirect.UI
{
    partial class frmExportGraphic
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
            if (disposing && (components is not null))
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
            this.cmdExport = new System.Windows.Forms.Button();
            this.cmdCancel = new System.Windows.Forms.Button();
            this.grpFormat = new System.Windows.Forms.GroupBox();
            this.rdoPng = new System.Windows.Forms.RadioButton();
            this.rdoJpeg = new System.Windows.Forms.RadioButton();
            this.rdoBitmap = new System.Windows.Forms.RadioButton();
            this.rdoMetafile = new System.Windows.Forms.RadioButton();
            this.grpCompression = new System.Windows.Forms.GroupBox();
            this.trkCompression = new System.Windows.Forms.TrackBar();
            this.lblSmall = new System.Windows.Forms.Label();
            this.lblLarge = new System.Windows.Forms.Label();
            this.grpSize = new System.Windows.Forms.GroupBox();
            this.lblWidth = new System.Windows.Forms.Label();
            this.lblHeight = new System.Windows.Forms.Label();
            this.txtWidth = new System.Windows.Forms.TextBox();
            this.txtHeight = new System.Windows.Forms.TextBox();
            this.chkKeepAspectRatio = new System.Windows.Forms.CheckBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.saveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.grpFormat.SuspendLayout();
            this.grpCompression.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.trkCompression)).BeginInit();
            this.grpSize.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // cmdExport
            // 
            this.cmdExport.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdExport.Location = new System.Drawing.Point(247, 12);
            this.cmdExport.Name = "cmdExport";
            this.cmdExport.Size = new System.Drawing.Size(75, 23);
            this.cmdExport.TabIndex = 0;
            this.cmdExport.Text = "&Export";
            this.cmdExport.UseVisualStyleBackColor = true;
            this.cmdExport.Click += new System.EventHandler(this.cmdExport_Click);
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(247, 41);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(75, 23);
            this.cmdCancel.TabIndex = 1;
            this.cmdCancel.Text = "&Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // grpFormat
            // 
            this.grpFormat.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.grpFormat.Controls.Add(this.rdoMetafile);
            this.grpFormat.Controls.Add(this.rdoBitmap);
            this.grpFormat.Controls.Add(this.rdoJpeg);
            this.grpFormat.Controls.Add(this.rdoPng);
            this.grpFormat.Location = new System.Drawing.Point(247, 70);
            this.grpFormat.Name = "grpFormat";
            this.grpFormat.Size = new System.Drawing.Size(75, 115);
            this.grpFormat.TabIndex = 2;
            this.grpFormat.TabStop = false;
            this.grpFormat.Text = "Format";
            // 
            // rdoPng
            // 
            this.rdoPng.AutoSize = true;
            this.rdoPng.Checked = true;
            this.rdoPng.Location = new System.Drawing.Point(7, 20);
            this.rdoPng.Name = "rdoPng";
            this.rdoPng.Size = new System.Drawing.Size(48, 17);
            this.rdoPng.TabIndex = 0;
            this.rdoPng.TabStop = true;
            this.rdoPng.Text = "PNG";
            this.rdoPng.UseVisualStyleBackColor = true;
            this.rdoPng.CheckedChanged += new System.EventHandler(this.FormatChanged);
            // 
            // rdoJpeg
            // 
            this.rdoJpeg.AutoSize = true;
            this.rdoJpeg.Location = new System.Drawing.Point(7, 43);
            this.rdoJpeg.Name = "rdoJpeg";
            this.rdoJpeg.Size = new System.Drawing.Size(52, 17);
            this.rdoJpeg.TabIndex = 1;
            this.rdoJpeg.Text = "JPEG";
            this.rdoJpeg.UseVisualStyleBackColor = true;
            this.rdoJpeg.CheckedChanged += new System.EventHandler(this.FormatChanged);
            // 
            // rdoBitmap
            // 
            this.rdoBitmap.AutoSize = true;
            this.rdoBitmap.Location = new System.Drawing.Point(7, 66);
            this.rdoBitmap.Name = "rdoBitmap";
            this.rdoBitmap.Size = new System.Drawing.Size(57, 17);
            this.rdoBitmap.TabIndex = 2;
            this.rdoBitmap.Text = "Bitmap";
            this.rdoBitmap.UseVisualStyleBackColor = true;
            this.rdoBitmap.CheckedChanged += new System.EventHandler(this.FormatChanged);
            // 
            // rdoMetafile
            // 
            this.rdoMetafile.AutoSize = true;
            this.rdoMetafile.Location = new System.Drawing.Point(7, 89);
            this.rdoMetafile.Name = "rdoMetafile";
            this.rdoMetafile.Size = new System.Drawing.Size(62, 17);
            this.rdoMetafile.TabIndex = 3;
            this.rdoMetafile.Text = "Metafile";
            this.rdoMetafile.UseVisualStyleBackColor = true;
            this.rdoMetafile.CheckedChanged += new System.EventHandler(this.FormatChanged);
            // 
            // grpCompression
            // 
            this.grpCompression.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.grpCompression.Controls.Add(this.lblLarge);
            this.grpCompression.Controls.Add(this.lblSmall);
            this.grpCompression.Controls.Add(this.trkCompression);
            this.grpCompression.Location = new System.Drawing.Point(140, 191);
            this.grpCompression.Name = "grpCompression";
            this.grpCompression.Size = new System.Drawing.Size(182, 91);
            this.grpCompression.TabIndex = 4;
            this.grpCompression.TabStop = false;
            this.grpCompression.Text = "JPEG compression";
            // 
            // trkCompression
            // 
            this.trkCompression.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.trkCompression.AutoSize = false;
            this.trkCompression.LargeChange = 10;
            this.trkCompression.Location = new System.Drawing.Point(7, 20);
            this.trkCompression.Maximum = 100;
            this.trkCompression.Minimum = 10;
            this.trkCompression.Name = "trkCompression";
            this.trkCompression.Size = new System.Drawing.Size(169, 20);
            this.trkCompression.TabIndex = 0;
            this.trkCompression.TickFrequency = 10;
            this.trkCompression.Value = 100;
            // 
            // lblSmall
            // 
            this.lblSmall.Location = new System.Drawing.Point(6, 43);
            this.lblSmall.Name = "lblSmall";
            this.lblSmall.Size = new System.Drawing.Size(63, 31);
            this.lblSmall.TabIndex = 1;
            this.lblSmall.Text = "10%\r\n(small file)\r\n";
            // 
            // lblLarge
            // 
            this.lblLarge.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblLarge.Location = new System.Drawing.Point(113, 43);
            this.lblLarge.Name = "lblLarge";
            this.lblLarge.Size = new System.Drawing.Size(63, 31);
            this.lblLarge.TabIndex = 2;
            this.lblLarge.Text = "100%\r\n(large file)\r\n";
            this.lblLarge.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // grpSize
            // 
            this.grpSize.Controls.Add(this.chkKeepAspectRatio);
            this.grpSize.Controls.Add(this.txtHeight);
            this.grpSize.Controls.Add(this.txtWidth);
            this.grpSize.Controls.Add(this.lblHeight);
            this.grpSize.Controls.Add(this.lblWidth);
            this.grpSize.Location = new System.Drawing.Point(12, 191);
            this.grpSize.Name = "grpSize";
            this.grpSize.Size = new System.Drawing.Size(122, 91);
            this.grpSize.TabIndex = 3;
            this.grpSize.TabStop = false;
            this.grpSize.Text = "Size";
            // 
            // lblWidth
            // 
            this.lblWidth.AutoSize = true;
            this.lblWidth.Location = new System.Drawing.Point(7, 19);
            this.lblWidth.Name = "lblWidth";
            this.lblWidth.Size = new System.Drawing.Size(35, 13);
            this.lblWidth.TabIndex = 0;
            this.lblWidth.Text = "Width";
            // 
            // lblHeight
            // 
            this.lblHeight.AutoSize = true;
            this.lblHeight.Location = new System.Drawing.Point(59, 20);
            this.lblHeight.Name = "lblHeight";
            this.lblHeight.Size = new System.Drawing.Size(38, 13);
            this.lblHeight.TabIndex = 1;
            this.lblHeight.Text = "Height";
            // 
            // txtWidth
            // 
            this.txtWidth.Location = new System.Drawing.Point(7, 36);
            this.txtWidth.Name = "txtWidth";
            this.txtWidth.Size = new System.Drawing.Size(49, 20);
            this.txtWidth.TabIndex = 2;
            this.txtWidth.TextChanged += new System.EventHandler(this.txtWidth_TextChanged);
            // 
            // txtHeight
            // 
            this.txtHeight.Location = new System.Drawing.Point(62, 36);
            this.txtHeight.Name = "txtHeight";
            this.txtHeight.Size = new System.Drawing.Size(49, 20);
            this.txtHeight.TabIndex = 3;
            this.txtHeight.TextChanged += new System.EventHandler(this.txtHeight_TextChanged);
            // 
            // chkKeepAspectRatio
            // 
            this.chkKeepAspectRatio.AutoSize = true;
            this.chkKeepAspectRatio.Checked = true;
            this.chkKeepAspectRatio.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkKeepAspectRatio.Location = new System.Drawing.Point(7, 62);
            this.chkKeepAspectRatio.Name = "chkKeepAspectRatio";
            this.chkKeepAspectRatio.Size = new System.Drawing.Size(109, 17);
            this.chkKeepAspectRatio.TabIndex = 4;
            this.chkKeepAspectRatio.Text = "Keep aspect ratio";
            this.chkKeepAspectRatio.UseVisualStyleBackColor = true;
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.Color.White;
            this.pictureBox1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pictureBox1.Location = new System.Drawing.Point(12, 12);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(217, 161);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 5;
            this.pictureBox1.TabStop = false;
            // 
            // saveFileDialog
            // 
            this.saveFileDialog.ShowHelp = true;
            this.saveFileDialog.HelpRequest += new System.EventHandler(this.HelpRequest);
            // 
            // frmExportGraphic
            // 
            this.AcceptButton = this.cmdExport;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdCancel;
            this.ClientSize = new System.Drawing.Size(334, 300);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.grpSize);
            this.Controls.Add(this.grpCompression);
            this.Controls.Add(this.grpFormat);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.cmdExport);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmExportGraphic";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Graphical Image Export";
            this.grpFormat.ResumeLayout(false);
            this.grpFormat.PerformLayout();
            this.grpCompression.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.trkCompression)).EndInit();
            this.grpSize.ResumeLayout(false);
            this.grpSize.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button cmdExport;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.GroupBox grpFormat;
        private System.Windows.Forms.RadioButton rdoMetafile;
        private System.Windows.Forms.RadioButton rdoBitmap;
        private System.Windows.Forms.RadioButton rdoJpeg;
        private System.Windows.Forms.RadioButton rdoPng;
        private System.Windows.Forms.GroupBox grpCompression;
        private System.Windows.Forms.Label lblSmall;
        private System.Windows.Forms.TrackBar trkCompression;
        private System.Windows.Forms.Label lblLarge;
        private System.Windows.Forms.GroupBox grpSize;
        private System.Windows.Forms.CheckBox chkKeepAspectRatio;
        private System.Windows.Forms.TextBox txtHeight;
        private System.Windows.Forms.TextBox txtWidth;
        private System.Windows.Forms.Label lblHeight;
        private System.Windows.Forms.Label lblWidth;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.SaveFileDialog saveFileDialog;
    }
}