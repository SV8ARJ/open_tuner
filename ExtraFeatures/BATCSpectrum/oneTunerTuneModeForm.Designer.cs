namespace opentuner.ExtraFeatures.BATCSpectrum
{
    partial class oneTunerTuneModeForm
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
            this.groupTuningMode = new System.Windows.Forms.GroupBox();
            this.radioAutoTimed = new System.Windows.Forms.RadioButton();
            this.radioAutoNextNew = new System.Windows.Forms.RadioButton();
            this.radioAutoHold = new System.Windows.Forms.RadioButton();
            this.radioManual = new System.Windows.Forms.RadioButton();
            this.checkAvoidBeacon = new System.Windows.Forms.CheckBox();
            this.labelTuner = new System.Windows.Forms.Label();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupTuningMode.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupTuningMode
            // 
            this.groupTuningMode.Controls.Add(this.radioAutoTimed);
            this.groupTuningMode.Controls.Add(this.radioAutoNextNew);
            this.groupTuningMode.Controls.Add(this.radioAutoHold);
            this.groupTuningMode.Controls.Add(this.radioManual);
            this.groupTuningMode.Controls.Add(this.checkAvoidBeacon);
            this.groupTuningMode.Controls.Add(this.labelTuner);
            this.groupTuningMode.Location = new System.Drawing.Point(15, 11);
            this.groupTuningMode.Name = "groupTuningMode";
            this.groupTuningMode.Size = new System.Drawing.Size(269, 120);
            this.groupTuningMode.TabIndex = 5;
            this.groupTuningMode.TabStop = false;
            this.groupTuningMode.Text = "Tuning Mode";
            // 
            // radioAutoTimed
            // 
            this.radioAutoTimed.AutoSize = true;
            this.radioAutoTimed.Location = new System.Drawing.Point(50, 91);
            this.radioAutoTimed.Name = "radioAutoTimed";
            this.radioAutoTimed.Size = new System.Drawing.Size(85, 17);
            this.radioAutoTimed.TabIndex = 12;
            this.radioAutoTimed.TabStop = true;
            this.radioAutoTimed.Text = "Auto (Timed)";
            this.radioAutoTimed.UseVisualStyleBackColor = true;
            this.radioAutoTimed.CheckedChanged += new System.EventHandler(this.radioMode_CheckedChanged);
            // 
            // radioAutoNextNew
            // 
            this.radioAutoNextNew.AutoSize = true;
            this.radioAutoNextNew.Location = new System.Drawing.Point(50, 68);
            this.radioAutoNextNew.Name = "radioAutoNextNew";
            this.radioAutoNextNew.Size = new System.Drawing.Size(101, 17);
            this.radioAutoNextNew.TabIndex = 11;
            this.radioAutoNextNew.TabStop = true;
            this.radioAutoNextNew.Text = "Auto (Next new)";
            this.radioAutoNextNew.UseVisualStyleBackColor = true;
            this.radioAutoNextNew.CheckedChanged += new System.EventHandler(this.radioMode_CheckedChanged);
            // 
            // radioAutoHold
            // 
            this.radioAutoHold.AutoSize = true;
            this.radioAutoHold.Location = new System.Drawing.Point(50, 45);
            this.radioAutoHold.Name = "radioAutoHold";
            this.radioAutoHold.Size = new System.Drawing.Size(78, 17);
            this.radioAutoHold.TabIndex = 10;
            this.radioAutoHold.TabStop = true;
            this.radioAutoHold.Text = "Auto (Hold)";
            this.radioAutoHold.UseVisualStyleBackColor = true;
            this.radioAutoHold.CheckedChanged += new System.EventHandler(this.radioMode_CheckedChanged);
            // 
            // radioManual
            // 
            this.radioManual.AutoSize = true;
            this.radioManual.Location = new System.Drawing.Point(50, 22);
            this.radioManual.Name = "radioManual";
            this.radioManual.Size = new System.Drawing.Size(60, 17);
            this.radioManual.TabIndex = 9;
            this.radioManual.TabStop = true;
            this.radioManual.Text = "Manual";
            this.radioManual.UseVisualStyleBackColor = true;
            this.radioManual.CheckedChanged += new System.EventHandler(this.radioMode_CheckedChanged);
            // 
            // checkAvoidBeacon
            // 
            this.checkAvoidBeacon.AutoSize = true;
            this.checkAvoidBeacon.Checked = true;
            this.checkAvoidBeacon.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkAvoidBeacon.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.checkAvoidBeacon.Location = new System.Drawing.Point(157, 56);
            this.checkAvoidBeacon.Name = "checkAvoidBeacon";
            this.checkAvoidBeacon.Size = new System.Drawing.Size(111, 20);
            this.checkAvoidBeacon.TabIndex = 8;
            this.checkAvoidBeacon.Text = "Avoid Beacon";
            this.checkAvoidBeacon.UseVisualStyleBackColor = true;
            // 
            // labelTuner
            // 
            this.labelTuner.AutoSize = true;
            this.labelTuner.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelTuner.Location = new System.Drawing.Point(6, 57);
            this.labelTuner.Name = "labelTuner";
            this.labelTuner.Size = new System.Drawing.Size(38, 16);
            this.labelTuner.TabIndex = 0;
            this.labelTuner.Text = "RX 1:";
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(184, 138);
            this.btnSave.Margin = new System.Windows.Forms.Padding(4);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 28);
            this.btnSave.TabIndex = 7;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(76, 138);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // oneTunerTuneModeForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(298, 178);
            this.ControlBox = false;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.groupTuningMode);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "oneTunerTuneModeForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.groupTuningMode.ResumeLayout(false);
            this.groupTuningMode.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupTuningMode;
        private System.Windows.Forms.Label labelTuner;
        private System.Windows.Forms.CheckBox checkAvoidBeacon;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.RadioButton radioAutoTimed;
        private System.Windows.Forms.RadioButton radioAutoNextNew;
        private System.Windows.Forms.RadioButton radioAutoHold;
        private System.Windows.Forms.RadioButton radioManual;
    }
}