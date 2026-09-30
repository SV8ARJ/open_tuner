using System;
using System.Windows.Forms;

namespace opentuner.ExtraFeatures.BATCSpectrum
{
    public partial class BATCSpectrumSettingsForm : Form
    {
        private const int TuneModeTimed = BATCSpectrumSettings.ModeAutoTimed;

        // how the tuning modes work, shown at the right of the settings
        private static readonly string ModeHelp = string.Join(Environment.NewLine, new[]
        {
            "A free signal is one that no other tuner is on (the signal of a manual tuner counts as taken). Only signals of at least the threshold are used. Once a second at most one tuner is switched, RX 1 first.",
            "",
            "Manual",
            "Nothing happens by itself.",
            "",
            "Auto (Hold): stay with the station",
            "- No signal yet (e.g. after the start): the free signal with the highest level.",
            "- The signal is there: the tuner stays on it.",
            "- The signal is gone: it waits for the Hold Time for the signal to come back, then takes the free signal nearest to its last frequency (up or down).",
            "- Never the beacon.",
            "",
            "Auto (Next new): stay, but look for new stations",
            "- Like Hold.",
            "- Also: after the Timed interval on the same signal it goes to the nearest free signal without callsign (not decoded yet), if there is one; otherwise it stays.",
            "- Never the beacon.",
            "",
            "Auto (Timed): step through the signals",
            "- No signal yet: the free signal with the lowest frequency.",
            "- Every Timed interval, or at once when the signal is gone: the next free signal above the current frequency; after the highest one it starts again at the lowest.",
            "- The beacon only with Avoid Beacon off."
        });

        private readonly BATCSpectrumSettings spectrumSettings;
        private readonly ComboBox[] tuneModes;
        private readonly CheckBox[] avoidBeacons;

        public BATCSpectrumSettingsForm(BATCSpectrumSettings _spectrumSettings)
        {
            spectrumSettings = _spectrumSettings;
            InitializeComponent();

            textModeHelp.Text = ModeHelp;

            tuneModes = new[] { tuneMode1, tuneMode2, tuneMode3, tuneMode4 };
            avoidBeacons = new[] { avoidBeacon1, avoidBeacon2, avoidBeacon3, avoidBeacon4 };

            for (int i = 0; i < tuneModes.Length; i++)
            {
                tuneModes[i].SelectedIndex = Math.Max(0, Math.Min(spectrumSettings.tuneMode[i], tuneModes[i].Items.Count - 1));
                avoidBeacons[i].Checked = spectrumSettings.avoidBeacon[i];
            }

            thresholdValue.Value = Math.Max(thresholdValue.Minimum, Math.Min(thresholdValue.Maximum, Convert.ToDecimal(spectrumSettings.threshold)));
            autoHoldTimeValue.Value = Math.Max(autoHoldTimeValue.Minimum, Math.Min(autoHoldTimeValue.Maximum, spectrumSettings.autoHoldTimeValue));
            autoTuneTimeValue.Value = Math.Max(autoTuneTimeValue.Minimum, Math.Min(autoTuneTimeValue.Maximum, spectrumSettings.autoTuneTimeValue));

            overPowerIndicatorLayout.SelectedIndex = Math.Max(0, Math.Min(spectrumSettings.overPowerIndicatorLayout, overPowerIndicatorLayout.Items.Count - 1));
            overPowerLimitValue.Value = Math.Max(overPowerLimitValue.Minimum, Math.Min(overPowerLimitValue.Maximum, Convert.ToDecimal(spectrumSettings.overPowerLimit)));
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < tuneModes.Length; i++)
            {
                spectrumSettings.tuneMode[i] = tuneModes[i].SelectedIndex;
                spectrumSettings.avoidBeacon[i] = avoidBeacons[i].Checked;
            }

            spectrumSettings.threshold = Convert.ToSingle(thresholdValue.Value);
            spectrumSettings.autoHoldTimeValue = Convert.ToInt32(autoHoldTimeValue.Value);
            spectrumSettings.autoTuneTimeValue = Convert.ToInt32(autoTuneTimeValue.Value);

            spectrumSettings.overPowerIndicatorLayout = overPowerIndicatorLayout.SelectedIndex;
            spectrumSettings.overPowerLimit = Convert.ToSingle(overPowerLimitValue.Value);

            DialogResult = DialogResult.OK;
            Close();
        }

        // "Avoid Beacon" can only be chosen for Auto (Timed): Manual does not tune, the other Auto modes never go to the beacon
        private void tuneMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            int index = Array.IndexOf(tuneModes ?? new ComboBox[0], sender);
            if (index < 0)
                return;

            int mode = tuneModes[index].SelectedIndex;
            avoidBeacons[index].Visible = mode == TuneModeTimed;

            if (mode != TuneModeTimed)
                avoidBeacons[index].Checked = mode != BATCSpectrumSettings.ModeManual;
        }
    }
}
