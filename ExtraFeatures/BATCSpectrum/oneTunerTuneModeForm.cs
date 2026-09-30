using System;
using System.Windows.Forms;

namespace opentuner.ExtraFeatures.BATCSpectrum
{
    // Small dialog (middle click in the spectrum) to set the tuning mode of one tuner
    public partial class oneTunerTuneModeForm : Form
    {
        private int tuneMode;
        private bool avoidBeacon;
        private readonly RadioButton[] modeButtons;

        public oneTunerTuneModeForm(int _tuner, int _tuneMode, bool _avoidBeacon)
        {
            tuneMode = _tuneMode;
            avoidBeacon = _avoidBeacon;
            InitializeComponent();

            modeButtons = new[] { radioManual, radioAutoHold, radioAutoNextNew, radioAutoTimed };
            modeButtons[Math.Max(0, Math.Min(tuneMode, modeButtons.Length - 1))].Checked = true;

            checkAvoidBeacon.Checked = avoidBeacon;
            labelTuner.Text = "RX " + _tuner.ToString() + ":";
        }

        public int getTuneMode()
        {
            return tuneMode;
        }

        public bool getAvoidBeacon()
        {
            return avoidBeacon;
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            tuneMode = Array.FindIndex(modeButtons, b => b.Checked);
            avoidBeacon = checkAvoidBeacon.Checked;

            DialogResult = DialogResult.OK;
            Close();
        }

        // "Avoid Beacon" can only be chosen for Auto (Timed): Manual does not tune, the other Auto modes never go to the beacon
        private void radioMode_CheckedChanged(object sender, EventArgs e)
        {
            if (modeButtons == null || !((RadioButton)sender).Checked)
                return;

            bool timed = radioAutoTimed.Checked;
            checkAvoidBeacon.Visible = timed;

            if (!timed)
                checkAvoidBeacon.Checked = !radioManual.Checked;
            else
                checkAvoidBeacon.Checked = avoidBeacon;
        }
    }
}
