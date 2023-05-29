
namespace RenTradeWindowForm
{
    partial class SerialQueryForm
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
            this.grpRef = new System.Windows.Forms.GroupBox();
            this.txtRef = new System.Windows.Forms.TextBox();
            this.btnRefOk = new System.Windows.Forms.Button();
            this.lblRefRemarks = new System.Windows.Forms.Label();
            this.label11 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.lblSerialResult = new System.Windows.Forms.Label();
            this.lblCountResult = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.grpRef.SuspendLayout();
            this.SuspendLayout();
            // 
            // grpRef
            // 
            this.grpRef.BackColor = System.Drawing.SystemColors.Control;
            this.grpRef.Controls.Add(this.txtRef);
            this.grpRef.Controls.Add(this.btnRefOk);
            this.grpRef.Controls.Add(this.lblRefRemarks);
            this.grpRef.Controls.Add(this.label11);
            this.grpRef.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.grpRef.Font = new System.Drawing.Font("Segoe UI Semibold", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.grpRef.Location = new System.Drawing.Point(12, 12);
            this.grpRef.Name = "grpRef";
            this.grpRef.Size = new System.Drawing.Size(247, 113);
            this.grpRef.TabIndex = 18;
            this.grpRef.TabStop = false;
            this.grpRef.Text = "Reference Nos.";
            // 
            // txtRef
            // 
            this.txtRef.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtRef.Location = new System.Drawing.Point(6, 45);
            this.txtRef.Name = "txtRef";
            this.txtRef.Size = new System.Drawing.Size(162, 29);
            this.txtRef.TabIndex = 3;
            // 
            // btnRefOk
            // 
            this.btnRefOk.BackColor = System.Drawing.SystemColors.Control;
            this.btnRefOk.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.btnRefOk.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.btnRefOk.ForeColor = System.Drawing.Color.Black;
            this.btnRefOk.Location = new System.Drawing.Point(174, 45);
            this.btnRefOk.Name = "btnRefOk";
            this.btnRefOk.Size = new System.Drawing.Size(65, 29);
            this.btnRefOk.TabIndex = 4;
            this.btnRefOk.Text = "Search";
            this.btnRefOk.UseVisualStyleBackColor = false;
            this.btnRefOk.Click += new System.EventHandler(this.btnRefOk_Click);
            // 
            // lblRefRemarks
            // 
            this.lblRefRemarks.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblRefRemarks.Location = new System.Drawing.Point(4, 75);
            this.lblRefRemarks.Name = "lblRefRemarks";
            this.lblRefRemarks.Size = new System.Drawing.Size(235, 24);
            this.lblRefRemarks.TabIndex = 11;
            this.lblRefRemarks.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Font = new System.Drawing.Font("Segoe UI", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label11.Location = new System.Drawing.Point(6, 29);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(154, 13);
            this.label11.TabIndex = 14;
            this.label11.Text = "Please input Reference value";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Calibri", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.label1.Location = new System.Drawing.Point(12, 138);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(77, 14);
            this.label1.TabIndex = 19;
            this.label1.Text = "Serial Results:";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Calibri", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label2.Location = new System.Drawing.Point(28, 157);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(66, 14);
            this.label2.TabIndex = 20;
            this.label2.Text = "Serial Nos:";
            // 
            // lblSerialResult
            // 
            this.lblSerialResult.AutoSize = true;
            this.lblSerialResult.Font = new System.Drawing.Font("Calibri", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblSerialResult.Location = new System.Drawing.Point(121, 157);
            this.lblSerialResult.Name = "lblSerialResult";
            this.lblSerialResult.Size = new System.Drawing.Size(13, 14);
            this.lblSerialResult.TabIndex = 21;
            this.lblSerialResult.Text = "0";
            // 
            // lblCountResult
            // 
            this.lblCountResult.AutoSize = true;
            this.lblCountResult.Font = new System.Drawing.Font("Calibri", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.lblCountResult.Location = new System.Drawing.Point(121, 175);
            this.lblCountResult.Name = "lblCountResult";
            this.lblCountResult.Size = new System.Drawing.Size(13, 14);
            this.lblCountResult.TabIndex = 23;
            this.lblCountResult.Text = "0";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Calibri", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.label5.Location = new System.Drawing.Point(28, 175);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(41, 14);
            this.label5.TabIndex = 22;
            this.label5.Text = "Count:";
            // 
            // SerialQueryForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(271, 209);
            this.Controls.Add(this.lblCountResult);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.lblSerialResult);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.grpRef);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "SerialQueryForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Serial Query Finder";
            this.TopMost = true;
            this.grpRef.ResumeLayout(false);
            this.grpRef.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.GroupBox grpRef;
        private System.Windows.Forms.TextBox txtRef;
        private System.Windows.Forms.Button btnRefOk;
        private System.Windows.Forms.Label lblRefRemarks;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lblSerialResult;
        private System.Windows.Forms.Label lblCountResult;
        private System.Windows.Forms.Label label5;
    }
}