namespace StatsDirect.UI
{
    partial class ctlConvertUnits : System.Windows.Forms.UserControl 
    { 
        
        // Form overrides dispose to clean up the component list.
        [ System.Diagnostics.DebuggerNonUserCode() ]
        protected override void Dispose( bool disposing ) 
        { 
            try 
            { 
                if ( disposing && components is not null ) 
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
            this.txtFrom = new System.Windows.Forms.TextBox();
            this.txtTo = new System.Windows.Forms.TextBox();
            this.lblFrom = new System.Windows.Forms.Label();
            this.lblTo = new System.Windows.Forms.Label();
            this.cmdConvert = new System.Windows.Forms.Button();
            this.cboConversion = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // txtFrom
            // 
            this.txtFrom.Location = new System.Drawing.Point(0, 0);
            this.txtFrom.Name = "txtFrom";
            this.txtFrom.Size = new System.Drawing.Size(175, 20);
            this.txtFrom.TabIndex = 0;
            this.txtFrom.DoubleClick += new System.EventHandler(this.DoubleClickTextbox);
            this.txtFrom.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.edpdf_KeyPress);
            // 
            // txtTo
            // 
            this.txtTo.Location = new System.Drawing.Point(298, 0);
            this.txtTo.Name = "txtTo";
            this.txtTo.ReadOnly = true;
            this.txtTo.Size = new System.Drawing.Size(175, 20);
            this.txtTo.TabIndex = 3;
            this.txtTo.DoubleClick += new System.EventHandler(this.DoubleClickTextbox);
            // 
            // lblFrom
            // 
            this.lblFrom.AutoSize = true;
            this.lblFrom.Location = new System.Drawing.Point(181, 3);
            this.lblFrom.Name = "lblFrom";
            this.lblFrom.Size = new System.Drawing.Size(30, 13);
            this.lblFrom.TabIndex = 7;
            this.lblFrom.Text = "From";
            // 
            // lblTo
            // 
            this.lblTo.AutoSize = true;
            this.lblTo.Location = new System.Drawing.Point(479, 3);
            this.lblTo.Name = "lblTo";
            this.lblTo.Size = new System.Drawing.Size(20, 13);
            this.lblTo.TabIndex = 10;
            this.lblTo.Text = "To";
            // 
            // cmdConvert
            // 
            this.cmdConvert.Location = new System.Drawing.Point(217, -2);
            this.cmdConvert.Name = "cmdConvert";
            this.cmdConvert.Size = new System.Drawing.Size(75, 23);
            this.cmdConvert.TabIndex = 6;
            this.cmdConvert.Text = "&Convert >";
            this.cmdConvert.UseVisualStyleBackColor = true;
            this.cmdConvert.Click += new System.EventHandler(this.Calc_Click);
            // 
            // cboConversion
            // 
            this.cboConversion.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboConversion.FormattingEnabled = true;
            this.cboConversion.Location = new System.Drawing.Point(298, 26);
            this.cboConversion.Name = "cboConversion";
            this.cboConversion.Size = new System.Drawing.Size(175, 21);
            this.cboConversion.TabIndex = 11;
            // 
            // ctlConvertUnits
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.cboConversion);
            this.Controls.Add(this.lblTo);
            this.Controls.Add(this.txtTo);
            this.Controls.Add(this.cmdConvert);
            this.Controls.Add(this.lblFrom);
            this.Controls.Add(this.txtFrom);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "ctlConvertUnits";
            this.Size = new System.Drawing.Size(502, 50);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        internal System.Windows.Forms.TextBox txtFrom;
        internal System.Windows.Forms.TextBox txtTo;
        internal System.Windows.Forms.Label lblFrom;
        internal System.Windows.Forms.Label lblTo;
        internal System.Windows.Forms.Button cmdConvert;
        private System.Windows.Forms.ComboBox cboConversion; 
    } 
    
    
} 
