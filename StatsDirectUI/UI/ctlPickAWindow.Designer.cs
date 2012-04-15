namespace StatsDirect.UI
{
    partial class ctlPickAWindow
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
            this.lblSelectWindow = new System.Windows.Forms.Label();
            this.cboWindows = new System.Windows.Forms.ComboBox();
            this.rdoFirstColumn = new System.Windows.Forms.RadioButton();
            this.rdoBeforeSelection = new System.Windows.Forms.RadioButton();
            this.rdoAfterSelection = new System.Windows.Forms.RadioButton();
            this.rdoLastColumn = new System.Windows.Forms.RadioButton();
            this.SuspendLayout();
            // 
            // lblSelectWindow
            // 
            this.lblSelectWindow.AutoSize = true;
            this.lblSelectWindow.BackColor = System.Drawing.Color.Transparent;
            this.lblSelectWindow.Location = new System.Drawing.Point(279, 3);
            this.lblSelectWindow.Margin = new System.Windows.Forms.Padding(0, 0, 3, 0);
            this.lblSelectWindow.Name = "lblSelectWindow";
            this.lblSelectWindow.Size = new System.Drawing.Size(133, 13);
            this.lblSelectWindow.TabIndex = 2;
            this.lblSelectWindow.Text = "Select a window for output";
            // 
            // cboWindows
            // 
            this.cboWindows.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.cboWindows.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cboWindows.FormattingEnabled = true;
            this.cboWindows.Location = new System.Drawing.Point(0, 0);
            this.cboWindows.Margin = new System.Windows.Forms.Padding(3, 0, 0, 0);
            this.cboWindows.MaxDropDownItems = 20;
            this.cboWindows.Name = "cboWindows";
            this.cboWindows.Size = new System.Drawing.Size(279, 21);
            this.cboWindows.TabIndex = 3;
            // 
            // rdoFirstColumn
            // 
            this.rdoFirstColumn.AutoSize = true;
            this.rdoFirstColumn.Location = new System.Drawing.Point(4, 25);
            this.rdoFirstColumn.Name = "rdoFirstColumn";
            this.rdoFirstColumn.Size = new System.Drawing.Size(81, 17);
            this.rdoFirstColumn.TabIndex = 4;
            this.rdoFirstColumn.Text = "First column";
            this.rdoFirstColumn.UseVisualStyleBackColor = true;
            this.rdoFirstColumn.CheckedChanged += new System.EventHandler(this.rdoFirstColumn_CheckedChanged);
            // 
            // rdoBeforeSelection
            // 
            this.rdoBeforeSelection.AutoSize = true;
            this.rdoBeforeSelection.Location = new System.Drawing.Point(92, 25);
            this.rdoBeforeSelection.Name = "rdoBeforeSelection";
            this.rdoBeforeSelection.Size = new System.Drawing.Size(101, 17);
            this.rdoBeforeSelection.TabIndex = 5;
            this.rdoBeforeSelection.Text = "Before selection";
            this.rdoBeforeSelection.UseVisualStyleBackColor = true;
            this.rdoBeforeSelection.CheckedChanged += new System.EventHandler(this.rdoBeforeSelection_CheckedChanged);
            // 
            // rdoAfterSelection
            // 
            this.rdoAfterSelection.AutoSize = true;
            this.rdoAfterSelection.Checked = true;
            this.rdoAfterSelection.Location = new System.Drawing.Point(200, 25);
            this.rdoAfterSelection.Name = "rdoAfterSelection";
            this.rdoAfterSelection.Size = new System.Drawing.Size(92, 17);
            this.rdoAfterSelection.TabIndex = 6;
            this.rdoAfterSelection.TabStop = true;
            this.rdoAfterSelection.Text = "After selection";
            this.rdoAfterSelection.UseVisualStyleBackColor = true;
            this.rdoAfterSelection.CheckedChanged += new System.EventHandler(this.rdoAfterSelection_CheckedChanged);
            // 
            // rdoLastColumn
            // 
            this.rdoLastColumn.AutoSize = true;
            this.rdoLastColumn.Location = new System.Drawing.Point(299, 25);
            this.rdoLastColumn.Name = "rdoLastColumn";
            this.rdoLastColumn.Size = new System.Drawing.Size(82, 17);
            this.rdoLastColumn.TabIndex = 7;
            this.rdoLastColumn.Text = "Last column";
            this.rdoLastColumn.UseVisualStyleBackColor = true;
            this.rdoLastColumn.CheckedChanged += new System.EventHandler(this.rdoLastColumn_CheckedChanged);
            // 
            // ctlPickAWindow
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.rdoLastColumn);
            this.Controls.Add(this.rdoAfterSelection);
            this.Controls.Add(this.rdoBeforeSelection);
            this.Controls.Add(this.rdoFirstColumn);
            this.Controls.Add(this.lblSelectWindow);
            this.Controls.Add(this.cboWindows);
            this.Name = "ctlPickAWindow";
            this.Size = new System.Drawing.Size(415, 45);
            this.Load += new System.EventHandler(this.ctlPickAWindow_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblSelectWindow;
        private System.Windows.Forms.ComboBox cboWindows;
        private System.Windows.Forms.RadioButton rdoFirstColumn;
        private System.Windows.Forms.RadioButton rdoBeforeSelection;
        private System.Windows.Forms.RadioButton rdoAfterSelection;
        private System.Windows.Forms.RadioButton rdoLastColumn;
    }
}