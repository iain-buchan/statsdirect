namespace StatsDirect.UI
{
    partial class frmImagePreview
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmImagePreview));
            this.cmdClose = new System.Windows.Forms.Button();
            this.pic = new System.Windows.Forms.PictureBox();
            this.pnlScroller = new System.Windows.Forms.Panel();
            this.tlpOuter = new System.Windows.Forms.TableLayoutPanel();
            this.pnlButtons = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.pic)).BeginInit();
            this.pnlScroller.SuspendLayout();
            this.tlpOuter.SuspendLayout();
            this.pnlButtons.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdClose
            // 
            this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdClose.Location = new System.Drawing.Point(6, 4);
            this.cmdClose.Name = "cmdClose";
            this.cmdClose.Size = new System.Drawing.Size(75, 23);
            this.cmdClose.TabIndex = 0;
            this.cmdClose.Text = "Close";
            this.cmdClose.UseVisualStyleBackColor = true;
            this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
            // 
            // pic
            // 
            this.pic.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.pic.Location = new System.Drawing.Point(0, 0);
            this.pic.Margin = new System.Windows.Forms.Padding(0);
            this.pic.Name = "pic";
            this.pic.Size = new System.Drawing.Size(666, 498);
            this.pic.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pic.TabIndex = 1;
            this.pic.TabStop = false;
            // 
            // pnlScroller
            // 
            this.pnlScroller.AutoScroll = true;
            this.pnlScroller.Controls.Add(this.pic);
            this.pnlScroller.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlScroller.Location = new System.Drawing.Point(0, 0);
            this.pnlScroller.Margin = new System.Windows.Forms.Padding(0);
            this.pnlScroller.Name = "pnlScroller";
            this.pnlScroller.Size = new System.Drawing.Size(669, 499);
            this.pnlScroller.TabIndex = 3;
            // 
            // tlpOuter
            // 
            this.tlpOuter.ColumnCount = 1;
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpOuter.Controls.Add(this.pnlScroller, 0, 0);
            this.tlpOuter.Controls.Add(this.pnlButtons, 0, 1);
            this.tlpOuter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tlpOuter.Location = new System.Drawing.Point(0, 0);
            this.tlpOuter.Margin = new System.Windows.Forms.Padding(0);
            this.tlpOuter.Name = "tlpOuter";
            this.tlpOuter.RowCount = 2;
            this.tlpOuter.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tlpOuter.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOuter.Size = new System.Drawing.Size(669, 530);
            this.tlpOuter.TabIndex = 4;
            // 
            // pnlButtons
            // 
            this.pnlButtons.Controls.Add(this.cmdClose);
            this.pnlButtons.Dock = System.Windows.Forms.DockStyle.Right;
            this.pnlButtons.Location = new System.Drawing.Point(585, 499);
            this.pnlButtons.Margin = new System.Windows.Forms.Padding(0);
            this.pnlButtons.Name = "pnlButtons";
            this.pnlButtons.Size = new System.Drawing.Size(84, 31);
            this.pnlButtons.TabIndex = 4;
            // 
            // frmImagePreview
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(669, 530);
            this.Controls.Add(this.tlpOuter);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(250, 200);
            this.Name = "frmImagePreview";
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Show;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Preview";
            this.Resize += new System.EventHandler(this.frmImagePreview_Resize);
            ((System.ComponentModel.ISupportInitialize)(this.pic)).EndInit();
            this.pnlScroller.ResumeLayout(false);
            this.tlpOuter.ResumeLayout(false);
            this.pnlButtons.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button cmdClose;
        private System.Windows.Forms.PictureBox pic;
        private System.Windows.Forms.Panel pnlScroller;
        private System.Windows.Forms.TableLayoutPanel tlpOuter;
        private System.Windows.Forms.Panel pnlButtons;
    }
}