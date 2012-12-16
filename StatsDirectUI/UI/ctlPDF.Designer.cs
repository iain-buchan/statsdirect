namespace StatsDirect.UI
{
    partial class ctlPDF : System.Windows.Forms.UserControl 
    { 
        
        // Form overrides dispose to clean up the component list.
        [ System.Diagnostics.DebuggerNonUserCode() ]
        protected override void Dispose( bool disposing ) 
        { 
            try 
            { 
                if ( disposing && components != null ) 
                { 
                    components.Dispose(); 
                } 
            } 
            finally 
            { 
                base.Dispose( disposing ); 
            } 
        } 
        
        
        // Required by the Windows Form Designer
        private System.ComponentModel.IContainer components = null; 
        
        // NOTE: The following procedure is required by the Windows Form Designer
        // It can be modified using the Windows Form Designer.  
        // Do not modify it using the code editor.
        [ System.Diagnostics.DebuggerStepThrough() ]
        private void InitializeComponent() 
        { 
            this.txtPdf = new System.Windows.Forms.TextBox();
            this.txtDf = new System.Windows.Forms.TextBox();
            this.txtDf2 = new System.Windows.Forms.TextBox();
            this.txtLp = new System.Windows.Forms.TextBox();
            this.txtUp = new System.Windows.Forms.TextBox();
            this.txt2p = new System.Windows.Forms.TextBox();
            this.cboCl = new System.Windows.Forms.ComboBox();
            this.lblPdf = new System.Windows.Forms.Label();
            this.lblDf = new System.Windows.Forms.Label();
            this.lblDf2 = new System.Windows.Forms.Label();
            this.lblLp = new System.Windows.Forms.Label();
            this.lblUp = new System.Windows.Forms.Label();
            this.lbl2p = new System.Windows.Forms.Label();
            this.cmdCalculate = new System.Windows.Forms.Button();
            this.lblCl = new System.Windows.Forms.Label();
            this.cmdUcl = new System.Windows.Forms.Button();
            this.cmdLcl = new System.Windows.Forms.Button();
            this.tlpOuter = new System.Windows.Forms.TableLayoutPanel();
            this.tlpLeft = new System.Windows.Forms.TableLayoutPanel();
            this.pnlPdf = new System.Windows.Forms.Panel();
            this.pnlDf = new System.Windows.Forms.Panel();
            this.pnlDf2 = new System.Windows.Forms.Panel();
            this.pnlCl = new System.Windows.Forms.Panel();
            this.pnlCalculate = new System.Windows.Forms.Panel();
            this.cmdInvert = new System.Windows.Forms.Button();
            this.tlpProbabilities = new System.Windows.Forms.TableLayoutPanel();
            this.pnlLp = new System.Windows.Forms.Panel();
            this.pnlUp = new System.Windows.Forms.Panel();
            this.pnl2p = new System.Windows.Forms.Panel();
            this.tlpOuter.SuspendLayout();
            this.tlpLeft.SuspendLayout();
            this.pnlPdf.SuspendLayout();
            this.pnlDf.SuspendLayout();
            this.pnlDf2.SuspendLayout();
            this.pnlCl.SuspendLayout();
            this.pnlCalculate.SuspendLayout();
            this.tlpProbabilities.SuspendLayout();
            this.pnlLp.SuspendLayout();
            this.pnlUp.SuspendLayout();
            this.pnl2p.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtPdf
            // 
            this.txtPdf.Location = new System.Drawing.Point(3, 3);
            this.txtPdf.Name = "txtPdf";
            this.txtPdf.Size = new System.Drawing.Size(175, 20);
            this.txtPdf.TabIndex = 0;
            this.txtPdf.DoubleClick += new System.EventHandler(this.DoubleClickTextbox);
            this.txtPdf.Enter += new System.EventHandler(this.EnterTextbox);
            this.txtPdf.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.edpdf_KeyPress);
            this.txtPdf.Leave += new System.EventHandler(this.LeaveTextbox);
            // 
            // txtDf
            // 
            this.txtDf.Location = new System.Drawing.Point(3, 3);
            this.txtDf.Name = "txtDf";
            this.txtDf.Size = new System.Drawing.Size(175, 20);
            this.txtDf.TabIndex = 1;
            this.txtDf.DoubleClick += new System.EventHandler(this.DoubleClickTextbox);
            this.txtDf.Enter += new System.EventHandler(this.EnterTextbox);
            this.txtDf.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.eddf_KeyPress);
            this.txtDf.Leave += new System.EventHandler(this.LeaveTextbox);
            // 
            // txtDf2
            // 
            this.txtDf2.Location = new System.Drawing.Point(3, 3);
            this.txtDf2.Name = "txtDf2";
            this.txtDf2.Size = new System.Drawing.Size(175, 20);
            this.txtDf2.TabIndex = 2;
            this.txtDf2.DoubleClick += new System.EventHandler(this.DoubleClickTextbox);
            this.txtDf2.Enter += new System.EventHandler(this.EnterTextbox);
            this.txtDf2.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.eddf2_KeyPress);
            this.txtDf2.Leave += new System.EventHandler(this.LeaveTextbox);
            // 
            // txtLp
            // 
            this.txtLp.Location = new System.Drawing.Point(3, 3);
            this.txtLp.Name = "txtLp";
            this.txtLp.Size = new System.Drawing.Size(175, 20);
            this.txtLp.TabIndex = 3;
            this.txtLp.DoubleClick += new System.EventHandler(this.DoubleClickTextbox);
            this.txtLp.Enter += new System.EventHandler(this.EnterTextbox);
            this.txtLp.Leave += new System.EventHandler(this.LeaveTextbox);
            // 
            // txtUp
            // 
            this.txtUp.Location = new System.Drawing.Point(3, 3);
            this.txtUp.Name = "txtUp";
            this.txtUp.Size = new System.Drawing.Size(175, 20);
            this.txtUp.TabIndex = 4;
            this.txtUp.DoubleClick += new System.EventHandler(this.DoubleClickTextbox);
            this.txtUp.Enter += new System.EventHandler(this.EnterTextbox);
            this.txtUp.Leave += new System.EventHandler(this.LeaveTextbox);
            // 
            // txt2p
            // 
            this.txt2p.Location = new System.Drawing.Point(3, 3);
            this.txt2p.Name = "txt2p";
            this.txt2p.Size = new System.Drawing.Size(175, 20);
            this.txt2p.TabIndex = 5;
            this.txt2p.DoubleClick += new System.EventHandler(this.DoubleClickTextbox);
            this.txt2p.Enter += new System.EventHandler(this.EnterTextbox);
            this.txt2p.Leave += new System.EventHandler(this.LeaveTextbox);
            // 
            // cboCl
            // 
            this.cboCl.FormattingEnabled = true;
            this.cboCl.Items.AddRange(new object[] {
            "95",
            "99",
            "90",
            "85",
            "80",
            "75"});
            this.cboCl.Location = new System.Drawing.Point(9, 26);
            this.cboCl.Name = "cboCl";
            this.cboCl.Size = new System.Drawing.Size(38, 21);
            this.cboCl.TabIndex = 20;
            this.cboCl.Text = "95";
            // 
            // lblPdf
            // 
            this.lblPdf.AutoSize = true;
            this.lblPdf.Location = new System.Drawing.Point(184, 6);
            this.lblPdf.Name = "lblPdf";
            this.lblPdf.Size = new System.Drawing.Size(0, 13);
            this.lblPdf.TabIndex = 7;
            // 
            // lblDf
            // 
            this.lblDf.AutoSize = true;
            this.lblDf.Location = new System.Drawing.Point(184, 6);
            this.lblDf.Name = "lblDf";
            this.lblDf.Size = new System.Drawing.Size(100, 13);
            this.lblDf.TabIndex = 8;
            this.lblDf.Text = "Degrees of freedom";
            // 
            // lblDf2
            // 
            this.lblDf2.AutoSize = true;
            this.lblDf2.Location = new System.Drawing.Point(184, 6);
            this.lblDf2.Name = "lblDf2";
            this.lblDf2.Size = new System.Drawing.Size(0, 13);
            this.lblDf2.TabIndex = 9;
            // 
            // lblLp
            // 
            this.lblLp.AutoSize = true;
            this.lblLp.Location = new System.Drawing.Point(184, 6);
            this.lblLp.Name = "lblLp";
            this.lblLp.Size = new System.Drawing.Size(62, 13);
            this.lblLp.TabIndex = 10;
            this.lblLp.Text = "Lower tail P";
            // 
            // lblUp
            // 
            this.lblUp.AutoSize = true;
            this.lblUp.Location = new System.Drawing.Point(184, 6);
            this.lblUp.Name = "lblUp";
            this.lblUp.Size = new System.Drawing.Size(62, 13);
            this.lblUp.TabIndex = 11;
            this.lblUp.Text = "Upper tail P";
            // 
            // lbl2p
            // 
            this.lbl2p.AutoSize = true;
            this.lbl2p.Location = new System.Drawing.Point(184, 6);
            this.lbl2p.Name = "lbl2p";
            this.lbl2p.Size = new System.Drawing.Size(66, 13);
            this.lbl2p.TabIndex = 12;
            this.lbl2p.Text = "Two tailed P";
            // 
            // cmdCalculate
            // 
            this.cmdCalculate.Location = new System.Drawing.Point(3, 3);
            this.cmdCalculate.Name = "cmdCalculate";
            this.cmdCalculate.Size = new System.Drawing.Size(75, 23);
            this.cmdCalculate.TabIndex = 6;
            this.cmdCalculate.Text = "&Calculate >";
            this.cmdCalculate.UseVisualStyleBackColor = true;
            this.cmdCalculate.Click += new System.EventHandler(this.Calc_Click);
            // 
            // lblCl
            // 
            this.lblCl.AutoSize = true;
            this.lblCl.Location = new System.Drawing.Point(46, 29);
            this.lblCl.Name = "lblCl";
            this.lblCl.Size = new System.Drawing.Size(15, 13);
            this.lblCl.TabIndex = 17;
            this.lblCl.Text = "%";
            // 
            // cmdUcl
            // 
            this.cmdUcl.Location = new System.Drawing.Point(67, 10);
            this.cmdUcl.Name = "cmdUcl";
            this.cmdUcl.Size = new System.Drawing.Size(75, 23);
            this.cmdUcl.TabIndex = 22;
            this.cmdUcl.Text = "&Upper CL";
            this.cmdUcl.UseVisualStyleBackColor = true;
            this.cmdUcl.Click += new System.EventHandler(this.BtnUclClick);
            // 
            // cmdLcl
            // 
            this.cmdLcl.Location = new System.Drawing.Point(67, 38);
            this.cmdLcl.Name = "cmdLcl";
            this.cmdLcl.Size = new System.Drawing.Size(75, 23);
            this.cmdLcl.TabIndex = 21;
            this.cmdLcl.Text = "&Lower CL";
            this.cmdLcl.UseVisualStyleBackColor = true;
            this.cmdLcl.Click += new System.EventHandler(this.BtnLclClick);
            // 
            // tlpOuter
            // 
            this.tlpOuter.AutoSize = true;
            this.tlpOuter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpOuter.ColumnCount = 4;
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.Controls.Add(this.tlpLeft, 0, 0);
            this.tlpOuter.Controls.Add(this.pnlCl, 3, 0);
            this.tlpOuter.Controls.Add(this.pnlCalculate, 1, 0);
            this.tlpOuter.Controls.Add(this.tlpProbabilities, 2, 0);
            this.tlpOuter.Location = new System.Drawing.Point(0, 0);
            this.tlpOuter.Margin = new System.Windows.Forms.Padding(0);
            this.tlpOuter.Name = "tlpOuter";
            this.tlpOuter.RowCount = 1;
            this.tlpOuter.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOuter.Size = new System.Drawing.Size(778, 84);
            this.tlpOuter.TabIndex = 23;
            // 
            // tlpLeft
            // 
            this.tlpLeft.AutoSize = true;
            this.tlpLeft.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpLeft.ColumnCount = 1;
            this.tlpLeft.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpLeft.Controls.Add(this.pnlPdf, 0, 0);
            this.tlpLeft.Controls.Add(this.pnlDf, 0, 1);
            this.tlpLeft.Controls.Add(this.pnlDf2, 0, 2);
            this.tlpLeft.Location = new System.Drawing.Point(0, 0);
            this.tlpLeft.Margin = new System.Windows.Forms.Padding(0);
            this.tlpLeft.Name = "tlpLeft";
            this.tlpLeft.RowCount = 6;
            this.tlpLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLeft.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpLeft.Size = new System.Drawing.Size(287, 78);
            this.tlpLeft.TabIndex = 1;
            // 
            // pnlPdf
            // 
            this.pnlPdf.AutoSize = true;
            this.pnlPdf.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlPdf.Controls.Add(this.txtPdf);
            this.pnlPdf.Controls.Add(this.lblPdf);
            this.pnlPdf.Location = new System.Drawing.Point(0, 0);
            this.pnlPdf.Margin = new System.Windows.Forms.Padding(0);
            this.pnlPdf.Name = "pnlPdf";
            this.pnlPdf.Size = new System.Drawing.Size(187, 26);
            this.pnlPdf.TabIndex = 0;
            // 
            // pnlDf
            // 
            this.pnlDf.AutoSize = true;
            this.pnlDf.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlDf.Controls.Add(this.txtDf);
            this.pnlDf.Controls.Add(this.lblDf);
            this.pnlDf.Location = new System.Drawing.Point(0, 26);
            this.pnlDf.Margin = new System.Windows.Forms.Padding(0);
            this.pnlDf.Name = "pnlDf";
            this.pnlDf.Size = new System.Drawing.Size(287, 26);
            this.pnlDf.TabIndex = 1;
            // 
            // pnlDf2
            // 
            this.pnlDf2.AutoSize = true;
            this.pnlDf2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlDf2.Controls.Add(this.txtDf2);
            this.pnlDf2.Controls.Add(this.lblDf2);
            this.pnlDf2.Location = new System.Drawing.Point(0, 52);
            this.pnlDf2.Margin = new System.Windows.Forms.Padding(0);
            this.pnlDf2.Name = "pnlDf2";
            this.pnlDf2.Size = new System.Drawing.Size(187, 26);
            this.pnlDf2.TabIndex = 2;
            this.pnlDf2.Visible = false;
            // 
            // pnlCl
            // 
            this.pnlCl.AutoSize = true;
            this.pnlCl.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlCl.Controls.Add(this.cmdLcl);
            this.pnlCl.Controls.Add(this.cboCl);
            this.pnlCl.Controls.Add(this.cmdUcl);
            this.pnlCl.Controls.Add(this.lblCl);
            this.pnlCl.Location = new System.Drawing.Point(633, 0);
            this.pnlCl.Margin = new System.Windows.Forms.Padding(0);
            this.pnlCl.Name = "pnlCl";
            this.pnlCl.Size = new System.Drawing.Size(145, 64);
            this.pnlCl.TabIndex = 0;
            this.pnlCl.Visible = false;
            // 
            // pnlCalculate
            // 
            this.pnlCalculate.AutoSize = true;
            this.pnlCalculate.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlCalculate.Controls.Add(this.cmdInvert);
            this.pnlCalculate.Controls.Add(this.cmdCalculate);
            this.pnlCalculate.Location = new System.Drawing.Point(290, 0);
            this.pnlCalculate.Margin = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.pnlCalculate.Name = "pnlCalculate";
            this.pnlCalculate.Size = new System.Drawing.Size(81, 55);
            this.pnlCalculate.TabIndex = 3;
            // 
            // cmdInvert
            // 
            this.cmdInvert.Location = new System.Drawing.Point(3, 29);
            this.cmdInvert.Name = "cmdInvert";
            this.cmdInvert.Size = new System.Drawing.Size(75, 23);
            this.cmdInvert.TabIndex = 7;
            this.cmdInvert.Text = "< &Invert";
            this.cmdInvert.UseVisualStyleBackColor = true;
            this.cmdInvert.Click += new System.EventHandler(this.cmdInvert_Click);
            // 
            // tlpProbabilities
            // 
            this.tlpProbabilities.AutoSize = true;
            this.tlpProbabilities.ColumnCount = 1;
            this.tlpProbabilities.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpProbabilities.Controls.Add(this.pnlLp, 0, 0);
            this.tlpProbabilities.Controls.Add(this.pnlUp, 0, 1);
            this.tlpProbabilities.Controls.Add(this.pnl2p, 0, 2);
            this.tlpProbabilities.Location = new System.Drawing.Point(377, 3);
            this.tlpProbabilities.Name = "tlpProbabilities";
            this.tlpProbabilities.RowCount = 3;
            this.tlpProbabilities.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpProbabilities.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpProbabilities.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpProbabilities.Size = new System.Drawing.Size(253, 78);
            this.tlpProbabilities.TabIndex = 4;
            // 
            // pnlLp
            // 
            this.pnlLp.AutoSize = true;
            this.pnlLp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlLp.Controls.Add(this.txtLp);
            this.pnlLp.Controls.Add(this.lblLp);
            this.pnlLp.Location = new System.Drawing.Point(0, 0);
            this.pnlLp.Margin = new System.Windows.Forms.Padding(0);
            this.pnlLp.Name = "pnlLp";
            this.pnlLp.Size = new System.Drawing.Size(249, 26);
            this.pnlLp.TabIndex = 3;
            // 
            // pnlUp
            // 
            this.pnlUp.AutoSize = true;
            this.pnlUp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlUp.Controls.Add(this.txtUp);
            this.pnlUp.Controls.Add(this.lblUp);
            this.pnlUp.Location = new System.Drawing.Point(0, 26);
            this.pnlUp.Margin = new System.Windows.Forms.Padding(0);
            this.pnlUp.Name = "pnlUp";
            this.pnlUp.Size = new System.Drawing.Size(249, 26);
            this.pnlUp.TabIndex = 4;
            // 
            // pnl2p
            // 
            this.pnl2p.AutoSize = true;
            this.pnl2p.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnl2p.Controls.Add(this.txt2p);
            this.pnl2p.Controls.Add(this.lbl2p);
            this.pnl2p.Location = new System.Drawing.Point(0, 52);
            this.pnl2p.Margin = new System.Windows.Forms.Padding(0);
            this.pnl2p.Name = "pnl2p";
            this.pnl2p.Size = new System.Drawing.Size(253, 26);
            this.pnl2p.TabIndex = 5;
            // 
            // ctlPDF
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.tlpOuter);
            this.Name = "ctlPDF";
            this.Size = new System.Drawing.Size(778, 84);
            this.tlpOuter.ResumeLayout(false);
            this.tlpOuter.PerformLayout();
            this.tlpLeft.ResumeLayout(false);
            this.tlpLeft.PerformLayout();
            this.pnlPdf.ResumeLayout(false);
            this.pnlPdf.PerformLayout();
            this.pnlDf.ResumeLayout(false);
            this.pnlDf.PerformLayout();
            this.pnlDf2.ResumeLayout(false);
            this.pnlDf2.PerformLayout();
            this.pnlCl.ResumeLayout(false);
            this.pnlCl.PerformLayout();
            this.pnlCalculate.ResumeLayout(false);
            this.tlpProbabilities.ResumeLayout(false);
            this.tlpProbabilities.PerformLayout();
            this.pnlLp.ResumeLayout(false);
            this.pnlLp.PerformLayout();
            this.pnlUp.ResumeLayout(false);
            this.pnlUp.PerformLayout();
            this.pnl2p.ResumeLayout(false);
            this.pnl2p.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        } 
        
        internal System.Windows.Forms.TextBox txtPdf; 
        internal System.Windows.Forms.TextBox txtDf; 
        internal System.Windows.Forms.TextBox txtDf2; 
        internal System.Windows.Forms.TextBox txtLp; 
        internal System.Windows.Forms.TextBox txtUp; 
        internal System.Windows.Forms.TextBox txt2p; 
        internal System.Windows.Forms.ComboBox cboCl; 
        internal System.Windows.Forms.Label lblPdf; 
        internal System.Windows.Forms.Label lblDf; 
        internal System.Windows.Forms.Label lblDf2; 
        internal System.Windows.Forms.Label lblLp; 
        internal System.Windows.Forms.Label lblUp;
        internal System.Windows.Forms.Label lbl2p; 
        internal System.Windows.Forms.Button cmdCalculate; 
        internal System.Windows.Forms.Label lblCl; 
        internal System.Windows.Forms.Button cmdUcl;
        internal System.Windows.Forms.Button cmdLcl;
        private System.Windows.Forms.TableLayoutPanel tlpOuter;
        private System.Windows.Forms.Panel pnlCl;
        private System.Windows.Forms.TableLayoutPanel tlpLeft;
        private System.Windows.Forms.Panel pnlPdf;
        private System.Windows.Forms.Panel pnlDf;
        private System.Windows.Forms.Panel pnlDf2;
        private System.Windows.Forms.Panel pnlLp;
        private System.Windows.Forms.Panel pnlUp;
        private System.Windows.Forms.Panel pnl2p;
        private System.Windows.Forms.Panel pnlCalculate;
        private System.Windows.Forms.TableLayoutPanel tlpProbabilities;
        private System.Windows.Forms.Button cmdInvert; 
    } 
    
    
} 
