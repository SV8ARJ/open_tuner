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
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.txtTuner1FreqOffset = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.groupHardwareInterface = new System.Windows.Forms.GroupBox();
            this.label7 = new System.Windows.Forms.Label();
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
            this.groupBox1.SuspendLayout();
            this.groupHardwareInterface.SuspendLayout();
            this.SuspendLayout();
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.txtTuner1FreqOffset);
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Location = new System.Drawing.Point(16, 380);
            this.groupBox1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Padding = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.groupBox1.Size = new System.Drawing.Size(437, 95);
            this.groupBox1.TabIndex = 3;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Tuner Properties";
            // 
            // txtTuner1FreqOffset
            // 
            this.txtTuner1FreqOffset.Location = new System.Drawing.Point(199, 37);
            this.txtTuner1FreqOffset.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtTuner1FreqOffset.Name = "txtTuner1FreqOffset";
            this.txtTuner1FreqOffset.Size = new System.Drawing.Size(212, 22);
            this.txtTuner1FreqOffset.TabIndex = 4;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(21, 41);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(123, 16);
            this.label3.TabIndex = 0;
            this.label3.Text = "Tuner 1 Freq Offset:";
            // 
            // groupHardwareInterface
            // 
            this.groupHardwareInterface.Controls.Add(this.label7);
            this.groupHardwareInterface.Controls.Add(this.txtTSPort);
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
            this.groupHardwareInterface.Size = new System.Drawing.Size(437, 358);
            this.groupHardwareInterface.TabIndex = 2;
            this.groupHardwareInterface.TabStop = false;
            this.groupHardwareInterface.Text = "Hardware Interface";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(23, 66);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(55, 16);
            this.label7.TabIndex = 15;
            this.label7.Text = "TS Port:";
            // 
            // txtTSPort
            // 
            this.txtTSPort.Location = new System.Drawing.Point(199, 63);
            this.txtTSPort.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtTSPort.Name = "txtTSPort";
            this.txtTSPort.Size = new System.Drawing.Size(129, 22);
            this.txtTSPort.TabIndex = 14;
            // 
            // txtBaseCmdTopic
            // 
            this.txtBaseCmdTopic.Location = new System.Drawing.Point(200, 286);
            this.txtBaseCmdTopic.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtBaseCmdTopic.Name = "txtBaseCmdTopic";
            this.txtBaseCmdTopic.Size = new System.Drawing.Size(212, 22);
            this.txtBaseCmdTopic.TabIndex = 13;
            // 
            // txtMqttPort
            // 
            this.txtMqttPort.Location = new System.Drawing.Point(200, 230);
            this.txtMqttPort.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtMqttPort.Name = "txtMqttPort";
            this.txtMqttPort.Size = new System.Drawing.Size(212, 22);
            this.txtMqttPort.TabIndex = 12;
            // 
            // txtWSPort
            // 
            this.txtWSPort.Location = new System.Drawing.Point(200, 151);
            this.txtWSPort.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtWSPort.Name = "txtWSPort";
            this.txtWSPort.Size = new System.Drawing.Size(212, 22);
            this.txtWSPort.TabIndex = 11;
            // 
            // labelBaseCmdTopic
            // 
            this.labelBaseCmdTopic.AutoSize = true;
            this.labelBaseCmdTopic.Location = new System.Drawing.Point(23, 289);
            this.labelBaseCmdTopic.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelBaseCmdTopic.Name = "labelBaseCmdTopic";
            this.labelBaseCmdTopic.Size = new System.Drawing.Size(155, 16);
            this.labelBaseCmdTopic.TabIndex = 10;
            this.labelBaseCmdTopic.Text = "MQTT Base CMD Topic:";
            // 
            // labelMqttPort
            // 
            this.labelMqttPort.AutoSize = true;
            this.labelMqttPort.Location = new System.Drawing.Point(23, 234);
            this.labelMqttPort.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelMqttPort.Name = "labelMqttPort";
            this.labelMqttPort.Size = new System.Drawing.Size(84, 16);
            this.labelMqttPort.TabIndex = 8;
            this.labelMqttPort.Text = "Port (MQTT):";
            // 
            // txtMqttIpAddress
            // 
            this.txtMqttIpAddress.Location = new System.Drawing.Point(200, 198);
            this.txtMqttIpAddress.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtMqttIpAddress.Name = "txtMqttIpAddress";
            this.txtMqttIpAddress.Size = new System.Drawing.Size(212, 22);
            this.txtMqttIpAddress.TabIndex = 7;
            // 
            // labelMqttIpAddress
            // 
            this.labelMqttIpAddress.AutoSize = true;
            this.labelMqttIpAddress.Location = new System.Drawing.Point(23, 202);
            this.labelMqttIpAddress.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelMqttIpAddress.Name = "labelMqttIpAddress";
            this.labelMqttIpAddress.Size = new System.Drawing.Size(129, 16);
            this.labelMqttIpAddress.TabIndex = 6;
            this.labelMqttIpAddress.Text = "IP Address (MQTT): ";
            // 
            // labelWSPort
            // 
            this.labelWSPort.AutoSize = true;
            this.labelWSPort.Location = new System.Drawing.Point(23, 155);
            this.labelWSPort.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.labelWSPort.Name = "labelWSPort";
            this.labelWSPort.Size = new System.Drawing.Size(67, 16);
            this.labelWSPort.TabIndex = 4;
            this.labelWSPort.Text = "Port (WS):";
            // 
            // txtWSIpAddress
            // 
            this.txtWSIpAddress.Location = new System.Drawing.Point(200, 119);
            this.txtWSIpAddress.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.txtWSIpAddress.Name = "txtWSIpAddress";
            this.txtWSIpAddress.Size = new System.Drawing.Size(212, 22);
            this.txtWSIpAddress.TabIndex = 3;
            // 
            // labelWSIpAddress
            // 
            this.labelWSIpAddress.AutoSize = true;
            this.labelWSIpAddress.Location = new System.Drawing.Point(23, 123);
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
            this.btnCancel.Location = new System.Drawing.Point(245, 482);
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
            this.btnSave.Location = new System.Drawing.Point(353, 482);
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
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.groupHardwareInterface);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "LongmyndSettingsForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Longmynd Settings";
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupHardwareInterface.ResumeLayout(false);
            this.groupHardwareInterface.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.TextBox txtTuner1FreqOffset;
        private System.Windows.Forms.Label label3;
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
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.TextBox txtTSPort;
    }
}