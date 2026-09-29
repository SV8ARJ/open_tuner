using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using opentuner.MediaSources.Minitiouner.HardwareInterfaces;
using opentuner.MediaSources.Minitiouner;
using Serilog;

namespace opentuner
{
    public delegate void SourceStatusCallback(TunerStatus status);

    public class NimThread
    {
        MTHardwareInterface hardware;

        nim _nim;
        stv0910 _stv0910;
        stv6120 _stv6120;
        stvvglna stvvglna_top;
        stvvglna stvvglna_bottom;

        ConcurrentQueue<TunerConfig> config_queue;
        private List<SourceStatusCallback> status_callback = null;

        bool lna_top_ok = false;
        bool lna_bottom_ok = false;
        bool reset = false;
        bool no_lna = false;

        // last applied config per tuner (index 0 = tuner 1 / T1P2, index 1 = tuner 2 / T2P1) -
        // used by the Digole display update in get_nim_status(), which otherwise has no
        // access to the currently tuned frequency/symbol rate (nim_config in worker_thread()
        // is a loop-local variable).
        private TunerConfig[] current_config = new TunerConfig[2];

        private DigoleDisplay digole;
        private bool digole_enabled;
        private string device_name;
        private string digole_callsign;
        private string digole_locator;
        private string digole_name;
        private bool digole_showed_greeting = false;
        private int digole_greeting_failures = 0;
        // First no-lock greeting after connect is labelled "START", later ones "NO SIGNAL".
        private bool digole_start_shown = false;

        // Service name (from TS/SDT parsing) and video codec (from the media player's
        // OpenCompleted event) live on different threads (ts_parser_thread / media player)
        // than NimThread's own I2C polling loop - MinitiounerProperties.cs pushes the latest
        // values here (plain reference assignment, atomic enough for a display refresh) so
        // update_digole_display() can read them without crossing threads itself.
        private string[] service_name = new string[] { "", "" };
        private string[] video_codec = new string[] { "", "" };

        public void UpdateServiceName(int tuner_index, string name)
        {
            if (tuner_index >= 0 && tuner_index < service_name.Length)
                service_name[tuner_index] = name;
        }

        public void UpdateVideoCodec(int tuner_index, string codec)
        {
            if (tuner_index >= 0 && tuner_index < video_codec.Length)
                video_codec[tuner_index] = codec;
        }

        // One-shot DiSEqC tone burst trigger (22kHz "TS" mode) - set from the UI thread,
        // consumed once and cleared in worker_thread()'s own polling loop so the actual I2C
        // sequence runs on the same thread as the rest of the NIM traffic.
        private volatile bool trigger_burst_0 = false;
        private volatile bool trigger_burst_1 = false;

        public void TriggerToneBurst(int tuner_index)
        {
            if (tuner_index == 0) trigger_burst_0 = true;
            else if (tuner_index == 1) trigger_burst_1 = true;
        }

        // "Stop TS" (issue #9) - same one-shot pattern as the tone burst above: set from the UI
        // thread, consumed and cleared on this thread so the DMDISTATE write runs alongside the
        // rest of the NIM I2C traffic instead of racing it from another thread.
        private volatile bool stop_tuner_0 = false;
        private volatile bool stop_tuner_1 = false;

        public void StopTuner(int tuner_index)
        {
            if (tuner_index == 0) stop_tuner_0 = true;
            else if (tuner_index == 1) stop_tuner_1 = true;
        }

        // Digole callsign/locator/name changed in the settings dialog while connected - takes
        // effect at once (the greeting is redrawn on the next poll if no signal is locked).
        public void UpdateDigoleIdentity(string callsign, string locator, string name)
        {
            digole_callsign = callsign;
            digole_locator = locator;
            digole_name = name;
            digole_showed_greeting = false;
        }

        // Which tuner's data to show on the Digole when BOTH are locked at once (only one
        // physical display for two tuners) - updated on every SetFrequency call, so the last
        // tuner the user actually selected (BATC spectrum click, tuner dialog, preset, ...)
        // wins. Doesn't override a genuine lock: if only the OTHER tuner is locked, that one
        // still shows (see update_digole_display) - this only breaks the tie between two
        // simultaneously locked tuners.
        private volatile int preferred_tuner = 0;

        public void SetPreferredTuner(int tuner_index)
        {
            preferred_tuner = tuner_index;
        }

        // Thread.Abort() doesn't exist on modern .NET (throws PlatformNotSupportedException) -
        // worker_thread() checks this cooperatively instead.
        private volatile bool _stopRequested = false;
        public void Stop() { _stopRequested = true; }

        // Guards every access to the shared FTDI I2C-channel state (MPSSEbuffer etc.) from
        // this thread's own hardware calls below. MinitiounerSource.Close() takes the same
        // lock before its own hw_ts_led/hw_set_polarization_supply calls, so LED/LNB shutoff
        // no longer has to wait for the full worker_thread() Join to finish (which could hang
        // for 20s+ on a stuck I2C retry, see MinitiounerSource.Close()'s comment) - it only
        // ever waits as long as whatever hardware call NimThread actually has in flight right
        // now, typically well under a second.
        public readonly object HwLock = new object();

        //byte current_demod = stv0910.STV0910_DEMOD_BOTTOM;

        // Offsets (LNB LO frequency etc., MinitiounerSettings.Offset1/Offset2) added back onto
        // the tuned IF frequency for display, so the Digole shows the real downlink frequency
        // (e.g. "10491500 kHz") rather than the internal IF value the STV6120 is actually set
        // to (current_config[i].frequency, e.g. 741525) - matches MinitiounerSource.GetFrequency's
        // offset_included=true behavior, which the main GUI itself uses for the same reason.
        private uint[] frequency_offsets;

        // The receiver settings of this board (low symbol rate profile, carrier algorithm, baseband gain ...) go to its own
        // demodulator/tuner object; call before the thread is started (they were static before, shared by all boards).
        // false: the LNB supply pins of this board are never touched (see MinitiounerSettings.LnbSupplyControl)
        public bool LnbSupplyControl { get; set; } = true;

        // number of tuners in use on this board: 2 for a Pro, 1 for a board with a single TS (V2, E-Tiouner)
        public int TunerCount { get; set; } = 2;

        public void ApplyReceiverSettings(MinitiounerSettings s)
        {
            LnbSupplyControl = s.LnbSupplyControl;
            _stv0910.AllowLowSrClock = s.LowSrClock;
            _stv0910.LowSrProfile = s.LowSrProfile;
            _stv0910.MiniTiouneInit = s.MiniTiouneInit;
            _stv0910.LowSrDvbS1 = s.LowSrDvbS1;
            _stv0910.LowSrSrScan = s.LowSrSrScan;
            _stv0910.LowSrManualSfr = s.LowSrManualSfr;
            _stv0910.CarrierPhaseAlgo = (byte)Math.Max(0, Math.Min(2, (int)s.CarrierPhaseAlgo));
            _stv0910.IqSwap = s.IqSwap;
            _stv6120.BasebandGainCode = (byte)Math.Max(0, Math.Min(8, s.BasebandGainDb / 2));
        }

        public NimThread(ConcurrentQueue<TunerConfig> _config_queue, MTHardwareInterface _hardware, SourceStatusCallback _status_callback, bool _no_lna, bool _enable_digole = false, byte _digole_i2c_address = 0x27, string _device_name = "", uint[] _frequency_offsets = null, string _digole_callsign = "", string _digole_locator = "", string _digole_name = "")
        {
            hardware = _hardware;
            config_queue = _config_queue;
            //status_callback = _status_callback;
            status_callback = new List<SourceStatusCallback>();
            status_callback.Add(_status_callback);

            _nim = new nim(hardware);

            _stv0910 = new stv0910(_nim);
            _stv6120 = new stv6120(_nim);
            no_lna = _no_lna;
            stvvglna_top = new stvvglna(_nim);
            stvvglna_bottom = new stvvglna(_nim);

            digole_enabled = _enable_digole;
            device_name = _device_name;
            digole_callsign = _digole_callsign;
            digole_locator = _digole_locator;
            digole_name = _digole_name;
            frequency_offsets = _frequency_offsets ?? new uint[] { 0, 0 };
            if (digole_enabled)
            {
                digole = new DigoleDisplay(hardware, _digole_i2c_address);
                Log.Information("Nim Thread: Digole enabled, callsign=\"" + digole_callsign + "\", locator=\"" + digole_locator + "\", name=\"" + digole_name + "\"");
            }
        }

        // Picks whichever tuner is currently locked (T1P2/"TUNER A" preferred, falling back to
        // T2P1/"TUNER B") and pushes its status to the Digole display - mirrors the single-tuner
        // layout of the existing MiniTioune Digole integration this is modeled on.
        private void update_digole_display(TunerStatus status)
        {
            // Initialize() always auto-tunes both channels to a fixed placeholder (741525 kHz
            // IF / 1500 KS/s) right at connect, before the user picks a real frequency via the
            // BATC spectrum click - so current_config[i] != null almost immediately and is not
            // a meaningful "user tuned in" signal. Gate the greeting on genuine demod lock
            // instead: it stays up through that meaningless placeholder tune (which won't lock
            // onto anything real) and only switches to status once a real signal is received.
            bool t1_locked = status.T1P2_demod_status == stv0910.DEMOD_S || status.T1P2_demod_status == stv0910.DEMOD_S2;

            // A board with a single TS (V2, E-Tiouner) has only tuner A in use; its second demodulator still runs on the
            // placeholder tune (the beacon) and must not take part in what the display shows (it made the display switch
            // between A and B).
            bool t2_locked = TunerCount > 1 && (status.T2P1_demod_status == stv0910.DEMOD_S || status.T2P1_demod_status == stv0910.DEMOD_S2);

            if (!t1_locked && !t2_locked)
            {
                // Show a callsign greeting instead of a meaningless placeholder frequency, but
                // only once (not every ~200ms poll tick).
                if (!digole_showed_greeting)
                {
                    string phase = digole_start_shown ? "NO SIGNAL" : "START";
                    byte greeting_err = digole.ShowGreeting(device_name, digole_callsign, digole_locator, digole_name, phase);
                    Log.Information("Nim Thread: [Digole] " + phase + " greeting sent, err=" + greeting_err + ", attempt=" + (digole_greeting_failures + 1));

                    // A failed write (e.g. display not ready right after connect) is retried on the
                    // next poll tick instead of being treated as shown; give up after 10 tries so a
                    // dead display doesn't get hammered forever.
                    if (greeting_err == 0 || ++digole_greeting_failures >= 10)
                    {
                        digole_showed_greeting = true;
                        digole_start_shown = true;
                        digole_greeting_failures = 0;
                    }
                }
                return;
            }

            int tuner_index;
            string tuner_label;

            if (t1_locked && t2_locked)
            {
                tuner_index = preferred_tuner == 1 ? 1 : 0;
            }
            else if (t1_locked)
            {
                tuner_index = 0;
            }
            else
            {
                tuner_index = 1;
            }

            tuner_label = tuner_index == 0 ? "TUNER A" : "TUNER B";

            digole_showed_greeting = false; // a tuner is now locked - back to normal status updates

            TunerConfig cfg = current_config[tuner_index];
            if (cfg == null)
                return;

            long freq_kHz = cfg.frequency + frequency_offsets[tuner_index];
            uint sr_kS = cfg.symbol_rate;
            short rf_level_dBm = tuner_index == 0 ? status.T1P2_input_power_level : status.T2P1_input_power_level;
            byte demod_status = tuner_index == 0 ? status.T1P2_demod_status : status.T2P1_demod_status;
            uint modcode = tuner_index == 0 ? status.T1P2_modcode : status.T2P1_modcode;
            double mer_dB = (tuner_index == 0 ? status.T1P2_mer : status.T2P1_mer) / 10.0;

            string modcod_name = "";
            if (demod_status == stv0910.DEMOD_S2 && lookups.modcod_lookup_dvbs2.ContainsKey(modcode))
                modcod_name = lookups.modcod_lookup_dvbs2[modcode];
            else if (demod_status == stv0910.DEMOD_S && lookups.modcod_lookup_dvbs.ContainsKey(modcode))
                modcod_name = lookups.modcod_lookup_dvbs[modcode];

            digole.UpdateStatus(device_name, tuner_label, freq_kHz, sr_kS, rf_level_dBm, mer_dB,
                service_name[tuner_index], video_codec[tuner_index], modcod_name);
        }

        public void register_callback(SourceStatusCallback cb)
        {
            status_callback.Add(cb);
        }

        // https://wiki.batc.org.uk/MiniTiouner_Power_Level_Indication
        short get_rf_level(ushort agc1, ushort agc2)
        {
            int index = -1;

            if (agc1 > 0)
            {
                // stronger signal (above about -70 dBm): AGC1 does the work and AGC2 sits at its floor, so AGC1 gives the level
                index = lookups.agc1_lookup.BinarySearch(agc1);

                if (index < 0)
                    index = ~index;
            }
            else
            {
                // weak signal: AGC1 is at 0 and AGC2 does the work. The AGC2 table falls from 3200 (-97 dBm) to 148, so it is
                // searched from the top for the first entry at or below the reading (BinarySearch needs an ascending list).
                index = lookups.agc2_lookup.Count - 1;

                for (int i = 0; i < lookups.agc2_lookup.Count; i++)
                {
                    if (lookups.agc2_lookup[i] <= agc2)
                    {
                        index = i;
                        break;
                    }
                }
            }

            if (index < 0) index = 0;

            if (index >= lookups.rf_power_level.Count())
                index = lookups.rf_power_level.Count() - 1;

            return lookups.rf_power_level[index];
        }

        // Lock time per tuner (index 0 = tuner 1), measured in software - only ever touched on this
        // thread: stopwatch timestamp of the scan start (0 = none), time to lock (-1 = not locked yet)
        // and whether the last poll saw a lock, so a lost lock restarts the measurement.
        private readonly long[] lock_scan_start = new long[2];
        private readonly double[] lock_time_ms = { -1, -1 };
        private readonly bool[] lock_was_locked = new bool[2];

        // Refresh time: average of the last RefreshHistory intervals between two status polls.
        private const int RefreshHistory = 8;
        private long last_status_timestamp = 0;
        private readonly System.Collections.Generic.Queue<double> refresh_intervals_ms = new System.Collections.Generic.Queue<double>();

        private static double ElapsedMs(long start_timestamp)
        {
            return (System.Diagnostics.Stopwatch.GetTimestamp() - start_timestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        }

        // The demod scan of this tuner has just been started.
        private void start_lock_timer(int tuner_index)
        {
            lock_scan_start[tuner_index] = System.Diagnostics.Stopwatch.GetTimestamp();
            lock_time_ms[tuner_index] = -1;
            lock_was_locked[tuner_index] = false;
        }

        private void update_lock_time(int tuner_index, byte demod_status)
        {
            bool locked = demod_status == stv0910.DEMOD_S || demod_status == stv0910.DEMOD_S2;

            if (locked)
            {
                if (!lock_was_locked[tuner_index])
                    _stv0910.stv0910_clear_delock(tuner_index == 0 ? stv0910.STV0910_DEMOD_TOP : stv0910.STV0910_DEMOD_BOTTOM); // DEMOD_DELOCK = lock lost since the last lock

                if (!lock_was_locked[tuner_index] && lock_scan_start[tuner_index] != 0)
                    lock_time_ms[tuner_index] = ElapsedMs(lock_scan_start[tuner_index]);
            }
            else if (lock_was_locked[tuner_index])
            {
                // lock lost: time the way back in
                lock_scan_start[tuner_index] = System.Diagnostics.Stopwatch.GetTimestamp();
                lock_time_ms[tuner_index] = -1;
            }

            lock_was_locked[tuner_index] = locked;
        }

        private uint update_refresh_time()
        {
            long now = System.Diagnostics.Stopwatch.GetTimestamp();

            if (last_status_timestamp != 0)
            {
                refresh_intervals_ms.Enqueue(ElapsedMs(last_status_timestamp));
                while (refresh_intervals_ms.Count > RefreshHistory)
                    refresh_intervals_ms.Dequeue();
            }

            last_status_timestamp = now;

            return refresh_intervals_ms.Count == 0 ? 0 : (uint)Math.Round(refresh_intervals_ms.Average());
        }

        // Switches the LNA of a NIM input on (fully automatic gain), once per input. A missing LNA (older NIM) or a
        // failing init is logged but never stops the tuner.
        private readonly bool[] lna_initialised = new bool[2];
        private const int LnaReadIntervalMs = 1000;
        private long last_lna_read = 0;
        private long last_rf_log = 0;
        private long last_noise_log = 0;
        private long last_search_log = 0;
        private readonly ushort[] lna_raw = new ushort[2]; // last (gain << 5 | vgo) of the top / bottom LNA

        private void init_lna(byte input)
        {
            int index = input == nim.NIM_INPUT_TOP ? 0 : 1;
            if (lna_initialised[index])
                return;

            bool lna_ok = false;
            byte lna_err = stvvglna_top.stvvglna_init(input, stvvglna.STVVGLNA_ON, ref lna_ok);
            if (lna_err != 0)
                Log.Information("Nim Thread: LNA init failed on input " + input + ", error " + lna_err);

            if (input == nim.NIM_INPUT_TOP)
                lna_top_ok = lna_ok;
            else
                lna_bottom_ok = lna_ok;

            lna_initialised[index] = true;
            Log.Information("Nim Thread: LNA on input " + input + (lna_ok ? " found and switched on" : " not present"));
        }

        // 16 (I, Q) samples of one demodulator, or null when it is not locked.
        private byte[,] read_constellation(byte demod, byte demod_status)
        {
            if (demod_status != stv0910.DEMOD_S && demod_status != stv0910.DEMOD_S2)
                return null;

            byte[,] data = new byte[16, 2];
            byte con_i = 0;
            byte con_q = 0;

            for (int count = 0; count < 16; count++)
            {
                _stv0910.stv0910_read_constellation(demod, ref con_i, ref con_q);
                data[count, 0] = con_i;
                data[count, 1] = con_q;
            }

            return data;
        }

        // where the last status cycle spent its time, for the warning about a slow cycle
        private long _last_digole_ms;
        private long _last_callback_ms;

        byte get_nim_status()
        {
            _last_digole_ms = 0;
            _last_callback_ms = 0;
            TunerStatus nim_status = new TunerStatus();
            nim_status.refresh_ms = update_refresh_time();

            byte err = 0;

            nim_status.T1P2_reset = reset;

            // LNA of the NIM inputs: gain readout (this block was commented out, so the tuner properties always showed 0)
            if (no_lna)
            {
                nim_status.lna_bottom_ok = false;
                nim_status.lna_top_ok = false;
                nim_status.T1P2_lna_gain = 0;
                nim_status.T2P1_lna_gain = 0;
            }
            else
            {
                nim_status.lna_bottom_ok = lna_bottom_ok;
                nim_status.lna_top_ok = lna_top_ok;

                // the AGC readout takes several I2C transactions per LNA, once a second is enough
                if (err == 0 && Environment.TickCount64 - last_lna_read >= LnaReadIntervalMs)
                {
                    last_lna_read = Environment.TickCount64;
                    byte gain = 0, vgo = 0;
                    byte lna_err;

                    if (lna_top_ok)
                    {
                        lna_err = stvvglna_top.stvvglna_read_agc(nim.NIM_INPUT_TOP, ref gain, ref vgo);
                        lna_raw[0] = (ushort)((gain << 5) | vgo);
                        Log.Debug("Nim Thread: LNA top gain=" + gain + " vgo=" + vgo + " raw=" + lna_raw[0] + " err=" + lna_err);
                    }

                    if (lna_bottom_ok)
                    {
                        lna_err = stvvglna_bottom.stvvglna_read_agc(nim.NIM_INPUT_BOTTOM, ref gain, ref vgo);
                        lna_raw[1] = (ushort)((gain << 5) | vgo);
                        Log.Debug("Nim Thread: LNA bottom gain=" + gain + " vgo=" + vgo + " raw=" + lna_raw[1] + " err=" + lna_err);
                    }
                }

                // each tuner shows the LNA of the RF input it is set to

                nim_status.T1P2_lna_gain = (current_config[0] != null && current_config[0].rf_input == nim.NIM_INPUT_BOTTOM) ? lna_raw[1] : lna_raw[0];
                nim_status.T2P1_lna_gain = (current_config[1] != null && current_config[1].rf_input == nim.NIM_INPUT_BOTTOM) ? lna_raw[1] : lna_raw[0];
            }

            byte rf_input_1 = 0;
            byte rf_input_2 = 0;
            byte temp = 0;
            err = _stv6120.stv6120_read_rf_sel(ref temp);

            rf_input_1 = (byte)(temp & stv6120_regs.STV6120_CTRL9_RFSEL_1_MASK);
            rf_input_2 = (byte)(temp & stv6120_regs.STV6120_CTRL9_RFSEL_2_MASK);

            if (rf_input_1 == 1)
                nim_status.T1P2_rf_input = nim.NIM_INPUT_TOP;
            else
                nim_status.T1P2_rf_input = nim.NIM_INPUT_BOTTOM;

            if (rf_input_2 == 4)
                nim_status.T2P1_rf_input = nim.NIM_INPUT_TOP;
            else
                nim_status.T2P1_rf_input = nim.NIM_INPUT_BOTTOM;

            // get scan state (demod state)
            byte demod_state = 0;
            err = _stv0910.stv0910_read_scan_state(stv0910.STV0910_DEMOD_TOP, ref demod_state);
            nim_status.T1P2_demod_status = demod_state;

            byte demod2_state = 0;
            err = _stv0910.stv0910_read_scan_state(stv0910.STV0910_DEMOD_BOTTOM, ref demod2_state);
            nim_status.T2P1_demod_status = demod2_state;

            update_lock_time(0, demod_state);
            update_lock_time(1, demod2_state);
            nim_status.T1P2_lock_time_ms = lock_time_ms[0];
            nim_status.T2P1_lock_time_ms = lock_time_ms[1];

            // lock indicators for the LEDs / gauges on the Expert tab
            byte dstatus = 0;
            byte dstatus2 = 0;
            sbyte ldi = 0;
            ushort tmglock = 0;
            if (err == 0) err = _stv0910.stv0910_read_lock_indicators(stv0910.STV0910_DEMOD_TOP, ref dstatus, ref dstatus2, ref ldi, ref tmglock);
            nim_status.T1P2_dstatus = dstatus;
            nim_status.T1P2_dstatus2 = dstatus2;
            nim_status.T1P2_ldi = ldi;
            nim_status.T1P2_tmglock = tmglock;

            dstatus = 0;
            dstatus2 = 0;
            ldi = 0;
            tmglock = 0;
            if (err == 0) err = _stv0910.stv0910_read_lock_indicators(stv0910.STV0910_DEMOD_BOTTOM, ref dstatus, ref dstatus2, ref ldi, ref tmglock);
            nim_status.T2P1_dstatus = dstatus;
            nim_status.T2P1_dstatus2 = dstatus2;
            nim_status.T2P1_ldi = ldi;
            nim_status.T2P1_tmglock = tmglock;

            // stream flags for the LEDs on the Expert tab (BCHERR is chip-wide, the value of the last read counts)
            byte pdelstatus1 = 0;
            bool spectrum_inverted = false;
            byte bcherr = 0;
            if (err == 0) err = _stv0910.stv0910_read_stream_flags(stv0910.STV0910_DEMOD_TOP, ref pdelstatus1, ref spectrum_inverted, ref bcherr);
            nim_status.T1P2_pdelstatus1 = pdelstatus1;
            nim_status.T1P2_spectrum_inverted = spectrum_inverted;

            pdelstatus1 = 0;
            spectrum_inverted = false;
            if (err == 0) err = _stv0910.stv0910_read_stream_flags(stv0910.STV0910_DEMOD_BOTTOM, ref pdelstatus1, ref spectrum_inverted, ref bcherr);
            nim_status.T2P1_pdelstatus1 = pdelstatus1;
            nim_status.T2P1_spectrum_inverted = spectrum_inverted;
            nim_status.bcherr = bcherr;

            // TS bit rate of the packet delineator output for the Expert tab
            ushort ts_bitrate_raw = 0;
            if (err == 0) err = _stv0910.stv0910_read_ts_bitrate(stv0910.STV0910_DEMOD_TOP, ref ts_bitrate_raw);
            nim_status.T1P2_ts_bitrate_raw = ts_bitrate_raw;
            ts_bitrate_raw = 0;
            if (err == 0) err = _stv0910.stv0910_read_ts_bitrate(stv0910.STV0910_DEMOD_BOTTOM, ref ts_bitrate_raw);
            nim_status.T2P1_ts_bitrate_raw = ts_bitrate_raw;

            // noise level for the Noise bar on the Expert tab - only while locked
            ushort noise = 0;
            if (err == 0 && (nim_status.T1P2_demod_status == stv0910.DEMOD_S2 || nim_status.T1P2_demod_status == stv0910.DEMOD_S))
                err = _stv0910.stv0910_read_noise(stv0910.STV0910_DEMOD_TOP, nim_status.T1P2_demod_status == stv0910.DEMOD_S2, ref noise);
            nim_status.T1P2_noise = noise;

            noise = 0;
            if (err == 0 && (nim_status.T2P1_demod_status == stv0910.DEMOD_S2 || nim_status.T2P1_demod_status == stv0910.DEMOD_S))
                err = _stv0910.stv0910_read_noise(stv0910.STV0910_DEMOD_BOTTOM, nim_status.T2P1_demod_status == stv0910.DEMOD_S2, ref noise);
            nim_status.T2P1_noise = noise;

            // debug log of all noise registers, to compare with the numbers MiniTioune shows for the same signal
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug) && Environment.TickCount64 - last_noise_log >= 2000)
            {
                last_noise_log = Environment.TickCount64;
                var candidates = new ushort[7];
                for (int d = 0; d < 2; d++)
                {
                    byte demod_status_d = d == 0 ? nim_status.T1P2_demod_status : nim_status.T2P1_demod_status;
                    if (demod_status_d != stv0910.DEMOD_S2 && demod_status_d != stv0910.DEMOD_S)
                        continue;

                    byte demod_id = d == 0 ? stv0910.STV0910_DEMOD_TOP : stv0910.STV0910_DEMOD_BOTTOM;
                    byte log_power_i = 0;
                    byte log_power_q = 0;
                    _stv0910.stv0910_read_power(demod_id, ref log_power_i, ref log_power_q);

                    if (_stv0910.stv0910_read_noise_candidates(demod_id, candidates) == 0)
                        Log.Debug("Nim Thread: Noise T" + (d + 1) + (demod_status_d == stv0910.DEMOD_S2 ? " (S2)" : " (S)") +
                                  ": NNOSPLHT=" + candidates[0] + " NNOSPLH=" + candidates[1] + " NNOSDATAT=" + candidates[2] + " NNOSDATA=" + candidates[3] +
                                  " NNOSFRAME=" + candidates[4] + " NNOSRAD=" + candidates[5] + " NOSDATAT_abs=" + candidates[6] +
                                  " | power I=" + log_power_i + " Q=" + log_power_q +
                                  " | TSBITRATE=" + (d == 0 ? nim_status.T1P2_ts_bitrate_raw : nim_status.T2P1_ts_bitrate_raw) +
                                  " -> " + ((d == 0 ? nim_status.T1P2_ts_bitrate_raw : nim_status.T2P1_ts_bitrate_raw) * (long)_stv0910.MclkHz / 16384) + " bit/s");
                }
            }

            // debug log of the symbol rate / carrier search registers next to what the setup code wrote (every 2 s, while not locked)
            if (Log.IsEnabled(Serilog.Events.LogEventLevel.Debug) && Environment.TickCount64 - last_search_log >= 2000)
            {
                last_search_log = Environment.TickCount64;
                var search_values = new byte[stv0910.SearchRegisterNames.Length];
                for (int d = 0; d < 2; d++)
                {
                    byte search_demod_status = d == 0 ? nim_status.T1P2_demod_status : nim_status.T2P1_demod_status;
                    if (search_demod_status == stv0910.DEMOD_S || search_demod_status == stv0910.DEMOD_S2)
                        continue;

                    if (_stv0910.stv0910_read_search_registers(d == 0 ? stv0910.STV0910_DEMOD_TOP : stv0910.STV0910_DEMOD_BOTTOM, search_values) != 0)
                        continue;

                    var text = new System.Text.StringBuilder("Nim Thread: Search T" + (d + 1) + ":");
                    for (int i = 0; i < search_values.Length; i++)
                        text.Append(" " + stv0910.SearchRegisterNames[i] + "=" + search_values[i].ToString("X2"));

                    // 16 bit pairs in Hz: SFRxxx = MCLK * value / 2^16, CFRxxx = MCLK * value / 2^16 (signed)
                    double sfr_init = (double)_stv0910.MclkHz * ((search_values[0] << 8) | search_values[1]) / 65536.0;
                    double sfr_up = (double)_stv0910.MclkHz * (((search_values[2] & 0x7F) << 8) | search_values[3]) / 65536.0;
                    double sfr_low = (double)_stv0910.MclkHz * (((search_values[4] & 0x7F) << 8) | search_values[5]) / 65536.0;
                    double cfr_init = (double)_stv0910.MclkHz * (short)((search_values[6] << 8) | search_values[7]) / 65536.0;
                    double cfr_up = (double)_stv0910.MclkHz * (short)((search_values[8] << 8) | search_values[9]) / 65536.0;
                    double cfr_low = (double)_stv0910.MclkHz * (short)((search_values[10] << 8) | search_values[11]) / 65536.0;
                    text.Append(" | SFRINIT=" + Math.Round(sfr_init) + " SFRUP=" + Math.Round(sfr_up) + " SFRLOW=" + Math.Round(sfr_low) +
                                " S/s, CFRINIT=" + Math.Round(cfr_init) + " CFRUP=" + Math.Round(cfr_up) + " CFRLOW=" + Math.Round(cfr_low) + " Hz");
                    Log.Debug(text.ToString());
                }
            }

            // LDPC iterations - DVB-S2 only
            byte ldpc_iterations = 0;
            byte ldpc_max_iterations = 0;
            if (err == 0 && nim_status.T1P2_demod_status == stv0910.DEMOD_S2)
                err = _stv0910.stv0910_read_ldpc_iterations(stv0910.STV0910_DEMOD_TOP, ref ldpc_iterations, ref ldpc_max_iterations);
            nim_status.T1P2_ldpc_iterations = ldpc_iterations;
            nim_status.T1P2_ldpc_max_iterations = ldpc_max_iterations;

            ldpc_iterations = 0;
            ldpc_max_iterations = 0;
            if (err == 0 && nim_status.T2P1_demod_status == stv0910.DEMOD_S2)
                err = _stv0910.stv0910_read_ldpc_iterations(stv0910.STV0910_DEMOD_BOTTOM, ref ldpc_iterations, ref ldpc_max_iterations);
            nim_status.T2P1_ldpc_iterations = ldpc_iterations;
            nim_status.T2P1_ldpc_max_iterations = ldpc_max_iterations;

            // derotator search range for the bar and the carrier trace on the Special tab
            Int32 carrier_low_hz = 0;
            Int32 carrier_up_hz = 0;
            if (err == 0)
                err = _stv0910.stv0910_read_carrier_range(stv0910.STV0910_DEMOD_TOP, ref carrier_low_hz, ref carrier_up_hz);
            nim_status.T1P2_carrier_low_hz = carrier_low_hz - _stv0910.AppliedLowSrOffsetHz(0);
            nim_status.T1P2_carrier_up_hz = carrier_up_hz - _stv0910.AppliedLowSrOffsetHz(0);

            carrier_low_hz = 0;
            carrier_up_hz = 0;
            if (err == 0)
                err = _stv0910.stv0910_read_carrier_range(stv0910.STV0910_DEMOD_BOTTOM, ref carrier_low_hz, ref carrier_up_hz);
            nim_status.T2P1_carrier_low_hz = carrier_low_hz - _stv0910.AppliedLowSrOffsetHz(1);
            nim_status.T2P1_carrier_up_hz = carrier_up_hz - _stv0910.AppliedLowSrOffsetHz(1);

            // chip identification (cached from init) and the demodulator's system PLL
            nim_status.mclk_hz = _stv0910.MclkHz;
            nim_status.chip_mid = _stv0910.ChipMid;
            nim_status.chip_did = _stv0910.ChipDid;
            bool pll_locked = false;
            if (err == 0) err = _stv0910.stv0910_read_pll_lock(ref pll_locked);
            nim_status.pll_locked = pll_locked;

            // power
            byte power_i = 0;
            byte power_q = 0;
            if (err == 0) err = _stv0910.stv0910_read_power(stv0910.STV0910_DEMOD_TOP, ref power_i, ref power_q);
            nim_status.T1P2_power_i = power_i;
            nim_status.T1P2_power_q = power_q;

            // I/Q constellation samples for the "Expert" tab - only while the demod is locked (no
            // point in the I2C traffic otherwise), null = nothing to show
            nim_status.T1P2_constellation = err == 0 ? read_constellation(stv0910.STV0910_DEMOD_TOP, nim_status.T1P2_demod_status) : null;
            nim_status.T2P1_constellation = err == 0 ? read_constellation(stv0910.STV0910_DEMOD_BOTTOM, nim_status.T2P1_demod_status) : null;

            /* LDPC Error Count */
            UInt32 errors_ldpc_count = 0;
            if (err == 0) err = _stv0910.stv0910_read_errors_ldpc_count(stv0910.STV0910_DEMOD_TOP, ref errors_ldpc_count);
            nim_status.errors_ldpc_count = errors_ldpc_count;

            /* puncture rate */
            byte puncture_rate = 0;
            if (err == 0) err = _stv0910.stv0910_read_puncture_rate(stv0910.STV0910_DEMOD_TOP, ref puncture_rate);
            nim_status.T1P2_puncture_rate = puncture_rate;

            if (err == 0) err = _stv0910.stv0910_read_puncture_rate(stv0910.STV0910_DEMOD_BOTTOM, ref puncture_rate);
            nim_status.T2P1_puncture_rate = puncture_rate;

            /* carrier frequency offset we are trying */
            Int32 frequency_offset = 0;
            if (err == 0) err = _stv0910.stv0910_read_car_freq(stv0910.STV0910_DEMOD_TOP, ref frequency_offset);
            nim_status.T1P2_frequency_carrier_offset = frequency_offset - _stv0910.AppliedLowSrOffsetHz(0); // without the low symbol rate offset
            if (err == 0) err = _stv0910.stv0910_read_car_freq(stv0910.STV0910_DEMOD_BOTTOM, ref frequency_offset);
            nim_status.T2P1_frequency_carrier_offset = frequency_offset - _stv0910.AppliedLowSrOffsetHz(1);

            /* symbol rate we are trying */
            UInt32 sr = 0;
            if (err == 0) err = _stv0910.stv0910_read_sr(stv0910.STV0910_DEMOD_TOP, ref sr);
            nim_status.T1P2_symbol_rate = sr;
            if (err == 0) err = _stv0910.stv0910_read_sr(stv0910.STV0910_DEMOD_BOTTOM, ref sr);
            nim_status.T2P1_symbol_rate = sr;

            /* viterbi error rate */
            UInt32 viterbi_error_rate = 0;
            if (err == 0) err = _stv0910.stv0910_read_err_rate(stv0910.STV0910_DEMOD_TOP, ref viterbi_error_rate);
            nim_status.T1P2_viterbi_error_rate = viterbi_error_rate;
            if (err == 0) err = _stv0910.stv0910_read_err_rate(stv0910.STV0910_DEMOD_BOTTOM, ref viterbi_error_rate);
            nim_status.T2P1_viterbi_error_rate = viterbi_error_rate;

            /* BER */
            UInt32 ber = 0;
            if (err == 0) err = _stv0910.stv0910_read_ber(stv0910.STV0910_DEMOD_TOP, ref ber);
            nim_status.T1P2_ber = ber;
            if (err == 0) err = _stv0910.stv0910_read_ber(stv0910.STV0910_DEMOD_BOTTOM, ref ber);
            nim_status.T2P1_ber = ber;

            /* BCH Uncorrected Flag */
            bool errors_bch_uncorrected = false;
            if (err == 0) err = _stv0910.stv0910_read_errors_bch_uncorrected(stv0910.STV0910_DEMOD_TOP, ref errors_bch_uncorrected);
            nim_status.T1P2_errors_bch_uncorrected = errors_bch_uncorrected;
            if (err == 0) err = _stv0910.stv0910_read_errors_bch_uncorrected(stv0910.STV0910_DEMOD_BOTTOM, ref errors_bch_uncorrected);
            nim_status.T2P1_errors_bch_uncorrected = errors_bch_uncorrected;

            /* BCH Error Count */
            UInt32 errors_bch_count = 0;
            if (err == 0) err = _stv0910.stv0910_read_errors_bch_count(stv0910.STV0910_DEMOD_TOP, ref errors_bch_count);
            nim_status.T1P2_errors_bch_count = errors_bch_count;
            if (err == 0) err = _stv0910.stv0910_read_errors_bch_count(stv0910.STV0910_DEMOD_BOTTOM, ref errors_bch_count);
            nim_status.T2P1_errors_bch_count = errors_bch_count;


            // agc1 gain
            ushort agc1_gain = 0;
            if (err == 0) err = _stv0910.stv0910_read_agc1_gain(stv0910.STV0910_DEMOD_TOP, ref agc1_gain);
            nim_status.T1P2_agc1_gain = agc1_gain;
            if (err == 0) err = _stv0910.stv0910_read_agc1_gain(stv0910.STV0910_DEMOD_BOTTOM, ref agc1_gain);
            nim_status.T2P1_agc1_gain = agc1_gain;

            // agc2 gain
            ushort agc2_gain = 0;
            if (err == 0) err = _stv0910.stv0910_read_agc2_gain(stv0910.STV0910_DEMOD_TOP, ref agc2_gain);
            nim_status.T1P2_agc2_gain = agc2_gain;
            // (each tuner with its OWN AGC1: agc1_gain still holds the value of the bottom demod at this point, so tuner 1
            // used to show the level of tuner 2)
            // The level includes the gain of the NIM's LNA, like in MiniTioune (a preamp with 12 dB gain makes the level
            // 12 dB higher: table -39 dBm + 12,3 dB = -26,7 dBm, MiniTioune shows -26 dBm). Curves without a known
            // gain formula add 0.
            double lna_db_1, lna_db_2;
            stvvglna.stvvglna_gain_db(nim_status.T1P2_lna_gain, out lna_db_1);
            stvvglna.stvvglna_gain_db(nim_status.T2P1_lna_gain, out lna_db_2);
            nim_status.T1P2_input_power_level = (short)Math.Round(get_rf_level(nim_status.T1P2_agc1_gain, agc2_gain) + lna_db_1);

            if (err == 0) err = _stv0910.stv0910_read_agc2_gain(stv0910.STV0910_DEMOD_BOTTOM, ref agc2_gain);
            nim_status.T2P1_agc2_gain = agc2_gain;
            nim_status.T2P1_input_power_level = (short)Math.Round(get_rf_level(nim_status.T2P1_agc1_gain, agc2_gain) + lna_db_2);

            // raw AGC values next to the LNA readout and the derived level, to calibrate them against MiniTioune
            if (Environment.TickCount64 - last_rf_log >= 2000)
            {
                last_rf_log = Environment.TickCount64;
                Log.Debug("Nim Thread: RF T1 agc1=" + nim_status.T1P2_agc1_gain + " agc2=" + nim_status.T1P2_agc2_gain + " -> " + nim_status.T1P2_input_power_level +
                          " dBm | T2 agc1=" + nim_status.T2P1_agc1_gain + " agc2=" + nim_status.T2P1_agc2_gain + " -> " + nim_status.T2P1_input_power_level +
                          " dBm | LNA raw top=" + lna_raw[0] + " (gain " + (lna_raw[0] >> 5) + ", vgo " + (lna_raw[0] & 31) + "), bottom=" + lna_raw[1] +
                          " (gain " + (lna_raw[1] >> 5) + ", vgo " + (lna_raw[1] & 31) + ")");
            }

            // ma type
            UInt32 ma_type1 = 0;
            UInt32 ma_type2 = 0;

            if (err == 0) _stv0910.stv0910_read_matype(stv0910.STV0910_DEMOD_TOP, ref ma_type1, ref ma_type2);
            nim_status.T1P2_stream_format = (ma_type1 & 0xC0) >> 6; ;

            if (err == 0) _stv0910.stv0910_read_matype(stv0910.STV0910_DEMOD_BOTTOM, ref ma_type1, ref ma_type2);
            nim_status.T2P1_stream_format = (ma_type1 & 0xC0) >> 6; ;

            Int32 mer = 0;

            if (nim_status.T1P2_demod_status == stv0910.DEMOD_S || nim_status.T1P2_demod_status == stv0910.DEMOD_S2)
            {
                if (err == 0) err = _stv0910.stv0910_read_mer(stv0910.STV0910_DEMOD_TOP, ref mer);
            }

            nim_status.T1P2_mer = mer;

            if (nim_status.T2P1_demod_status == stv0910.DEMOD_S || nim_status.T2P1_demod_status == stv0910.DEMOD_S2)
            {
                if (err == 0) err = _stv0910.stv0910_read_mer(stv0910.STV0910_DEMOD_BOTTOM, ref mer);
            }

            nim_status.T2P1_mer = mer;

            /* MODCOD, Short Frames, Pilots */
            UInt32 modcod = 0;
            bool short_frame = false;
            bool pilots = false;
            byte rolloff = 0;
            
            if (err == 0) err = _stv0910.stv0910_read_modcod_and_type(stv0910.STV0910_DEMOD_TOP, ref modcod, ref short_frame, ref pilots, ref rolloff);
            nim_status.T1P2_modcode = modcod;
            nim_status.T1P2_rolloff = rolloff;
            nim_status.T1P2_short_frame = short_frame;
            nim_status.T1P2_pilots = pilots;

            if (err == 0) err = _stv0910.stv0910_read_modcod_and_type(stv0910.STV0910_DEMOD_BOTTOM, ref modcod, ref short_frame, ref pilots, ref rolloff);
            nim_status.T2P1_modcode = modcod;
            nim_status.T2P1_rolloff = rolloff;
            nim_status.T2P1_short_frame = short_frame;
            nim_status.T2P1_pilots = pilots;

            // Must come AFTER the demod/MER/MODCOD fields above are filled in: nim_status is a fresh
            // TunerStatus on every call, so drawing earlier showed modcod 0 ("DummyPL") all the time.
            if (digole_enabled && digole != null)
            {
                var digole_sw = System.Diagnostics.Stopwatch.StartNew();
                update_digole_display(nim_status);
                _last_digole_ms = digole_sw.ElapsedMilliseconds;
            }

            // TSSTATUS - decoded TS_VALID/TS_ERR/sync bits, read directly via I2C (see
            // stv0910_read_ts_status_decoded's doc comment for why this replaces the unused
            // hardware BC3_1/BC3_2 NAND signal).
            bool ts_line_ok = false, ts_error = false, ts_nosync = false;
            if (err == 0) err = _stv0910.stv0910_read_ts_status_decoded(stv0910.STV0910_DEMOD_TOP, ref ts_line_ok, ref ts_error, ref ts_nosync);
            nim_status.T1P2_ts_line_ok = ts_line_ok;
            nim_status.T1P2_ts_error = ts_error;
            nim_status.T1P2_ts_nosync = ts_nosync;

            if (err == 0) err = _stv0910.stv0910_read_ts_status_decoded(stv0910.STV0910_DEMOD_BOTTOM, ref ts_line_ok, ref ts_error, ref ts_nosync);
            nim_status.T2P1_ts_line_ok = ts_line_ok;
            nim_status.T2P1_ts_error = ts_error;
            nim_status.T2P1_ts_nosync = ts_nosync;

            if (nim_status.T1P2_demod_status != stv0910.DEMOD_S2)
            {
                /* short frames & pilots only valid for S2 DEMOD state */
                nim_status.T1P2_short_frame = false;
                nim_status.T1P2_pilots = false;
            }

            if (nim_status.T2P1_demod_status != stv0910.DEMOD_S2)
            {
                /* short frames & pilots only valid for S2 DEMOD state */
                nim_status.T2P1_short_frame = false;
                nim_status.T2P1_pilots = false;
            }

            // send status callback if available
            var callback_sw = System.Diagnostics.Stopwatch.StartNew();
            for ( int c = 0;c < status_callback.Count; c++)
            {
                status_callback[c](nim_status);
            }
            _last_callback_ms = callback_sw.ElapsedMilliseconds;


            reset = false;

            return err;
        }

        public void worker_thread()
        {
            int hw_errors = 0;

            try
            {

                Log.Information("Nim Thread: Starting...");

                bool initialConfig = false;

                TunerConfig nim_config = null;

                byte err = _stv0910.stv0910_init(hardware.RequireSerialTS);

                if (err != 0)
                {
                    Log.Information("STV0910 Init Error: " + err.ToString());
                }

                Log.Information("Init Nim");
                err = _nim.nim_init();

                while (!_stopRequested)
                {
                    if (initialConfig == false)
                    {
                        Log.Information("Nim Thread: Initial Config");
                    }

                    if (config_queue.Count() > 0 || initialConfig == false)
                    {
                        while (config_queue.TryDequeue(out nim_config))
                        {
                            Thread.Sleep(10);

                            current_config[nim_config.tuner - 1] = nim_config;

                            lock (HwLock)
                            {
                                // a board that cannot switch the LNB voltage (E-Tiouner) is left alone
                                if (LnbSupplyControl)
                                {
                                    switch(nim_config.lnba_psu)
                                    {
                                        case 0:
                                            hardware.hw_set_polarization_supply(0, false, false);
                                            break;
                                        case 1:
                                            hardware.hw_set_polarization_supply(0, true, false);
                                            break;
                                        case 2:
                                            hardware.hw_set_polarization_supply(0, true, true);
                                            break;
                                    }

                                    switch (nim_config.lnbb_psu)
                                    {
                                        case 0:
                                            hardware.hw_set_polarization_supply(1, false, false);
                                            break;
                                        case 1:
                                            hardware.hw_set_polarization_supply(1, true, false);
                                            break;
                                        case 2:
                                            hardware.hw_set_polarization_supply(1, true, true);
                                            break;
                                    }
                                }


                                // setup demod
                                if (err == 0)
                                {
                                    Log.Information("Configure Demod Receive - " + nim_config.tuner);
                                    if (nim_config.tuner == 1)
                                    {
                                        err = _stv0910.stv0910_setup_receive(stv0910.STV0910_DEMOD_TOP, nim_config.symbol_rate, nim_config.capture_range_khz);
                                    }
                                    else
                                    {
                                        err = _stv0910.stv0910_setup_receive(stv0910.STV0910_DEMOD_BOTTOM, nim_config.symbol_rate, nim_config.capture_range_khz);
                                    }
                                }
                                else
                                {
                                    Log.Information("Error before Demod");
                                }

                                // configure tuner
                                if (err == 0)
                                {
                                    Log.Information("Configure Tuner - " + nim_config.tuner.ToString());

                                    if (nim_config.tuner == 1)
                                    {
                                        err = _stv6120.stv6120_init(1, (uint)((int)nim_config.frequency + nim_config.freq_correction_khz - _stv0910.LowSrOffsetKHz(nim_config.symbol_rate)), nim_config.rf_input, nim_config.symbol_rate);
                                    }
                                    else
                                    {
                                        //err = _stv6120.stv6120_init(2, 749246, nim.NIM_INPUT_TOP, 333);
                                        err = _stv6120.stv6120_init(2, (uint)((int)nim_config.frequency + nim_config.freq_correction_khz - _stv0910.LowSrOffsetKHz(nim_config.symbol_rate)), nim_config.rf_input, nim_config.symbol_rate);
                                    }
                                }
                                else
                                {
                                    Log.Information("Error before Tuner");
                                }

                                // The NIM's own LNA of the selected RF input has to be switched on: stv6120_init does not
                                // (longmynd / MiniTioune do it right after the tuner init, that call was lost in the port,
                                // so the LNA stayed off: LNA gain 0 instead of about 12 dB and a much weaker signal).
                                if (err == 0 && !no_lna)
                                    init_lna((byte)nim_config.rf_input);

                                // demod - start scan
                                if (err == 0)
                                {
                                    Log.Information("Demod Start Scan - " + nim_config.tuner.ToString() );

                                    if (nim_config.tuner == 1)
                                    {
                                        err = _stv0910.stv0910_start_scan(stv0910.STV0910_DEMOD_TOP);
                                    }
                                    else
                                    {
                                        err = _stv0910.stv0910_start_scan(stv0910.STV0910_DEMOD_BOTTOM);
                                    }
                                }
                                else
                                {
                                    Log.Information("Error before demod scan");
                                }


                                // 22 kHz - independent per tuner (22K-A/22K-B)
                                if (err == 0)
                                {
                                    err = _stv0910.stv0910_switch_22Khz(nim_config.tuner == 1 ? stv0910.STV0910_DEMOD_TOP : stv0910.STV0910_DEMOD_BOTTOM, nim_config.tone_22kHz_P1);
                                }
                            }

                            // scan is running: this is where the lock-time measurement of this tuner starts
                            if (err == 0)
                                start_lock_timer(nim_config.tuner - 1);

                            // done, if we have errors, then exit thread
                            if (err != 0)
                            {
                                Log.Information("****** Nim Thread: Hardware Error: " + err.ToString() + " ******");
                                hw_errors += 1;
                                if (hw_errors > 5)
                                {
                                    Log.Information("Too many hardware errors");
                                    return;
                                }
                            }
                            else
                            {
                                Log.Information("Nim Thread: Nim Init Good");
                            }

                            initialConfig = true;
                            reset = true;
                        }
                    }
                    else
                    {
                        // Timed diagnostic for the "Digole doesn't clear on exit" investigation:
                        // Join(5s) in MinitiounerSource.Close() has timed out without this loop
                        // ever reaching its "Loop exited" log line - i.e. it's stuck somewhere in
                        // here, not in the Digole shutdown write itself. Logging every call's
                        // elapsed time (not just slow ones) so the moment Stop() is requested is
                        // visible relative to which get_nim_status() call was in flight.
                        var status_sw = System.Diagnostics.Stopwatch.StartNew();
                        var i2c_before = opentuner.Utilities.I2cStats.Snapshot();
                        long lock_wait_ms = 0;
                        lock (HwLock)
                        {
                            lock_wait_ms = status_sw.ElapsedMilliseconds;
                            if (trigger_burst_0)
                            {
                                trigger_burst_0 = false;
                                _stv0910.stv0910_send_tone_burst_p1();
                                // the burst leaves DISEQC_MODE at 3 (ToneBurst); put the continuous 22K-A
                                // tone back the way it was, or it silently drops out without the UI noticing
                                if (current_config[0] != null)
                                    _stv0910.stv0910_switch_22Khz(stv0910.STV0910_DEMOD_TOP, current_config[0].tone_22kHz_P1);
                            }
                            if (trigger_burst_1)
                            {
                                trigger_burst_1 = false;
                                _stv0910.stv0910_send_tone_burst_p2();
                                if (current_config[1] != null)
                                    _stv0910.stv0910_switch_22Khz(stv0910.STV0910_DEMOD_BOTTOM, current_config[1].tone_22kHz_P1);
                            }
                            if (stop_tuner_0)
                            {
                                stop_tuner_0 = false;
                                Log.Information("Nim Thread: Stop TS - tuner 1");
                                _stv0910.stv0910_stop_demod(stv0910.STV0910_DEMOD_TOP);
                                current_config[0] = null; // tuner has to be told a new frequency again, like at startup
                            }
                            if (stop_tuner_1)
                            {
                                stop_tuner_1 = false;
                                Log.Information("Nim Thread: Stop TS - tuner 2");
                                _stv0910.stv0910_stop_demod(stv0910.STV0910_DEMOD_BOTTOM);
                                current_config[1] = null;
                            }
                            get_nim_status();
                        }
                        status_sw.Stop();
                        if (status_sw.ElapsedMilliseconds >= 1000)
                        {
                            var i2c_after = opentuner.Utilities.I2cStats.Snapshot();
                            Log.Warning("Nim Thread: get_nim_status() took " + status_sw.ElapsedMilliseconds + "ms (waiting for HwLock " + lock_wait_ms + "ms, I2C " + (i2c_after.count - i2c_before.count) + " transfers " + (i2c_after.ms - i2c_before.ms) + "ms, Digole " + _last_digole_ms + "ms, status callbacks " + _last_callback_ms + "ms), _stopRequested=" + _stopRequested);
                        }
                        else
                        {
                            Log.Debug("Nim Thread: get_nim_status() took " + status_sw.ElapsedMilliseconds + "ms, _stopRequested=" + _stopRequested);
                        }
                        Thread.Sleep(200);
                    }
                }

                // Leave the Digole on the callsign greeting (or a plain Clear if no callsign is
                // set) instead of a stale reading after OpenTuner closes. Done here, on the
                // worker thread's own way out, not from Stop() (called from the UI thread) -
                // guarded by HwLock like every other hardware access on this thread, so it can't
                // interleave with MinitiounerSource.Close()'s own LED/LNB shutoff writes.
                Log.Information("Nim Thread: Loop exited, digole_enabled=" + digole_enabled.ToString());
                if (digole_enabled && digole != null)
                {
                    var shutdown_sw = System.Diagnostics.Stopwatch.StartNew();
                    byte digole_err;
                    lock (HwLock)
                    {
                        if (!string.IsNullOrEmpty(digole_callsign))
                        {
                            Log.Information("Nim Thread: Sending Digole shutdown greeting...");
                            digole_err = digole.ShowGreeting(device_name, digole_callsign, digole_locator, digole_name, "END");
                        }
                        else
                        {
                            Log.Information("Nim Thread: Sending Digole shutdown clear...");
                            digole_err = digole.Clear();
                        }
                    }
                    Log.Information("Nim Thread: Digole shutdown write sent, err=" + digole_err + ", elapsed=" + shutdown_sw.ElapsedMilliseconds + "ms");
                }
            }
            catch (ThreadAbortException)
            {
                Log.Information("Nim Thread: Closing");
            }
            catch (Exception ex)
            {
                // Without this, any exception here (e.g. from the Digole shutdown I2C write)
                // would silently kill the thread before the shutdown block below could run or
                // log anything - making the "Digole doesn't clear on exit" symptom unexplainable
                // from the logs alone.
                Log.Error(ex, "Nim Thread: Unhandled exception in worker_thread");
            }

        }

    }
}
