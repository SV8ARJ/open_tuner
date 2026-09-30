using System;
using System.Drawing;
using System.Windows.Forms;
using opentuner.Utilities;
using Serilog;

namespace opentuner.MediaSources.Minitiouner
{
    // One tuner's group on the "Expert" tab, top to bottom: I/Q constellation, four gauges (Carrier Lock,
    // SR Lock, RF Power, C/N MER), lock time and the DSTATUS / DSTATUS2 fields of the demodulator as LEDs.
    public class ExpertTunerView
    {
        private const int RawLogIntervalMs = 2000;
        private const double TmgLockThFall = 8; // TMGTHFALL as set in stv0910_regs_init (0x08): lock is lost below this level
        private const int TopPadding = 7; // was 20: everything sits 13 px higher, the group height stays as it was so the last row fits
        private const int FaultHoldMs = 1500; // DSTATUS2 fault bits are cleared by the read, so keep them visible for a moment

        private readonly string _title;
        private readonly CustomGroupBox _group;
        private readonly ConstellationControl _constellation = new ConstellationControl();
        private readonly GaugeControl _carrier_gauge = new GaugeControl("Carrier Lock", 0, 100, 20, 2);
        private readonly GaugeControl _sr_gauge = new GaugeControl("SR Lock", 0, 60, 10, 2);
        private readonly GaugeControl _rf_gauge = new GaugeControl("RF Power", -110, -10, 20, 2);
        private readonly GaugeControl _mer_gauge = new GaugeControl("C/N MER", -5, 25, 5, 5);   // -5 .. 25 dB: a strong signal (local generator) reads 15 dB and more, the pointer sat at the end stop

        private readonly Label _lock_time_label = new Label();
        private readonly Label _refresh_label = new Label();
        private readonly Label _ts_bitrate_label = new Label();
        private double _ts_bitrate_avg = double.NaN; // smoothed TSBITRATE raw value, NaN = not locked
        private readonly PeakBar _ldpc_bar = new PeakBar("LDPC Iterations", 32);
        private readonly PeakBar _ldpc_errors_bar = new PeakBar("LDPC Errors", 16);
        private readonly TraceBar _noise_bar = new TraceBar("Noise", " %");
        private readonly Label _verror_label = new Label();
        private readonly Label _agc_label = new Label();

        // DSTATUS
        private readonly LedControl _car_lock_led = new LedControl("CAR_LOCK");
        private readonly LedControl _tmg_quality_led = new LedControl("TMGLOCK_QUALITY");
        private readonly LedControl _lock_definitif_led = new LedControl("LOCK_DEFINITIF");
        private readonly LedControl _ovadc_led = new LedControl("OVADC_DETECT");
        // PDELSTATUS1 / PLHMODCOD / BCHERR
        private readonly LedControl _pkt_lock_led = new LedControl("PKTDELIN_LOCK");
        private readonly LedControl _first_lock_led = new LedControl("FIRST_LOCK");
        private readonly LedControl _bch_flag_led = new LedControl("BCH_ERROR_FLAG");
        private readonly LedControl _specinv_led = new LedControl("SPECINV_DEMOD");
        private readonly Label _bch_label = new Label();
        // DSTATUS2
        private readonly LedControl _delock_led = new LedControl("DEMOD_DELOCK");
        private readonly LedControl _cfr_led = new LedControl("CFR_OVERFLOW");
        private readonly LedControl _gamma_led = new LedControl("GAMMA_OVERUNDER");

        private readonly long[] _fault_until = new long[3]; // CFR, GAMMA, BCH_ERROR_FLAG
        private long _last_raw_log = 0;
        private double _last_cn_needed_db = double.NaN;

        // colour of the border, Color.Empty for the normal one (tuner selected by a click on its video)
        public void SetHighlight(Color color)
        {
            _group.HighlightColor = color;
        }

        public ExpertTunerView(string title, Control parent)
        {
            _title = title;

            _group = new CustomGroupBox();
            _group.Dock = DockStyle.Top;
            _group.Height = 760; // starting value, FitHeight() sets it from the content
            _group.Text = title;
            _group.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            _group.Padding = new Padding(8, TopPadding, 8, 8);

            // Docking is evaluated last-added first, so the controls are added bottom to top.
            var tips = new ToolTip();
            tips.ShowAlways = true;

            _bch_label.Dock = DockStyle.Top;
            _bch_label.Height = 26;
            _bch_label.Text = "BCHERR:  -";
            _bch_label.TextAlign = ContentAlignment.MiddleLeft;
            tips.SetToolTip(_bch_label, "BCHERR register (chip-wide, the same value for both tuners): ERRORFLAG = comparison flag between the counter and the corrected error number, COUNTER = degree of the BCH error location polynomial of the current output frame (max 10 for 2/3 and 5/6, 8 for 8/9 and 9/10, 12 otherwise)");
            _group.Controls.Add(_bch_label);

            var stream = new FlowLayoutPanel();
            stream.Dock = DockStyle.Top;
            stream.AutoSize = true;
            stream.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            stream.SizeChanged += (s, e) => FitHeight();
            stream.Padding = new Padding(2, 2, 0, 0);
            AddLed(stream, tips, _pkt_lock_led, "PDELSTATUS1[1] PKTDELIN_LOCK: packet delineator locked");
            AddLed(stream, tips, _first_lock_led, "PDELSTATUS1[0] FIRST_LOCK: packet delineator locked and processing frames (functioning normally)");
            AddLed(stream, tips, _bch_flag_led, "PDELSTATUS1[3] BCH_ERROR_FLAG: a BCH error occurred in one of the previous frames (cleared by the read)");
            AddLed(stream, tips, _specinv_led, "PLHMODCOD[7] SPECINV_DEMOD: the demodulator found the spectrum inverted and corrects it (information, not an error)");
            _group.Controls.Add(stream);

            var stream_header = new Label();
            stream_header.Dock = DockStyle.Top;
            stream_header.Height = 20;
            stream_header.Text = "PDELSTATUS1 / PLHMODCOD / BCHERR";
            stream_header.Font = new Font(_group.Font, FontStyle.Bold);
            stream_header.TextAlign = ContentAlignment.BottomLeft;
            _group.Controls.Add(stream_header);

            var status = new FlowLayoutPanel();
            status.Dock = DockStyle.Top;
            status.AutoSize = true;
            status.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            status.SizeChanged += (s, e) => FitHeight();
            status.Padding = new Padding(2, 2, 0, 0);
            AddLed(status, tips, _car_lock_led, "DSTATUS[7] CAR_LOCK: carrier lock");
            AddLed(status, tips, _tmg_quality_led, "DSTATUS[6:5] TMGLOCK_QUALITY: 00 timing not locked, 01 in process of being locked, 1x locked");
            AddLed(status, tips, _lock_definitif_led, "DSTATUS[3] LOCK_DEFINITIF: demodulator locked - the official locking indicator");
            AddLed(status, tips, _ovadc_led, "DSTATUS[0] OVADC_DETECT: persistent ADC overflow (more than 1/16 of the samples)");
            AddLed(status, tips, _delock_led, "DSTATUS2[7] DEMOD_DELOCK: LOCK_DEFINITIF went through zero - the lock was lost since the last lock (cleared by a write at every new lock, stays orange while the lock is missing after a loss)");
            AddLed(status, tips, _cfr_led, "DSTATUS2[1] CFR_OVERFLOW: carrier frequency register reached the limit of CFRUP, CFRLOW or the tuner range (cleared by the read)");
            AddLed(status, tips, _gamma_led, "DSTATUS2[0] GAMMA_OVERUNDER: SFR reached the limit of SFRmin or SFRmax (cleared by the read)");
            _group.Controls.Add(status);

            var header = new Label();
            header.Dock = DockStyle.Top;
            header.Height = 20;
            header.Text = "DSTATUS / DSTATUS2";
            header.Font = new Font(_group.Font, FontStyle.Bold);
            header.TextAlign = ContentAlignment.BottomLeft;
            _group.Controls.Add(header);

            // VERROR (left, DVB-S only) and the two AGC gain words (right) share one row
            var verror_row = new TableLayoutPanel();
            verror_row.Dock = DockStyle.Top;
            verror_row.Height = 26;
            verror_row.RowCount = 1;
            verror_row.ColumnCount = 2;
            verror_row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            verror_row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            verror_row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _verror_label.Dock = DockStyle.Fill;
            _verror_label.Margin = new Padding(0);
            _verror_label.Font = new Font("Microsoft Sans Serif", 8f);
            _verror_label.Text = "VERROR (Viterbi):  -";
            _verror_label.TextAlign = ContentAlignment.MiddleLeft;
            _agc_label.Dock = DockStyle.Fill;
            _agc_label.Margin = new Padding(0);
            _agc_label.Font = new Font("Microsoft Sans Serif", 8f);
            _agc_label.Text = "AGC1  -    AGC2  -";
            _agc_label.TextAlign = ContentAlignment.MiddleLeft;
            tips.SetToolTip(_agc_label, "AGC1 = gain word of the tuner (RF) loop, AGC2 = AGC2I, the gain of the demodulator's channel loop behind the Nyquist filter: 0x0000 minimum, 0xFFFF maximum amplification. " +
                                        "AGC1 follows the total power in the tuner passband and moves for strong input; AGC2 climbs when little signal is left in the channel (weak signal or none). " +
                                        "The RF Power table uses AGC1 above about -70 dBm and AGC2 below (AGC1 = 0)");
            verror_row.Controls.Add(_verror_label, 0, 0);
            verror_row.Controls.Add(_agc_label, 1, 0);
            _group.Controls.Add(verror_row);

            // LDPC iterations, LDPC errors and noise share one row
            var ldpc_row = new TableLayoutPanel();
            ldpc_row.Dock = DockStyle.Top;
            ldpc_row.Height = 44;
            ldpc_row.RowCount = 1;
            ldpc_row.ColumnCount = 3;
            for (int col = 0; col < 3; col++)
                ldpc_row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            ldpc_row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _ldpc_bar.Dock = DockStyle.Fill;
            _ldpc_bar.Margin = new Padding(0, 0, 2, 0);
            _ldpc_errors_bar.Dock = DockStyle.Fill;
            _ldpc_errors_bar.Margin = new Padding(2, 0, 2, 0);
            _noise_bar.Dock = DockStyle.Fill;
            _noise_bar.Margin = new Padding(2, 0, 0, 0);
            tips.SetToolTip(_ldpc_bar, "STATUSITER: LDPC iterations of the last frame (bar), red marker = STATUSMAXITER, held for 3 s");
            tips.SetToolTip(_ldpc_errors_bar, "LDPCERR: LDPC errors of the current output frame (bar, chip-wide), red marker = highest value, held for 3 s");
            ldpc_row.Controls.Add(_ldpc_bar, 0, 0);
            ldpc_row.Controls.Add(_ldpc_errors_bar, 1, 0);
            tips.SetToolTip(_noise_bar, "Noise amplitude relative to the signal amplitude (lower is better): NNOSPLHT (measured on PLHeader and pilots) in DVB-S2, NNOSDATAT (measured on the data) in DVB-S; 0x4000 = 100 % = noise as strong as the signal. Trace of the last ~30 s, the vertical scale follows its minimum and maximum (shown in brackets). The chip filters this value heavily, so it moves slowly");
            ldpc_row.Controls.Add(_noise_bar, 2, 0);

            // A bar that is too narrow for title and value puts the value on a second line and needs more height: the
            // row follows the highest of the three, everything below it moves down and the group grows with it.
            Action fit_ldpc_row = () =>
                ldpc_row.Height = Math.Max(_ldpc_bar.PreferredHeight, Math.Max(_ldpc_errors_bar.PreferredHeight, _noise_bar.PreferredHeight));
            _ldpc_bar.PreferredHeightChanged += (s, e) => fit_ldpc_row();
            _ldpc_errors_bar.PreferredHeightChanged += (s, e) => fit_ldpc_row();
            _noise_bar.PreferredHeightChanged += (s, e) => fit_ldpc_row();
            ldpc_row.SizeChanged += (s, e) => FitHeight();

            _group.Controls.Add(ldpc_row);

            // Lock Time (left) and Refresh Time of the status polling (right, the same for both tuners) share one row
            var lock_row = new TableLayoutPanel();
            lock_row.Dock = DockStyle.Top;
            lock_row.Height = 26;
            lock_row.RowCount = 1;
            lock_row.ColumnCount = 3;
            for (int col = 0; col < 3; col++)
                lock_row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 3f));
            lock_row.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _lock_time_label.Dock = DockStyle.Fill;
            _lock_time_label.Margin = new Padding(0);
            _lock_time_label.Text = "Lock Time:  -";
            _lock_time_label.TextAlign = ContentAlignment.MiddleLeft;
            _refresh_label.Dock = DockStyle.Fill;
            _refresh_label.Margin = new Padding(0);
            _refresh_label.Text = "Refresh Time:  -";
            _refresh_label.TextAlign = ContentAlignment.MiddleLeft;
            _ts_bitrate_label.Dock = DockStyle.Fill;
            _ts_bitrate_label.Margin = new Padding(0);
            _ts_bitrate_label.Text = "TS Bitrate:  -";
            _ts_bitrate_label.TextAlign = ContentAlignment.MiddleLeft;
            tips.SetToolTip(_ts_bitrate_label, "TSBITRATE register: raw bit rate of the stream leaving the packet delineator = 135 MHz * TSFIFO_BITRATE / 16384 (one step is 8.24 kbit/s), averaged over a few seconds");
            foreach (var label in new Label[] { _lock_time_label, _refresh_label, _ts_bitrate_label })
                label.Font = new Font("Microsoft Sans Serif", 8f);
            lock_row.Controls.Add(_lock_time_label, 0, 0);
            lock_row.Controls.Add(_refresh_label, 1, 0);
            lock_row.Controls.Add(_ts_bitrate_label, 2, 0);
            _group.Controls.Add(lock_row);

            var gauges = new TableLayoutPanel();
            gauges.Dock = DockStyle.Top;
            gauges.Height = 150;
            gauges.RowCount = 1;
            gauges.ColumnCount = 4;
            for (int c = 0; c < 4; c++)
                gauges.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
            gauges.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            var all = new GaugeControl[] { _carrier_gauge, _sr_gauge, _rf_gauge, _mer_gauge };
            for (int c = 0; c < all.Length; c++)
            {
                all[c].Dock = DockStyle.Fill;
                all[c].Margin = new Padding(2);
                gauges.Controls.Add(all[c], c, 0);
            }
            _group.Controls.Add(gauges);

            _sr_gauge.SetBand(0, TmgLockThFall, Color.Orange); // below TMGTHFALL the demodulator declares the timing lock lost

            _constellation.Dock = DockStyle.Top;
            _constellation.Height = 240;
            _group.Controls.Add(_constellation);

            _group.SizeChanged += (s, e) => FitHeight(); // the LEDs wrap differently when the width changes

            parent.Controls.Add(_group);
            _group.BringToFront(); // stacks the groups top to bottom in creation order
            FitHeight();
        }

        // Group height = padding + everything docked at the top, so nothing is cut off or left empty at the bottom.
        // The 20 px the top padding used to have are still counted, so the height is unchanged.
        private void FitHeight()
        {
            int height = _group.Padding.Vertical + (20 - TopPadding) + 4;
            foreach (Control child in _group.Controls)
            {
                if (child.Dock == DockStyle.Top)
                    height += child.Height;
            }

            if (_group.Height != height)
                _group.Height = height;
        }

        private static void AddLed(FlowLayoutPanel panel, ToolTip tips, LedControl led, string tip)
        {
            led.Margin = new Padding(0, 0, 0, 2);
            tips.SetToolTip(led, tip);
            panel.Controls.Add(led);
        }

        // demod_status: 2 = DVB-S2 locked, 3 = DVB-S locked (stv0910.DEMOD_S2 / DEMOD_S).
        // dstatus / dstatus2: the DSTATUS / DSTATUS2 registers. ldi: carrier lock indicator accumulator
        // (signed), tmglock: timing lock indicator accumulator (16 bit). constellation: 16 (I, Q) samples
        // or null. lock_time_ms: software-measured time to lock, -1 = not locked yet. cn_needed_db: C/N the
        // received MODCOD needs (NaN = unknown).
        // master clock of the demodulator this view belongs to (from the status), for the TS bit rate
        public uint MclkHz { get; set; } = 135000000;

        public void Update(byte demod_status, short rf_dbm, double mer_db, byte dstatus, byte dstatus2, sbyte ldi, ushort tmglock,
                           uint symbol_rate, byte[,] constellation, double lock_time_ms, double cn_needed_db,
                           byte ldpc_iterations, byte ldpc_max_iterations, uint viterbi_error_rate,
                           byte pdelstatus1, bool spectrum_inverted, byte bcherr, uint ldpc_errors, uint refresh_ms, ushort noise, ushort ts_bitrate_raw, ushort agc1, ushort agc2)
        {
            bool locked = demod_status == stv0910.DEMOD_S || demod_status == stv0910.DEMOD_S2;
            long now = Environment.TickCount64;

            // constellation and gauges
            _constellation.AddSamples(constellation, constellation != null);
            _rf_gauge.SetValue(rf_dbm, rf_dbm.ToString() + " dBm");

            // Carrier Lock and SR Lock show the lock indicators while the demodulator searches, too: first the timing
            // level (SR Lock) rises, then the carrier indicator, and only then it locks and the C/N is valid
            double carrier_percent = CarrierLockPercent(ldi);
            double timing_level = TimingLockLevel(tmglock);
            _carrier_gauge.SetValue(carrier_percent, carrier_percent.ToString("N0") + " %");
            _sr_gauge.SetValue(timing_level, timing_level.ToString("N1"));

            if (locked)
            {
                _mer_gauge.SetValue(mer_db, mer_db.ToString("N1") + " dB");
                if (!double.IsNaN(cn_needed_db))
                    _last_cn_needed_db = cn_needed_db; // kept while a dummy frame (MODCOD 0) is reported in between
                _mer_gauge.SetBand(-5, _last_cn_needed_db, Color.Orange); // scale below the C/N the current MODCOD needs
            }
            else
            {
                _mer_gauge.SetValue(-5, "-");
                _last_cn_needed_db = double.NaN;
                _mer_gauge.SetBand(double.NaN, double.NaN, Color.Orange);
            }


            SetText(_lock_time_label, "Lock Time:  " + LockTimeText(lock_time_ms));
            _ldpc_bar.SetValue(demod_status == stv0910.DEMOD_S2, ldpc_iterations, ldpc_max_iterations);
            int errors = (int)Math.Min(ldpc_errors, 65535u);
            _ldpc_errors_bar.SetValue(demod_status == stv0910.DEMOD_S2, errors, errors);
            _noise_bar.SetValue(locked, noise * 100.0 / 0x4000);
            if (locked && ts_bitrate_raw > 0)
                _ts_bitrate_avg = double.IsNaN(_ts_bitrate_avg) ? ts_bitrate_raw : _ts_bitrate_avg * 0.9 + ts_bitrate_raw * 0.1;
            else if (!locked)
                _ts_bitrate_avg = double.NaN;
            SetText(_ts_bitrate_label, double.IsNaN(_ts_bitrate_avg)
                ? "TS Bitrate:  -"
                : "TS Bitrate:  " + (_ts_bitrate_avg * (MclkHz / 1e6) / 16384.0).ToString("N3") + " Mb/s");
            SetText(_refresh_label, "Refresh Time:  " + refresh_ms + " ms");

            // VERROR: error rate seen by the Viterbi decoder, DVB-S (not S2) only. viterbi_error_rate is in 1/100 %.
            SetText(_verror_label, demod_status == stv0910.DEMOD_S
                ? "VERROR (Viterbi):  " + (viterbi_error_rate / 100.0).ToString("N2") + " %"
                : "VERROR (Viterbi):  -   (DVB-S only)");

            SetText(_agc_label, "AGC1  " + agc1 + "    AGC2  " + agc2);

            // DSTATUS: green = set / good
            _car_lock_led.Set((dstatus & 0x80) != 0 ? Color.LimeGreen : LedControl.OffColor);

            int timing_quality = (dstatus >> 5) & 0x03; // 00 not locked, 01 locking, 1x locked
            _tmg_quality_led.Set(timing_quality >= 2 ? Color.LimeGreen : timing_quality == 1 ? Color.Orange : LedControl.OffColor,
                                 "TMGLOCK_QUALITY: " + (timing_quality >= 2 ? "locked" : timing_quality == 1 ? "locking" : "not locked"));

            _lock_definitif_led.Set((dstatus & 0x08) != 0 ? Color.LimeGreen : LedControl.OffColor);
            _ovadc_led.Set((dstatus & 0x01) != 0 ? Color.Red : LedControl.OffColor);

            // DSTATUS2: DEMOD_DELOCK stays set until a write (NimThread writes it at every new lock, orange = "lock lost since the last lock"), the
            // failure bits 3..0 are cleared by every read, so a hit is held visible for a moment
            _delock_led.Set((dstatus2 & 0x80) != 0 ? Color.Orange : LedControl.OffColor);
            SetFault(0, _cfr_led, (dstatus2 & 0x02) != 0, now);
            SetFault(1, _gamma_led, (dstatus2 & 0x01) != 0, now);

            // PDELSTATUS1: BCH_ERROR_FLAG is cleared by the read, so a hit is held visible for a moment
            _pkt_lock_led.Set((pdelstatus1 & 0x02) != 0 ? Color.LimeGreen : LedControl.OffColor);
            _first_lock_led.Set((pdelstatus1 & 0x01) != 0 ? Color.LimeGreen : LedControl.OffColor);
            SetFault(2, _bch_flag_led, locked && (pdelstatus1 & 0x08) != 0, now);
            _specinv_led.Set(locked && spectrum_inverted ? Color.DodgerBlue : LedControl.OffColor);
            SetText(_bch_label, locked
                ? "BCHERR:  counter " + (bcherr & 0x0F) + ",  ERRORFLAG " + ((bcherr >> 4) & 1) + "   (chip-wide)"
                : "BCHERR:  -");

            LogRawValues(demod_status, dstatus, dstatus2, ldi, tmglock, symbol_rate, now);
        }

        private void SetFault(int index, LedControl led, bool set, long now)
        {
            if (set)
                _fault_until[index] = now + FaultHoldMs;

            led.Set(now < _fault_until[index] ? Color.Red : LedControl.OffColor);
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

        // ms below one second, otherwise seconds. Software-measured, so the resolution is the poll interval.
        private static string LockTimeText(double lock_time_ms)
        {
            if (lock_time_ms < 0)
                return "-";

            return lock_time_ms < 1000 ? Math.Round(lock_time_ms).ToString("N0") + " ms" : (lock_time_ms / 1000.0).ToString("N2") + " s";
        }

        // PROVISIONAL - MiniTioune's own conversion is unknown. LDI is a signed 8 bit accumulator that
        // is "maximised when locked" (datasheet 5.10; lock above LDT = -48, lost below LDT2 = -72), so
        // -128..+127 is mapped to 0..100 %. To be calibrated against MiniTioune on the same signal.
        private static double CarrierLockPercent(sbyte ldi)
        {
            return Math.Max(0, Math.Min(100, (ldi + 128) * 100.0 / 255.0));
        }

        // TMGLOCK is the signed 16 bit timing lock indicator accumulator. The demodulator compares its high byte
        // (TMGLOCK_LEVEL[15:8], signed) with TMGTHRISE (0x1E, lock) and TMGTHFALL (0x08, lock lost) to get the 2 bit
        // TMGLOCK_QUALITY of DSTATUS. Measured at lock: 0x1C00..0x20DB = 28..32; unlocked it is negative or small.
        // The gauge shows the level in units of that high byte, with fractions.
        private static double TimingLockLevel(ushort tmglock)
        {
            return unchecked((short)tmglock) / 256.0;
        }

        // Raw values every 2 s at debug level (--debuglevel 1) so the conversions above can be
        // calibrated against MiniTioune by comparing its gauges with these numbers.
        private void LogRawValues(byte demod_status, byte dstatus, byte dstatus2, sbyte ldi, ushort tmglock, uint symbol_rate, long now)
        {
            if (now - _last_raw_log < RawLogIntervalMs)
                return;

            _last_raw_log = now;
            Log.Debug("Expert " + _title + ": demod_status=" + demod_status + ", DSTATUS=0x" + dstatus.ToString("X2") +
                      ", DSTATUS2=0x" + dstatus2.ToString("X2") + ", LDI=" + ldi + " (0x" + ((byte)ldi).ToString("X2") +
                      "), TMGLOCK=" + tmglock + " (0x" + tmglock.ToString("X4") + "), SR=" + symbol_rate);
        }
    }
}
