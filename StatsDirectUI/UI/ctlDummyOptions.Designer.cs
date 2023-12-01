namespace StatsDirect.UI
{
    partial class ctlDummyOptions
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
            this.lblRubric = new System.Windows.Forms.Label();
            this.lstVariables = new System.Windows.Forms.ListBox();
            this.SuspendLayout();
            // 
            // lblRubric
            // 
            this.lblRubric.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lblRubric.Location = new System.Drawing.Point(4, 4);
            this.lblRubric.Name = "lblRubric";
            this.lblRubric.Size = new System.Drawing.Size(366, 112);
            this.lblRubric.TabIndex = 6;
            this.lblRubric.Text = "Rubric here";
            // 
            // lstVariables
            // 
            this.lstVariables.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lstVariables.FormattingEnabled = true;
            this.lstVariables.Location = new System.Drawing.Point(7, 119);
            this.lstVariables.Name = "lstVariables";
            this.lstVariables.Size = new System.Drawing.Size(363, 160);
            this.lstVariables.TabIndex = 7;
            // 
            // ctlDummyOptions
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.lstVariables);
            this.Controls.Add(this.lblRubric);
            this.Name = "ctlDummyOptions";
            this.Size = new System.Drawing.Size(373, 291);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblRubric;
        private System.Windows.Forms.ListBox lstVariables;
    }
}