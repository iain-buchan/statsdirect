namespace StatsDirect.UI
{
    partial class ctlExtraction
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
            this.cmdCount = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.lblIdentifiers = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.lblMessage = new System.Windows.Forms.Label();
            this.chkKeepRowPositions = new System.Windows.Forms.CheckBox();
            this.txtExpression = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // cmdCount
            // 
            this.cmdCount.Location = new System.Drawing.Point(427, 14);
            this.cmdCount.Name = "cmdCount";
            this.cmdCount.Size = new System.Drawing.Size(75, 23);
            this.cmdCount.TabIndex = 3;
            this.cmdCount.Text = "Cou&nt";
            this.cmdCount.UseVisualStyleBackColor = true;
            this.cmdCount.Click += new System.EventHandler(this.cmdCount_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(-3, 97);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(227, 13);
            this.label1.TabIndex = 4;
            this.label1.Text = "List of identifiers to use in extraction expression";
            // 
            // lblIdentifiers
            // 
            this.lblIdentifiers.AutoSize = true;
            this.lblIdentifiers.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblIdentifiers.Location = new System.Drawing.Point(-3, 121);
            this.lblIdentifiers.Margin = new System.Windows.Forms.Padding(3);
            this.lblIdentifiers.Name = "lblIdentifiers";
            this.lblIdentifiers.Size = new System.Drawing.Size(76, 13);
            this.lblIdentifiers.TabIndex = 5;
            this.lblIdentifiers.Text = "<identifiers>";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(-3, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(252, 13);
            this.label3.TabIndex = 6;
            this.label3.Text = "Enter an expression/rule for extracting your variable:";
            // 
            // lblMessage
            // 
            this.lblMessage.AutoSize = true;
            this.lblMessage.Location = new System.Drawing.Point(-3, 39);
            this.lblMessage.Name = "lblMessage";
            this.lblMessage.Size = new System.Drawing.Size(188, 13);
            this.lblMessage.TabIndex = 7;
            this.lblMessage.Text = "Example: X1=1 and X2<>0 and X3>20";
            // 
            // chkKeepRowPositions
            // 
            this.chkKeepRowPositions.AutoSize = true;
            this.chkKeepRowPositions.Location = new System.Drawing.Point(0, 68);
            this.chkKeepRowPositions.Name = "chkKeepRowPositions";
            this.chkKeepRowPositions.Size = new System.Drawing.Size(264, 17);
            this.chkKeepRowPositions.TabIndex = 1;
            this.chkKeepRowPositions.Text = "Keep row positions the same as in the original data";
            this.chkKeepRowPositions.UseVisualStyleBackColor = true;
            // 
            // txtExpression
            // 
            this.txtExpression.Location = new System.Drawing.Point(0, 16);
            this.txtExpression.Name = "txtExpression";
            this.txtExpression.Size = new System.Drawing.Size(421, 20);
            this.txtExpression.TabIndex = 0;
            // 
            // ctlExtraction
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.Controls.Add(this.chkKeepRowPositions);
            this.Controls.Add(this.txtExpression);
            this.Controls.Add(this.lblMessage);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.lblIdentifiers);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.cmdCount);
            this.Margin = new System.Windows.Forms.Padding(0, 0, 0, 3);
            this.Name = "ctlExtraction";
            this.Size = new System.Drawing.Size(505, 137);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdCount;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblIdentifiers;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label lblMessage;
        private System.Windows.Forms.CheckBox chkKeepRowPositions;
        private System.Windows.Forms.TextBox txtExpression;
    }
}