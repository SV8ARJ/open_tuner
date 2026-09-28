using opentuner.MediaSources.Minitiouner.HardwareInterfaces;
using opentuner.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using opentuner.MediaPlayers;
using Serilog;

namespace opentuner.MediaSources.Minitiouner
{
    public partial class MinitiounerSource : OTSource
    {
        public MTHardwareInterface hardware_interface;
        public bool hardware_connected = false;

        public int ts_devices = 1;

        ConcurrentQueue<TunerConfig> config_queue = new ConcurrentQueue<TunerConfig>();

        public CircularBuffer ts_data_queue = new CircularBuffer(GlobalDefines.CircularBufferStartingCapacity);
        public CircularBuffer ts_data_queue2 = new CircularBuffer(GlobalDefines.CircularBufferStartingCapacity);

        public TSThread ts_thread;
        public TSThread ts_thread2;

        NimThread nim_thread;
        TSParserThread ts_parser_thread;
        TSParserThread ts_parser_thread2;

        Thread ts_parser_t = null;
        Thread ts_parser_2_t = null;

        // threads
        Thread nim_thread_t = null;

        Thread ts_thread_t = null;
        Thread ts_thread_2_t = null;

        bool T1P2_prevLocked = false;
        bool T2P1_prevLocked = false;


        List<OTMediaPlayer> _media_player = new List<OTMediaPlayer> ();
        private List<TSRecorder> _ts_recorders;
        private List<TSUdpStreamer> _ts_streamers;
        private List<TunerControlForm> _tuner_forms;

        string _mediapath = "";

        public override bool DeviceConnected
        {
            get { return hardware_connected; }
        }

        // EXTERN-0..7 (AUX chip GPIO, see MinitiounerProperties.cs "Switches" panel) - kept as
        // one packed byte, mirrored to _settings.ExternState for persistence across connects.
        public bool AuxAvailable => hardware_connected && hardware_interface != null && hardware_interface.AuxAvailable;

        public bool GetExternOutput(int index)
        {
            return _settings.ExternState[index];
        }

        public void SetExternOutput(int index, bool on)
        {
            _settings.ExternState[index] = on;

            if (!AuxAvailable)
                return;

            byte value = 0;
            for (int i = 0; i < _settings.ExternState.Length; i++)
            {
                if (_settings.ExternState[i])
                    value |= (byte)(1 << i);
            }

            hardware_interface.aux_gpio_write(value);
        }

        // One-shot 22kHz DiSEqC tone burst ("TS" mode) - see NimThread.TriggerToneBurst.
        public void SendToneBurst(int tuner_index)
        {
            nim_thread?.TriggerToneBurst(tuner_index);
        }

        // "Stop TS" (issue #9) - stops the demodulator; the existing "not locked" handling in
        // nim_status_feedback/UpdateTunerProperties then stops the player, TS recording and UDP
        // streaming on the next status update, same as when a signal is lost.
        public override void StopTuner(int tuner_index)
        {
            nim_thread?.StopTuner(tuner_index);
        }

        // The Switches state the user picked last (LNB-A/LNB-B supply, 22kHz tone per tuner) is
        // kept for the next connect: Initialize() restores it before the Switches dropdowns are
        // built, so the setup survives restarts without a trip through the settings dialog.
        // Frequency tab: new capture range / correction of one tuner. Remembered in the settings and applied by
        // tuning again to the current frequency and symbol rate.
        private void ApplyTunerTrim(int device, uint capture_range, double correction_ppm, int offset_khz)
        {
            if (device < 0 || device > 1)
                return;

            capture_range_khz[device] = capture_range;
            freq_correction_ppm[device] = correction_ppm;
            freq_offset_khz[device] = offset_khz;

            if (_settings.CaptureRangeKHz == null || _settings.CaptureRangeKHz.Length < 2)
                _settings.CaptureRangeKHz = new uint[2];
            if (_settings.FreqCorrectionPpm == null || _settings.FreqCorrectionPpm.Length < 2)
                _settings.FreqCorrectionPpm = new double[2];

            _settings.CaptureRangeKHz[device] = capture_range;
            _settings.FreqCorrectionPpm[device] = correction_ppm;
            _settingsManager.SaveSettings(_settings);

            if (device == 0)
                change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
            else
                change_frequency(1, current_frequency_1, current_sr_1, current_rf_input_1, current_tone_22kHz_1, current_lnba_psu, current_lnbb_psu);
        }

        private void RememberSwitchState()
        {
            if (_settings.Tone22kHz == null || _settings.Tone22kHz.Length < 2)
                _settings.Tone22kHz = new bool[2];

            if (_settings.DefaultLnbASupply == current_lnba_psu && _settings.DefaultLnbBSupply == current_lnbb_psu &&
                _settings.Tone22kHz[0] == current_tone_22kHz_0 && _settings.Tone22kHz[1] == current_tone_22kHz_1)
                return;

            _settings.DefaultLnbASupply = current_lnba_psu;
            _settings.DefaultLnbBSupply = current_lnbb_psu;
            _settings.Tone22kHz[0] = current_tone_22kHz_0;
            _settings.Tone22kHz[1] = current_tone_22kHz_1;
            _settingsManager.SaveSettings(_settings);
        }

        // tuner specific

        // OTSource Properties
        private uint current_rf_input_0 = nim.NIM_INPUT_TOP;
        private uint current_rf_input_1 = nim.NIM_INPUT_TOP;
        private uint current_frequency_0 = 0;
        private uint current_frequency_1 = 0;
        private uint current_sr_0 = 0;
        private uint current_sr_1 = 0;

        // independent 22kHz tone per tuner (22K-A / 22K-B) - see stv0910_switch_22Khz
        private bool current_tone_22kHz_0 = false;
        private bool current_tone_22kHz_1 = false;
        // tuning trim per tuner, see MinitiounerSettings.CaptureRangeKHz / FreqCorrectionKHz
        private uint[] capture_range_khz = new uint[2];
        private double[] freq_correction_ppm = new double[2];
        private int[] freq_offset_khz = new int[2];      // manual offset from the Special tab, on top of the ppm correction (not saved)

        // The correction is a reference (crystal) error, so it scales with the tuner frequency: kHz = ppm * IF / 1e6.
        private int CorrectionKHz(int device, uint if_khz)
        {
            return (int)Math.Round(freq_correction_ppm[device] * if_khz / 1e6) + freq_offset_khz[device];
        }

        private uint current_offset_0 = 0;
        private uint current_offset_1 = 0;

        private byte current_lnba_psu = 0;
        private byte current_lnbb_psu = 0;

        private string last_service_name_0 = "";
        private string last_service_provider_0 = "";
        private string last_dbm_0 = "";
        private string last_mer_0 = "";
        private string last_video_codec_0 = "";

        private string last_service_name_1 = "";
        private string last_service_provider_1 = "";
        private string last_dbm_1 = "";
        private string last_mer_1 = "";
        private string last_video_codec_1 = "";


        private VideoChangeCallback VideoChangeCB;

        // todo: this is the callback we want to remove and keep internal
        private SourceStatusCallback SourceStatusCB;

        public string HardwareDevice { get; set; }

        private SettingsManager<MinitiounerSettings> _settingsManager;
        private MinitiounerSettings _settings;

        public MinitiounerSource() 
        {
            // settings
            _settings = new MinitiounerSettings();
            _settingsManager = new SettingsManager<MinitiounerSettings>("minitiouner_settings");
            _settings = (_settingsManager.LoadSettings(_settings));
        }

        public override int GetVideoSourceCount()
        {
            return ts_devices;
        }

        public override string GetDeviceName()
        {
            return HardwareDevice;
        }

        public override void RegisterTSConsumer(int device, CircularBuffer ts_buffer_queue)
        {
            switch (device)
            {
                case 0: ts_thread.RegisterTSConsumer(ts_buffer_queue); break;
                case 1: ts_thread2.RegisterTSConsumer(ts_buffer_queue); break;
            }
        }


        public override void StartStreaming(int device)
        {
            switch(device)
            {
                case 0:
                    if (ts_thread != null)
                        ts_thread.start_ts();
                    break;
                case 1:
                    if (ts_thread2 != null)
                        ts_thread2.start_ts();
                    break;
            }
        }

        public override void StopStreaming(int device)
        {
            switch (device)
            {
                case 0:
                    if (ts_thread != null)
                        ts_thread.stop_ts();
                    break;
                case 1:
                    if (ts_thread2 != null)
                        ts_thread2.stop_ts();
                    break;
            }
        }


        public override long GetFrequency(int device, bool offset_included)
        {
            long frequency = 0;

            switch (device)
            {
                case 0:  
                    int offset0 = offset_included ? (int)current_offset_0 : 0;
                    frequency = current_frequency_0 + offset0; 
                    break;
                case 1:  
                    int offset1 = offset_included ? (int)current_offset_1 : 0;
                    frequency = current_frequency_1 + offset1; 
                    break;
            }

            return frequency;
        }

        // device : 0 = Tuner 1, 1 = Tuner 2
        public override void SetFrequency(int device, uint frequency, uint symbol_rate, bool offset_included)
        {
            uint freq = 0;

            if (device >= 0 && device < 2)
                fallback_step[device] = -1; // any other tune (dialog, presets, fixed rate) cancels a running fallback

            if (device == 0 )
                freq = frequency - (offset_included ? current_offset_0 : 0);
            else
                freq = frequency - (offset_included ? current_offset_1 : 0);

            change_frequency((byte)(device), freq, symbol_rate,  (device == 0 ? current_rf_input_0 : current_rf_input_1 ), (device == 0 ? current_tone_22kHz_0 : current_tone_22kHz_1), current_lnba_psu, current_lnbb_psu);

            // last tuner the user actually selected (BATC spectrum click, tuner dialog, preset)
            // wins the Digole display when both tuners are locked at once.
            nim_thread?.SetPreferredTuner(device);
        }


        // ---- Automatic symbol rate fallback for a click in the BATC spectrum -----------------------------------
        // The spectrum only estimates the symbol rate from the width of the signal (a signal at the top of the
        // FFT range is measured too wide, for example). If the demodulator does not lock within FallbackWaitMs(),
        // the next smaller standard rate is tried (66 -> 33 -> 25 -> 20 kS), and if none of them locks the
        // original rate is set again. A rate typed in by hand or chosen in the spectrum's rate field never
        // falls back (SetFrequency() disarms it).
        private static readonly uint[] FallbackRates = { 66, 33, 25, 20 };
        // The lower the rate the longer the demodulator needs to find carrier and timing (MiniTioune took well over 20 s
        // for 25 kS): 1500 / rate seconds, at least 10 s - 66 kS 23 s, 33 kS 45 s, 25 kS 60 s, 20 kS 75 s.
        private static int FallbackWaitMs(uint rate_kS)
        {
            return (int)Math.Max(10000, 1500000 / Math.Max(1u, rate_kS));
        }

        private readonly int[] fallback_step = { -1, -1 };          // index into FallbackRates being tried, -1 = off
        private readonly uint[] fallback_original_sr = new uint[2];
        private readonly long[] fallback_deadline = new long[2];    // Environment.TickCount64

        public override void SetFrequencyFromSpectrum(int device, uint frequency, uint symbol_rate)
        {
            SetFrequency(device, frequency, symbol_rate, true);

            int index = Array.IndexOf(FallbackRates, symbol_rate);
            if (_settings.AutoSrFallback && device >= 0 && device < 2 && index >= 0)
            {
                fallback_step[device] = index;
                fallback_original_sr[device] = symbol_rate;
                fallback_deadline[device] = Environment.TickCount64 + FallbackWaitMs(symbol_rate);
                Log.Information("SR fallback armed: tuner " + (device + 1) + ", " + symbol_rate + " kS");
            }
        }

        // Called with every status update (NimThread): locked -> done, no lock after the wait -> next rate.
        private void CheckSrFallback(TunerStatus status)
        {
            for (int device = 0; device < 2; device++)
            {
                if (fallback_step[device] < 0)
                    continue;

                byte demod_status = device == 0 ? status.T1P2_demod_status : status.T2P1_demod_status;
                if (demod_status == stv0910.DEMOD_S || demod_status == stv0910.DEMOD_S2)
                {
                    Log.Information("SR fallback: tuner " + (device + 1) + " locked at " + (device == 0 ? current_sr_0 : current_sr_1) + " kS");
                    fallback_step[device] = -1;
                    continue;
                }

                if (Environment.TickCount64 < fallback_deadline[device])
                    continue;

                uint sr;
                int next = fallback_step[device] + 1;
                if (next < FallbackRates.Length)
                {
                    sr = FallbackRates[next];
                    fallback_step[device] = next;
                    fallback_deadline[device] = Environment.TickCount64 + FallbackWaitMs(sr);
                    Log.Information("SR fallback: tuner " + (device + 1) + " no lock, trying " + sr + " kS");
                }
                else
                {
                    sr = fallback_original_sr[device];
                    fallback_step[device] = -1;
                    Log.Information("SR fallback: tuner " + (device + 1) + " no lock with any rate, back to " + sr + " kS");
                }

                RetuneWithSymbolRate(device, sr);
            }
        }

        // Tunes again to the current frequency with another symbol rate, on the UI thread like every other tune.
        private void RetuneWithSymbolRate(int device, uint sr)
        {
            Action tune = () =>
            {
                if (device == 0)
                    change_frequency(0, current_frequency_0, sr, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                else
                    change_frequency(1, current_frequency_1, sr, current_rf_input_1, current_tone_22kHz_1, current_lnba_psu, current_lnbb_psu);
            };

            if (_parent != null && _parent.IsHandleCreated)
                _parent.BeginInvoke(tune);
            else
                tune();
        }

        public void change_frequency(byte device, UInt32 freq, UInt32 sr,  uint rf_input, bool tone_22kHz_P1, byte lnbA_supply, byte lnbB_supply)
        {
            if (!hardware_connected)
                return;

            if (device >= GetVideoSourceCount())
            {
                return;
            }

            Log.Information("Change Frequency: " + device.ToString());

            switch (rf_input)
            {
                case nim.NIM_INPUT_TOP: Log.Information("RF Input: Nim Input Top Specified"); break;
                case nim.NIM_INPUT_BOTTOM: Log.Information("RF Input: Nim Input Bottom Specified"); break;
                default: Log.Information("Error: Invalid RF Input: " + rf_input.ToString()); break;
            }

            TunerConfig newConfig = new TunerConfig();

            newConfig.tuner = (byte)(device+1);
            newConfig.frequency = freq;
            newConfig.symbol_rate = sr;
            newConfig.rf_input = rf_input;
            newConfig.tone_22kHz_P1 = tone_22kHz_P1;
            newConfig.lnba_psu = lnbA_supply;
            newConfig.lnbb_psu = lnbB_supply;
            newConfig.capture_range_khz = capture_range_khz[device];
            newConfig.freq_correction_khz = CorrectionKHz(device, freq);

            if (newConfig.frequency < 144000 || newConfig.frequency > 2450000)
            {
                Log.Information("Error: Invalid Frequency: " + newConfig.frequency);
                return;
            }

            Log.Information("Main: New Config: " + newConfig.ToString());

            if (device == 0)
            {
                current_frequency_0 = newConfig.frequency;
                current_sr_0 = sr;
                current_rf_input_0 = rf_input;
                current_tone_22kHz_0 = tone_22kHz_P1;

                if (VideoChangeCB != null)
                {
                    VideoChangeCB(1, false);
                }

            }
            else
            {
                current_frequency_1 = newConfig.frequency;
                current_sr_1 = sr;
                current_rf_input_1 = rf_input;
                current_tone_22kHz_1 = tone_22kHz_P1;

                if (VideoChangeCB != null)
                {
                    VideoChangeCB(2, false);
                }

            }

            config_queue.Enqueue(newConfig);

            

            if (_tuner_forms[device] != null)
            {
                _tuner_forms[device].UpdateTuner( (device == 0) ? current_frequency_0 : current_frequency_1, (device == 0) ? current_sr_0 : current_sr_0, (device == 0 ) ? current_offset_0 : current_offset_1);
            }

        }

        //public byte set_polarization_supply(byte lnb_num, bool supply_enable, bool supply_horizontal)
        //{
        //    return hardware_interface.hw_set_polarization_supply(lnb_num, supply_enable, supply_horizontal);
        //}

        public override int Initialize(VideoChangeCallback VideoChangeCB, Control Parent)
        {            
            switch (_settings.DefaultInterface)
            {
                case 0:
                    var hw_ask = new ChooseMinitiounerHardwareInterfaceForm();

                    if (hw_ask.ShowDialog() == DialogResult.OK)
                    {
                        switch(hw_ask.comboHardwareSelect.SelectedIndex)
                        {
                            case 0: hardware_interface = new FTDIInterface(); break;
                            case 1: hardware_interface = new PicoTunerInterface(); break;
                        }
                    }
                    else return -1;

                    break;
                case 1: hardware_interface = new FTDIInterface(); break;
                case 2: hardware_interface = new PicoTunerInterface(); break;
            }


            return Initialize(VideoChangeCB, SourceStatusCB, false, "", "", "", Parent);
        }

        public void nim_status_feedback(TunerStatus nim_status)
        {
            bool T1P2locked = false;
            bool T2P1Locked = false;

            if (nim_status.T1P2_demod_status >= 2) T1P2locked = true;
            if (nim_status.T2P1_demod_status >= 2) T2P1Locked = true;


            if (T1P2_prevLocked != T1P2locked)
            {
                Log.Information("T1P2 - Lock State Change: " + T1P2_prevLocked.ToString() + "->" + T1P2locked.ToString());

                if (nim_status.T1P2_demod_status >= 2)
                {
                    if (VideoChangeCB != null)
                    {
                        VideoChangeCB(1, true);
                    }
                    hardware_interface.hw_ts_led(0, true);
                }
                else
                {
                    if (VideoChangeCB != null)
                    {
                        VideoChangeCB(1, false);
                    }

                    hardware_interface.hw_ts_led(0, false);
                }

                T1P2_prevLocked = T1P2locked;
            }

            if (GetVideoSourceCount() == 2)
            {
                if (T2P1_prevLocked != T2P1Locked)
                {
                    Log.Information("T2P1 - Lock State Change: " + T2P1_prevLocked.ToString() + "->" + T2P1Locked.ToString());

                    if (nim_status.T2P1_demod_status >= 2)
                    {
                        if (VideoChangeCB != null)
                        {
                            VideoChangeCB(2, true);
                        }

                        hardware_interface.hw_ts_led(1, true);
                    }
                    else
                    {
                        if (VideoChangeCB != null)
                        {
                            VideoChangeCB(2, false);
                        }

                        hardware_interface.hw_ts_led(1, false);
                    }

                    T2P1_prevLocked = T2P1Locked;
                }
            }

            nim_status.T2P1_requested_frequency = current_frequency_0;
            nim_status.T1P2_requested_frequency = current_frequency_1;

            // update properties
            UpdateTunerProperties(nim_status);
        }

        private void ChangeRFInput(byte Tuner, uint RFInput)
        {
            switch(Tuner)
            {
                case 0:
                    change_frequency(Tuner, current_frequency_0, current_sr_0, RFInput, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    break;
                case 1:
                    change_frequency(Tuner, current_frequency_1, current_sr_1, RFInput, current_tone_22kHz_1, current_lnba_psu, current_lnbb_psu);
                    break;
            }

        }

        private void ChangeSymbolRate(byte Tuner, uint SymbolRate)
        {
            if (Tuner < 2)
                fallback_step[Tuner] = -1; // chosen by hand: no automatic fallback

            switch (Tuner)
            {
                case 0:
                    change_frequency(Tuner, current_frequency_0, SymbolRate, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    break;
                case 1:
                    change_frequency(Tuner, current_frequency_1, SymbolRate, current_rf_input_1, current_tone_22kHz_1, current_lnba_psu, current_lnbb_psu);
                    break;
            }
        }

        private void ChangeOffset(byte Tuner, int offset)
        {
            switch(Tuner)
            {
                case 0:
                    current_offset_0 = (uint)offset;
                    break;
                case 1:
                    current_offset_1 = (uint)offset;
                    break;
            }

            if (_tuner_forms[Tuner] != null)
            {
                _tuner_forms[Tuner].UpdateTuner((Tuner == 0) ? current_frequency_0 : current_frequency_1, (Tuner == 0) ? current_sr_0 : current_sr_0, (Tuner == 0) ? current_offset_0 : current_offset_1);
            }

        }

        private int Initialize(VideoChangeCallback VideoChangeCB, SourceStatusCallback SourceStatusCB, bool manual, string i2c_serial, string ts_serial, string ts2_serial, Control Parent)
        {

            this.VideoChangeCB = VideoChangeCB;
            this.SourceStatusCB = SourceStatusCB;

            hardware_init(manual, i2c_serial, ts_serial, ts2_serial);

            if (!hardware_connected)
            {
                MessageBox.Show("Error: No Working Hardware Detected");
                return -1;
            }

            // build properties
            _parent = Parent;

            // Restore the last used Switches state (LNB supply, 22kHz tone) BEFORE the properties are
            // built: the Switches dropdowns take their initial selection from these fields.
            if (_settings.Tone22kHz == null || _settings.Tone22kHz.Length < 2)
                _settings.Tone22kHz = new bool[2];
            current_lnba_psu = _settings.DefaultLnbASupply;
            current_lnbb_psu = _settings.DefaultLnbBSupply;
            current_tone_22kHz_0 = _settings.Tone22kHz[0];
            current_tone_22kHz_1 = _settings.Tone22kHz[1];

            // tuning trim per tuner: the sliders and rows of the properties take these values when they are built
            for (int t = 0; t < 2; t++)
            {
                capture_range_khz[t] = (_settings.CaptureRangeKHz != null && _settings.CaptureRangeKHz.Length > t) ? _settings.CaptureRangeKHz[t] : 0;
                freq_correction_ppm[t] = (_settings.FreqCorrectionPpm != null && _settings.FreqCorrectionPpm.Length > t) ? _settings.FreqCorrectionPpm[t] : 0;
            }

            BuildSourceProperties();

            _source_properties.UpdateValue("source_hw_interface", hardware_interface.GetName);

            Log.Information("Main: Starting Nim Thread");

            Log.Information("Switch LED's");
            
            
            // switch off ts leds
            hardware_interface.hw_ts_led(0, false);
            hardware_interface.hw_ts_led(1, false);

            // configure nim thread
            nim_thread = new NimThread(config_queue, hardware_interface, nim_status_feedback, false,
                _settings.EnableDigoleDisplay, _settings.DigoleI2cAddress, HardwareDevice,
                new uint[] { _settings.Offset1, _settings.Offset2 }, _settings.DigoleCallsign,
                _settings.DigoleLocator, _settings.DigoleName);
            nim_thread_t = new Thread(nim_thread.worker_thread);
            nim_thread_t.IsBackground = true;



            /*
             
            TODO: default lnb voltage supply settings

            // set default lnb supply
            Log.Information(current_enable_lnb_supply.ToString());
            Log.Information(current_enable_horiz_supply.ToString());

            */

            /*
            current_lnba_psu = _settings.DefaultLnbASupply;
            switch (current_lnba_psu)
            {
                case 0: hardware_interface.hw_set_polarization_supply(0, false, false);
                    break;
                case 1:
                    hardware_interface.hw_set_polarization_supply(0, true, false);
                    break;
                case 2:
                    hardware_interface.hw_set_polarization_supply(0, true, true);
                    break;
            }

            current_lnbb_psu = _settings.DefaultLnbBSupply;
            switch (current_lnbb_psu)
            {
                case 0:
                    hardware_interface.hw_set_polarization_supply(1, false, false);
                    break;
                case 1:
                    hardware_interface.hw_set_polarization_supply(1, true, false);
                    break;
                case 2:
                    hardware_interface.hw_set_polarization_supply(1, true, true);
                    break;
            }
            */

            current_lnba_psu = _settings.DefaultLnbASupply;
            current_lnbb_psu = _settings.DefaultLnbBSupply;

            // (Removed: an unconditional hardware_interface.hw_set_polarization_supply(1, false,
            // false) used to sit here - leftover from the commented-out settings-switch block
            // above, ignoring current_lnbb_psu entirely. It force-killed LNB-B's supply for
            // ~600ms before the real per-settings enable further down turned it back on - an
            // extra disable/re-enable cycle LNB-A never got. If the RT5047 needs a minimum
            // off-time or has fault-latch behavior on a fast re-enable, this alone could explain
            // LNB-B never powering up cleanly while LNB-A (no such pulse) works fine.)

            // set startup rf inputs according to settings
            current_rf_input_0 = nim.NIM_INPUT_TOP;
            current_rf_input_1 = nim.NIM_INPUT_TOP;

            switch (_settings.DefaultRFInput )
            {
                case 1:
                    current_rf_input_0 = nim.NIM_INPUT_TOP;
                    current_rf_input_1 = nim.NIM_INPUT_BOTTOM;
                    break;
                case 2:
                    current_rf_input_0 = nim.NIM_INPUT_BOTTOM;
                    current_rf_input_1 = nim.NIM_INPUT_TOP;
                    break;
                case 3:
                    current_rf_input_0 = nim.NIM_INPUT_BOTTOM;
                    current_rf_input_1 = nim.NIM_INPUT_BOTTOM;
                    break;
            }

            current_frequency_0 = 741525;
            current_frequency_1 = 741525;

            current_offset_0 = _settings.Offset1;
            current_offset_1 = _settings.Offset2;


            current_sr_0 = 1500;
            current_sr_1 = 1500;

            // setup tuner 0
            change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);

            if (ts_devices == 2)
            {
                // setup tuner 1
                change_frequency(1, current_frequency_1, current_sr_1, current_rf_input_1, current_tone_22kHz_1, current_lnba_psu, current_lnbb_psu);
            }

            nim_thread_t.Start();

            // TS thread - T1P2
            ts_thread = new TSThread(ts_data_queue, FlushTS2, ReadTS2, "MT TS2");
            ts_thread_t = new Thread(ts_thread.worker_thread);
            ts_thread_t.IsBackground = true;
            ts_thread_t.Start();

            if (ts_devices == 2)
            {
                ts_thread2 = new TSThread(ts_data_queue2, FlushTS1, ReadTS1, "MT TS1");
                ts_thread_2_t = new Thread(ts_thread2.worker_thread);
                ts_thread_2_t.IsBackground = true;
                ts_thread_2_t.Start();
            }

            // start TS Parser
            ts_parser_thread = new TSParserThread(parse_ts_data_callback);
            RegisterTSConsumer(0, ts_parser_thread.parser_ts_data_queue);
            ts_parser_t = new Thread(ts_parser_thread.worker_thread);
            ts_parser_t.IsBackground = true;
            ts_parser_t.Start();

            if (ts_devices == 2)
            {
                ts_parser_thread2 = new TSParserThread(parse_ts2_data_callback);
                RegisterTSConsumer(1, ts_parser_thread2.parser_ts_data_queue);
                ts_parser_2_t = new Thread(ts_parser_thread2.worker_thread);
                ts_parser_2_t.IsBackground = true;
                ts_parser_2_t.Start();
            }

            return ts_devices;
        }

        public void parse_ts_data_callback(TSStatus ts_status)
        {
            UpdateTSProperties(1, ts_status);
        }

        public void parse_ts2_data_callback(TSStatus ts_status)
        {
            UpdateTSProperties(2, ts_status);
        }


        public override CircularBuffer GetVideoDataQueue(int device)
        {
            switch (device)
            {
                case 0: return ts_data_queue;
                case 1: return ts_data_queue2;
            }

            return ts_data_queue;
        }

        void FlushTS2()
        {
            hardware_interface.transport_flush(PicoTunerInterface.TS2);
        }

        byte ReadTS2(ref byte[] data, ref uint dataRead)
        {
            byte err = hardware_interface.transport_read(PicoTunerInterface.TS2, ref data, ref dataRead);
            DataReceived(PicoTunerInterface.TS2);
            return err;
        }

        byte ReadTS1(ref byte[] data, ref uint dataRead)
        {
            byte err = hardware_interface.transport_read(PicoTunerInterface.TS1, ref data, ref dataRead);
            DataReceived(PicoTunerInterface.TS1);
            return err;
        }


        void FlushTS1()
        {
            hardware_interface.transport_flush(PicoTunerInterface.TS1);
        }


        private void DataReceived(byte TS)
        {
            if (TS == 1)
            {
                ts_thread.NewDataPresent();
            }
            else
            {
                ts_thread2.NewDataPresent();
            }
        }

        private void hardware_init(bool manual, string i2c_serial, string ts_serial, string ts2_serial)
        {

            // detect ftdi devices
            uint i2c_port = 99;
            uint ts_port = 99;
            uint ts_port2 = 99;
            uint aux_port = 99;

            string deviceName = "Unknown";

            byte err = 0;

            if (manual)
            {
                err = hardware_interface.hw_detect(ref i2c_port, ref ts_port, ref ts_port2, ref aux_port, ref deviceName, i2c_serial, ts_serial, ts2_serial, "");
            }
            else
            {
                err = hardware_interface.hw_detect(ref i2c_port, ref ts_port, ref ts_port2, ref aux_port, ref deviceName);
            }


            if (ts_port2 == 99)
                ts_devices = 1;
            else
                ts_devices = 2;

            //ts_devices = 1;

            if (i2c_port == 99 || ts_port == 99)    // not detected properly, revert to 0 and 1 and hope for the best
            {
                ts_devices = 1;
                Log.Information("Hardware not detected properly, reverting to 0,1");
                err = hardware_interface.hw_init(0, 1, 99, 99);
            }
            else
            {
                Log.Information("Trying detected ports:");
                Log.Information("i2c port: " + i2c_port.ToString());
                Log.Information("ts port: " + ts_port.ToString());
                Log.Information("ts2 port: " + ts_port2.ToString());
                Log.Information("aux port: " + aux_port.ToString());
                err = hardware_interface.hw_init(i2c_port, ts_port, ts_port2, aux_port);
            }

            if (err != 0)
            {
                Log.Information("Main: Error: FTDI Failed " + err.ToString());
                hardware_connected = false;
                return;
            }

            hardware_connected = true;

            HardwareDevice = deviceName;

            if (AuxAvailable)
            {
                byte externValue = 0;
                for (int i = 0; i < _settings.ExternState.Length; i++)
                {
                    if (_settings.ExternState[i])
                        externValue |= (byte)(1 << i);
                }
                hardware_interface.aux_gpio_write(externValue);
            }
        }



        public override void ConfigureVideoPlayers(List<OTMediaPlayer> MediaPlayers)
        {
            if (MediaPlayers.Count != ts_devices)
            {
                Log.Information("Error: MinitiounerSource Expected " +  ts_devices.ToString() + " video players, but only received " + MediaPlayers.Count);
            }

            for (int c = 0; c < MediaPlayers.Count; c++)
            {
                _media_player.Add(MediaPlayers[c]);
                _media_player[c].onVideoOut += MinitiounerSource_onVideoOut;
                if (_settings.DefaultMuted[c])
                {
                    _media_player[c].SetVolume(0);
                }
                else
                {
                    _media_player[c].SetVolume((int)_settings.DefaultVolume[c]);
                }
            }

        }

        private void MinitiounerSource_onVideoOut(object sender, MediaStatus e)
        {
            for (int c = 0; c < _media_player.Count; c++)
            {
                if ((OTMediaPlayer)sender == _media_player[c])
                {
                    preMute[c] = (int)_settings.DefaultVolume[c];
                    muted[c] = _settings.DefaultMuted[c];
                    if (muted[c] == true)
                    {
                        _media_player[c].SetVolume(0);
                    }
                    else
                    {
                        _media_player[c].SetVolume(preMute[c]);
                    }

                    UpdateMediaProperties(c, e);
                    break;
                }
            }
        }

        public override string GetName()
        {
            return "Minitiouner Variant";
        }

        public override void OverrideDefaultMuted(bool Override)
        {
            if (Override)
            {
                preMute[0] = (int)_settings.DefaultVolume[0];               // save DefaultVolume in preMute
                _tuner1_properties.UpdateValue("volume_slider_1", "0");     // side effect: will set DefaultVolume to 0
                _tuner1_properties.UpdateMuteButtonColor("media_controls_1", Color.PaleVioletRed);
                muted[0] = _settings.DefaultMuted[0] = true;
                _settings.DefaultVolume[0] = (uint)preMute[0];              // restore DefaultVolume

                if (ts_devices == 2)
                {
                    preMute[1] = (int)_settings.DefaultVolume[1];           // save DefaultVolume in preMute
                    _tuner2_properties.UpdateValue("volume_slider_2", "0"); // side effect: will set DefaultVolume to 0
                    _tuner2_properties.UpdateMuteButtonColor("media_controls_2", Color.PaleVioletRed);
                    muted[1] = _settings.DefaultMuted[1] = true;
                    _settings.DefaultVolume[1] = (uint)preMute[1];          // restore DefaultVolume
                }
            }
        }

        public override string GetDescription()
        {
            return "Minitiouner Variant" +
            Environment.NewLine + Environment.NewLine +
            "Should work with most Minitiouner Variants" +
            Environment.NewLine + Environment.NewLine +
            "Select FTDI or PicoTuner interface in Settings";
        }

        public override void Close()
        {
            _settingsManager.SaveSettings(_settings);

            // Thread.Abort() doesn't exist on modern .NET (throws PlatformNotSupportedException,
            // which used to take the whole process down since these are foreground threads) -
            // signal each worker cooperatively instead and let it exit its own loop.
            ts_parser_thread?.Stop();
            ts_parser_thread2?.Stop();
            bool ts_thread_stopped = false;
            ts_thread?.Stop(ref ts_thread_stopped);
            bool ts_thread2_stopped = false;
            ts_thread2?.Stop(ref ts_thread2_stopped);
            nim_thread?.Stop();

            // Wait for NimThread to finish - WITH message pumping. This is the actual fix for
            // "Digole doesn't clear on exit" and the hang in Close(): NimThread runs
            // get_nim_status() -> nim_status_feedback() -> UpdateTunerProperties() -> OnSourceData
            // -> MainForm.UpdateInfo(), which does a SYNCHRONOUS Control.Invoke onto the UI
            // thread. We are on the UI thread here (FormClosing). A plain Join() or lock(HwLock)
            // without pumping therefore deadlocks whenever a get_nim_status() cycle is in flight
            // (NimThread waits for the UI thread inside Invoke, the UI thread waits for NimThread):
            // NimThread never reaches its "Loop exited" / Digole shutdown write, and Close() never
            // returns ("Bye!" never logged). Pumping lets that pending Invoke complete so the
            // worker can leave its loop, send the Digole greeting/clear and exit.
            // Normal case: finishes within one status cycle (~100-300 ms).
            bool nim_thread_joined = true;
            if (nim_thread_t != null)
            {
                var join_sw = System.Diagnostics.Stopwatch.StartNew();
                while (!(nim_thread_joined = nim_thread_t.Join(10)))
                {
                    if (join_sw.Elapsed > TimeSpan.FromSeconds(5))
                        break;
                    Application.DoEvents();
                }
                Log.Information("Nim Thread Join returned, joined=" + nim_thread_joined + ", waited=" + join_sw.ElapsedMilliseconds + "ms");
            }

            // Switch off TS LEDs and LNB-A/LNB-B power supply, then EXTERN-0..7.
            // NimThread has normally exited by now, so nothing races us on the shared FTDI
            // I2C-channel state (MPSSEbuffer etc.). If the join timed out it is still alive, so
            // take HwLock (bounded wait, never on an unbounded lock from the UI thread) to at
            // least not interleave with a transaction that is in flight.
            //
            // Previously missing entirely: the LNB supply (and its LNBA/LNBB indicator LEDs)
            // stayed on after Close() - nothing ever called hw_set_polarization_supply(..,
            // false, ..) on shutdown, only the TS LEDs were turned off.
            bool have_hw_lock = false;
            if (!nim_thread_joined && nim_thread != null)
            {
                have_hw_lock = Monitor.TryEnter(nim_thread.HwLock, TimeSpan.FromSeconds(2));
                if (!have_hw_lock)
                    Log.Warning("Close(): NimThread still running and HwLock not acquired, switching hardware off without it");
            }
            try
            {
                hardware_interface?.hw_ts_led(0, false);
                hardware_interface?.hw_ts_led(1, false);
                hardware_interface?.hw_set_polarization_supply(0, false, false);
                hardware_interface?.hw_set_polarization_supply(1, false, false);
            }
            finally
            {
                if (have_hw_lock)
                    Monitor.Exit(nim_thread.HwLock);
            }

            // EXTERN-0..7 (AUX chip GPIO) - separate FTDI device/USB connection from the NIM
            // I2C bus (see aux_gpio_write's doc comment), so unlike the writes above this isn't
            // racing NimThread and doesn't need HwLock either. Not tied to _settings.ExternState
            // (that's just the persisted UI checkbox state for next connect) - this
            // unconditionally drives the physical pins off so nothing stays energized after
            // OpenTuner closes.
            hardware_interface?.aux_gpio_write(0);
        }

        public override void ShowSettings()
        {
            MinitiounerSettingsForm settings_form = new MinitiounerSettingsForm(ref _settings);
            if (settings_form.ShowDialog() == DialogResult.OK) 
            {
                _settingsManager.SaveSettings(_settings);
                // Digole text can be applied live; other settings (interface, offsets, LNB defaults,
                // Digole enable/address) are read on connect and need a reconnect.
                nim_thread?.UpdateDigoleIdentity(_settings.DigoleCallsign, _settings.DigoleLocator, _settings.DigoleName); 

                // tuning trim (correction / capture range) is applied live: sliders, properties and a new tune
                for (int t = 0; t < 2; t++)
                {
                    uint capture = (_settings.CaptureRangeKHz != null && _settings.CaptureRangeKHz.Length > t) ? _settings.CaptureRangeKHz[t] : 0;
                    double correction = (_settings.FreqCorrectionPpm != null && _settings.FreqCorrectionPpm.Length > t) ? _settings.FreqCorrectionPpm[t] : 0;

                    if (capture != capture_range_khz[t] || correction != freq_correction_ppm[t])
                    {
                        (t == 0 ? _frequency_1 : _frequency_2)?.SetTrim(capture, correction, freq_offset_khz[t]);
                        ApplyTunerTrim(t, capture, correction, freq_offset_khz[t]);
                    }
                }
            }
            
        }

        public override void ConfigureTSRecorders(List<TSRecorder> TSRecorders)
        {
            _ts_recorders = TSRecorders;

            for (int c = 0; c < _ts_recorders.Count; c++)
            {
                _ts_recorders[c].onRecordStatusChange += MinitiounerSource_onRecordStatusChange;
            }

            if (_ts_recorders.Count > 0)
                _tuner1_properties?.SetRecordTooltip("media_controls_1", _ts_recorders[0].Path);
            if (_ts_recorders.Count > 1)
                _tuner2_properties?.SetRecordTooltip("media_controls_2", _ts_recorders[1].Path);
        }

        private void MinitiounerSource_onRecordStatusChange(object sender, bool e)
        {
            Log.Information(((TSRecorder)(sender)).ID.ToString() + " recording status : " + e.ToString());
        }

        public override void ConfigureTSStreamers(List<TSUdpStreamer> TSStreamers)
        {
            _ts_streamers = TSStreamers;
            
            for (int c = 0; c < _ts_streamers.Count; c++)
            {
                _ts_streamers[c].onStreamStatusChange += MinitiounerSource_onStreamStatusChange;
            }
        }

        private void MinitiounerSource_onStreamStatusChange(object sender, bool e)
        {
            Log.Information(((TSUdpStreamer)(sender)).ID.ToString() + " streaming status : " + e.ToString());
        }

        public override void ConfigureMediaPath(string MediaPath)
        {
            _mediapath = MediaPath;
            _tuner1_properties?.SetSnapshotTooltip("media_controls_1", _mediapath);
            _tuner2_properties?.SetSnapshotTooltip("media_controls_2", _mediapath);
        }

        public override string GetMoreInfoLink()
        {
            return "https://www.zr6tg.co.za/opentuner-minitiouner-source/";
        }
    }
}
