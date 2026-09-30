namespace opentuner.ExtraFeatures.BATCSpectrum
{
    partial class BATCSpectrumSettingsForm
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
            this.labelThresholdUnit = new System.Windows.Forms.Label();
            this.thresholdValue = new System.Windows.Forms.NumericUpDown();
            this.avoidBeacon4 = new System.Windows.Forms.CheckBox();
            this.avoidBeacon3 = new System.Windows.Forms.CheckBox();
            this.labelThreshold = new System.Windows.Forms.Label();
            this.avoidBeacon2 = new System.Windows.Forms.CheckBox();
            this.avoidBeacon1 = new System.Windows.Forms.CheckBox();
            this.tuneMode4 = new System.Windows.Forms.ComboBox();
            this.tuneMode3 = new System.Windows.Forms.ComboBox();
            this.tuneMode2 = new System.Windows.Forms.ComboBox();
            this.tuneMode1 = new System.Windows.Forms.ComboBox();
            this.labelRx4 = new System.Windows.Forms.Label();
            this.labelRx3 = new System.Windows.Forms.Label();
            this.labelRx2 = new System.Windows.Forms.Label();
            this.labelRx1 = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.groupOverpowerLayout = new System.Windows.Forms.GroupBox();
            this.overPowerIndicatorLayout = new System.Windows.Forms.ComboBox();
            this.labelOverpowerLimit = new System.Windows.Forms.Label();
            this.overPowerLimitValue = new System.Windows.Forms.NumericUpDown();
            this.groupTimer = new System.Windows.Forms.GroupBox();
            this.labelTimedTime = new System.Windows.Forms.Label();
            this.labelHoldTime = new System.Windows.Forms.Label();
            this.labelTimedSeconds = new System.Windows.Forms.Label();
            this.autoTuneTimeValue = new System.Windows.Forms.NumericUpDown();
            this.autoHoldTimeValue = new System.Windows.Forms.NumericUpDown();
            this.labelHoldSeconds = new System.Windows.Forms.Label();
            this.groupTuningMode.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.thresholdValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.overPowerLimitValue)).BeginInit();
            this.groupOverpowerLayout.SuspendLayout();
            this.groupTimer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.autoTuneTimeValue)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.autoHoldTimeValue)).BeginInit();
            this.SuspendLayout();
            // 
            // groupTuningMode
            // 
            this.groupTuningMode.Controls.Add(this.labelThresholdUnit);
            this.groupTuningMode.Controls.Add(this.thresholdValue);
            this.groupTuningMode.Controls.Add(this.avoidBeacon4);
            this.groupTuningMode.Controls.Add(this.avoidBeacon3);
            this.groupTuningMode.Controls.Add(this.labelThreshold);
            this.groupTuningMode.Controls.Add(this.avoidBeacon2);
            this.groupTuningMode.Controls.Add(this.avoidBeacon1);
            this.groupTuningMode.Controls.Add(this.tuneMode4);
            this.groupTuningMode.Controls.Add(this.tuneMode3);
            this.groupTuningMode.Controls.Add(this.tuneMode2);
            this.groupTuningMode.Controls.Add(this.tuneMode1);
            this.groupTuningMode.Controls.Add(this.labelRx4);
            this.groupTuningMode.Controls.Add(this.labelRx3);
            this.groupTuningMode.Controls.Add(this.labelRx2);
            this.groupTuningMode.Controls.Add(this.labelRx1);
            this.groupTuningMode.Location = new System.Drawing.Point(15, 15);
            this.groupTuningMode.Name = "groupTuningMode";
            this.groupTuningMode.Size = new System.Drawing.Size(304, 181);
            this.groupTuningMode.TabIndex = 4;
            this.groupTuningMode.TabStop = false;
            this.groupTuningMode.Text = "Tuning Mode";
            // 
            // labelThresholdUnit
            // 
            this.labelThresholdUnit.AutoSize = true;
            this.labelThresholdUnit.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelThresholdUnit.Location = new System.Drawing.Point(175, 145);
            this.labelThresholdUnit.Name = "labelThresholdUnit";
            this.labelThresholdUnit.Size = new System.Drawing.Size(113, 16);
            this.labelThresholdUnit.TabIndex = 12;
            this.labelThresholdUnit.Text = "dB below Beacon";
            // 
            // thresholdValue
            // 
            this.thresholdValue.DecimalPlaces = 1;
            this.thresholdValue.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.thresholdValue.Location = new System.Drawing.Point(80, 145);
            this.thresholdValue.Maximum = new decimal(new int[] {
            0,
            0,
            0,
            65536});
            this.thresholdValue.Minimum = new decimal(new int[] {
            150,
            0,
            0,
            -2147418112});
            this.thresholdValue.Name = "thresholdValue";
            this.thresholdValue.Size = new System.Drawing.Size(89, 20);
            this.thresholdValue.TabIndex = 11;
            this.thresholdValue.Value = new decimal(new int[] {
            60,
            0,
            0,
            -2147418112});
            // 
            // avoidBeacon4
            // 
            this.avoidBeacon4.AutoSize = true;
            this.avoidBeacon4.Checked = true;
            this.avoidBeacon4.CheckState = System.Windows.Forms.CheckState.Checked;
            this.avoidBeacon4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.avoidBeacon4.Location = new System.Drawing.Point(177, 116);
            this.avoidBeacon4.Name = "avoidBeacon4";
            this.avoidBeacon4.Size = new System.Drawing.Size(111, 20);
            this.avoidBeacon4.TabIndex = 11;
            this.avoidBeacon4.Text = "Avoid Beacon";
            this.avoidBeacon4.UseVisualStyleBackColor = true;
            // 
            // avoidBeacon3
            // 
            this.avoidBeacon3.AutoSize = true;
            this.avoidBeacon3.Checked = true;
            this.avoidBeacon3.CheckState = System.Windows.Forms.CheckState.Checked;
            this.avoidBeacon3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.avoidBeacon3.Location = new System.Drawing.Point(177, 86);
            this.avoidBeacon3.Name = "avoidBeacon3";
            this.avoidBeacon3.Size = new System.Drawing.Size(111, 20);
            this.avoidBeacon3.TabIndex = 10;
            this.avoidBeacon3.Text = "Avoid Beacon";
            this.avoidBeacon3.UseVisualStyleBackColor = true;
            // 
            // labelThreshold
            // 
            this.labelThreshold.AutoSize = true;
            this.labelThreshold.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelThreshold.Location = new System.Drawing.Point(6, 145);
            this.labelThreshold.Name = "labelThreshold";
            this.labelThreshold.Size = new System.Drawing.Size(68, 16);
            this.labelThreshold.TabIndex = 10;
            this.labelThreshold.Text = "Threshold";
            // 
            // avoidBeacon2
            // 
            this.avoidBeacon2.AutoSize = true;
            this.avoidBeacon2.Checked = true;
            this.avoidBeacon2.CheckState = System.Windows.Forms.CheckState.Checked;
            this.avoidBeacon2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.avoidBeacon2.Location = new System.Drawing.Point(177, 56);
            this.avoidBeacon2.Name = "avoidBeacon2";
            this.avoidBeacon2.Size = new System.Drawing.Size(111, 20);
            this.avoidBeacon2.TabIndex = 9;
            this.avoidBeacon2.Text = "Avoid Beacon";
            this.avoidBeacon2.UseVisualStyleBackColor = true;
            // 
            // avoidBeacon1
            // 
            this.avoidBeacon1.AutoSize = true;
            this.avoidBeacon1.Checked = true;
            this.avoidBeacon1.CheckState = System.Windows.Forms.CheckState.Checked;
            this.avoidBeacon1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.avoidBeacon1.Location = new System.Drawing.Point(177, 26);
            this.avoidBeacon1.Name = "avoidBeacon1";
            this.avoidBeacon1.Size = new System.Drawing.Size(111, 20);
            this.avoidBeacon1.TabIndex = 8;
            this.avoidBeacon1.Text = "Avoid Beacon";
            this.avoidBeacon1.UseVisualStyleBackColor = true;
            // 
            // tuneMode4
            // 
            this.tuneMode4.FormattingEnabled = true;
            this.tuneMode4.Items.AddRange(new object[] {
            "Manual",
            "Auto (Hold)",
            "Auto (Next new)",
            "Auto (Timed)"});
            this.tuneMode4.Location = new System.Drawing.Point(50, 115);
            this.tuneMode4.Name = "tuneMode4";
            this.tuneMode4.Size = new System.Drawing.Size(121, 21);
            this.tuneMode4.TabIndex = 7;
            this.tuneMode4.SelectedIndexChanged += new System.EventHandler(this.tuneMode_SelectedIndexChanged);
            // 
            // tuneMode3
            // 
            this.tuneMode3.FormattingEnabled = true;
            this.tuneMode3.Items.AddRange(new object[] {
            "Manual",
            "Auto (Hold)",
            "Auto (Next new)",
            "Auto (Timed)"});
            this.tuneMode3.Location = new System.Drawing.Point(50, 85);
            this.tuneMode3.Name = "tuneMode3";
            this.tuneMode3.Size = new System.Drawing.Size(121, 21);
            this.tuneMode3.TabIndex = 6;
            this.tuneMode3.SelectedIndexChanged += new System.EventHandler(this.tuneMode_SelectedIndexChanged);
            // 
            // tuneMode2
            // 
            this.tuneMode2.FormattingEnabled = true;
            this.tuneMode2.Items.AddRange(new object[] {
            "Manual",
            "Auto (Hold)",
            "Auto (Next new)",
            "Auto (Timed)"});
            this.tuneMode2.Location = new System.Drawing.Point(50, 55);
            this.tuneMode2.Name = "tuneMode2";
            this.tuneMode2.Size = new System.Drawing.Size(121, 21);
            this.tuneMode2.TabIndex = 5;
            this.tuneMode2.SelectedIndexChanged += new System.EventHandler(this.tuneMode_SelectedIndexChanged);
            // 
            // tuneMode1
            // 
            this.tuneMode1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.tuneMode1.FormattingEnabled = true;
            this.tuneMode1.Items.AddRange(new object[] {
            "Manual",
            "Auto (Hold)",
            "Auto (Next new)",
            "Auto (Timed)"});
            this.tuneMode1.Location = new System.Drawing.Point(50, 25);
            this.tuneMode1.Name = "tuneMode1";
            this.tuneMode1.Size = new System.Drawing.Size(121, 21);
            this.tuneMode1.TabIndex = 4;
            this.tuneMode1.SelectedIndexChanged += new System.EventHandler(this.tuneMode_SelectedIndexChanged);
            // 
            // labelRx4
            // 
            this.labelRx4.AutoSize = true;
            this.labelRx4.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelRx4.Location = new System.Drawing.Point(6, 118);
            this.labelRx4.Name = "labelRx4";
            this.labelRx4.Size = new System.Drawing.Size(38, 16);
            this.labelRx4.TabIndex = 3;
            this.labelRx4.Text = "RX 4:";
            // 
            // labelRx3
            // 
            this.labelRx3.AutoSize = true;
            this.labelRx3.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelRx3.Location = new System.Drawing.Point(6, 88);
            this.labelRx3.Name = "labelRx3";
            this.labelRx3.Size = new System.Drawing.Size(38, 16);
            this.labelRx3.TabIndex = 2;
            this.labelRx3.Text = "RX 3:";
            // 
            // labelRx2
            // 
            this.labelRx2.AutoSize = true;
            this.labelRx2.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelRx2.Location = new System.Drawing.Point(6, 58);
            this.labelRx2.Name = "labelRx2";
            this.labelRx2.Size = new System.Drawing.Size(38, 16);
            this.labelRx2.TabIndex = 1;
            this.labelRx2.Text = "RX 2:";
            // 
            // labelRx1
            // 
            this.labelRx1.AutoSize = true;
            this.labelRx1.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelRx1.Location = new System.Drawing.Point(6, 28);
            this.labelRx1.Name = "labelRx1";
            this.labelRx1.Size = new System.Drawing.Size(38, 16);
            this.labelRx1.TabIndex = 0;
            this.labelRx1.Text = "RX 1:";
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(111, 365);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(219, 365);
            this.btnSave.Margin = new System.Windows.Forms.Padding(4);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 28);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // groupOverpowerLayout
            // 
            this.groupOverpowerLayout.Controls.Add(this.overPowerIndicatorLayout);
            this.groupOverpowerLayout.Controls.Add(this.labelOverpowerLimit);
            this.groupOverpowerLayout.Controls.Add(this.overPowerLimitValue);
            this.groupOverpowerLayout.Location = new System.Drawing.Point(15, 298);
            this.groupOverpowerLayout.Name = "groupOverpowerLayout";
            this.groupOverpowerLayout.Size = new System.Drawing.Size(304, 60);
            this.groupOverpowerLayout.TabIndex = 5;
            this.groupOverpowerLayout.TabStop = false;
            this.groupOverpowerLayout.Text = "Overpower Indicator (layout, marked above dBb)";
            // 
            // overPowerIndicatorLayout
            // 
            this.overPowerIndicatorLayout.FormattingEnabled = true;
            this.overPowerIndicatorLayout.Items.AddRange(new object[] {
            "classic",
            "classic + line",
            "box from top to line",
            "box from line to bottom",
            "line"});
            this.overPowerIndicatorLayout.Location = new System.Drawing.Point(9, 25);
            this.overPowerIndicatorLayout.Name = "overPowerIndicatorLayout";
            this.overPowerIndicatorLayout.Size = new System.Drawing.Size(162, 21);
            this.overPowerIndicatorLayout.TabIndex = 0;
            // 
            // labelOverpowerLimit
            // 
            this.labelOverpowerLimit.AutoSize = true;
            this.labelOverpowerLimit.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelOverpowerLimit.Location = new System.Drawing.Point(178, 26);
            this.labelOverpowerLimit.Name = "labelOverpowerLimit";
            this.labelOverpowerLimit.Size = new System.Drawing.Size(55, 16);
            this.labelOverpowerLimit.TabIndex = 2;
            this.labelOverpowerLimit.Text = "above";
            // 
            // overPowerLimitValue
            // 
            this.overPowerLimitValue.DecimalPlaces = 1;
            this.overPowerLimitValue.Increment = new decimal(new int[] {
            1,
            0,
            0,
            65536});
            this.overPowerLimitValue.Location = new System.Drawing.Point(226, 25);
            this.overPowerLimitValue.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            65536});
            this.overPowerLimitValue.Minimum = new decimal(new int[] {
            60,
            0,
            0,
            -2147418112});
            this.overPowerLimitValue.Name = "overPowerLimitValue";
            this.overPowerLimitValue.Size = new System.Drawing.Size(66, 20);
            this.overPowerLimitValue.TabIndex = 1;
            // 
            // groupTimer
            // 
            this.groupTimer.Controls.Add(this.labelTimedTime);
            this.groupTimer.Controls.Add(this.labelHoldTime);
            this.groupTimer.Controls.Add(this.labelTimedSeconds);
            this.groupTimer.Controls.Add(this.autoTuneTimeValue);
            this.groupTimer.Controls.Add(this.autoHoldTimeValue);
            this.groupTimer.Controls.Add(this.labelHoldSeconds);
            this.groupTimer.Location = new System.Drawing.Point(15, 202);
            this.groupTimer.Name = "groupTimer";
            this.groupTimer.Size = new System.Drawing.Size(304, 90);
            this.groupTimer.TabIndex = 6;
            this.groupTimer.TabStop = false;
            this.groupTimer.Text = "Timer";
            // 
            // labelTimedTime
            // 
            this.labelTimedTime.AutoSize = true;
            this.labelTimedTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelTimedTime.Location = new System.Drawing.Point(6, 55);
            this.labelTimedTime.Name = "labelTimedTime";
            this.labelTimedTime.Size = new System.Drawing.Size(46, 16);
            this.labelTimedTime.TabIndex = 9;
            this.labelTimedTime.Text = "Timed";
            // 
            // labelHoldTime
            // 
            this.labelHoldTime.AutoSize = true;
            this.labelHoldTime.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelHoldTime.Location = new System.Drawing.Point(8, 25);
            this.labelHoldTime.Name = "labelHoldTime";
            this.labelHoldTime.Size = new System.Drawing.Size(70, 16);
            this.labelHoldTime.TabIndex = 8;
            this.labelHoldTime.Text = "Hold Time";
            // 
            // labelTimedSeconds
            // 
            this.labelTimedSeconds.AutoSize = true;
            this.labelTimedSeconds.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelTimedSeconds.Location = new System.Drawing.Point(177, 55);
            this.labelTimedSeconds.Name = "labelTimedSeconds";
            this.labelTimedSeconds.Size = new System.Drawing.Size(61, 16);
            this.labelTimedSeconds.TabIndex = 7;
            this.labelTimedSeconds.Text = "Seconds";
            // 
            // autoTuneTimeValue
            // 
            this.autoTuneTimeValue.Location = new System.Drawing.Point(82, 55);
            this.autoTuneTimeValue.Maximum = new decimal(new int[] {
            600,
            0,
            0,
            0});
            this.autoTuneTimeValue.Minimum = new decimal(new int[] {
            5,
            0,
            0,
            0});
            this.autoTuneTimeValue.Name = "autoTuneTimeValue";
            this.autoTuneTimeValue.Size = new System.Drawing.Size(89, 20);
            this.autoTuneTimeValue.TabIndex = 6;
            this.autoTuneTimeValue.Value = new decimal(new int[] {
            30,
            0,
            0,
            0});
            // 
            // autoHoldTimeValue
            // 
            this.autoHoldTimeValue.Location = new System.Drawing.Point(82, 25);
            this.autoHoldTimeValue.Maximum = new decimal(new int[] {
            60,
            0,
            0,
            0});
            this.autoHoldTimeValue.Name = "autoHoldTimeValue";
            this.autoHoldTimeValue.Size = new System.Drawing.Size(89, 20);
            this.autoHoldTimeValue.TabIndex = 5;
            this.autoHoldTimeValue.Value = new decimal(new int[] {
            5,
            0,
            0,
            0});
            // 
            // labelHoldSeconds
            // 
            this.labelHoldSeconds.AutoSize = true;
            this.labelHoldSeconds.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F);
            this.labelHoldSeconds.Location = new System.Drawing.Point(177, 25);
            this.labelHoldSeconds.Name = "labelHoldSeconds";
            this.labelHoldSeconds.Size = new System.Drawing.Size(61, 16);
            this.labelHoldSeconds.TabIndex = 4;
            this.labelHoldSeconds.Text = "Seconds";
            // 
            // BATCSpectrumSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(335, 403);
            this.ControlBox = false;
            this.Controls.Add(this.groupTimer);
            this.Controls.Add(this.groupOverpowerLayout);
            this.Controls.Add(this.groupTuningMode);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "BATCSpectrumSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "BATC spectrum settings";
            this.groupTuningMode.ResumeLayout(false);
            this.groupTuningMode.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.thresholdValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.overPowerLimitValue)).EndInit();
            this.groupOverpowerLayout.ResumeLayout(false);
            this.groupTimer.ResumeLayout(false);
            this.groupTimer.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.autoTuneTimeValue)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.autoHoldTimeValue)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupTuningMode;
        private System.Windows.Forms.Label labelRx1;
        private System.Windows.Forms.Label labelRx2;
        private System.Windows.Forms.Label labelRx3;
        private System.Windows.Forms.Label labelRx4;
        private System.Windows.Forms.ComboBox tuneMode1;
        private System.Windows.Forms.ComboBox tuneMode2;
        private System.Windows.Forms.ComboBox tuneMode3;
        private System.Windows.Forms.ComboBox tuneMode4;
        private System.Windows.Forms.CheckBox avoidBeacon1;
        private System.Windows.Forms.CheckBox avoidBeacon2;
        private System.Windows.Forms.CheckBox avoidBeacon3;
        private System.Windows.Forms.CheckBox avoidBeacon4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.GroupBox groupOverpowerLayout;
        private System.Windows.Forms.ComboBox overPowerIndicatorLayout;
        private System.Windows.Forms.Label labelOverpowerLimit;
        private System.Windows.Forms.NumericUpDown overPowerLimitValue;
        private System.Windows.Forms.GroupBox groupTimer;
        private System.Windows.Forms.Label labelHoldSeconds;
        private System.Windows.Forms.NumericUpDown autoHoldTimeValue;
        private System.Windows.Forms.Label labelTimedSeconds;
        private System.Windows.Forms.NumericUpDown autoTuneTimeValue;
        private System.Windows.Forms.Label labelHoldTime;
        private System.Windows.Forms.Label labelTimedTime;
        private System.Windows.Forms.Label labelThreshold;
        private System.Windows.Forms.Label labelThresholdUnit;
        private System.Windows.Forms.NumericUpDown thresholdValue;
    }
}