namespace StatsDirect.UI
{
    partial class ctlFindAndReplaceData
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
            this.label3 = new System.Windows.Forms.Label();
            this.lblMessage = new System.Windows.Forms.Label();
            this.txtExpression = new System.Windows.Forms.TextBox();
            this.txtReplace = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(-3, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(331, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Enter an expression/rule for extracting your variable (example: X < 0):";
            // 
            // lblMessage
            // 
            this.lblMessage.AutoSize = true;
            this.lblMessage.Location = new System.Drawing.Point(-3, 39);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Size = new System.Drawing.Size(383, 13);
            this.lblMessage.TabIndex = 7;
            this.lblMessage.Text = "Enter an expression for the replacement for matching values (example: X + 100):";
            // 
            // txtExpression
            // 
            this.txtExpression.Location = new System.Drawing.Point(0, 16);
            this.txtExpression.Name = "txtExpression";
            this.txtExpression.Size = new System.Drawing.Size(421, 20);
            this.txtExpression.TabIndex = 0;
            // 
            // txtReplace
            // 
            this.txtReplace.Location = new System.Drawing.Point(0, 55);
            this.txtReplace.Name = "txtReplace";
            this.txtReplace.Size = new System.Drawing.Size(421, 20);
            this.txtReplace.TabIndex = 1;
            // 
            // ctlFindAndReplaceData
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.txtReplace);
            this.Controls.Add(this.txtExpression);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.label3);
            this.Margin = new System.Windows.Forms.Padding(0, 0, 0, 3);
            this.Name = "ctlFindAndReplaceData";
            this.Size = new System.Drawing.Size(424, 78);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblMessage;
        private System.Windows.Forms.TextBox txtExpression;
        private System.Windows.Forms.TextBox txtReplace;
    }
}