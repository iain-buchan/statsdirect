namespace StatsDirect.UI
{
    partial class frmInsertCells
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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.rdoColumn = new System.Windows.Forms.RadioButton();
            this.rdoRow = new System.Windows.Forms.RadioButton();
            this.rdoDown = new System.Windows.Forms.RadioButton();
            this.rdoRight = new System.Windows.Forms.RadioButton();
            this.cmdCancel = new System.Windows.Forms.Button();
            this.cmdOK = new System.Windows.Forms.Button();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)
                        | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.rdoColumn);
            this.groupBox1.Controls.Add(this.rdoRow);
            this.groupBox1.Controls.Add(this.rdoDown);
            this.groupBox1.Controls.Add(this.rdoRight);
            this.groupBox1.Location = new System.Drawing.Point(12, 42);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(156, 130);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Paste";
            // 
            // rdoColumn
            // 
            this.rdoColumn.AutoSize = true;
            this.rdoColumn.Location = new System.Drawing.Point(7, 100);
            this.rdoColumn.Name = "rdoColumn";
            this.rdoColumn.Size = new System.Drawing.Size(89, 17);
            this.rdoColumn.TabIndex = 3;
            this.rdoColumn.Text = "Entire column";
            this.rdoColumn.UseVisualStyleBackColor = true;
            // 
            // rdoRow
            // 
            this.rdoRow.AutoSize = true;
            this.rdoRow.Location = new System.Drawing.Point(7, 76);
            this.rdoRow.Name = "rdoRow";
            this.rdoRow.Size = new System.Drawing.Size(72, 17);
            this.rdoRow.TabIndex = 2;
            this.rdoRow.Text = "Entire row";
            this.rdoRow.UseVisualStyleBackColor = true;
            // 
            // rdoDown
            // 
            this.rdoDown.AutoSize = true;
            this.rdoDown.Location = new System.Drawing.Point(7, 44);
            this.rdoDown.Name = "rdoDown";
            this.rdoDown.Size = new System.Drawing.Size(99, 17);
            this.rdoDown.TabIndex = 1;
            this.rdoDown.Text = "Shift cells down";
            this.rdoDown.UseVisualStyleBackColor = true;
            // 
            // rdoRight
            // 
            this.rdoRight.AutoSize = true;
            this.rdoRight.Checked = true;
            this.rdoRight.Location = new System.Drawing.Point(7, 20);
            this.rdoRight.Name = "rdoRight";
            this.rdoRight.Size = new System.Drawing.Size(93, 17);
            this.rdoRight.TabIndex = 0;
            this.rdoRight.TabStop = true;
            this.rdoRight.Text = "Shift cells right";
            this.rdoRight.UseVisualStyleBackColor = true;
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(93, 13);
            this.cmdCancel.Name = "cmdCancel";
            this.cmdCancel.Size = new System.Drawing.Size(75, 23);
            this.cmdCancel.TabIndex = 1;
            this.cmdCancel.Text = "&Cancel";
            this.cmdCancel.UseVisualStyleBackColor = true;
            this.cmdCancel.Click += new System.EventHandler(this.cmdCancel_Click);
            // 
            // cmdOK
            // 
            this.cmdOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdOK.Location = new System.Drawing.Point(12, 13);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(75, 23);
            this.cmdOK.TabIndex = 2;
            this.cmdOK.Text = "&OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // frmInsertCells
            // 
            this.AcceptButton = this.cmdOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdCancel;
            this.ClientSize = new System.Drawing.Size(180, 184);
            this.Controls.Add(this.cmdOK);
            this.Controls.Add(this.cmdCancel);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "frmInsertCells";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Insert Cells";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.RadioButton rdoRight;
        private System.Windows.Forms.RadioButton rdoColumn;
        private System.Windows.Forms.RadioButton rdoRow;
        private System.Windows.Forms.RadioButton rdoDown;
    }
}