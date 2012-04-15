namespace StatsDirect.UI
{
    partial class frmExtraction
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmExtraction));
            this.cmdClose = new System.Windows.Forms.Button();
            this.cmdHelp = new System.Windows.Forms.Button();
            this.cmdCount = new System.Windows.Forms.Button();
            this.cmdExtract = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.lblIdentifiers = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.chkKeepRowPositions = new System.Windows.Forms.CheckBox();
            this.txtMessage = new System.Windows.Forms.TextBox();
            this.txtExpression = new System.Windows.Forms.TextBox();
            this.grpOutput = new System.Windows.Forms.GroupBox();
            this.windowPicker = new StatsDirect.UI.ctlPickAWindow();
            this.grpOutput.SuspendLayout();
            this.SuspendLayout();
            // 
            // cmdClose
            // 
            this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdClose.Location = new System.Drawing.Point(651, 13);
            this.cmdClose.Name = "cmdClose";
            this.cmdClose.Size = new System.Drawing.Size(75, 23);
            this.cmdClose.TabIndex = 5;
            this.cmdClose.Text = "&Close";
            this.cmdClose.UseVisualStyleBackColor = true;
            this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
            // 
            // cmdHelp
            // 
            this.cmdHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdHelp.Location = new System.Drawing.Point(570, 13);
            this.cmdHelp.Name = "cmdHelp";
            this.cmdHelp.Size = new System.Drawing.Size(75, 23);
            this.cmdHelp.TabIndex = 4;
            this.cmdHelp.Text = "&Help";
            this.cmdHelp.UseVisualStyleBackColor = true;
            this.cmdHelp.Click += new System.EventHandler(this.cmdHelp_Click);
            // 
            // cmdCount
            // 
            this.cmdCount.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCount.Location = new System.Drawing.Point(489, 13);
            this.cmdCount.Name = "cmdCount";
            this.cmdCount.Size = new System.Drawing.Size(75, 23);
            this.cmdCount.TabIndex = 3;
            this.cmdCount.Text = "Cou&nt";
            this.cmdCount.UseVisualStyleBackColor = true;
            this.cmdCount.Click += new System.EventHandler(this.cmdCount_Click);
            // 
            // cmdExtract
            // 
            this.cmdExtract.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdExtract.Location = new System.Drawing.Point(408, 13);
            this.cmdExtract.Name = "cmdExtract";
            this.cmdExtract.Size = new System.Drawing.Size(75, 23);
            this.cmdExtract.TabIndex = 2;
            this.cmdExtract.Text = "&Extract";
            this.cmdExtract.UseVisualStyleBackColor = true;
            this.cmdExtract.Click += new System.EventHandler(this.cmdExtract_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 45);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(227, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "List of identifiers to use in extraction expression";
            // 
            // lblIdentifiers
            // 
            this.lblIdentifiers.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left)));
            this.lblIdentifiers.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblIdentifiers.Location = new System.Drawing.Point(12, 67);
            this.lblIdentifiers.Name = "lblIdentifiers";
            this.lblIdentifiers.Size = new System.Drawing.Size(318, 217);
            this.lblIdentifiers.TabIndex = 5;
            this.lblIdentifiers.Text = "<identifiers>";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(333, 45);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(252, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Enter an expression/rule for extracting your variable:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(333, 90);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(188, 13);
            this.label4.TabIndex = 7;
            this.label4.Text = "Example: X1=1 and X2<>0 and X3>20";
            // 
            // chkKeepRowPositions
            // 
            this.chkKeepRowPositions.AutoSize = true;
            this.chkKeepRowPositions.Location = new System.Drawing.Point(6, 18);
            this.chkKeepRowPositions.Name = "chkKeepRowPositions";
            this.chkKeepRowPositions.Size = new System.Drawing.Size(264, 17);
            this.chkKeepRowPositions.TabIndex = 1;
            this.chkKeepRowPositions.Text = "Keep row positions the same as in the original data";
            this.chkKeepRowPositions.UseVisualStyleBackColor = true;
            // 
            // txtMessage
            // 
            this.txtMessage.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtMessage.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.txtMessage.Location = new System.Drawing.Point(336, 207);
            this.txtMessage.Multiline = true;
            this.txtMessage.Name = "txtMessage";
            this.txtMessage.ReadOnly = true;
            this.txtMessage.Size = new System.Drawing.Size(390, 77);
            this.txtMessage.TabIndex = 9;
            this.txtMessage.TabStop = false;
            // 
            // txtExpression
            // 
            this.txtExpression.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtExpression.Location = new System.Drawing.Point(336, 67);
            this.txtExpression.Name = "txtExpression";
            this.txtExpression.Size = new System.Drawing.Size(390, 20);
            this.txtExpression.TabIndex = 0;
            // 
            // grpOutput
            // 
            this.grpOutput.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grpOutput.Controls.Add(this.chkKeepRowPositions);
            this.grpOutput.Controls.Add(this.windowPicker);
            this.grpOutput.Location = new System.Drawing.Point(336, 115);
            this.grpOutput.Name = "grpOutput";
            this.grpOutput.Size = new System.Drawing.Size(390, 86);
            this.grpOutput.TabIndex = 11;
            this.grpOutput.TabStop = false;
            this.grpOutput.Text = "Output";
            // 
            // windowPicker
            // 
            this.windowPicker.AutoSize = true;
            this.windowPicker.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.windowPicker.BackColor = System.Drawing.Color.Transparent;
            this.windowPicker.Location = new System.Drawing.Point(6, 41);
            this.windowPicker.Name = "windowPicker";
            this.windowPicker.OutputType = StatsDirect.UI.OutputType.Frame;
            this.windowPicker.ShowLabel = false;
            this.windowPicker.Size = new System.Drawing.Size(415, 45);
            this.windowPicker.TabIndex = 10;
            this.windowPicker.WritePosition = StatsDirect.UI.RelativePosition.AfterSelection;
            // 
            // frmExtraction
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(738, 293);
            this.Controls.Add(this.grpOutput);
            this.Controls.Add(this.txtExpression);
            this.Controls.Add(this.txtMessage);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lblIdentifiers);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cmdExtract);
            this.Controls.Add(this.cmdCount);
            this.Controls.Add(this.cmdHelp);
            this.Controls.Add(this.cmdClose);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmExtraction";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Extract Variables";
            this.Shown += new System.EventHandler(this.frmExtraction_Shown);
            this.grpOutput.ResumeLayout(false);
            this.grpOutput.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdClose;
        private System.Windows.Forms.Button cmdHelp;
        private System.Windows.Forms.Button cmdCount;
        private System.Windows.Forms.Button cmdExtract;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblIdentifiers;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox chkKeepRowPositions;
        private System.Windows.Forms.TextBox txtMessage;
        private System.Windows.Forms.TextBox txtExpression;
        private ctlPickAWindow windowPicker;
        private System.Windows.Forms.GroupBox grpOutput;
    }
}