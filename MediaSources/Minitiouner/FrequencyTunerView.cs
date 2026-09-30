using System;
using System.Drawing;
using System.Windows.Forms;
using opentuner.Utilities;
using Serilog;

namespace opentuner.MediaSources.Minitiouner
{
    // One tuner's group on the "Frequency" tab: how far the demodulator's derotator had to pull the
    // carrier (derotator bar over the search range CFRLOW..CFRUP), the measured symbol rate and the two
    // tuning trims: the capture range of the derotator and a frequency correction for the tuner.
    public class FrequencyTunerView
    {
        // capture range steps in kHz on each side of the tuned frequency, 0 = automatic (1.5 x symbol rate)
        private static readonly uint[] CaptureSteps = { 0, 100, 200, 300, 500, 750, 1000, 1500, 2000, 3000, 5000 };
        private const int MaxCorrectionPpm = 250;        // slider range of the reference error correction in ppm
        private const int UnitsPerPpm = 10;              // slider unit = 0.1 ppm
        private const int MaxOffsetKHz = 100;
        private const int ApplyDelayMs = 600; // wait for the slider to rest before tuning again

        // Sign of the carrier offset (CFR) relative to the correction that removes it: the tuner is moved
        // by +CFR so that the derotator has nothing left to do. Flip to -1 if "Adopt CFR" makes it worse.
        private const int CfrSign = 1;

        private readonly CustomGroupBox _group;
        private readonly DerotatorBar _derotator = new DerotatorBar();
        private readonly CarrierTrace _trace = new CarrierTrace();
        private readonly Label _found_label = new Label();
        private readonly Label _carrier_offset_label = new Label();
        private readonly Label _symbol_rate_label = new Label();

        private readonly TrackBar _capture_bar = new TrackBar();
        private readonly Label _capture_label = new Label();
        private readonly TrackBar _correction_bar = new TrackBar();
        private readonly Label _correction_label = new Label();
        private readonly TrackBar _offset_bar = new TrackBar();      // manual offset in kHz on top of the ppm correction
        private readonly Label _offset_label = new Label();
        private readonly Timer _apply_timer = new Timer();
        private bool _loading = false;
        private int _last_carrier_offset_hz = 0;
        private int _cfr_sign = CfrSign;                 // turned round by AdoptCarrierOffset if the offset grows after an adopt
        private double _adopt_cfr_hz = double.NaN;         // carrier offset at the last adopt
        private long _adopt_time = 0;
        private long _last_log = 0;
        // signal search ("Find signal"): steps the correction through +-SearchRangeKHz and measures the channel level AGC2 after every step
        private const int SearchRangeKHz = 100;
        private const int SearchSettleMs = 1800;   // after a step: the tuner tunes, the acquisition starts at the centre of the derotator window
        private const int SearchDwellMs = 6000;    // the acquisition dwells there for some seconds first: the lowest AGC2 in this time counts
        private readonly Button _find_button = new Button();
        private readonly Label _agc2_label = new Label();
        private readonly Label _search_label = new Label();
        private readonly Timer _search_timer = new Timer();
        private readonly object _search_lock = new object();
        private bool _searching = false;
        private bool _search_setting = false;      // the search moves the slider itself
        private readonly System.Collections.Generic.List<int> _search_offsets_khz = new System.Collections.Generic.List<int>();
        private readonly System.Collections.Generic.List<int> _search_min = new System.Collections.Generic.List<int>();
        private int _search_index = -1;
        private int _search_center_units = 0;
        private long _search_step_start = 0;
        private int _step_min = int.MaxValue;
        private uint _requested_rate_ks = 0;
        // the Reset button goes back to this value (MinitiounerSettings.DefaultFreqCorrectionPpm), not to 0
        public double DefaultCorrectionPpm = 0;
        private double _if_khz = 0;       // tuner frequency, for the kHz equivalent of the correction and for Adopt CFR
        // Mirrors _correction_bar.Value, kept in sync via ValueChanged (fires on the UI thread for both user and
        // programmatic changes). Update() runs on the NIM worker thread and must never read _correction_bar.Value
        // directly - unlike a Label's Text, TrackBar.Value queries the native control and throws a cross-thread
        // InvalidOperationException off the UI thread. That exception is unhandled up to worker_thread(), killing
        // the NIM thread for good (tuning stops entirely) - but only when a debugger is attached, which is why this
        // was invisible running the plain .exe and only showed up under Visual Studio.
        private int _correction_units = 0;
        private int _offset_khz = 0;      // mirrors _offset_bar.Value, same reason as _correction_units above
        private bool _last_locked = false;
        private ushort _last_agc2 = ushort.MaxValue;
        private const int CarrierInChannelAgc2 = 40;   // AGC2 below this: the derotator sits on a carrier (empty channel 50 .. 170)

        // symbol rates offered as buttons at the top of the group (kS), narrow ones first
        private static readonly uint[] RateButtons = { 20, 25, 33, 66, 125, 250, 333, 500, 1000, 1500, 2000 };
        private readonly System.Collections.Generic.List<Button> _rate_buttons = new System.Collections.Generic.List<Button>();

        // button caption: 1000 / 1500 / 2000 kS as 1k / 1k5 / 2k so the buttons can stay narrow
        private static string RateText(uint rate)
        {
            if (rate >= 1000)
                return rate % 1000 == 0 ? (rate / 1000) + "k" : (rate / 1000) + "k" + ((rate % 1000) / 100);

            return rate.ToString();
        }

        // a rate button was clicked (kS)
        public event Action<uint> SymbolRateSelected;

        // capture range in kHz (0 = automatic) and correction in ppm, fired once the sliders rest
        public event Action<uint, double, int> TrimChanged;      // capture range kHz, correction ppm, manual offset kHz

        // colour of the border, Color.Empty for the normal one (tuner selected by a click on its video)
        public void SetHighlight(Color color)
        {
            _group.HighlightColor = color;
        }

        public FrequencyTunerView(string title, Control parent)
        {
            _group = new CustomGroupBox();
            _group.Dock = DockStyle.Top;
            _group.Height = 600;
            _group.Text = title;
            _group.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            _group.Padding = new Padding(8, 20, 8, 8);

            // Docking is evaluated last-added first, so the controls are added bottom to top.
            var buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Top;
            buttons.Height = 34;
            var adopt = new Button { Text = "Adopt CFR", Width = 100, Height = 26 };
            var reset = new Button { Text = "Reset", Width = 80, Height = 26 };
            var tips = new ToolTip { ShowAlways = true };
            tips.SetToolTip(adopt, "Adds the current carrier offset (CFR) to the correction, so the tuner is set onto the carrier and the derotator has nothing left to do. Use it while locked on a signal of known frequency, e.g. the QO-100 beacon. " +
                                     "Also works without a lock as long as the channel level AGC2 shows a carrier (below 40) and the CFR is steady - useful for the low symbol rates that do not lock yet.");
            tips.SetToolTip(reset, "Frequency correction back to the default (setting DefaultFreqCorrectionPpm, 0 ppm if not set)");
            adopt.Click += (s, e) => AdoptCarrierOffset();
            reset.Click += (s, e) => { _correction_bar.Value = (int)Math.Round(DefaultCorrectionPpm * UnitsPerPpm); _offset_bar.Value = 0; _adopt_cfr_hz = double.NaN; ApplyNow(); };
            _find_button.Text = "Find signal";
            _find_button.Width = 100;
            _find_button.Height = 26;
            tips.SetToolTip(_find_button, "Steps the frequency correction through +-100 kHz (about one signal width per step) and measures the channel level AGC2 at every step. A low AGC2 means the carrier is in the channel. " +
                                          "At the end the correction stays where AGC2 was lowest. Ends at once when the demodulator locks. Click again to stop.");
            _find_button.Click += (s, e) => ToggleSearch();
            buttons.Controls.Add(adopt);
            buttons.Controls.Add(reset);
            buttons.Controls.Add(_find_button);
            _group.Controls.Add(buttons);

            _search_label.Dock = DockStyle.Top;
            _search_label.Height = 22;
            _search_label.Text = "";
            _search_label.TextAlign = ContentAlignment.MiddleLeft;
            _group.Controls.Add(_search_label);

            _search_timer.Interval = 250;
            _search_timer.Tick += (s, e) => SearchTick();

            AddTrimRow(_correction_label, _correction_bar, -MaxCorrectionPpm * UnitsPerPpm, MaxCorrectionPpm * UnitsPerPpm, 5 * UnitsPerPpm,
                       "Reference (crystal) error of the tuner in ppm of the tuner frequency: the tuner is set that far off, so the correction in kHz grows with the frequency " +
                       "(MiniTioune: ppm calib). Mouse wheel 0.5 ppm, Ctrl 5 ppm. The displayed frequency stays the nominal one.", tips, UnitsPerPpm / 2);
            AddTrimRow(_offset_label, _offset_bar, -MaxOffsetKHz, MaxOffsetKHz, 10,
                       "Manual frequency offset in kHz on top of the ppm correction, for the signal you are tuned to (the transmitter or the spectrum click is a few kHz off). " +
                       "Mouse wheel 1 kHz, Ctrl 10 kHz. It stays until you move it or press Reset, also for other frequencies.", tips, 1);
            AddTrimRow(_capture_label, _capture_bar, 0, CaptureSteps.Length - 1, 1,
                       "How far on each side of the tuned frequency the derotator searches for the carrier. Automatic is 1.5 x the symbol rate - too narrow for a low symbol rate on a drifting LNB, a wide range needs longer to lock.", tips);

            _symbol_rate_label.Dock = DockStyle.Top;
            _symbol_rate_label.Height = 26;
            _symbol_rate_label.Text = "Measured symbol rate:  -";
            _symbol_rate_label.TextAlign = ContentAlignment.MiddleLeft;
            _group.Controls.Add(_symbol_rate_label);

            _carrier_offset_label.Dock = DockStyle.Top;
            _carrier_offset_label.Height = 26;
            _carrier_offset_label.Text = "Carrier offset (CFR):  -";
            _carrier_offset_label.TextAlign = ContentAlignment.MiddleLeft;
            _group.Controls.Add(_carrier_offset_label);

            _agc2_label.Dock = DockStyle.Top;
            _agc2_label.Height = 22;
            _agc2_label.Text = "Channel level (AGC2):  -";
            _agc2_label.TextAlign = ContentAlignment.MiddleLeft;
            tips.SetToolTip(_agc2_label, "Gain of the demodulator's channel loop (AGC2): low (about 15 - 20) while the derotator is on a carrier, high (50 - 170) when the channel is empty. " +
                                         "It is the only measure of a signal while there is no lock.");
            _group.Controls.Add(_agc2_label);

            _found_label.Dock = DockStyle.Top;
            _found_label.Height = 26;
            _found_label.Text = "Freq found:  -";
            _found_label.TextAlign = ContentAlignment.MiddleLeft;
            tips.SetToolTip(_found_label, "The frequency the carrier is found at: the nominal frequency of the tuner plus the carrier offset (CFR) of the derotator. Like MiniTioune's \"Freq found\", also shown while the demodulator is still searching.");
            _group.Controls.Add(_found_label);

            _trace.Dock = DockStyle.Top;
            tips.SetToolTip(_trace, "Carrier offset (CFR) over time, newest on top. Yellow = the demodulator searches, green = locked. It runs without a lock, so a signal that does not lock can still be seen.");
            _group.Controls.Add(_trace);

            _derotator.Dock = DockStyle.Top;
            _group.Controls.Add(_derotator);

            // symbol rate buttons on top; the active (requested) rate is highlighted
            var rates = new FlowLayoutPanel();
            rates.Dock = DockStyle.Top;
            rates.Height = 34;
            rates.Padding = new Padding(0, 2, 0, 0);

            // In a narrow tab the buttons no longer fit in one line: the row wraps into a second one (2k / 1k5 were cut
            // off) and grows, everything below it (the derotator ...) moves down, and the group gets the extra height.
            rates.WrapContents = true;
            rates.AutoSize = true;
            rates.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            rates.MinimumSize = new Size(0, 34);
            rates.SizeChanged += (s, e) => _group.Height = 600 + Math.Max(0, rates.Height - 34);
            var rate_label = new Label { Text = "SR (kS):", AutoSize = false, Width = 56, Height = 26, TextAlign = ContentAlignment.MiddleLeft };
            rates.Controls.Add(rate_label);
            var rate_tips = new ToolTip { ShowAlways = true };
            foreach (uint rate in RateButtons)
            {
                var button = new Button { Text = RateText(rate), Tag = rate, Width = 37, Height = 26, Margin = new Padding(1, 0, 1, 0), FlatStyle = FlatStyle.Flat };
                button.Font = new Font("Microsoft Sans Serif", 8f);
                button.FlatAppearance.BorderColor = Color.Gray;
                button.Click += (s, e) => SymbolRateSelected?.Invoke((uint)((Button)s).Tag);
                rate_tips.SetToolTip(button, "Set the symbol rate of this tuner to " + rate + " kS");
                rates.Controls.Add(button);
                _rate_buttons.Add(button);
            }
            _group.Controls.Add(rates);

            _apply_timer.Interval = ApplyDelayMs;
            _apply_timer.Tick += (s, e) => ApplyNow();

            _capture_bar.ValueChanged += (s, e) => TrimEdited();
            _correction_bar.ValueChanged += (s, e) => { _correction_units = _correction_bar.Value; TrimEdited(); };
            _offset_bar.ValueChanged += (s, e) => { _offset_khz = _offset_bar.Value; TrimEdited(); };
            UpdateTrimLabels();

            parent.Controls.Add(_group);
            _group.BringToFront(); // stacks the groups top to bottom in creation order
        }

        // a label on the left and a slider on the right, added to the group as one row
        private void AddTrimRow(Label label, TrackBar bar, int min, int max, int ctrl_step, string tip, ToolTip tips, int notch_step = 1)
        {
            var row = new Panel();
            row.Dock = DockStyle.Top;
            row.Height = 40;

            label.Dock = DockStyle.Left;
            label.Width = 210;
            label.TextAlign = ContentAlignment.MiddleLeft;

            bar.Dock = DockStyle.Fill;
            bar.Minimum = min;
            bar.Maximum = max;
            bar.TickStyle = TickStyle.None;
            bar.AutoSize = false;
            bar.SmallChange = 1;
            bar.LargeChange = Math.Max(1, ctrl_step);

            // Mouse wheel: exactly one step per notch (the default scrolls several lines per notch, which
            // was 6 kHz for the correction), Ctrl = ctrl_step per notch. Wheel up = larger value.
            bar.MouseWheel += (s, e) =>
            {
                if (e is HandledMouseEventArgs handled)
                    handled.Handled = true;

                int notches = e.Delta / 120;
                int step = (Control.ModifierKeys & Keys.Control) != 0 ? ctrl_step : notch_step;
                bar.Value = Math.Max(bar.Minimum, Math.Min(bar.Maximum, bar.Value + notches * step));
            };

            tips.SetToolTip(bar, tip);
            tips.SetToolTip(label, tip);

            row.Controls.Add(bar);   // Fill first, the label docks to the left of it
            row.Controls.Add(label);
            _group.Controls.Add(row);
        }

        // Sets the sliders from the stored values without firing TrimChanged.
        public void SetTrim(uint capture_range_khz, double correction_ppm, int offset_khz)
        {
            _loading = true;
            try
            {
                int step = 0;
                for (int n = 0; n < CaptureSteps.Length; n++)
                {
                    if (Math.Abs((int)CaptureSteps[n] - (int)capture_range_khz) < Math.Abs((int)CaptureSteps[step] - (int)capture_range_khz))
                        step = n;
                }

                _capture_bar.Value = step;
                _correction_bar.Value = Math.Max(-MaxCorrectionPpm * UnitsPerPpm, Math.Min(MaxCorrectionPpm * UnitsPerPpm, (int)Math.Round(correction_ppm * UnitsPerPpm)));
                _offset_bar.Value = Math.Max(-MaxOffsetKHz, Math.Min(MaxOffsetKHz, offset_khz));
                UpdateTrimLabels();
            }
            finally
            {
                _loading = false;
            }
        }

        // Highlights the button of the symbol rate the tuner is set to (kS). May be called from any thread.
        public void SetRequestedRate(uint symbol_rate)
        {
            _requested_rate_ks = symbol_rate;

            if (_rate_buttons.Count == 0 || !_group.IsHandleCreated || _group.IsDisposed)
                return;

            try
            {
                _group.BeginInvoke((MethodInvoker)(() =>
                {
                    foreach (var button in _rate_buttons)
                        button.BackColor = (uint)button.Tag == symbol_rate ? Color.FromArgb(255, 204, 128) : SystemColors.Control;
                }));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException)
            {
            }
        }

        private void TrimEdited()
        {
            UpdateTrimLabels();

            if (_search_setting)
                return;

            if (_searching)
                EndSearch("stopped: slider moved", false);

            if (_loading)
                return;

            _apply_timer.Stop();
            _apply_timer.Start(); // (re)start: tune once the slider has rested
        }

        private void ApplyNow()
        {
            _apply_timer.Stop();
            TrimChanged?.Invoke(CaptureSteps[_capture_bar.Value], _correction_bar.Value / (double)UnitsPerPpm, _offset_bar.Value);
        }

        private void AdoptCarrierOffset()
        {
            bool carrier_only = !_last_locked;

            if (_if_khz <= 0 || (carrier_only && _last_agc2 >= CarrierInChannelAgc2))
            {
                _search_label.Text = "Adopt CFR: needs a lock or a carrier in the channel (AGC2 < " + CarrierInChannelAgc2 + ")";
                return;
            }

            long now = Environment.TickCount64;

            // The last adopt should have brought the offset towards 0. If it is clearly larger now, the sign is wrong (for example
            // with I/Q swap): turn it round before this adopt.
            if (!carrier_only && !double.IsNaN(_adopt_cfr_hz) && now - _adopt_time < 90000 && Math.Abs(_last_carrier_offset_hz) > Math.Abs(_adopt_cfr_hz) * 1.3 + 500)
            {
                _cfr_sign = -_cfr_sign;
                Log.Information("Adopt CFR " + _group.Text + ": offset grew from " + _adopt_cfr_hz + " to " + _last_carrier_offset_hz + " Hz, sign turned to " + _cfr_sign);
            }

            double old_ppm = _correction_bar.Value / (double)UnitsPerPpm;
            double delta_ppm = _cfr_sign * _last_carrier_offset_hz / (_if_khz * 1000.0) * 1e6;
            int units = _correction_bar.Value + (int)Math.Round(delta_ppm * UnitsPerPpm);
            _correction_bar.Value = Math.Max(-MaxCorrectionPpm * UnitsPerPpm, Math.Min(MaxCorrectionPpm * UnitsPerPpm, units));

            _adopt_cfr_hz = _last_carrier_offset_hz;
            _adopt_time = now;
            _search_label.Text = "Adopt CFR: " + _last_carrier_offset_hz + " Hz" + (carrier_only ? " (no lock, AGC2 " + _last_agc2 + ")" : "");
            Log.Information("Adopt CFR " + _group.Text + (carrier_only ? " without lock (AGC2 " + _last_agc2 + ")" : "") + ": CFR " + _last_carrier_offset_hz + " Hz, IF " + _if_khz + " kHz, sign " + _cfr_sign +
                            ", correction " + old_ppm.ToString("0.0") + " -> " + (_correction_bar.Value / (double)UnitsPerPpm).ToString("0.0") + " ppm");
            ApplyNow();
        }

        // ---- signal search --------------------------------------------------------------------------------------------------
        private int KHzToUnits(double khz)
        {
            return (int)Math.Round(khz * 1e6 / _if_khz * UnitsPerPpm);   // ppm = kHz * 1e6 / tuner frequency in kHz, slider unit 0.1 ppm
        }

        private void ToggleSearch()
        {
            if (_searching)
            {
                EndSearch("stopped", true);
                return;
            }

            if (_if_khz <= 0)
            {
                _search_label.Text = "Find signal: no tuner frequency yet";
                return;
            }

            uint rate = _requested_rate_ks > 0 ? _requested_rate_ks : 25;
            int step = (int)Math.Max(8, Math.Min(40, rate));       // about one signal width, the windows overlap a little

            _search_offsets_khz.Clear();
            _search_min.Clear();
            _search_offsets_khz.Add(0);
            for (int k = step; k <= SearchRangeKHz; k += step)
            {
                _search_offsets_khz.Add(k);
                _search_offsets_khz.Add(-k);
            }

            _apply_timer.Stop();
            _search_center_units = _correction_bar.Value;
            _search_index = -1;
            _searching = true;
            _find_button.Text = "Stop search";
            Log.Information("Find signal " + _group.Text + ": " + _search_offsets_khz.Count + " steps of " + step + " kHz around " + (_search_center_units / (double)UnitsPerPpm).ToString("0.0") + " ppm, SR " + rate + " kS");
            SearchNextStep();
            _search_timer.Start();
        }

        private void SearchNextStep()
        {
            _search_index++;
            if (_search_index >= _search_offsets_khz.Count)
            {
                FinishSearch();
                return;
            }

            int units = _search_center_units + KHzToUnits(_search_offsets_khz[_search_index]);
            units = Math.Max(-MaxCorrectionPpm * UnitsPerPpm, Math.Min(MaxCorrectionPpm * UnitsPerPpm, units));

            _search_setting = true;
            _correction_bar.Value = units;
            _search_setting = false;

            lock (_search_lock)
            {
                _step_min = int.MaxValue;
                _search_step_start = Environment.TickCount64;
            }

            _search_label.Text = "Find signal " + (_search_index + 1) + "/" + _search_offsets_khz.Count + ":  " + (units / (double)UnitsPerPpm).ToString("+0.0;-0.0;0.0") + " ppm";
            ApplyNow();
        }

        private void SearchTick()
        {
            if (!_searching)
                return;

            if (_last_locked)
            {
                EndSearch("locked at " + (_correction_bar.Value / (double)UnitsPerPpm).ToString("+0.0;-0.0;0.0") + " ppm", false);
                return;
            }

            if (Environment.TickCount64 - _search_step_start < SearchSettleMs + SearchDwellMs)
                return;

            int min;
            lock (_search_lock)
                min = _step_min == int.MaxValue ? 99999 : _step_min;
            _search_min.Add(min);
            Log.Information("Find signal " + _group.Text + ": " + (_search_center_units + KHzToUnits(_search_offsets_khz[_search_index])) / (double)UnitsPerPpm + " ppm (" +
                            _search_offsets_khz[_search_index] + " kHz): lowest AGC2 " + min);
            SearchNextStep();
        }

        private void FinishSearch()
        {
            var sorted = new System.Collections.Generic.List<int>(_search_min);
            sorted.Sort();
            int median = sorted[sorted.Count / 2];
            int best = 0;
            for (int i = 1; i < _search_min.Count; i++)
            {
                if (_search_min[i] < _search_min[best])
                    best = i;
            }

            if (_search_min[best] < 0.6 * median)
            {
                int units = Math.Max(-MaxCorrectionPpm * UnitsPerPpm, Math.Min(MaxCorrectionPpm * UnitsPerPpm, _search_center_units + KHzToUnits(_search_offsets_khz[best])));
                _search_setting = true;
                _correction_bar.Value = units;
                _search_setting = false;
                EndSearch("signal near " + (units / (double)UnitsPerPpm).ToString("+0.0;-0.0;0.0") + " ppm (AGC2 " + _search_min[best] + ", empty channel about " + median + ")", true);
            }
            else
            {
                _search_setting = true;
                _correction_bar.Value = _search_center_units;
                _search_setting = false;
                EndSearch("no signal found (lowest AGC2 " + _search_min[best] + ", median " + median + "), correction back", true);
            }
        }

        // apply = tune with the slider value as it is now (after a search that found something or a restored value)
        private void EndSearch(string text, bool apply)
        {
            _search_timer.Stop();
            _searching = false;
            _find_button.Text = "Find signal";
            _search_label.Text = "Find signal: " + text;
            Log.Information("Find signal " + _group.Text + ": " + text);

            if (apply)
                ApplyNow();
        }

        private void UpdateTrimLabels()
        {
            uint capture = CaptureSteps[_capture_bar.Value];
            _capture_label.Text = capture == 0 ? "Capture range:  auto (1.5 x SR)" : "Capture range:  +-" + capture + " kHz";
            _correction_label.Text = CorrectionLabelText();
            _offset_label.Text = "Manual offset:  " + _offset_bar.Value.ToString("+0;-0;0") + " kHz";
        }

        // "+42.0 ppm (+48 kHz)": the kHz the tuner is moved by at the current tuner frequency
        private string CorrectionLabelText()
        {
            double ppm = _correction_units / (double)UnitsPerPpm;
            string text = "Frequency correction:  " + ppm.ToString("+0.0;-0.0;0.0") + " ppm";

            if (_if_khz > 0)
                text += "  (" + (ppm * _if_khz / 1e6).ToString("+0;-0;0") + " kHz)";

            return text;
        }

        // demod_status: 2 = DVB-S2 locked, 3 = DVB-S locked. carrier_offset_hz: CFR in Hz, carrier_low_hz /
        // carrier_up_hz: search range CFRLOW / CFRUP in Hz. symbol_rate: measured symbol rate in Hz.
        public void Update(byte demod_status, int carrier_offset_hz, int carrier_low_hz, int carrier_up_hz, uint symbol_rate, double nominal_khz, double if_khz, ushort agc2)
        {
            bool locked = demod_status == stv0910.DEMOD_S || demod_status == stv0910.DEMOD_S2;

            SetText(_agc2_label, "Channel level (AGC2):  " + agc2 + (agc2 < 40 ? "   (signal in the channel)" : ""));
            lock (_search_lock)
            {
                if (_searching && Environment.TickCount64 - _search_step_start >= SearchSettleMs)
                    _step_min = Math.Min(_step_min, agc2);
            }

            _if_khz = if_khz;
            SetText(_correction_label, CorrectionLabelText());

            _last_locked = locked;
            _last_agc2 = agc2;
            _last_carrier_offset_hz = carrier_offset_hz;

            long log_now = Environment.TickCount64;
            if (log_now - _last_log >= 2000)
            {
                _last_log = log_now;
                Log.Debug("Special " + _group.Text + ": " + (locked ? "locked" : "searching") + ", CFR " + carrier_offset_hz + " Hz, IF " + if_khz + " kHz, correction " +
                          (_correction_units / (double)UnitsPerPpm).ToString("0.0") + " ppm");
            }

            _derotator.SetValue(locked && carrier_up_hz > carrier_low_hz, carrier_offset_hz, carrier_low_hz, carrier_up_hz);

            _trace.SetValue(carrier_offset_hz, carrier_low_hz, carrier_up_hz, locked);

            // the carrier offset is valid without a lock too: while searching it is the frequency the derotator tries
            SetText(_carrier_offset_label, "Carrier offset (CFR):  " + (carrier_offset_hz / 1000.0).ToString("+0.000;-0.000;0.000") + " kHz" + (locked ? "" : "  (searching)"));
            SetText(_found_label, "Freq found:  " + (nominal_khz + _offset_khz + carrier_offset_hz / 1000.0).ToString("N1") + " kHz" + (locked ? "" : "  (searching)"));
            SetText(_symbol_rate_label, locked
                ? "Measured symbol rate:  " + (symbol_rate / 1000.0).ToString("N3") + " kS/s"
                : "Measured symbol rate:  -");
        }

        private static void SetText(Control control, string text)
        {
            if (control.Text == text || !control.IsHandleCreated || control.IsDisposed)
                return;

            try
            {
                control.BeginInvoke((MethodInvoker)(() => control.Text = text));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException)
            {
            }
        }
    }
}
