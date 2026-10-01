using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using opentuner.Utilities;

namespace opentuner.MediaSources.Minitiouner
{
    // The "Chip" tab (issue #62): the receiver settings of MiniTioune's Extra Panel that open_tuner can write itself, one group per
    // topic - carrier loops, I/Q, equalizer, noise and constellation of the STV0910 (demodulator), baseband gain of the STV6120
    // (tuner) and the software settings (status polling, fill level of the TS buffers). The controls read from and write into the
    // settings of the board; 0.4 s after the last change SettingsChanged is raised (the caller stores the settings and has the
    // chips written again), so dragging the gain slider does not tune again at every step.
    public class ChipSettingsView
    {
        private const int ApplyDelayMs = 400;

        private static readonly string[] IqChoices = { "off", "on", "off, last value kept", "on, fixed" };
        private static readonly string[] DfeChoices = { "off", "frozen", "very slow", "median", "fastest" };
        private static readonly string[] FfeChoices = { "frozen", "very slow", "median", "fastest" };
        private static readonly string[] Algo2Choices = { "algorithm 0 (CNR > 7 dB)", "algorithm 1 (CNR < 7 dB)", "native (recommended)" };
        private static readonly string[] NoiseDataChoices = { "2^-20 (slowest)", "2^-18", "2^-16", "2^-14", "2^-12 (default)", "2^-10", "2^-8", "2^0 (fastest)" };
        private static readonly string[] NoisePlhChoices = { "slowest", "2^-10", "2^-8 (default)", "2^0 (fastest)" };
        private static readonly string[] ConstellationChoices =
        {
            "demodulator IQ output", "equalizer output", "derotator 2 output", "symbols + inter symbols", "symbols",
            "inter symbols", "derotator 1 output", "IQ mismatches output", "demodulator input",
        };
        private static readonly int[] RefreshChoices = { 125, 200, 300 };

        private readonly MinitiounerSettings _settings;
        private readonly Func<int, int> _buffer_bytes;
        private readonly int _tuners;
        private readonly Control _first_group;
        private readonly ToolTip _tips = new ToolTip();
        private readonly List<Action> _loaders = new List<Action>();   // controls <- settings
        private readonly RadioButton[] _algo = new RadioButton[3];
        private readonly RadioButton[] _refresh = new RadioButton[3];
        private readonly TrackBar _gain = new TrackBar();
        private readonly Label _gain_label = new Label();
        private readonly EqualizerControl[] _equalizer;
        private readonly Label[] _iq_label;
        private readonly int[][] _iq_values;
        private readonly Label _buffer_label = new Label();
        private readonly Timer _apply_timer = new Timer();
        private readonly Timer _ui_timer = new Timer();
        private bool _loading = false;

        // The settings were changed by the user (the caller stores them and has the chips written again).
        public event Action SettingsChanged;

        public ChipSettingsView(Control parent, MinitiounerSettings settings, int tuners, Func<int, int> buffer_bytes)
        {
            _settings = settings;
            _tuners = tuners;
            _buffer_bytes = buffer_bytes;
            _equalizer = new EqualizerControl[tuners];
            _iq_label = new Label[tuners];
            _iq_values = new int[tuners][];
            _tips.ShowAlways = true;

            // ---- STV0910: carrier loops ----
            TableLayoutPanel loops = AddGroup(parent, "STV0910 (demodulator) - carrier loops", out _first_group);

            BindCheck(loops, "Carrier loop 1", "closed (on)", () => _settings.Loop1On, v => _settings.Loop1On = v,
                      "CARCFG.ROTAON: carrier loop 1 derotator in action. Off = loop open. MiniTioune: loop1 On.");

            var algo_panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0, 2, 0, 6) };
            string[] algo_names = { "costas (MiniTioune)", "citroen 1", "citroen 2 (longmynd)" };
            for (int i = 0; i < _algo.Length; i++)
            {
                _algo[i] = new RadioButton { Text = algo_names[i], AutoSize = true, Tag = i };
                _algo[i].CheckedChanged += (s, e) =>
                {
                    if (_loading || !((RadioButton)s).Checked)
                        return;
                    _settings.CarrierPhaseAlgo = (byte)(int)((RadioButton)s).Tag;
                    Changed();
                };
                algo_panel.Controls.Add(_algo[i]);
            }
            _loaders.Add(() => _algo[Math.Max(0, Math.Min(2, (int)_settings.CarrierPhaseAlgo))].Checked = true);
            AddRow(loops, "Loop 1, phase\ndetector", algo_panel);
            _tips.SetToolTip(algo_panel, "CARCFG.PH_DET_ALGO of carrier loop 1. MiniTioune uses costas, longmynd citroen 2.");

            BindCheck(loops, "Carrier loop 2", "closed (on)", () => _settings.Loop2On, v => _settings.Loop2On = v,
                      "CAR2CFG.ROTA2ON: carrier loop 2 derotator in action. Off = loop open.");
            BindCombo(loops, "Loop 2, phase\ndetector", Algo2Choices, () => _settings.Algo2, v => _settings.Algo2 = v,
                      "CAR2CFG.PH_DET_ALGO2: algorithm for the phase error on QPSK symbols. Algorithms 0 and 1 are for legacy compatibility, native is recommended.");

            // ---- STV0910: I/Q ----
            TableLayoutPanel iq = AddGroup(parent, "STV0910 (demodulator) - I/Q", out _);

            BindCheck(iq, "I/Q swap", "on", () => _settings.IqSwap, v => _settings.IqSwap = v,
                      "TNRCFG2.TUN_IQSWAP. On flips the sign of the carrier offset (CFR).");

            string iq_tip = "AGC1CFG: off = no correction, on = running (reset value), off with the last measure kept, on but fixed (the value found so far is no longer updated).";
            BindCombo(iq, "DC offset", IqChoices, () => _settings.IqDc, v => _settings.IqDc = v, iq_tip);
            BindCombo(iq, "Imbalance", IqChoices, () => _settings.IqAmplitude, v => _settings.IqAmplitude = v, iq_tip);
            BindCombo(iq, "Quadrature", IqChoices, () => _settings.IqQuadrature, v => _settings.IqQuadrature = v, iq_tip);

            for (int i = 0; i < tuners; i++)
            {
                _iq_label[i] = new Label { AutoSize = true, Text = "-", Margin = new Padding(3, 4, 3, 6) };
                AddRow(iq, "Measured,\ntuner " + (i + 1), _iq_label[i]);
                _tips.SetToolTip(_iq_label[i], "What the demodulator corrects (IDCCOMP / QDCCOMP: DC offset of I and Q in half ADC steps, AGC1AMM: imbalance of Q against I, AGC1QUAD: quadrature error). 0 = nothing to correct.");
            }

            // ---- STV0910: equalizer ----
            TableLayoutPanel eq = AddGroup(parent, "STV0910 (demodulator) - equalizer", out _);

            BindCombo(eq, "Equalizer, DFE", DfeChoices, () => _settings.EqualizerDfe, v => _settings.EqualizerDfe = v,
                      "EQUALCFG: the equalizer that removes echoes (cable reflections). Off stops and resets it, frozen keeps the learned coefficients, otherwise the speed of the adaption (MU_EQUALDFE): very slow = reset value and MiniTioune's setting.");
            BindCombo(eq, "Equalizer, FFE", FfeChoices, () => _settings.EqualizerFfe, v => _settings.EqualizerFfe = v,
                      "FFECFG: the equalizer that compensates filter and group delay errors. It always stays on (the data sheet says it has to); the speed of the adaption (MU_EQUALFFE): very slow = reset value and MiniTioune's setting.");

            for (int i = 0; i < tuners; i++)
            {
                _equalizer[i] = new EqualizerControl { Width = 270, Margin = new Padding(3, 2, 3, 8) };
                AddRow(eq, "Equalizer taps,\ntuner " + (i + 1), _equalizer[i]);
                _tips.SetToolTip(_equalizer[i], "Coefficients of the DFE (8 taps) and the FFE (4 taps), I yellow, Q light blue. Bars near the middle line = nothing to correct.");
            }

            // ---- STV0910: noise and constellation ----
            TableLayoutPanel noise = AddGroup(parent, "STV0910 (demodulator) - noise and constellation", out _);

            BindCombo(noise, "Noise speed,\ndata", NoiseDataChoices, () => _settings.NoiseData, v => _settings.NoiseData = v,
                      "NOSCFG.NOSDATA_BETA: how fast the noise measurement on the data (DVB-S, DVB-S2) follows the signal. Slower = smoother, faster = livelier. MiniTioune: noise speed.");
            BindCombo(noise, "Noise speed,\nPL header", NoisePlhChoices, () => _settings.NoisePlh, v => _settings.NoisePlh = v,
                      "NOSCFG.NOSPLH_BETA: the same for the structure symbols of DVB-S2 (PL header, pilots). The noise bar of the Expert tab uses this one in DVB-S2.");
            BindCombo(noise, "Constellation\nsource", ConstellationChoices, () => _settings.ConstellationSource, v => _settings.ConstellationSource = v,
                      "IQCONST.IQSYMB_SEL: the point in the data flow whose I/Q samples the constellation of the Expert tab shows.");

            // ---- STV6120, the tuner ----
            TableLayoutPanel tuner = AddGroup(parent, "STV6120 (tuner)", out _);

            var gain_panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 0, 0, 6) };
            _gain.Minimum = 0;
            _gain.Maximum = 8;
            _gain.TickFrequency = 1;
            _gain.LargeChange = 1;
            _gain.SmallChange = 1;
            _gain.Width = 190;
            _gain.Height = 32;
            _gain.ValueChanged += (s, e) =>
            {
                UpdateGainLabel();
                if (_loading)
                    return;
                _settings.BasebandGainDb = _gain.Value * 2;
                Changed();
            };

            // one step (2 dB) per notch of the mouse wheel; the default of a TrackBar is three steps
            _gain.MouseWheel += (s, e) =>
            {
                if (e is HandledMouseEventArgs handled)
                    handled.Handled = true;

                if (e.Delta != 0)
                    _gain.Value = Math.Max(_gain.Minimum, Math.Min(_gain.Maximum, _gain.Value + (e.Delta > 0 ? 1 : -1)));
            };

            _gain_label.AutoSize = false;
            _gain_label.Width = 60;
            _gain_label.Height = 28;
            _gain_label.TextAlign = ContentAlignment.MiddleLeft;
            gain_panel.Controls.Add(_gain);
            gain_panel.Controls.Add(_gain_label);
            _loaders.Add(() => _gain.Value = Math.Max(0, Math.Min(8, _settings.BasebandGainDb / 2)));
            AddRow(tuner, "Baseband gain", gain_panel);
            _tips.SetToolTip(_gain, "STV6120 CTRL2.BBGAIN in steps of 2 dB (MiniTioune 8 dB, longmynd 6 dB). Written at every tune. The demodulator's AGC makes up for it, so the picture hardly changes.");

            // ---- software ----
            TableLayoutPanel software = AddGroup(parent, "Software", out _);

            var refresh_panel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0, 2, 0, 6) };
            for (int i = 0; i < _refresh.Length; i++)
            {
                _refresh[i] = new RadioButton { Text = RefreshChoices[i] + " ms", AutoSize = true, Tag = i };
                _refresh[i].CheckedChanged += (s, e) =>
                {
                    if (_loading || !((RadioButton)s).Checked)
                        return;
                    _settings.RefreshIntervalMs = RefreshChoices[(int)((RadioButton)s).Tag];
                    Changed();
                };
                refresh_panel.Controls.Add(_refresh[i]);
            }
            _loaders.Add(() =>
            {
                int nearest = 0;
                for (int i = 1; i < RefreshChoices.Length; i++)
                {
                    if (Math.Abs(RefreshChoices[i] - _settings.RefreshIntervalMs) < Math.Abs(RefreshChoices[nearest] - _settings.RefreshIntervalMs))
                        nearest = i;
                }
                _refresh[nearest].Checked = true;
            });
            AddRow(software, "Status polling", refresh_panel);
            _tips.SetToolTip(refresh_panel, "Pause between two reads of the status registers (refresh timing of MiniTioune). Shorter = livelier gauges, more I2C traffic; the measured refresh time is on the Expert tab.");

            _buffer_label.AutoSize = true;
            _buffer_label.Margin = new Padding(3, 4, 3, 6);
            _buffer_label.Text = "-";
            AddRow(software, "TS buffer", _buffer_label);
            _tips.SetToolTip(_buffer_label, "Bytes waiting in the TS data queue of each tuner (before the players, recorders and the parser take them).");

            // ---- defaults and the note, below the groups ----
            var bottom = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(8, 6, 8, 8) };

            var defaults = new Button { Text = "Back to defaults", AutoSize = true, Margin = new Padding(3, 3, 3, 3) };
            defaults.Click += (s, e) =>
            {
                CopyChipFields(new MinitiounerSettings(), _settings);
                LoadAll();
                Changed();
            };
            bottom.Controls.Add(defaults);

            var note = new Label
            {
                Text = "A change takes effect at once: the chips are written again and both tuners of this board are tuned again, the picture is gone for a moment. " +
                       "The defaults are the values the chips start with (MiniTioune's where known).",
                AutoSize = true,
                Margin = new Padding(3, 18, 3, 3),
                ForeColor = SystemColors.GrayText,
                Font = new Font("Microsoft Sans Serif", 8f),
            };
            bottom.Controls.Add(note);
            parent.SizeChanged += (s, e) => note.MaximumSize = new Size(Math.Max(120, parent.Width - 40), 0);

            parent.Controls.Add(bottom);
            bottom.BringToFront();

            LoadAll();

            _apply_timer.Interval = ApplyDelayMs;
            _apply_timer.Tick += (s, e) =>
            {
                _apply_timer.Stop();
                SettingsChanged?.Invoke();
            };

            _ui_timer.Interval = 500;
            _ui_timer.Tick += (s, e) => { if (_first_group.Visible) UpdateLive(); };
            _ui_timer.Start();

            _first_group.Disposed += (s, e) =>
            {
                _apply_timer.Dispose();
                _ui_timer.Dispose();
                _tips.Dispose();
            };
        }

        // the settings the Chip tab changes (copied back to the defaults by "Back to defaults")
        private static void CopyChipFields(MinitiounerSettings from, MinitiounerSettings to)
        {
            to.CarrierPhaseAlgo = from.CarrierPhaseAlgo;
            to.IqSwap = from.IqSwap;
            to.BasebandGainDb = from.BasebandGainDb;
            to.RefreshIntervalMs = from.RefreshIntervalMs;
            to.EqualizerDfe = from.EqualizerDfe;
            to.EqualizerFfe = from.EqualizerFfe;
            to.Loop1On = from.Loop1On;
            to.Loop2On = from.Loop2On;
            to.Algo2 = from.Algo2;
            to.IqDc = from.IqDc;
            to.IqAmplitude = from.IqAmplitude;
            to.IqQuadrature = from.IqQuadrature;
            to.NoiseData = from.NoiseData;
            to.NoisePlh = from.NoisePlh;
            to.ConstellationSource = from.ConstellationSource;
        }

        // A group box with a table for its rows, as high as the rows need; the groups stand in the order they are added.
        private static TableLayoutPanel AddGroup(Control parent, string title, out Control group)
        {
            var box = new CustomGroupBox();
            box.Dock = DockStyle.Top;
            box.Height = 120;
            box.Text = title;
            box.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            box.Padding = new Padding(8, 7, 8, 8);

            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Top;
            table.AutoSize = true;
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130f));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            table.SizeChanged += (s, e) => box.Height = table.Height + box.Padding.Vertical + 28;

            box.Controls.Add(table);
            parent.Controls.Add(box);
            box.BringToFront();   // stacks the groups top to bottom in creation order

            group = box;
            return table;
        }

        private static void AddRow(TableLayoutPanel table, string caption, Control control)
        {
            int row = table.RowCount;
            table.RowCount = row + 1;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(3, 6, 3, 3) }, 0, row);
            table.Controls.Add(control, 1, row);
        }

        private void BindCombo(TableLayoutPanel table, string caption, string[] items, Func<int> get, Action<int> set, string tip)
        {
            var box = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 210, Margin = new Padding(3, 2, 3, 6) };
            box.Items.AddRange(items);
            box.SelectedIndexChanged += (s, e) =>
            {
                if (_loading)
                    return;
                set(box.SelectedIndex);
                Changed();
            };
            _loaders.Add(() => box.SelectedIndex = Math.Max(0, Math.Min(items.Length - 1, get())));
            AddRow(table, caption, box);
            _tips.SetToolTip(box, tip);
        }

        private void BindCheck(TableLayoutPanel table, string caption, string text, Func<bool> get, Action<bool> set, string tip)
        {
            var box = new CheckBox { Text = text, AutoSize = true, Margin = new Padding(3, 2, 3, 6) };
            box.CheckedChanged += (s, e) =>
            {
                if (_loading)
                    return;
                set(box.Checked);
                Changed();
            };
            _loaders.Add(() => box.Checked = get());
            AddRow(table, caption, box);
            _tips.SetToolTip(box, tip);
        }

        // controls <- settings, without reporting a change
        private void LoadAll()
        {
            _loading = true;
            try
            {
                foreach (Action load in _loaders)
                    load();

                UpdateGainLabel();
            }
            finally
            {
                _loading = false;
            }
        }

        private void UpdateGainLabel()
        {
            _gain_label.Text = (_gain.Value * 2) + " dB";
        }

        private void Changed()
        {
            if (_loading)
                return;

            _apply_timer.Stop();
            _apply_timer.Start();
        }

        // The equalizer coefficients of a tuner's demodulator (null = not locked); any thread.
        public void UpdateEqualizer(int tuner, sbyte[] dfe, sbyte[] ffe)
        {
            if (tuner >= 0 && tuner < _equalizer.Length)
                _equalizer[tuner].SetCoefficients(dfe, ffe);
        }

        // The I/Q compensation the demodulator measures (null = not locked); any thread, shown by the timer.
        public void UpdateIq(int tuner, int[] values)
        {
            if (tuner >= 0 && tuner < _iq_values.Length)
                _iq_values[tuner] = values;
        }

        // buffer fill level and I/Q readout, twice a second
        private void UpdateLive()
        {
            var parts = new string[_tuners];
            for (int i = 0; i < _tuners; i++)
            {
                parts[i] = "tuner " + (i + 1) + ":  " + _buffer_bytes(i).ToString("N0") + " bytes";

                int[] v = _iq_values[i];
                _iq_label[i].Text = v == null
                    ? "-"
                    : "Iof " + v[0] + "    Qof " + v[1] +
                      "    Imbal " + ((v[2] - 128) / 128.0).ToString("+0.00;-0.00;0.00") +
                      "    Quadra " + (v[3] / 128.0).ToString("+0.00;-0.00;0.00");
            }

            _buffer_label.Text = string.Join("      ", parts);
        }
    }
}
