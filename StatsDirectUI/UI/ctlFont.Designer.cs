namespace StatsDirect.UI
{
    partial class ctlFont
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.cmdChange = new System.Windows.Forms.Button();
            this.lblPurpose = new System.Windows.Forms.Label();
            this.lblDescription = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // cmdChange
            // 
            this.cmdChange.Location = new System.Drawing.Point(0, 4);
            this.cmdChange.Name = "cmdChange";
            this.cmdChange.Size = new System.Drawing.Size(75, 23);
            this.cmdChange.TabIndex = 0;
            this.cmdChange.Text = "Change...";
            this.cmdChange.UseVisualStyleBackColor = true;
            this.cmdChange.Click += new System.EventHandler(this.cmdChange_Click);
            // 
            // lblPurpose
            // 
            this.lblPurpose.AutoSize = true;
            this.lblPurpose.Location = new System.Drawing.Point(81, 0);
            this.lblPurpose.Name = "lblPurpose";
            this.lblPurpose.Size = new System.Drawing.Size(79, 13);
            this.lblPurpose.TabIndex = 1;
            this.lblPurpose.Text = "Purpose of font";
            // 
            // lblDescription
            // 
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new System.Drawing.Point(81, 18);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(93, 13);
            this.lblDescription.TabIndex = 2;
            this.lblDescription.Text = "Description of font";
            // 
            // ctlFont
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.lblPurpose);
            this.Controls.Add(this.cmdChange);
            this.Margin = new System.Windows.Forms.Padding(0);
            this.Name = "ctlFont";
            this.Size = new System.Drawing.Size(177, 31);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdChange;
        private System.Windows.Forms.Label lblPurpose;
        private System.Windows.Forms.Label lblDescription;
    }
}
