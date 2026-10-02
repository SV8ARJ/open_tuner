namespace opentuner.MediaSources.Longmynd
{
    partial class LongmyndSettingsForm
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
            this.groupTunerProperties = new System.Windows.Forms.GroupBox();
            this.txtTuner1FreqOffset = new System.Windows.Forms.TextBox();
            this.labelTuner1FreqOffset = new System.Windows.Forms.Label();
            this.groupHardwareInterface = new System.Windows.Forms.GroupBox();
            this.groupTsInput = new System.Windows.Forms.GroupBox();
            this.labelTsAddress = new System.Windows.Forms.Label();
            this.txtTsAddress = new System.Windows.Forms.TextBox();
            this.labelTsAddressHint = new System.Windows.Forms.Label();
            this.checkTestMode = new System.Windows.Forms.CheckBox();
            this.labelTsPort = new System.Windows.Forms.Label();
            this.txtTSPort = new System.Windows.Forms.TextBox();
            this.txtBaseCmdTopic = new System.Windows.Forms.TextBox();
            this.txtMqttPort = new System.Windows.Forms.TextBox();
            this.txtWSPort = new System.Windows.Forms.TextBox();
            this.labelBaseCmdTopic = new System.Windows.Forms.Label();
            this.labelMqttPort = new System.Windows.Forms.Label();
            this.txtMqttIpAddress = new System.Windows.Forms.MaskedTextBox();
            this.labelMqttIpAddress = new System.Windows.Forms.Label();
            this.labelWSPort = new System.Windows.Forms.Label();
            this.txtWSIpAddress = new System.Windows.Forms.MaskedTextBox();
            this.labelWSIpAddress = new System.Windows.Forms.Label();
            this.comboHardwareInterface = new System.Windows.Forms.ComboBox();
            this.labelControlInterface = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnSave = new System.Windows.Forms.Button();
            this.groupTunerProperties.SuspendLayout();
            this.groupHardwareInterface.SuspendLayout();
            this.groupTsInput.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupTunerProperties
            // 
            this.groupTunerProperties.Controls.Add(this.txtTuner1FreqOffset);
            this.groupTunerProperties.Controls.Add(this.labelTuner1FreqOffset);
            this.groupTunerProperties.Location = new System.Drawing.Point(16, 507);
            this.groupTunerProperties.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupTunerProperties.Name = "groupTunerProperties";
            this.groupTunerProperties.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupTunerProperties.Size = new System.Drawing.Size(437, 95);
            this.groupTunerProperties.TabIndex = 3;
            this.groupTunerProperties.TabStop = false;
            this.groupTunerProperties.Text = "Tuner Properties";
            // 
            // txtTuner1FreqOffset
            // 
            this.txtTuner1FreqOffset.Location = new System.Drawing.Point(199, 37);
            this.txtTuner1FreqOffset.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtTuner1FreqOffset.Name = "txtTuner1FreqOffset";
            this.txtTuner1FreqOffset.Size = new System.Drawing.Size(212, 22);
            this.txtTuner1FreqOffset.TabIndex = 4;
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
            // groupHardwareInterface
            // 
            this.groupHardwareInterface.Controls.Add(this.txtBaseCmdTopic);
            this.groupHardwareInterface.Controls.Add(this.txtMqttPort);
            this.groupHardwareInterface.Controls.Add(this.txtWSPort);
            this.groupHardwareInterface.Controls.Add(this.labelBaseCmdTopic);
            this.groupHardwareInterface.Controls.Add(this.labelMqttPort);
            this.groupHardwareInterface.Controls.Add(this.txtMqttIpAddress);
            this.groupHardwareInterface.Controls.Add(this.labelMqttIpAddress);
            this.groupHardwareInterface.Controls.Add(this.labelWSPort);
            this.groupHardwareInterface.Controls.Add(this.txtWSIpAddress);
            this.groupHardwareInterface.Controls.Add(this.labelWSIpAddress);
            this.groupHardwareInterface.Controls.Add(this.comboHardwareInterface);
            this.groupHardwareInterface.Controls.Add(this.labelControlInterface);
            this.groupHardwareInterface.Location = new System.Drawing.Point(16, 15);
            this.groupHardwareInterface.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupHardwareInterface.Name = "groupHardwareInterface";
            this.groupHardwareInterface.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupHardwareInterface.Size = new System.Drawing.Size(437, 318);
            this.groupHardwareInterface.TabIndex = 2;
            this.groupHardwareInterface.TabStop = false;
            this.groupHardwareInterface.Text = "Longmynd Control";
            // 
            // groupTsInput
            // 
            this.groupTsInput.Controls.Add(this.labelTsAddress);
            this.groupTsInput.Controls.Add(this.txtTsAddress);
            this.groupTsInput.Controls.Add(this.labelTsAddressHint);
            this.groupTsInput.Controls.Add(this.labelTsPort);
            this.groupTsInput.Controls.Add(this.txtTSPort);
            this.groupTsInput.Controls.Add(this.checkTestMode);
            this.groupTsInput.Location = new System.Drawing.Point(16, 340);
            this.groupTsInput.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupTsInput.Name = "groupTsInput";
            this.groupTsInput.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupTsInput.Size = new System.Drawing.Size(437, 160);
            this.groupTsInput.TabIndex = 6;
            this.groupTsInput.TabStop = false;
            this.groupTsInput.Text = "TS Input";
            // 
            // labelTsAddress
            // 
            this.labelTsAddress.AutoSize = true;
            this.labelTsAddress.Location = new System.Drawing.Point(23, 33);
            this.labelTsAddress.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTsAddress.Name = "labelTsAddress";
            this.labelTsAddress.Size = new System.Drawing.Size(80, 16);
            this.labelTsAddress.TabIndex = 0;
            this.labelTsAddress.Text = "TS Address:";
            // 
            // txtTsAddress
            // 
            this.txtTsAddress.Location = new System.Drawing.Point(199, 30);
            this.txtTsAddress.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtTsAddress.Name = "txtTsAddress";
            this.txtTsAddress.Size = new System.Drawing.Size(212, 22);
            this.txtTsAddress.TabIndex = 1;
            // 
            // labelTsAddressHint
            // 
            this.labelTsAddressHint.AutoSize = true;
            this.labelTsAddressHint.Location = new System.Drawing.Point(199, 57);
            this.labelTsAddressHint.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTsAddressHint.Name = "labelTsAddressHint";
            this.labelTsAddressHint.Size = new System.Drawing.Size(200, 16);
            this.labelTsAddressHint.TabIndex = 2;
            this.labelTsAddressHint.Text = "empty = Auto, 224-239.x.x.x = multicast";
            // 
            // checkTestMode
            // 
            this.checkTestMode.AutoSize = true;
            this.checkTestMode.Location = new System.Drawing.Point(26, 124);
            this.checkTestMode.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.checkTestMode.Name = "checkTestMode";
            this.checkTestMode.Size = new System.Drawing.Size(300, 20);
            this.checkTestMode.TabIndex = 4;
            this.checkTestMode.Text = "Test mode: only receive the TS, no Longmynd control";
            this.checkTestMode.UseVisualStyleBackColor = true;
            this.checkTestMode.CheckedChanged += new System.EventHandler(this.checkTestMode_CheckedChanged);
            // 
            // labelTsPort
            // 
            this.labelTsPort.AutoSize = true;
            this.labelTsPort.Location = new System.Drawing.Point(23, 89);
            this.labelTsPort.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelTsPort.Name = "labelTsPort";
            this.labelTsPort.Size = new System.Drawing.Size(55, 16);
            this.labelTsPort.TabIndex = 15;
            this.labelTsPort.Text = "TS Port:";
            // 
            // txtTSPort
            // 
            this.txtTSPort.Location = new System.Drawing.Point(199, 86);
            this.txtTSPort.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtTSPort.Name = "txtTSPort";
            this.txtTSPort.Size = new System.Drawing.Size(129, 22);
            this.txtTSPort.TabIndex = 14;
            // 
            // txtBaseCmdTopic
            // 
            this.txtBaseCmdTopic.Location = new System.Drawing.Point(200, 246);
            this.txtBaseCmdTopic.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtBaseCmdTopic.Name = "txtBaseCmdTopic";
            this.txtBaseCmdTopic.Size = new System.Drawing.Size(212, 22);
            this.txtBaseCmdTopic.TabIndex = 13;
            // 
            // txtMqttPort
            // 
            this.txtMqttPort.Location = new System.Drawing.Point(200, 190);
            this.txtMqttPort.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtMqttPort.Name = "txtMqttPort";
            this.txtMqttPort.Size = new System.Drawing.Size(212, 22);
            this.txtMqttPort.TabIndex = 12;
            // 
            // txtWSPort
            // 
            this.txtWSPort.Location = new System.Drawing.Point(200, 111);
            this.txtWSPort.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtWSPort.Name = "txtWSPort";
            this.txtWSPort.Size = new System.Drawing.Size(212, 22);
            this.txtWSPort.TabIndex = 11;
            // 
            // labelBaseCmdTopic
            // 
            this.labelBaseCmdTopic.AutoSize = true;
            this.labelBaseCmdTopic.Location = new System.Drawing.Point(23, 249);
            this.labelBaseCmdTopic.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelBaseCmdTopic.Name = "labelBaseCmdTopic";
            this.labelBaseCmdTopic.Size = new System.Drawing.Size(155, 16);
            this.labelBaseCmdTopic.TabIndex = 10;
            this.labelBaseCmdTopic.Text = "MQTT Base CMD Topic:";
            // 
            // labelMqttPort
            // 
            this.labelMqttPort.AutoSize = true;
            this.labelMqttPort.Location = new System.Drawing.Point(23, 194);
            this.labelMqttPort.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelMqttPort.Name = "labelMqttPort";
            this.labelMqttPort.Size = new System.Drawing.Size(84, 16);
            this.labelMqttPort.TabIndex = 8;
            this.labelMqttPort.Text = "Port (MQTT):";
            // 
            // txtMqttIpAddress
            // 
            this.txtMqttIpAddress.Location = new System.Drawing.Point(200, 158);
            this.txtMqttIpAddress.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtMqttIpAddress.Name = "txtMqttIpAddress";
            this.txtMqttIpAddress.Size = new System.Drawing.Size(212, 22);
            this.txtMqttIpAddress.TabIndex = 7;
            // 
            // labelMqttIpAddress
            // 
            this.labelMqttIpAddress.AutoSize = true;
            this.labelMqttIpAddress.Location = new System.Drawing.Point(23, 162);
            this.labelMqttIpAddress.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelMqttIpAddress.Name = "labelMqttIpAddress";
            this.labelMqttIpAddress.Size = new System.Drawing.Size(129, 16);
            this.labelMqttIpAddress.TabIndex = 6;
            this.labelMqttIpAddress.Text = "IP Address (MQTT): ";
            // 
            // labelWSPort
            // 
            this.labelWSPort.AutoSize = true;
            this.labelWSPort.Location = new System.Drawing.Point(23, 115);
            this.labelWSPort.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelWSPort.Name = "labelWSPort";
            this.labelWSPort.Size = new System.Drawing.Size(67, 16);
            this.labelWSPort.TabIndex = 4;
            this.labelWSPort.Text = "Port (WS):";
            // 
            // txtWSIpAddress
            // 
            this.txtWSIpAddress.Location = new System.Drawing.Point(200, 79);
            this.txtWSIpAddress.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtWSIpAddress.Name = "txtWSIpAddress";
            this.txtWSIpAddress.Size = new System.Drawing.Size(212, 22);
            this.txtWSIpAddress.TabIndex = 3;
            // 
            // labelWSIpAddress
            // 
            this.labelWSIpAddress.AutoSize = true;
            this.labelWSIpAddress.Location = new System.Drawing.Point(23, 83);
            this.labelWSIpAddress.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelWSIpAddress.Name = "labelWSIpAddress";
            this.labelWSIpAddress.Size = new System.Drawing.Size(112, 16);
            this.labelWSIpAddress.TabIndex = 2;
            this.labelWSIpAddress.Text = "IP Address (WS): ";
            // 
            // comboHardwareInterface
            // 
            this.comboHardwareInterface.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboHardwareInterface.FormattingEnabled = true;
            this.comboHardwareInterface.Items.AddRange(new object[] {
            "Websocket (M0DNY)",
            "Mqtt (F5OEO)"});
            this.comboHardwareInterface.Location = new System.Drawing.Point(199, 30);
            this.comboHardwareInterface.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.comboHardwareInterface.Name = "comboHardwareInterface";
            this.comboHardwareInterface.Size = new System.Drawing.Size(212, 24);
            this.comboHardwareInterface.TabIndex = 1;
            // 
            // labelControlInterface
            // 
            this.labelControlInterface.AutoSize = true;
            this.labelControlInterface.Location = new System.Drawing.Point(21, 33);
            this.labelControlInterface.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelControlInterface.Name = "labelControlInterface";
            this.labelControlInterface.Size = new System.Drawing.Size(109, 16);
            this.labelControlInterface.TabIndex = 0;
            this.labelControlInterface.Text = "Control Interface: ";
            // 
            // btnCancel
            // 
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(245, 609);
            this.btnCancel.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(100, 28);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // btnSave
            // 
            this.btnSave.Location = new System.Drawing.Point(353, 609);
            this.btnSave.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.btnSave.Name = "btnSave";
            this.btnSave.Size = new System.Drawing.Size(100, 28);
            this.btnSave.TabIndex = 4;
            this.btnSave.Text = "Save";
            this.btnSave.UseVisualStyleBackColor = true;
            this.btnSave.Click += new System.EventHandler(this.btnSave_Click);
            // 
            // LongmyndSettingsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(476, 526);
            this.ControlBox = false;
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnSave);
            this.Controls.Add(this.groupTunerProperties);
            this.Controls.Add(this.groupHardwareInterface);
            this.Controls.Add(this.groupTsInput);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "LongmyndSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Longmynd Settings";
            this.groupTunerProperties.ResumeLayout(false);
            this.groupTunerProperties.PerformLayout();
            this.groupHardwareInterface.ResumeLayout(false);
            this.groupHardwareInterface.PerformLayout();
            this.groupTsInput.ResumeLayout(false);
            this.groupTsInput.PerformLayout();
            this.groupHardwareInterface.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupTunerProperties;
        private System.Windows.Forms.TextBox txtTuner1FreqOffset;
        private System.Windows.Forms.Label labelTuner1FreqOffset;
        private System.Windows.Forms.GroupBox groupHardwareInterface;
        private System.Windows.Forms.MaskedTextBox txtWSIpAddress;
        private System.Windows.Forms.Label labelWSIpAddress;
        private System.Windows.Forms.ComboBox comboHardwareInterface;
        private System.Windows.Forms.Label labelControlInterface;
        private System.Windows.Forms.Label labelBaseCmdTopic;
        private System.Windows.Forms.Label labelMqttPort;
        private System.Windows.Forms.MaskedTextBox txtMqttIpAddress;
        private System.Windows.Forms.Label labelMqttIpAddress;
        private System.Windows.Forms.Label labelWSPort;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnSave;
        private System.Windows.Forms.TextBox txtBaseCmdTopic;
        private System.Windows.Forms.TextBox txtMqttPort;
        private System.Windows.Forms.TextBox txtWSPort;
        private System.Windows.Forms.Label labelTsPort;
        private System.Windows.Forms.TextBox txtTSPort;
        private System.Windows.Forms.GroupBox groupTsInput;
        private System.Windows.Forms.Label labelTsAddress;
        private System.Windows.Forms.TextBox txtTsAddress;
        private System.Windows.Forms.Label labelTsAddressHint;
        private System.Windows.Forms.CheckBox checkTestMode;
    }
}