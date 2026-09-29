using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace opentuner.MediaSources.Minitiouner
{
    public partial class MinitiounerSettingsForm : Form
    {
        private MinitiounerSettings _settings;
        private bool _single_tuner;   // the board has only one tuner (single TS)
        // With several boards the dialog says which board it is for (titleSuffix, e.g. "V2 - tuner 3"), numbers the tuners
        // like the rest of the program (tunerOffset = number of the first tuner minus 1) and hides the second tuner of a
        // board that has only one (tunerCount 1).
        public MinitiounerSettingsForm(ref MinitiounerSettings Settings, string titleSuffix = "", int tunerOffset = 0, int tunerCount = 2)
        {
            InitializeComponent();
            _settings = Settings;

            _single_tuner = tunerCount < 2;

            if (!string.IsNullOrEmpty(titleSuffix))
                Text += " - " + titleSuffix;

            labelTuner1FreqOffset.Text = "Tuner " + (tunerOffset + 1) + " Freq Offset:";
            labelTuner2FreqOffset.Text = "Tuner " + (tunerOffset + 2) + " Freq Offset:";
            labelTuner1Correction.Text = "Tuner " + (tunerOffset + 1) + " Correction (ppm):";
            labelTuner2Correction.Text = "Tuner " + (tunerOffset + 2) + " Correction (ppm):";
            if (tunerCount < 2)
            {
                labelTuner2FreqOffset.Visible = false;
                txtTuner2FreqOffset.Visible = false;
                labelTuner2Correction.Visible = false;
                txtTuner2FreqCorrection.Visible = false;
            }

            comboHardwareInterface.SelectedIndex = _settings.DefaultInterface;
            txtTuner1FreqOffset.Text = _settings.Offset1.ToString();
            txtTuner2FreqOffset.Text = _settings.Offset2.ToString();
            txtTuner1FreqCorrection.Text = PpmAt(_settings.FreqCorrectionPpm, 0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            txtTuner2FreqCorrection.Text = PpmAt(_settings.FreqCorrectionPpm, 1).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

            comboSupplyADefault.SelectedIndex = _settings.DefaultLnbASupply;
            comboSupplyBDefault.SelectedIndex = _settings.DefaultLnbBSupply;
            if (_single_tuner)
            {
                // one tuner (V2, E-Tiouner): only the input of that tuner is a choice. The setting keeps its four values
                // (0 A/A, 1 A/B, 2 B/A, 3 B/B); the second tuner is not used, so only the first input counts.
                ComboDefaultRFInput.Items.Clear();
                ComboDefaultRFInput.Items.Add("Tuner " + (tunerOffset + 1) + " = A");
                ComboDefaultRFInput.Items.Add("Tuner " + (tunerOffset + 1) + " = B");
                ComboDefaultRFInput.SelectedIndex = _settings.DefaultRFInput >= 2 ? 1 : 0;
            }
            else
            {
                ComboDefaultRFInput.SelectedIndex = _settings.DefaultRFInput;
            }

            // the two LNB supply defaults only mean something when the board can switch the LNB voltage
            checkLnbControl.CheckedChanged += (s, e) => UpdateLnbControls();
            checkLnbControl.Checked = _settings.LnbSupplyControl;
            UpdateLnbControls();

            checkEnableDigole.Checked = _settings.EnableDigoleDisplay;
            txtDigoleAddress.Text = _settings.DigoleI2cAddress.ToString("X2");
            txtDigoleCallsign.Text = _settings.DigoleCallsign;
            txtDigoleLocator.Text = _settings.DigoleLocator;
            txtDigoleName.Text = _settings.DigoleName;
        }

        private void UpdateLnbControls()
        {
            bool switchable = checkLnbControl.Checked;
            labelLnbASupplyDefault.Enabled = switchable;
            labelLnbBSupplyDefault.Enabled = switchable;
            comboSupplyADefault.Enabled = switchable;
            comboSupplyBDefault.Enabled = switchable;
        }

        private static double PpmAt(double[] values, int index)
        {
            return values != null && values.Length > index ? values[index] : 0;
        }

        private static int At(int[] values, int index)
        {
            return values != null && values.Length > index ? values[index] : 0;
        }


        private void btnCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            uint offset1 = 0;
            if (!uint.TryParse(txtTuner1FreqOffset.Text, out offset1))
            {
                MessageBox.Show("Invalid Offset 1");
                return;
            }

            uint offset2 = 0;
            if (!uint.TryParse(txtTuner2FreqOffset.Text, out offset2))
            {
                MessageBox.Show("Invalid Offset 2");
                return;
            }

            // reference error correction of the tuner in ppm (same range as the slider on the Special tab)
            double correction1 = 0, correction2 = 0;
            var invariant = System.Globalization.CultureInfo.InvariantCulture;
            if (!double.TryParse(txtTuner1FreqCorrection.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, invariant, out correction1) || correction1 < -250 || correction1 > 250)
            {
                MessageBox.Show("Invalid Tuner 1 Correction (-250 .. 250 ppm)");
                return;
            }

            if (!double.TryParse(txtTuner2FreqCorrection.Text.Replace(',', '.'), System.Globalization.NumberStyles.Float, invariant, out correction2) || correction2 < -250 || correction2 > 250)
            {
                MessageBox.Show("Invalid Tuner 2 Correction (-250 .. 250 ppm)");
                return;
            }


            byte digoleAddress = 0;
            if (!byte.TryParse(txtDigoleAddress.Text, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out digoleAddress))
            {
                MessageBox.Show("Invalid Digole I2C Address (hex, e.g. 27)");
                return;
            }

            _settings.DefaultInterface = (byte)comboHardwareInterface.SelectedIndex;
            _settings.DefaultLnbASupply = (byte)comboSupplyADefault.SelectedIndex;
            _settings.DefaultLnbBSupply = (byte)comboSupplyBDefault.SelectedIndex;
            _settings.DefaultRFInput = _single_tuner
                ? (byte)(ComboDefaultRFInput.SelectedIndex == 1 ? 2 : 0)   // Tuner = B: B/A, Tuner = A: A/A
                : (byte)ComboDefaultRFInput.SelectedIndex;

            _settings.Offset1 = offset1;
            _settings.Offset2 = offset2;

            _settings.FreqCorrectionPpm = new double[] { correction1, correction2 };


            _settings.LnbSupplyControl = checkLnbControl.Checked;
            _settings.EnableDigoleDisplay = checkEnableDigole.Checked;
            _settings.DigoleI2cAddress = digoleAddress;
            _settings.DigoleCallsign = txtDigoleCallsign.Text.Trim().ToUpperInvariant();
            _settings.DigoleLocator = txtDigoleLocator.Text.Trim().ToUpperInvariant();
            _settings.DigoleName = txtDigoleName.Text.Trim();

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
