namespace opentuner.MediaSources.Minitiouner
{
    partial class MinitiounerSettingsForm
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
            this.groupHardwareInterface = new System.Windows.Forms.GroupBox();
            this.txtIpAddress = new System.Windows.Forms.MaskedTextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.comboHardwareInterface = new System.Windows.Forms.ComboBox();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.labelDefaultRfInput = new System.Windows.Forms.Label();
            this.ComboDefaultRFInput = new System.Windows.Forms.ComboBox();
            this.comboSupplyBDefault = new System.Windows.Forms.ComboBox();
            this.comboSupplyADefault = new System.Windows.Forms.ComboBox();
            this.txtTuner2FreqOffset = new System.Windows.Forms.TextBox();
            this.txtTuner1FreqOffset = new System.Windows.Forms.TextBox();
            this.labelLnbBSupplyDefault = new System.Windows.Forms.Label();
            this.labelLnbASupplyDefault = new System.Windows.Forms.Label();
            this.labelTuner2FreqOffset = new System.Windows.Forms.Label();
            this.labelTuner1FreqOffset = new System.Windows.Forms.Label();
            this.labelTuner1Correction = new System.Windows.Forms.Label();
            this.labelTuner2Correction = new System.Windows.Forms.Label();
            this.txtTuner1FreqCorrection = new System.Windows.Forms.TextBox();
            this.txtTuner2FreqCorrection = new System.Windows.Forms.TextBox();
            this.btnSave = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.groupDigole = new System.Windows.Forms.GroupBox();
            this.txtDigoleAddress = new System.Windows.Forms.TextBox();
            this.labelDigoleAddress = new System.Windows.Forms.Label();
            this.checkEnableDigole = new System.Windows.Forms.CheckBox();
            this.checkLnbControl = new System.Windows.Forms.CheckBox();
            this.txtDigoleCallsign = new System.Windows.Forms.TextBox();
            this.labelDigoleCallsign = new System.Windows.Forms.Label();
            this.txtDigoleLocator = new System.Windows.Forms.TextBox();
            this.labelDigoleLocator = new System.Windows.Forms.Label();
            this.txtDigoleName = new System.Windows.Forms.TextBox();
            this.labelDigoleCallsignWidth = new System.Windows.Forms.Label();
            this.txtDigoleCallsignWidth = new System.Windows.Forms.TextBox();
            this.labelDigoleName = new System.Windows.Forms.Label();
            this.groupHardwareInterface.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupDigole.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupHardwareInterface
            // 
            this.groupHardwareInterface.Controls.Add(this.txtIpAddress);
            this.groupHardwareInterface.Controls.Add(this.label2);
            this.groupHardwareInterface.Controls.Add(this.comboHardwareInterface);
            this.groupHardwareInterface.Controls.Add(this.label1);
            this.groupHardwareInterface.Location = new System.Drawing.Point(16, 15);
            this.groupHardwareInterface.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupHardwareInterface.Name = "groupHardwareInterface";
            this.groupHardwareInterface.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupHardwareInterface.Size = new System.Drawing.Size(437, 129);
            this.groupHardwareInterface.TabIndex = 0;
            this.groupHardwareInterface.TabStop = false;
            this.groupHardwareInterface.Text = "Hardware Interface";
            //
            // checkLnbControl
            //
            this.checkLnbControl.AutoSize = true;
            this.checkLnbControl.Location = new System.Drawing.Point(21, 165);
            this.checkLnbControl.Name = "checkLnbControl";
            this.checkLnbControl.Size = new System.Drawing.Size(360, 20);
            this.checkLnbControl.TabIndex = 4;
            this.checkLnbControl.Text = "LNB voltage can be switched (untick for the E-Tiouner)";
            this.checkLnbControl.UseVisualStyleBackColor = true;
            // 
            // txtIpAddress
            // 
            this.txtIpAddress.Enabled = false;
            this.txtIpAddress.Location = new System.Drawing.Point(179, 76);
            this.txtIpAddress.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtIpAddress.Name = "txtIpAddress";
            this.txtIpAddress.Size = new System.Drawing.Size(212, 22);
            this.txtIpAddress.TabIndex = 3;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Enabled = false;
            this.label2.Location = new System.Drawing.Point(21, 80);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(79, 16);
            this.label2.TabIndex = 2;
            this.label2.Text = "IP Address: ";
            // 
            // comboHardwareInterface
            // 
            this.comboHardwareInterface.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboHardwareInterface.FormattingEnabled = true;
            this.comboHardwareInterface.Items.AddRange(new object[] {
            "Always Ask",
            "FTDI Module",
            "PicoTuner"});
            this.comboHardwareInterface.Location = new System.Drawing.Point(179, 30);
            this.comboHardwareInterface.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.comboHardwareInterface.Name = "comboHardwareInterface";
            this.comboHardwareInterface.Size = new System.Drawing.Size(212, 24);
            this.comboHardwareInterface.TabIndex = 1;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(21, 33);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(109, 16);
            this.label1.TabIndex = 0;
            this.label1.Text = "Default Interface: ";
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.checkLnbControl);
            this.groupBox1.Controls.Add(this.labelDefaultRfInput);
            this.groupBox1.Controls.Add(this.ComboDefaultRFInput);
            this.groupBox1.Controls.Add(this.comboSupplyBDefault);
            this.groupBox1.Controls.Add(this.comboSupplyADefault);
            this.groupBox1.Controls.Add(this.txtTuner2FreqOffset);
            this.groupBox1.Controls.Add(this.txtTuner1FreqOffset);
            this.groupBox1.Controls.Add(this.labelLnbBSupplyDefault);
            this.groupBox1.Controls.Add(this.labelLnbASupplyDefault);
            this.groupBox1.Controls.Add(this.labelTuner2FreqOffset);
            this.groupBox1.Controls.Add(this.labelTuner1FreqOffset);
            this.groupBox1.Controls.Add(this.labelTuner1Correction);
            this.groupBox1.Controls.Add(this.labelTuner2Correction);
            this.groupBox1.Controls.Add(this.txtTuner1FreqCorrection);
            this.groupBox1.Controls.Add(this.txtTuner2FreqCorrection);
            this.groupBox1.Location = new System.Drawing.Point(16, 151);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox1.Size = new System.Drawing.Size(437, 307);
            this.groupBox1.TabIndex = 1;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Tuner Properties";
            // 
            // labelDefaultRfInput
            // 
            this.labelDefaultRfInput.AutoSize = true;
            this.labelDefaultRfInput.Location = new System.Drawing.Point(21, 265);
            this.labelDefaultRfInput.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDefaultRfInput.Name = "labelDefaultRfInput";
            this.labelDefaultRfInput.Size = new System.Drawing.Size(104, 16);
            this.labelDefaultRfInput.TabIndex = 9;
            this.labelDefaultRfInput.Text = "Default RF Input:";
            // 
            // ComboDefaultRFInput
            // 
            this.ComboDefaultRFInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ComboDefaultRFInput.FormattingEnabled = true;
            this.ComboDefaultRFInput.Items.AddRange(new object[] {
            "Tuner 1 = A, Tuner 2 = A",
            "Tuner 1 = A, Tuner 2 = B",
            "Tuner 1 = B, Tuner 2 = A",
            "Tuner 1 = B, Tuner 2 = B"});
            this.ComboDefaultRFInput.Location = new System.Drawing.Point(179, 261);
            this.ComboDefaultRFInput.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.ComboDefaultRFInput.Name = "ComboDefaultRFInput";
            this.ComboDefaultRFInput.Size = new System.Drawing.Size(212, 24);
            this.ComboDefaultRFInput.TabIndex = 8;
            // 
            // comboSupplyBDefault
            // 
            this.comboSupplyBDefault.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSupplyBDefault.FormattingEnabled = true;
            this.comboSupplyBDefault.Items.AddRange(new object[] {
            "Off",
            "13V Vertical",
            "18V Horizontal"});
            this.comboSupplyBDefault.Location = new System.Drawing.Point(179, 228);
            this.comboSupplyBDefault.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.comboSupplyBDefault.Name = "comboSupplyBDefault";
            this.comboSupplyBDefault.Size = new System.Drawing.Size(212, 24);
            this.comboSupplyBDefault.TabIndex = 7;
            // 
            // comboSupplyADefault
            // 
            this.comboSupplyADefault.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboSupplyADefault.FormattingEnabled = true;
            this.comboSupplyADefault.Items.AddRange(new object[] {
            "Off",
            "13V Vertical",
            "18V Horizontal"});
            this.comboSupplyADefault.Location = new System.Drawing.Point(179, 195);
            this.comboSupplyADefault.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.comboSupplyADefault.Name = "comboSupplyADefault";
            this.comboSupplyADefault.Size = new System.Drawing.Size(212, 24);
            this.comboSupplyADefault.TabIndex = 6;
            // 
            // txtTuner2FreqOffset
            // 
            this.txtTuner2FreqOffset.Location = new System.Drawing.Point(179, 69);
            this.txtTuner2FreqOffset.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtTuner2FreqOffset.Name = "txtTuner2FreqOffset";
            this.txtTuner2FreqOffset.Size = new System.Drawing.Size(212, 22);
            this.txtTuner2FreqOffset.TabIndex = 5;
            // 
            // txtTuner1FreqOffset
            // 
            this.txtTuner1FreqOffset.Location = new System.Drawing.Point(179, 37);
            this.txtTuner1FreqOffset.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtTuner1FreqOffset.Name = "txtTuner1FreqOffset";
            this.txtTuner1FreqOffset.Size = new System.Drawing.Size(212, 22);
            this.txtTuner1FreqOffset.TabIndex = 4;
            // 
            // labelLnbBSupplyDefault
            // 
            this.labelLnbBSupplyDefault.AutoSize = true;
            this.labelLnbBSupplyDefault.Location = new System.Drawing.Point(21, 232);
            this.labelLnbBSupplyDefault.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelLnbBSupplyDefault.Name = "labelLnbBSupplyDefault";
            this.labelLnbBSupplyDefault.Size = new System.Drawing.Size(138, 16);
            this.labelLnbBSupplyDefault.TabIndex = 3;
            this.labelLnbBSupplyDefault.Text = "LNB B Supply Default:";
            // 
            // labelLnbASupplyDefault
            // 
            this.labelLnbASupplyDefault.AutoSize = true;
            this.labelLnbASupplyDefault.Location = new System.Drawing.Point(21, 199);
            this.labelLnbASupplyDefault.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelLnbASupplyDefault.Name = "labelLnbASupplyDefault";
            this.labelLnbASupplyDefault.Size = new System.Drawing.Size(138, 16);
            this.labelLnbASupplyDefault.TabIndex = 2;
            this.labelLnbASupplyDefault.Text = "LNB A Supply Default:";
            // 
            // labelTuner2FreqOffset
            // 
            this.labelTuner2FreqOffset.AutoSize = true;
            this.labelTuner2FreqOffset.Location = new System.Drawing.Point(21, 73);
            this.labelTuner2FreqOffset.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTuner2FreqOffset.Name = "labelTuner2FreqOffset";
            this.labelTuner2FreqOffset.Size = new System.Drawing.Size(123, 16);
            this.labelTuner2FreqOffset.TabIndex = 1;
            this.labelTuner2FreqOffset.Text = "Tuner 2 Freq Offset:";
            // 
            // labelTuner1FreqOffset
            // 
            this.labelTuner1FreqOffset.AutoSize = true;
            this.labelTuner1FreqOffset.Location = new System.Drawing.Point(21, 41);
            this.labelTuner1FreqOffset.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTuner1FreqOffset.Name = "labelTuner1FreqOffset";
            this.labelTuner1FreqOffset.Size = new System.Drawing.Size(123, 16);
            this.labelTuner1FreqOffset.TabIndex = 0;
            this.labelTuner1FreqOffset.Text = "Tuner 1 Freq Offset:";
            // 
            // labelTuner1Correction
            // 
            this.labelTuner1Correction.AutoSize = true;
            this.labelTuner1Correction.Location = new System.Drawing.Point(21, 105);
            this.labelTuner1Correction.Name = "labelTuner1Correction";
            this.labelTuner1Correction.Size = new System.Drawing.Size(150, 16);
            this.labelTuner1Correction.TabIndex = 10;
            this.labelTuner1Correction.Text = "Tuner 1 Correction (ppm):";
            // 
            // txtTuner1FreqCorrection
            // 
            this.txtTuner1FreqCorrection.Location = new System.Drawing.Point(179, 101);
            this.txtTuner1FreqCorrection.Name = "txtTuner1FreqCorrection";
            this.txtTuner1FreqCorrection.Size = new System.Drawing.Size(212, 22);
            this.txtTuner1FreqCorrection.TabIndex = 11;
            // 
            // labelTuner2Correction
            // 
            this.labelTuner2Correction.AutoSize = true;
            this.labelTuner2Correction.Location = new System.Drawing.Point(21, 137);
            this.labelTuner2Correction.Name = "labelTuner2Correction";
            this.labelTuner2Correction.Size = new System.Drawing.Size(150, 16);
            this.labelTuner2Correction.TabIndex = 12;
            this.labelTuner2Correction.Text = "Tuner 2 Correction (ppm):";
            // 
            // txtTuner2FreqCorrection
            // 
            this.txtTuner2FreqCorrection.Location = new System.Drawing.Point(179, 133);
            this.txtTuner2FreqCorrection.Name = "txtTuner2FreqCorrection";
            this.txtTuner2FreqCorrection.Size = new System.Drawing.Size(212, 22);
            this.txtTuner2FreqCorrection.TabIndex = 13;
            // 
            // btnSave
            //
            this.btnSave.Location = new System.Drawing.Point(353, 707);
            this.btnSave.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 28);
            this.btnSave.TabIndex = 2;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            //
            // btnCancel
            //
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(245, 707);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
            this.btnCancel.TabIndex = 3;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // groupDigole
            //
            this.groupDigole.Controls.Add(this.txtDigoleCallsignWidth);
            this.groupDigole.Controls.Add(this.labelDigoleCallsignWidth);
            this.groupDigole.Controls.Add(this.txtDigoleName);
            this.groupDigole.Controls.Add(this.labelDigoleName);
            this.groupDigole.Controls.Add(this.txtDigoleLocator);
            this.groupDigole.Controls.Add(this.labelDigoleLocator);
            this.groupDigole.Controls.Add(this.txtDigoleCallsign);
            this.groupDigole.Controls.Add(this.labelDigoleCallsign);
            this.groupDigole.Controls.Add(this.txtDigoleAddress);
            this.groupDigole.Controls.Add(this.labelDigoleAddress);
            this.groupDigole.Controls.Add(this.checkEnableDigole);
            this.groupDigole.Location = new System.Drawing.Point(16, 466);
            this.groupDigole.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupDigole.Name = "groupDigole";
            this.groupDigole.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupDigole.Size = new System.Drawing.Size(437, 233);
            this.groupDigole.TabIndex = 4;
            this.groupDigole.TabStop = false;
            this.groupDigole.Text = "Digole I2C Display (JP3 / I2C-NIM)";
            //
            // checkEnableDigole
            //
            this.checkEnableDigole.AutoSize = true;
            this.checkEnableDigole.Location = new System.Drawing.Point(21, 30);
            this.checkEnableDigole.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.checkEnableDigole.Name = "checkEnableDigole";
            this.checkEnableDigole.Size = new System.Drawing.Size(150, 20);
            this.checkEnableDigole.TabIndex = 0;
            this.checkEnableDigole.Text = "Enable Digole Display";
            this.checkEnableDigole.UseVisualStyleBackColor = true;
            //
            // labelDigoleAddress
            //
            this.labelDigoleAddress.AutoSize = true;
            this.labelDigoleAddress.Location = new System.Drawing.Point(21, 65);
            this.labelDigoleAddress.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDigoleAddress.Name = "labelDigoleAddress";
            this.labelDigoleAddress.Size = new System.Drawing.Size(120, 16);
            this.labelDigoleAddress.TabIndex = 1;
            this.labelDigoleAddress.Text = "I2C Address (hex):";
            //
            // txtDigoleAddress
            //
            this.txtDigoleAddress.Location = new System.Drawing.Point(179, 61);
            this.txtDigoleAddress.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtDigoleAddress.Name = "txtDigoleAddress";
            this.txtDigoleAddress.Size = new System.Drawing.Size(80, 22);
            this.txtDigoleAddress.TabIndex = 2;
            //
            // labelDigoleCallsign
            //
            this.labelDigoleCallsign.AutoSize = true;
            this.labelDigoleCallsign.Location = new System.Drawing.Point(21, 99);
            this.labelDigoleCallsign.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDigoleCallsign.Name = "labelDigoleCallsign";
            this.labelDigoleCallsign.Size = new System.Drawing.Size(120, 16);
            this.labelDigoleCallsign.TabIndex = 3;
            this.labelDigoleCallsign.Text = "Callsign:";
            //
            // txtDigoleCallsign
            //
            this.txtDigoleCallsign.Location = new System.Drawing.Point(179, 95);
            this.txtDigoleCallsign.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtDigoleCallsign.Name = "txtDigoleCallsign";
            this.txtDigoleCallsign.Size = new System.Drawing.Size(212, 22);
            this.txtDigoleCallsign.TabIndex = 4;
            //
            // labelDigoleLocator
            //
            this.labelDigoleLocator.AutoSize = true;
            this.labelDigoleLocator.Location = new System.Drawing.Point(21, 133);
            this.labelDigoleLocator.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDigoleLocator.Name = "labelDigoleLocator";
            this.labelDigoleLocator.Size = new System.Drawing.Size(120, 16);
            this.labelDigoleLocator.TabIndex = 5;
            this.labelDigoleLocator.Text = "Locator:";
            //
            // txtDigoleLocator
            //
            this.txtDigoleLocator.Location = new System.Drawing.Point(179, 129);
            this.txtDigoleLocator.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtDigoleLocator.Name = "txtDigoleLocator";
            this.txtDigoleLocator.Size = new System.Drawing.Size(212, 22);
            this.txtDigoleLocator.TabIndex = 6;
            //
            // labelDigoleName
            //
            this.labelDigoleName.AutoSize = true;
            this.labelDigoleName.Location = new System.Drawing.Point(21, 167);
            this.labelDigoleName.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDigoleName.Name = "labelDigoleName";
            this.labelDigoleName.Size = new System.Drawing.Size(120, 16);
            this.labelDigoleName.TabIndex = 7;
            this.labelDigoleName.Text = "Name:";
            //
            // txtDigoleName
            //
            this.txtDigoleName.Location = new System.Drawing.Point(179, 163);
            this.txtDigoleName.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtDigoleName.Name = "txtDigoleName";
            this.txtDigoleName.Size = new System.Drawing.Size(212, 22);
            this.txtDigoleName.TabIndex = 8;
            //
            // labelDigoleCallsignWidth
            //
            this.labelDigoleCallsignWidth.AutoSize = true;
            this.labelDigoleCallsignWidth.Location = new System.Drawing.Point(21, 200);
            this.labelDigoleCallsignWidth.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelDigoleCallsignWidth.Name = "labelDigoleCallsignWidth";
            this.labelDigoleCallsignWidth.Size = new System.Drawing.Size(150, 16);
            this.labelDigoleCallsignWidth.TabIndex = 9;
            this.labelDigoleCallsignWidth.Text = "Callsign char width (px):";
            //
            // txtDigoleCallsignWidth
            //
            this.txtDigoleCallsignWidth.Location = new System.Drawing.Point(179, 196);
            this.txtDigoleCallsignWidth.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtDigoleCallsignWidth.Name = "txtDigoleCallsignWidth";
            this.txtDigoleCallsignWidth.Size = new System.Drawing.Size(60, 22);
            this.txtDigoleCallsignWidth.TabIndex = 10;
            //
            // MinitiounerSettingsForm
            //
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(473, 747);
            this.ControlBox = false;
            this.Controls.Add(this.groupDigole);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupHardwareInterface);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "MinitiounerSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Minitiouner Settings";
            this.groupHardwareInterface.ResumeLayout(false);
            this.groupHardwareInterface.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupDigole.ResumeLayout(false);
            this.groupDigole.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupDigole;
        private System.Windows.Forms.TextBox txtDigoleAddress;
        private System.Windows.Forms.Label labelDigoleAddress;
        private System.Windows.Forms.CheckBox checkEnableDigole;
        private System.Windows.Forms.CheckBox checkLnbControl;
        private System.Windows.Forms.TextBox txtDigoleCallsign;
        private System.Windows.Forms.Label labelDigoleCallsign;
        private System.Windows.Forms.TextBox txtDigoleLocator;
        private System.Windows.Forms.Label labelDigoleLocator;
        private System.Windows.Forms.TextBox txtDigoleName;
        private System.Windows.Forms.Label labelDigoleCallsignWidth;
        private System.Windows.Forms.TextBox txtDigoleCallsignWidth;
        private System.Windows.Forms.Label labelDigoleName;

        private System.Windows.Forms.GroupBox groupHardwareInterface;
        private System.Windows.Forms.MaskedTextBox txtIpAddress;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ComboBox comboHardwareInterface;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label labelDefaultRfInput;
        private System.Windows.Forms.ComboBox ComboDefaultRFInput;
        private System.Windows.Forms.ComboBox comboSupplyBDefault;
        private System.Windows.Forms.ComboBox comboSupplyADefault;
        private System.Windows.Forms.TextBox txtTuner2FreqOffset;
        private System.Windows.Forms.TextBox txtTuner1FreqOffset;
        private System.Windows.Forms.Label labelLnbBSupplyDefault;
        private System.Windows.Forms.Label labelLnbASupplyDefault;
        private System.Windows.Forms.Label labelTuner2FreqOffset;
        private System.Windows.Forms.Label labelTuner1FreqOffset;
        private System.Windows.Forms.Label labelTuner1Correction;
        private System.Windows.Forms.Label labelTuner2Correction;
        private System.Windows.Forms.TextBox txtTuner1FreqCorrection;
        private System.Windows.Forms.TextBox txtTuner2FreqCorrection;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.Button btnCancel;
    }
}