using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace opentuner.MediaSources.Longmynd
{
    public partial class LongmyndSettingsForm : Form
    {

        private LongmyndSettings _settings;
        public LongmyndSettingsForm(ref LongmyndSettings Settings)
        {
            InitializeComponent();
            _settings = Settings;

            comboHardwareInterface.SelectedIndex = _settings.DefaultInterface;
            txtWSIpAddress.Text = _settings.LongmyndWSHost;
            txtWSPort.Text = _settings.LongmyndWSPort.ToString();
            txtMqttIpAddress.Text = _settings.LongmyndMqttHost;
            txtMqttPort.Text = _settings.LongmyndMqttPort.ToString();
            txtBaseCmdTopic.Text = _settings.CmdTopic;
            txtTuner1FreqOffset.Text = _settings.Offset1.ToString();
            txtTSPort.Text = _settings.TS_Port.ToString();
            txtTsAddress.Text = _settings.TS_Address;
            checkTestMode.Checked = _settings.TestMode;

            UpdateControlInterfaceFields();
            comboHardwareInterface.SelectedIndexChanged += comboHardwareInterface_SelectedIndexChanged;
        }

        private void comboHardwareInterface_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateControlInterfaceFields();
        }

        private void checkTestMode_CheckedChanged(object sender, EventArgs e)
        {
            UpdateControlInterfaceFields();
        }

        // only the fields of the selected control interface (0 = websocket, 1 = mqtt) are used, grey out the others;
        // in test mode there is no Longmynd control at all (and no frequency offset)
        private void UpdateControlInterfaceFields()
        {
            bool testMode = checkTestMode.Checked;
            bool websocket = !testMode && comboHardwareInterface.SelectedIndex == 0;
            bool mqtt = !testMode && comboHardwareInterface.SelectedIndex == 1;

            labelControlInterface.Enabled = !testMode;
            comboHardwareInterface.Enabled = !testMode;

            labelWSIpAddress.Enabled = websocket;
            txtWSIpAddress.Enabled = websocket;
            labelWSPort.Enabled = websocket;
            txtWSPort.Enabled = websocket;

            labelMqttIpAddress.Enabled = mqtt;
            txtMqttIpAddress.Enabled = mqtt;
            labelMqttPort.Enabled = mqtt;
            txtMqttPort.Enabled = mqtt;
            labelBaseCmdTopic.Enabled = mqtt;
            txtBaseCmdTopic.Enabled = mqtt;

            labelTuner1FreqOffset.Enabled = !testMode;
            txtTuner1FreqOffset.Enabled = !testMode;
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            uint offset = 0;
            int wsport = 0;
            int mqttport = 0;
            int tsport = 0;

            if (!uint.TryParse(txtTuner1FreqOffset.Text, out offset))
            {
                MessageBox.Show("Invalid Offset");
                return;
            }

            if (!int.TryParse(txtWSPort.Text, out wsport))
            {
                MessageBox.Show("Invalid WS Port");
                return;
            }

            if (!int.TryParse(txtMqttPort.Text, out mqttport))
            {
                MessageBox.Show("Invalid Mqtt Port");
                return;
            }

            if (!int.TryParse(txtTSPort.Text, out tsport))
            {
                MessageBox.Show("Invalid TS Port");
                return;
            }

            string tsaddress = txtTsAddress.Text.Trim();
            // IPAddress.TryParse also accepts short forms like "10" or "1.2.3", so ask for four parts explicitly
            if (tsaddress.Length > 0 && !(tsaddress.Split('.').Length == 4
                && System.Net.IPAddress.TryParse(tsaddress, out System.Net.IPAddress tsip)
                && tsip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork))
            {
                MessageBox.Show("Invalid TS Address (empty = Auto, or an IPv4 address like 192.168.0.10 or 230.0.0.10)");
                return;
            }

            _settings.DefaultInterface = (byte)comboHardwareInterface.SelectedIndex;
            _settings.TS_Port = tsport;
            _settings.TS_Address = tsaddress;
            _settings.TestMode = checkTestMode.Checked;
            _settings.LongmyndWSHost = txtWSIpAddress.Text;
            _settings.LongmyndWSPort = wsport;
            _settings.LongmyndMqttHost = txtMqttIpAddress.Text;
            _settings.LongmyndMqttPort  = mqttport;
            _settings.Offset1 = offset;
            _settings.CmdTopic = txtBaseCmdTopic.Text;

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
