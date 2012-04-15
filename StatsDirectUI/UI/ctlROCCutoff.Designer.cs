namespace StatsDirect.UI
{
    partial class ctlROCCutoff
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
            this.components = new System.ComponentModel.Container();
            this.lblA = new System.Windows.Forms.Label();
            this.txtA = new System.Windows.Forms.TextBox();
            this.txtB = new System.Windows.Forms.TextBox();
            this.lblB = new System.Windows.Forms.Label();
            this.txtD = new System.Windows.Forms.TextBox();
            this.lblD = new System.Windows.Forms.Label();
            this.txtC = new System.Windows.Forms.TextBox();
            this.lblC = new System.Windows.Forms.Label();
            this.txtNegative = new System.Windows.Forms.TextBox();
            this.lblNegative = new System.Windows.Forms.Label();
            this.txtPositive = new System.Windows.Forms.TextBox();
            this.lblPositive = new System.Windows.Forms.Label();
            this.txtSpecificity = new System.Windows.Forms.TextBox();
            this.lblSpecificity = new System.Windows.Forms.Label();
            this.txtSensitivity = new System.Windows.Forms.TextBox();
            this.lblSensitivity = new System.Windows.Forms.Label();
            this.lblOptimum = new System.Windows.Forms.Label();
            this.txtCutoff = new System.Windows.Forms.TextBox();
            this.lblCutoff = new System.Windows.Forms.Label();
            this.txtSensSpec = new System.Windows.Forms.TextBox();
            this.lblSensSpec = new System.Windows.Forms.Label();
            this.cmdReset = new System.Windows.Forms.Button();
            this.btnUp = new System.Windows.Forms.Button();
            this.btnDown = new System.Windows.Forms.Button();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // lblA
            // 
            this.lblA.AutoSize = true;
            this.lblA.Location = new System.Drawing.Point(3, 6);
            this.lblA.Name = "lblA";
            this.lblA.Size = new System.Drawing.Size(14, 13);
            this.lblA.TabIndex = 0;
            this.lblA.Text = "A";
            // 
            // txtA
            // 
            this.txtA.Location = new System.Drawing.Point(23, 3);
            this.txtA.Name = "txtA";
            this.txtA.ReadOnly = true;
            this.txtA.Size = new System.Drawing.Size(124, 20);
            this.txtA.TabIndex = 1;
            // 
            // txtB
            // 
            this.txtB.Location = new System.Drawing.Point(173, 3);
            this.txtB.Name = "txtB";
            this.txtB.ReadOnly = true;
            this.txtB.Size = new System.Drawing.Size(124, 20);
            this.txtB.TabIndex = 3;
            // 
            // lblB
            // 
            this.lblB.AutoSize = true;
            this.lblB.Location = new System.Drawing.Point(153, 6);
            this.lblB.Name = "lblB";
            this.lblB.Size = new System.Drawing.Size(14, 13);
            this.lblB.TabIndex = 2;
            this.lblB.Text = "B";
            // 
            // txtD
            // 
            this.txtD.Location = new System.Drawing.Point(173, 29);
            this.txtD.Name = "txtD";
            this.txtD.ReadOnly = true;
            this.txtD.Size = new System.Drawing.Size(124, 20);
            this.txtD.TabIndex = 7;
            // 
            // lblD
            // 
            this.lblD.AutoSize = true;
            this.lblD.Location = new System.Drawing.Point(153, 32);
            this.lblD.Name = "lblD";
            this.lblD.Size = new System.Drawing.Size(15, 13);
            this.lblD.TabIndex = 6;
            this.lblD.Text = "D";
            // 
            // txtC
            // 
            this.txtC.Location = new System.Drawing.Point(23, 29);
            this.txtC.Name = "txtC";
            this.txtC.ReadOnly = true;
            this.txtC.Size = new System.Drawing.Size(124, 20);
            this.txtC.TabIndex = 5;
            // 
            // lblC
            // 
            this.lblC.AutoSize = true;
            this.lblC.Location = new System.Drawing.Point(3, 32);
            this.lblC.Name = "lblC";
            this.lblC.Size = new System.Drawing.Size(14, 13);
            this.lblC.TabIndex = 4;
            this.lblC.Text = "C";
            // 
            // txtNegative
            // 
            this.txtNegative.Location = new System.Drawing.Point(156, 126);
            this.txtNegative.Name = "txtNegative";
            this.txtNegative.ReadOnly = true;
            this.txtNegative.Size = new System.Drawing.Size(141, 20);
            this.txtNegative.TabIndex = 15;
            // 
            // lblNegative
            // 
            this.lblNegative.AutoSize = true;
            this.lblNegative.Location = new System.Drawing.Point(153, 109);
            this.lblNegative.Name = "lblNegative";
            this.lblNegative.Size = new System.Drawing.Size(100, 13);
            this.lblNegative.TabIndex = 14;
            this.lblNegative.Text = "-ve predictive value";
            // 
            // txtPositive
            // 
            this.txtPositive.Location = new System.Drawing.Point(6, 126);
            this.txtPositive.Name = "txtPositive";
            this.txtPositive.ReadOnly = true;
            this.txtPositive.Size = new System.Drawing.Size(141, 20);
            this.txtPositive.TabIndex = 13;
            // 
            // lblPositive
            // 
            this.lblPositive.AutoSize = true;
            this.lblPositive.Location = new System.Drawing.Point(3, 109);
            this.lblPositive.Name = "lblPositive";
            this.lblPositive.Size = new System.Drawing.Size(103, 13);
            this.lblPositive.TabIndex = 12;
            this.lblPositive.Text = "+ve predictive value";
            // 
            // txtSpecificity
            // 
            this.txtSpecificity.Location = new System.Drawing.Point(156, 81);
            this.txtSpecificity.Name = "txtSpecificity";
            this.txtSpecificity.ReadOnly = true;
            this.txtSpecificity.Size = new System.Drawing.Size(141, 20);
            this.txtSpecificity.TabIndex = 11;
            // 
            // lblSpecificity
            // 
            this.lblSpecificity.AutoSize = true;
            this.lblSpecificity.Location = new System.Drawing.Point(153, 64);
            this.lblSpecificity.Name = "lblSpecificity";
            this.lblSpecificity.Size = new System.Drawing.Size(53, 13);
            this.lblSpecificity.TabIndex = 10;
            this.lblSpecificity.Text = "specificity";
            // 
            // txtSensitivity
            // 
            this.txtSensitivity.Location = new System.Drawing.Point(6, 81);
            this.txtSensitivity.Name = "txtSensitivity";
            this.txtSensitivity.ReadOnly = true;
            this.txtSensitivity.Size = new System.Drawing.Size(141, 20);
            this.txtSensitivity.TabIndex = 9;
            // 
            // lblSensitivity
            // 
            this.lblSensitivity.AutoSize = true;
            this.lblSensitivity.Location = new System.Drawing.Point(3, 64);
            this.lblSensitivity.Name = "lblSensitivity";
            this.lblSensitivity.Size = new System.Drawing.Size(52, 13);
            this.lblSensitivity.TabIndex = 8;
            this.lblSensitivity.Text = "sensitivity";
            // 
            // lblOptimum
            // 
            this.lblOptimum.AutoSize = true;
            this.lblOptimum.Location = new System.Drawing.Point(3, 158);
            this.lblOptimum.Name = "lblOptimum";
            this.lblOptimum.Size = new System.Drawing.Size(238, 13);
            this.lblOptimum.TabIndex = 16;
            this.lblOptimum.Text = "For optimum, sensitivity:specificity weighting = x:1";
            // 
            // txtCutoff
            // 
            this.txtCutoff.Location = new System.Drawing.Point(6, 209);
            this.txtCutoff.Name = "txtCutoff";
            this.txtCutoff.Size = new System.Drawing.Size(113, 20);
            this.txtCutoff.TabIndex = 20;
            this.txtCutoff.KeyPress += new System.Windows.Forms.KeyPressEventHandler(this.txtCutoff_KeyPress);
            // 
            // lblCutoff
            // 
            this.lblCutoff.AutoSize = true;
            this.lblCutoff.Location = new System.Drawing.Point(6, 192);
            this.lblCutoff.Name = "lblCutoff";
            this.lblCutoff.Size = new System.Drawing.Size(37, 13);
            this.lblCutoff.TabIndex = 19;
            this.lblCutoff.Text = "cut-off";
            // 
            // txtSensSpec
            // 
            this.txtSensSpec.Location = new System.Drawing.Point(315, 81);
            this.txtSensSpec.Name = "txtSensSpec";
            this.txtSensSpec.ReadOnly = true;
            this.txtSensSpec.Size = new System.Drawing.Size(135, 20);
            this.txtSensSpec.TabIndex = 18;
            // 
            // lblSensSpec
            // 
            this.lblSensSpec.AutoSize = true;
            this.lblSensSpec.Location = new System.Drawing.Point(315, 64);
            this.lblSensSpec.Name = "lblSensSpec";
            this.lblSensSpec.Size = new System.Drawing.Size(70, 13);
            this.lblSensSpec.TabIndex = 17;
            this.lblSensSpec.Text = "sens. + spec.";
            // 
            // cmdReset
            // 
            this.cmdReset.Location = new System.Drawing.Point(156, 207);
            this.cmdReset.Name = "cmdReset";
            this.cmdReset.Size = new System.Drawing.Size(75, 23);
            this.cmdReset.TabIndex = 23;
            this.cmdReset.Text = "&Reset";
            this.cmdReset.UseVisualStyleBackColor = true;
            this.cmdReset.Click += new System.EventHandler(this.cmdReset_Click);
            // 
            // btnUp
            // 
            this.btnUp.Location = new System.Drawing.Point(122, 209);
            this.btnUp.Margin = new System.Windows.Forms.Padding(0);
            this.btnUp.Name = "btnUp";
            this.btnUp.Size = new System.Drawing.Size(25, 10);
            this.btnUp.TabIndex = 24;
            this.btnUp.Text = "^";
            this.btnUp.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnUp.UseVisualStyleBackColor = true;
            this.btnUp.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnUp_MouseDown);
            this.btnUp.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnUp_MouseUp);
            // 
            // btnDown
            // 
            this.btnDown.Location = new System.Drawing.Point(122, 219);
            this.btnDown.Margin = new System.Windows.Forms.Padding(0);
            this.btnDown.Name = "btnDown";
            this.btnDown.Size = new System.Drawing.Size(25, 10);
            this.btnDown.TabIndex = 25;
            this.btnDown.Text = "v";
            this.btnDown.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnDown.UseVisualStyleBackColor = true;
            this.btnDown.MouseDown += new System.Windows.Forms.MouseEventHandler(this.btnDown_MouseDown);
            this.btnDown.MouseUp += new System.Windows.Forms.MouseEventHandler(this.btnDown_MouseUp);
            // 
            // timer1
            // 
            this.timer1.Interval = 25;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // ctlROCCutoff
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.btnDown);
            this.Controls.Add(this.btnUp);
            this.Controls.Add(this.cmdReset);
            this.Controls.Add(this.txtCutoff);
            this.Controls.Add(this.lblCutoff);
            this.Controls.Add(this.txtSensSpec);
            this.Controls.Add(this.lblSensSpec);
            this.Controls.Add(this.lblOptimum);
            this.Controls.Add(this.txtNegative);
            this.Controls.Add(this.lblNegative);
            this.Controls.Add(this.txtPositive);
            this.Controls.Add(this.lblPositive);
            this.Controls.Add(this.txtSpecificity);
            this.Controls.Add(this.lblSpecificity);
            this.Controls.Add(this.txtSensitivity);
            this.Controls.Add(this.lblSensitivity);
            this.Controls.Add(this.txtD);
            this.Controls.Add(this.lblD);
            this.Controls.Add(this.txtC);
            this.Controls.Add(this.lblC);
            this.Controls.Add(this.txtB);
            this.Controls.Add(this.lblB);
            this.Controls.Add(this.txtA);
            this.Controls.Add(this.lblA);
            this.Name = "ctlROCCutoff";
            this.Size = new System.Drawing.Size(459, 238);
            this.Load += new System.EventHandler(this.ctlROCCutoff_Load);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.frmROCCutoff_KeyDown);
            this.KeyUp += new System.Windows.Forms.KeyEventHandler(this.frmROCCutoff_KeyUp);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label lblA;
        private System.Windows.Forms.TextBox txtA;
        private System.Windows.Forms.TextBox txtB;
        private System.Windows.Forms.Label lblB;
        private System.Windows.Forms.TextBox txtD;
        private System.Windows.Forms.Label lblD;
        private System.Windows.Forms.TextBox txtC;
        private System.Windows.Forms.Label lblC;
        private System.Windows.Forms.TextBox txtNegative;
        private System.Windows.Forms.Label lblNegative;
        private System.Windows.Forms.TextBox txtPositive;
        private System.Windows.Forms.Label lblPositive;
        private System.Windows.Forms.TextBox txtSpecificity;
        private System.Windows.Forms.Label lblSpecificity;
        private System.Windows.Forms.TextBox txtSensitivity;
        private System.Windows.Forms.Label lblSensitivity;
        private System.Windows.Forms.Label lblOptimum;
        private System.Windows.Forms.TextBox txtCutoff;
        private System.Windows.Forms.Label lblCutoff;
        private System.Windows.Forms.TextBox txtSensSpec;
        private System.Windows.Forms.Label lblSensSpec;
        private System.Windows.Forms.Button cmdReset;
        private System.Windows.Forms.Button btnUp;
        private System.Windows.Forms.Button btnDown;
        private System.Windows.Forms.Timer timer1;
    }
}