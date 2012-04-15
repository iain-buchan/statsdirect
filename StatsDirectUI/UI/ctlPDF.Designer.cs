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
            this.edpdf = new System.Windows.Forms.TextBox();
            this.eddf = new System.Windows.Forms.TextBox();
            this.eddf2 = new System.Windows.Forms.TextBox();
            this.edlp = new System.Windows.Forms.TextBox();
            this.edup = new System.Windows.Forms.TextBox();
            this.ed2p = new System.Windows.Forms.TextBox();
            this.combo_cl = new System.Windows.Forms.ComboBox();
            this.lbpdf = new System.Windows.Forms.Label();
            this.lbdf = new System.Windows.Forms.Label();
            this.lbdf2 = new System.Windows.Forms.Label();
            this.lblp = new System.Windows.Forms.Label();
            this.lbup = new System.Windows.Forms.Label();
            this.lb2p = new System.Windows.Forms.Label();
            this.Save = new System.Windows.Forms.Button();
            this.Calc = new System.Windows.Forms.Button();
            this.label_cl = new System.Windows.Forms.Label();
            this.btn_ucl = new System.Windows.Forms.Button();
            this.btn_lcl = new System.Windows.Forms.Button();
            this.tlpOuter = new System.Windows.Forms.TableLayoutPanel();
            this.pnlRight = new System.Windows.Forms.Panel();
            this.tlpLeft = new System.Windows.Forms.TableLayoutPanel();
            this.pnlPdf = new System.Windows.Forms.Panel();
            this.pnlDf = new System.Windows.Forms.Panel();
            this.pnlDf2 = new System.Windows.Forms.Panel();
            this.pnlLp = new System.Windows.Forms.Panel();
            this.pnlUp = new System.Windows.Forms.Panel();
            this.pnl2p = new System.Windows.Forms.Panel();
            this.tlpOuter.SuspendLayout();
            this.pnlRight.SuspendLayout();
            this.tlpLeft.SuspendLayout();
            this.pnlPdf.SuspendLayout();
            this.pnlDf.SuspendLayout();
            this.pnlDf2.SuspendLayout();
            this.pnlLp.SuspendLayout();
            this.pnlUp.SuspendLayout();
            this.pnl2p.SuspendLayout();
            this.SuspendLayout();
            // 
            // edpdf
            // 
            this.edpdf.Location = new System.Drawing.Point(3, 3);
            this.edpdf.Name = "edpdf";
            this.edpdf.Size = new System.Drawing.Size(175, 20);
            this.edpdf.TabIndex = 0;
            // 
            // eddf
            // 
            this.eddf.Location = new System.Drawing.Point(3, 3);
            this.eddf.Name = "eddf";
            this.eddf.Size = new System.Drawing.Size(175, 20);
            this.eddf.TabIndex = 1;
            // 
            // eddf2
            // 
            this.eddf2.Location = new System.Drawing.Point(3, 3);
            this.eddf2.Name = "eddf2";
            this.eddf2.Size = new System.Drawing.Size(175, 20);
            this.eddf2.TabIndex = 2;
            // 
            // edlp
            // 
            this.edlp.Location = new System.Drawing.Point(3, 3);
            this.edlp.Name = "edlp";
            this.edlp.Size = new System.Drawing.Size(175, 20);
            this.edlp.TabIndex = 3;
            // 
            // edup
            // 
            this.edup.Location = new System.Drawing.Point(3, 3);
            this.edup.Name = "edup";
            this.edup.Size = new System.Drawing.Size(175, 20);
            this.edup.TabIndex = 4;
            // 
            // ed2p
            // 
            this.ed2p.Location = new System.Drawing.Point(3, 3);
            this.ed2p.Name = "ed2p";
            this.ed2p.Size = new System.Drawing.Size(175, 20);
            this.ed2p.TabIndex = 5;
            // 
            // combo_cl
            // 
            this.combo_cl.FormattingEnabled = true;
            this.combo_cl.Items.AddRange(new object[] {
            "95",
            "99",
            "90",
            "85",
            "80",
            "75"});
            this.combo_cl.Location = new System.Drawing.Point(9, 26);
            this.combo_cl.Name = "combo_cl";
            this.combo_cl.Size = new System.Drawing.Size(38, 21);
            this.combo_cl.TabIndex = 20;
            this.combo_cl.Text = "95";
            // 
            // lbpdf
            // 
            this.lbpdf.AutoSize = true;
            this.lbpdf.Location = new System.Drawing.Point(184, 6);
            this.lbpdf.Name = "lbpdf";
            this.lbpdf.Size = new System.Drawing.Size(0, 13);
            this.lbpdf.TabIndex = 7;
            // 
            // lbdf
            // 
            this.lbdf.AutoSize = true;
            this.lbdf.Location = new System.Drawing.Point(184, 6);
            this.lbdf.Name = "lbdf";
            this.lbdf.Size = new System.Drawing.Size(0, 13);
            this.lbdf.TabIndex = 8;
            // 
            // lbdf2
            // 
            this.lbdf2.AutoSize = true;
            this.lbdf2.Location = new System.Drawing.Point(184, 6);
            this.lbdf2.Name = "lbdf2";
            this.lbdf2.Size = new System.Drawing.Size(0, 13);
            this.lbdf2.TabIndex = 9;
            // 
            // lblp
            // 
            this.lblp.AutoSize = true;
            this.lblp.Location = new System.Drawing.Point(184, 6);
            this.lblp.Name = "lblp";
            this.lblp.Size = new System.Drawing.Size(0, 13);
            this.lblp.TabIndex = 10;
            // 
            // lbup
            // 
            this.lbup.AutoSize = true;
            this.lbup.Location = new System.Drawing.Point(184, 6);
            this.lbup.Name = "lbup";
            this.lbup.Size = new System.Drawing.Size(0, 13);
            this.lbup.TabIndex = 11;
            // 
            // lb2p
            // 
            this.lb2p.AutoSize = true;
            this.lb2p.Location = new System.Drawing.Point(184, 6);
            this.lb2p.Name = "lb2p";
            this.lb2p.Size = new System.Drawing.Size(0, 13);
            this.lb2p.TabIndex = 12;
            // 
            // Save
            // 
            this.Save.Location = new System.Drawing.Point(67, 89);
            this.Save.Name = "Save";
            this.Save.Size = new System.Drawing.Size(75, 23);
            this.Save.TabIndex = 7;
            this.Save.Text = "&Save";
            this.Save.UseVisualStyleBackColor = true;
            // 
            // Calc
            // 
            this.Calc.Location = new System.Drawing.Point(67, 115);
            this.Calc.Name = "Calc";
            this.Calc.Size = new System.Drawing.Size(75, 23);
            this.Calc.TabIndex = 6;
            this.Calc.Text = "&Calculate";
            this.Calc.UseVisualStyleBackColor = true;
            // 
            // label_cl
            // 
            this.label_cl.AutoSize = true;
            this.label_cl.Location = new System.Drawing.Point(46, 29);
            this.label_cl.Name = "label_cl";
            this.label_cl.Size = new System.Drawing.Size(15, 13);
            this.label_cl.TabIndex = 17;
            this.label_cl.Text = "%";
            // 
            // btn_ucl
            // 
            this.btn_ucl.Location = new System.Drawing.Point(67, 10);
            this.btn_ucl.Name = "btn_ucl";
            this.btn_ucl.Size = new System.Drawing.Size(75, 23);
            this.btn_ucl.TabIndex = 22;
            this.btn_ucl.Text = "&Upper CL";
            this.btn_ucl.UseVisualStyleBackColor = true;
            // 
            // btn_lcl
            // 
            this.btn_lcl.Location = new System.Drawing.Point(67, 38);
            this.btn_lcl.Name = "btn_lcl";
            this.btn_lcl.Size = new System.Drawing.Size(75, 23);
            this.btn_lcl.TabIndex = 21;
            this.btn_lcl.Text = "&Lower CL";
            this.btn_lcl.UseVisualStyleBackColor = true;
            // 
            // tlpOuter
            // 
            this.tlpOuter.AutoSize = true;
            this.tlpOuter.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.tlpOuter.ColumnCount = 2;
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle());
            this.tlpOuter.Controls.Add(this.pnlRight, 1, 0);
            this.tlpOuter.Controls.Add(this.tlpLeft, 0, 0);
            this.tlpOuter.Location = new System.Drawing.Point(0, 0);
            this.tlpOuter.Margin = new System.Windows.Forms.Padding(0);
            this.tlpOuter.Name = "tlpOuter";
            this.tlpOuter.RowCount = 1;
            this.tlpOuter.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tlpOuter.Size = new System.Drawing.Size(332, 156);
            this.tlpOuter.TabIndex = 23;
            // 
            // pnlRight
            // 
            this.pnlRight.AutoSize = true;
            this.pnlRight.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlRight.Controls.Add(this.btn_lcl);
            this.pnlRight.Controls.Add(this.combo_cl);
            this.pnlRight.Controls.Add(this.btn_ucl);
            this.pnlRight.Controls.Add(this.Save);
            this.pnlRight.Controls.Add(this.label_cl);
            this.pnlRight.Controls.Add(this.Calc);
            this.pnlRight.Location = new System.Drawing.Point(187, 0);
            this.pnlRight.Margin = new System.Windows.Forms.Padding(0);
            this.pnlRight.Name = "pnlRight";
            this.pnlRight.Size = new System.Drawing.Size(145, 141);
            this.pnlRight.TabIndex = 0;
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
            this.tlpLeft.Controls.Add(this.pnlLp, 0, 3);
            this.tlpLeft.Controls.Add(this.pnlUp, 0, 4);
            this.tlpLeft.Controls.Add(this.pnl2p, 0, 5);
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
            this.tlpLeft.Size = new System.Drawing.Size(187, 156);
            this.tlpLeft.TabIndex = 1;
            // 
            // pnlPdf
            // 
            this.pnlPdf.AutoSize = true;
            this.pnlPdf.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlPdf.Controls.Add(this.edpdf);
            this.pnlPdf.Controls.Add(this.lbpdf);
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
            this.pnlDf.Controls.Add(this.eddf);
            this.pnlDf.Controls.Add(this.lbdf);
            this.pnlDf.Location = new System.Drawing.Point(0, 26);
            this.pnlDf.Margin = new System.Windows.Forms.Padding(0);
            this.pnlDf.Name = "pnlDf";
            this.pnlDf.Size = new System.Drawing.Size(187, 26);
            this.pnlDf.TabIndex = 1;
            // 
            // pnlDf2
            // 
            this.pnlDf2.AutoSize = true;
            this.pnlDf2.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlDf2.Controls.Add(this.eddf2);
            this.pnlDf2.Controls.Add(this.lbdf2);
            this.pnlDf2.Location = new System.Drawing.Point(0, 52);
            this.pnlDf2.Margin = new System.Windows.Forms.Padding(0);
            this.pnlDf2.Name = "pnlDf2";
            this.pnlDf2.Size = new System.Drawing.Size(187, 26);
            this.pnlDf2.TabIndex = 2;
            // 
            // pnlLp
            // 
            this.pnlLp.AutoSize = true;
            this.pnlLp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlLp.Controls.Add(this.edlp);
            this.pnlLp.Controls.Add(this.lblp);
            this.pnlLp.Location = new System.Drawing.Point(0, 78);
            this.pnlLp.Margin = new System.Windows.Forms.Padding(0);
            this.pnlLp.Name = "pnlLp";
            this.pnlLp.Size = new System.Drawing.Size(187, 26);
            this.pnlLp.TabIndex = 3;
            // 
            // pnlUp
            // 
            this.pnlUp.AutoSize = true;
            this.pnlUp.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlUp.Controls.Add(this.edup);
            this.pnlUp.Controls.Add(this.lbup);
            this.pnlUp.Location = new System.Drawing.Point(0, 104);
            this.pnlUp.Margin = new System.Windows.Forms.Padding(0);
            this.pnlUp.Name = "pnlUp";
            this.pnlUp.Size = new System.Drawing.Size(187, 26);
            this.pnlUp.TabIndex = 4;
            // 
            // pnl2p
            // 
            this.pnl2p.AutoSize = true;
            this.pnl2p.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnl2p.Controls.Add(this.ed2p);
            this.pnl2p.Controls.Add(this.lb2p);
            this.pnl2p.Location = new System.Drawing.Point(0, 130);
            this.pnl2p.Margin = new System.Windows.Forms.Padding(0);
            this.pnl2p.Name = "pnl2p";
            this.pnl2p.Size = new System.Drawing.Size(187, 26);
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
            this.Size = new System.Drawing.Size(332, 156);
            this.tlpOuter.ResumeLayout(false);
            this.tlpOuter.PerformLayout();
            this.pnlRight.ResumeLayout(false);
            this.pnlRight.PerformLayout();
            this.tlpLeft.ResumeLayout(false);
            this.tlpLeft.PerformLayout();
            this.pnlPdf.ResumeLayout(false);
            this.pnlPdf.PerformLayout();
            this.pnlDf.ResumeLayout(false);
            this.pnlDf.PerformLayout();
            this.pnlDf2.ResumeLayout(false);
            this.pnlDf2.PerformLayout();
            this.pnlLp.ResumeLayout(false);
            this.pnlLp.PerformLayout();
            this.pnlUp.ResumeLayout(false);
            this.pnlUp.PerformLayout();
            this.pnl2p.ResumeLayout(false);
            this.pnl2p.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        } 
        
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.TextBox edpdf; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.TextBox eddf; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.TextBox eddf2; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.TextBox edlp; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.TextBox edup; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.TextBox ed2p; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.ComboBox combo_cl; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Label lbpdf; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Label lbdf; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Label lbdf2; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Label lblp; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Label lbup;
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Label lb2p; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Button Save; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Button Calc; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Label label_cl; 
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Button btn_ucl;
        internal /* TRANSINFO: WithEvents */ System.Windows.Forms.Button btn_lcl;
        private System.Windows.Forms.TableLayoutPanel tlpOuter;
        private System.Windows.Forms.Panel pnlRight;
        private System.Windows.Forms.TableLayoutPanel tlpLeft;
        private System.Windows.Forms.Panel pnlPdf;
        private System.Windows.Forms.Panel pnlDf;
        private System.Windows.Forms.Panel pnlDf2;
        private System.Windows.Forms.Panel pnlLp;
        private System.Windows.Forms.Panel pnlUp;
        private System.Windows.Forms.Panel pnl2p; 
    } 
    
    
} 
