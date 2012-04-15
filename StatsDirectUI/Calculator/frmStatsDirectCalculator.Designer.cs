namespace StatsDirect.Calculator
{
    partial class frmStatsDirectCalculator
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmStatsDirectCalculator));
            this.lblExpressionToEvaluate = new System.Windows.Forms.Label();
            this.txtExpression = new System.Windows.Forms.TextBox();
            this.txtResult = new System.Windows.Forms.TextBox();
            this.lblResult = new System.Windows.Forms.Label();
            this.cmdCalculate = new System.Windows.Forms.Button();
            this.cmdSave = new System.Windows.Forms.Button();
            this.cmdPaste = new System.Windows.Forms.Button();
            this.cmdDelete = new System.Windows.Forms.Button();
            this.cmdHelp = new System.Windows.Forms.Button();
            this.cmdClose = new System.Windows.Forms.Button();
            this.lstSavedExpressions = new System.Windows.Forms.ListBox();
            this.lblSavedExpressions = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblExpressionToEvaluate
            // 
            this.lblExpressionToEvaluate.AutoSize = true;
            this.lblExpressionToEvaluate.Location = new System.Drawing.Point(13, 13);
            this.lblExpressionToEvaluate.Name = "lblExpressionToEvaluate";
            this.lblExpressionToEvaluate.Size = new System.Drawing.Size(117, 13);
            this.lblExpressionToEvaluate.TabIndex = 0;
            this.lblExpressionToEvaluate.Text = "Expression to evaluate:";
            // 
            // txtExpression
            // 
            this.txtExpression.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtExpression.Location = new System.Drawing.Point(16, 30);
            this.txtExpression.Name = "txtExpression";
            this.txtExpression.Size = new System.Drawing.Size(550, 20);
            this.txtExpression.TabIndex = 1;
            // 
            // txtResult
            // 
            this.txtResult.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.txtResult.Location = new System.Drawing.Point(265, 69);
            this.txtResult.Name = "txtResult";
            this.txtResult.ReadOnly = true;
            this.txtResult.Size = new System.Drawing.Size(219, 20);
            this.txtResult.TabIndex = 2;
            // 
            // lblResult
            // 
            this.lblResult.AutoSize = true;
            this.lblResult.Location = new System.Drawing.Point(219, 72);
            this.lblResult.Name = "lblResult";
            this.lblResult.Size = new System.Drawing.Size(40, 13);
            this.lblResult.TabIndex = 3;
            this.lblResult.Text = "Result:";
            // 
            // cmdCalculate
            // 
            this.cmdCalculate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCalculate.Location = new System.Drawing.Point(490, 57);
            this.cmdCalculate.Name = "cmdCalculate";
            this.cmdCalculate.Size = new System.Drawing.Size(75, 23);
            this.cmdCalculate.TabIndex = 4;
            this.cmdCalculate.Text = "&Calculate";
            this.cmdCalculate.UseVisualStyleBackColor = true;
            this.cmdCalculate.Click += new System.EventHandler(this.cmdCalculate_Click);
            // 
            // cmdSave
            // 
            this.cmdSave.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdSave.Location = new System.Drawing.Point(490, 87);
            this.cmdSave.Name = "cmdSave";
            this.cmdSave.Size = new System.Drawing.Size(75, 23);
            this.cmdSave.TabIndex = 5;
            this.cmdSave.Text = "&Save";
            this.cmdSave.UseVisualStyleBackColor = true;
            this.cmdSave.Click += new System.EventHandler(this.cmdSave_Click);
            // 
            // cmdPaste
            // 
            this.cmdPaste.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdPaste.Enabled = false;
            this.cmdPaste.Location = new System.Drawing.Point(490, 117);
            this.cmdPaste.Name = "cmdPaste";
            this.cmdPaste.Size = new System.Drawing.Size(75, 23);
            this.cmdPaste.TabIndex = 6;
            this.cmdPaste.Text = "&Paste";
            this.cmdPaste.UseVisualStyleBackColor = true;
            this.cmdPaste.Click += new System.EventHandler(this.cmdPaste_Click);
            // 
            // cmdDelete
            // 
            this.cmdDelete.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdDelete.Enabled = false;
            this.cmdDelete.Location = new System.Drawing.Point(490, 147);
            this.cmdDelete.Name = "cmdDelete";
            this.cmdDelete.Size = new System.Drawing.Size(75, 23);
            this.cmdDelete.TabIndex = 7;
            this.cmdDelete.Text = "&Delete";
            this.cmdDelete.UseVisualStyleBackColor = true;
            this.cmdDelete.Click += new System.EventHandler(this.cmdDelete_Click);
            // 
            // cmdHelp
            // 
            this.cmdHelp.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdHelp.Location = new System.Drawing.Point(490, 177);
            this.cmdHelp.Name = "cmdHelp";
            this.cmdHelp.Size = new System.Drawing.Size(75, 23);
            this.cmdHelp.TabIndex = 8;
            this.cmdHelp.Text = "&Help";
            this.cmdHelp.UseVisualStyleBackColor = true;
            this.cmdHelp.Click += new System.EventHandler(this.cmdHelp_Click);
            // 
            // cmdClose
            // 
            this.cmdClose.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdClose.Location = new System.Drawing.Point(490, 207);
            this.cmdClose.Name = "cmdClose";
            this.cmdClose.Size = new System.Drawing.Size(75, 23);
            this.cmdClose.TabIndex = 9;
            this.cmdClose.Text = "&Close";
            this.cmdClose.UseVisualStyleBackColor = true;
            this.cmdClose.Click += new System.EventHandler(this.cmdClose_Click);
            // 
            // lstSavedExpressions
            // 
            this.lstSavedExpressions.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lstSavedExpressions.Enabled = false;
            this.lstSavedExpressions.FormattingEnabled = true;
            this.lstSavedExpressions.Location = new System.Drawing.Point(16, 108);
            this.lstSavedExpressions.Name = "lstSavedExpressions";
            this.lstSavedExpressions.Size = new System.Drawing.Size(468, 121);
            this.lstSavedExpressions.TabIndex = 10;
            this.lstSavedExpressions.Enter += new System.EventHandler(this.lstSavedExpressions_Enter);
            // 
            // lblSavedExpressions
            // 
            this.lblSavedExpressions.AutoSize = true;
            this.lblSavedExpressions.Location = new System.Drawing.Point(16, 89);
            this.lblSavedExpressions.Name = "lblSavedExpressions";
            this.lblSavedExpressions.Size = new System.Drawing.Size(99, 13);
            this.lblSavedExpressions.TabIndex = 11;
            this.lblSavedExpressions.Text = "Saved expressions:";
            // 
            // frmStatsDirectCalculator
            // 
            this.AcceptButton = this.cmdCalculate;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdClose;
            this.ClientSize = new System.Drawing.Size(578, 249);
            this.Controls.Add(this.lblSavedExpressions);
            this.Controls.Add(this.lstSavedExpressions);
            this.Controls.Add(this.cmdClose);
            this.Controls.Add(this.cmdHelp);
            this.Controls.Add(this.cmdDelete);
            this.Controls.Add(this.cmdPaste);
            this.Controls.Add(this.cmdSave);
            this.Controls.Add(this.cmdCalculate);
            this.Controls.Add(this.lblResult);
            this.Controls.Add(this.txtResult);
            this.Controls.Add(this.txtExpression);
            this.Controls.Add(this.lblExpressionToEvaluate);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(500, 276);
            this.Name = "frmStatsDirectCalculator";
            this.Text = "StatsDirect Calculator";
            this.Load += new System.EventHandler(this.frmStatsDirectCalculator_Load);
            this.Shown += new System.EventHandler(this.frmStatsDirectCalculator_Shown);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.frmStatsDirectCalculator_FormClosing);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblExpressionToEvaluate;
        private System.Windows.Forms.TextBox txtExpression;
        private System.Windows.Forms.TextBox txtResult;
        private System.Windows.Forms.Label lblResult;
        private System.Windows.Forms.Button cmdCalculate;
        private System.Windows.Forms.Button cmdSave;
        private System.Windows.Forms.Button cmdPaste;
        private System.Windows.Forms.Button cmdDelete;
        private System.Windows.Forms.Button cmdHelp;
        private System.Windows.Forms.Button cmdClose;
        private System.Windows.Forms.ListBox lstSavedExpressions;
        private System.Windows.Forms.Label lblSavedExpressions;
    }
}

