namespace StatsDirect.UI
{
    partial class ctlSort
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.panel1 = new System.Windows.Forms.Panel();
            this.lblRubric = new System.Windows.Forms.Label();
            this.chkUseHeaders = new System.Windows.Forms.CheckBox();
            this.gridKeys = new System.Windows.Forms.DataGridView();
            this.Level = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.SortBy = new StatsDirect.UI.DGVComboBoxItemColumn();
            this.Order = new System.Windows.Forms.DataGridViewComboBoxColumn();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridKeys)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.lblRubric);
            this.panel1.Controls.Add(this.chkUseHeaders);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(478, 48);
            this.panel1.TabIndex = 1;
            // 
            // lblRubric
            // 
            this.lblRubric.AutoSize = true;
            this.lblRubric.Location = new System.Drawing.Point(3, 32);
            this.lblRubric.Name = "lblRubric";
            this.lblRubric.Size = new System.Drawing.Size(228, 13);
            this.lblRubric.TabIndex = 1;
            this.lblRubric.Text = "Sort all selected rows by one or more variables:";
            // 
            // chkUseHeaders
            // 
            this.chkUseHeaders.AutoSize = true;
            this.chkUseHeaders.Checked = true;
            this.chkUseHeaders.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkUseHeaders.Location = new System.Drawing.Point(3, 8);
            this.chkUseHeaders.Name = "chkUseHeaders";
            this.chkUseHeaders.Size = new System.Drawing.Size(181, 17);
            this.chkUseHeaders.TabIndex = 0;
            this.chkUseHeaders.Text = "Treat top row as column heading";
            this.chkUseHeaders.UseVisualStyleBackColor = true;
            this.chkUseHeaders.CheckedChanged += new System.EventHandler(this.chkUseHeaders_CheckedChanged);
            // 
            // gridKeys
            // 
            this.gridKeys.AllowUserToAddRows = false;
            this.gridKeys.AllowUserToResizeRows = false;
            this.gridKeys.BackgroundColor = System.Drawing.SystemColors.Control;
            this.gridKeys.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridKeys.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Level,
            this.SortBy,
            this.Order});
            this.gridKeys.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridKeys.EditMode = System.Windows.Forms.DataGridViewEditMode.EditOnEnter;
            this.gridKeys.Location = new System.Drawing.Point(0, 48);
            this.gridKeys.MultiSelect = false;
            this.gridKeys.Name = "gridKeys";
            this.gridKeys.RowHeadersVisible = false;
            this.gridKeys.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.gridKeys.ShowEditingIcon = false;
            this.gridKeys.Size = new System.Drawing.Size(478, 119);
            this.gridKeys.TabIndex = 0;
            this.gridKeys.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.gridKeys_CellClick);
            this.gridKeys.DefaultValuesNeeded += new System.Windows.Forms.DataGridViewRowEventHandler(this.gridKeys_DefaultValuesNeeded);
            this.gridKeys.EditingControlShowing += new System.Windows.Forms.DataGridViewEditingControlShowingEventHandler(this.gridKeys_EditingControlShowing);
            // 
            // Level
            // 
            this.Level.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            this.Level.DefaultCellStyle = dataGridViewCellStyle1;
            this.Level.HeaderText = "Level";
            this.Level.Name = "Level";
            this.Level.ReadOnly = true;
            this.Level.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Level.Width = 58;
            // 
            // SortBy
            // 
            this.SortBy.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.SortBy.HeaderText = "Variable";
            this.SortBy.Name = "SortBy";
            this.SortBy.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            // 
            // Order
            // 
            this.Order.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.AllCells;
            this.Order.HeaderText = "Direction";
            this.Order.Items.AddRange(new object[] {
            "Low to high",
            "High to low"});
            this.Order.Name = "Order";
            this.Order.Resizable = System.Windows.Forms.DataGridViewTriState.False;
            this.Order.Width = 55;
            // 
            // ctlSort
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.gridKeys);
            this.Controls.Add(this.panel1);
            this.Name = "ctlSort";
            this.Size = new System.Drawing.Size(478, 167);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.gridKeys)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.DataGridView gridKeys;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.CheckBox chkUseHeaders;
        private System.Windows.Forms.Label lblRubric;
        private System.Windows.Forms.DataGridViewTextBoxColumn Level;
        private DGVComboBoxItemColumn SortBy;
        private System.Windows.Forms.DataGridViewComboBoxColumn Order;
    }
}
