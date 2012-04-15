namespace StatsDirect.UI
{
    partial class frmSetupTools
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
            this.cmdCancel = new System.Windows.Forms.Button();
            this.cmdOK = new System.Windows.Forms.Button();
            this.grid = new System.Windows.Forms.DataGridView();
            this.App = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Path = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.rdoExcelOn = new System.Windows.Forms.RadioButton();
            this.rdoExcelOff = new System.Windows.Forms.RadioButton();
            this.lblExcelLink = new System.Windows.Forms.Label();
            this.cmdDefaultTools = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.grid)).BeginInit();
            this.SuspendLayout();
            // 
            // cmdCancel
            // 
            this.cmdCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.cmdCancel.Location = new System.Drawing.Point(382, 13);
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
            this.cmdOK.Location = new System.Drawing.Point(301, 13);
            this.cmdOK.Name = "cmdOK";
            this.cmdOK.Size = new System.Drawing.Size(75, 23);
            this.cmdOK.TabIndex = 2;
            this.cmdOK.Text = "&OK";
            this.cmdOK.UseVisualStyleBackColor = true;
            this.cmdOK.Click += new System.EventHandler(this.cmdOK_Click);
            // 
            // grid
            // 
            this.grid.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.grid.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.grid.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.App,
            this.Path});
            this.grid.Location = new System.Drawing.Point(12, 43);
            this.grid.Name = "grid";
            this.grid.Size = new System.Drawing.Size(445, 272);
            this.grid.TabIndex = 3;
            // 
            // App
            // 
            this.App.HeaderText = "Menu entry";
            this.App.Name = "App";
            // 
            // Path
            // 
            this.Path.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.Path.HeaderText = "Command line";
            this.Path.Name = "Path";
            this.Path.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // rdoExcelOn
            // 
            this.rdoExcelOn.AutoSize = true;
            this.rdoExcelOn.Location = new System.Drawing.Point(73, 16);
            this.rdoExcelOn.Name = "rdoExcelOn";
            this.rdoExcelOn.Size = new System.Drawing.Size(39, 17);
            this.rdoExcelOn.TabIndex = 4;
            this.rdoExcelOn.TabStop = true;
            this.rdoExcelOn.Text = "On";
            this.rdoExcelOn.UseVisualStyleBackColor = true;
            // 
            // rdoExcelOff
            // 
            this.rdoExcelOff.AutoSize = true;
            this.rdoExcelOff.Location = new System.Drawing.Point(118, 16);
            this.rdoExcelOff.Name = "rdoExcelOff";
            this.rdoExcelOff.Size = new System.Drawing.Size(39, 17);
            this.rdoExcelOff.TabIndex = 5;
            this.rdoExcelOff.TabStop = true;
            this.rdoExcelOff.Text = "Off";
            this.rdoExcelOff.UseVisualStyleBackColor = true;
            // 
            // lblExcelLink
            // 
            this.lblExcelLink.AutoSize = true;
            this.lblExcelLink.Location = new System.Drawing.Point(12, 18);
            this.lblExcelLink.Name = "lblExcelLink";
            this.lblExcelLink.Size = new System.Drawing.Size(55, 13);
            this.lblExcelLink.TabIndex = 6;
            this.lblExcelLink.Text = "Excel link:";
            // 
            // cmdDefaultTools
            // 
            this.cmdDefaultTools.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.cmdDefaultTools.Location = new System.Drawing.Point(358, 321);
            this.cmdDefaultTools.Name = "cmdDefaultTools";
            this.cmdDefaultTools.Size = new System.Drawing.Size(98, 23);
            this.cmdDefaultTools.TabIndex = 7;
            this.cmdDefaultTools.Text = "Reset to defaults";
            this.cmdDefaultTools.UseVisualStyleBackColor = true;
            this.cmdDefaultTools.Click += new System.EventHandler(this.cmdDefaultTools_Click);
            // 
            // frmSetupTools
            // 
            this.AcceptButton = this.cmdOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.cmdCancel;
            this.ClientSize = new System.Drawing.Size(469, 356);
            this.Controls.Add(this.cmdDefaultTools);
            this.Controls.Add(this.lblExcelLink);
            this.Controls.Add(this.rdoExcelOff);
            this.Controls.Add(this.rdoExcelOn);
            this.Controls.Add(this.grid);
            this.Controls.Add(this.cmdOK);
            this.Controls.Add(this.cmdCancel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.MinimumSize = new System.Drawing.Size(300, 200);
            this.Name = "frmSetupTools";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Setup Tools";
            this.Load += new System.EventHandler(this.frmSetupTools_Load);
            ((System.ComponentModel.ISupportInitialize)(this.grid)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button cmdCancel;
        private System.Windows.Forms.Button cmdOK;
        private System.Windows.Forms.DataGridView grid;
        private System.Windows.Forms.DataGridViewTextBoxColumn App;
        private System.Windows.Forms.DataGridViewTextBoxColumn Path;
        private System.Windows.Forms.RadioButton rdoExcelOn;
        private System.Windows.Forms.RadioButton rdoExcelOff;
        private System.Windows.Forms.Label lblExcelLink;
        private System.Windows.Forms.Button cmdDefaultTools;
    }
}