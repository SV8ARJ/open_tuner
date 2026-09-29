using LibVLCSharp.Shared;
using NAudio.Gui;
using Newtonsoft.Json.Linq;
using opentuner.MediaSources.Minitiouner.HardwareInterfaces;
using opentuner.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static opentuner.Transmit.F5OEOPlutoControl;
using System.Drawing;
using System.Runtime.CompilerServices;
using FlyleafLib.MediaFramework.MediaFrame;
using Vortice.MediaFoundation;
using Serilog;
using opentuner.MediaSources.Longmynd;
using NAudio.SoundFont;
using System.Globalization;

namespace opentuner.MediaSources.Minitiouner
{
    public enum MinitiounerPropertyCommands
    {
        SETFREQUENCY,
        SETRFINPUTA,
        SETRFINPUTB,
        SETSYMBOLRATE,
        SETOFFSET,
        LNBA_OFF,
        LNBA_VERTICAL,
        LNBA_HORIZONTAL,
        LNBB_OFF,
        LNBB_VERTICAL,
        LNBB_HORIZONTAL,
        SETPRESET,
        TONE22K_OFF,
        TONE22K_ON,
    }

    public partial class MinitiounerSource
    {
        // properties management
        Control _parent = null;

        private static DynamicPropertyGroup _tuner1_properties = null;
        private static DynamicPropertyGroup _tuner2_properties = null;
        private static DynamicPropertyGroup _source_properties = null;

        // context menu strip
        ContextMenuStrip _genericContextStrip;

        public override event SourceDataChange OnSourceData;

        private List<StoredFrequency> _frequency_presets = null;
        private bool BuildSourceProperties()
        {
            if (_parent == null)
            {
                Log.Information("Fatal Error: No Properties Panel");
                return false;
            }

            _genericContextStrip = new ContextMenuStrip();
            _genericContextStrip.Opening += _genericContextStrip_Opening;


            if (ts_devices == 2)
            {
                _tuner2_properties = ConfigureTunerProperties(2);
                muted[1] = _settings.DefaultMuted[1];
                preMute[1] = (int)_settings.DefaultVolume[1];

                if (!_settings.DefaultMuted[1])
                {
                    _tuner2_properties.UpdateValue("volume_slider_2", _settings.DefaultVolume[1].ToString());
                    _settings.DefaultVolume[1] = (uint)preMute[1];  // restore as changed by update volume_slider function
                    _tuner2_properties.UpdateMuteButtonColor("media_controls_2", Color.Transparent);
                }
                else
                {
                    _tuner2_properties.UpdateValue("volume_slider_2", "0");
                    _settings.DefaultVolume[1] = (uint)preMute[1];  // restore as changed by update volume_slider function
                    _tuner2_properties.UpdateMuteButtonColor("media_controls_2", Color.PaleVioletRed);
                }
            }

            _tuner1_properties = ConfigureTunerProperties(1);
            muted[0] = _settings.DefaultMuted[0];
            preMute[0] = (int)_settings.DefaultVolume[0];

            if (!_settings.DefaultMuted[0])
            {
                _tuner1_properties.UpdateValue("volume_slider_1", _settings.DefaultVolume[0].ToString());
                _settings.DefaultVolume[0] = (uint)preMute[0];  // restore as changed by update volume_slider function
                _tuner1_properties.UpdateMuteButtonColor("media_controls_1", Color.PaleVioletRed);
            }
            else
            {
                _tuner1_properties.UpdateMuteButtonColor("media_controls_1", Color.PaleVioletRed);
                _settings.DefaultVolume[0] = (uint)preMute[0];  // restore as changed by update volume_slider function
                _tuner1_properties.UpdateMuteButtonColor("media_controls_1", Color.PaleVioletRed);
            }

            // "Expert" tab panel (MainForm adds it as a tab next to "Properties"): the per-tuner views and
            // Switches live in it, only the tuner groups stay on "Properties"
            _expert_panel = new Panel();
            _expert_panel.Dock = DockStyle.Fill;
            _expert_panel.AutoScroll = true;
            stv0910.AllowLowSrClock = _settings.LowSrClock;
            stv0910.LowSrProfile = _settings.LowSrProfile;
            stv0910.MiniTiouneInit = _settings.MiniTiouneInit;
            stv0910.LowSrDvbS1 = _settings.LowSrDvbS1;
            stv0910.LowSrSrScan = _settings.LowSrSrScan;
            stv0910.LowSrManualSfr = _settings.LowSrManualSfr;
            stv0910.CarrierPhaseAlgo = (byte)Math.Max(0, Math.Min(2, (int)_settings.CarrierPhaseAlgo));
            stv0910.IqSwap = _settings.IqSwap;
            stv6120.BasebandGainCode = (byte)Math.Max(0, Math.Min(8, _settings.BasebandGainDb / 2));

            // "Special" tab panel: the per-tuner symbol rate / derotator / trim views, Minitiouner Properties below them
            _frequency_panel = new Panel();
            _frequency_panel.Dock = DockStyle.Fill;
            _frequency_panel.AutoScroll = true;

            // source properties
            _source_properties = new DynamicPropertyGroup("Minitiouner Properties", _frequency_panel);
            _source_properties.setID(99);
            _source_properties.AddItem("source_hw_interface", "Hardware Interface");
            _source_properties.AddItem("source_chip_id", "Chip ID");
            _source_properties.AddItem("source_pll_status", "PLL Status");

            _tuner_forms = new List<TunerControlForm>();
            // tuner for each device
            for (int c = 0; c < ts_devices; c++)
            {
                var tunerControl = new TunerControlForm(c, 0, 0, (c == 0 ? current_offset_0 : current_offset_1), this);
                tunerControl.OnTunerChange += TunerControl_OnTunerChange;
                _tuner_forms.Add(tunerControl);
            }

            BuildSwitchesPanel();
            BuildExpertPanel();

            // Stack order on the "Properties" tab, top to bottom: Tuner 1, Tuner 2. All groups are
            // Dock=Top, so each BringToFront() moves the group below the ones before it.
            _tuner1_properties.BringToFront();
            if (ts_devices == 2) _tuner2_properties.BringToFront();

            return true;
        }

        // EXTERN-0..7 (AUX chip GPIO, MiniTiounerPro V2 only) as live checkboxes - not something
        // DynamicPropertyGroup's label+context-menu item model supports, so this is a small
        // custom panel added directly to _parent instead, positioned after both tuner groups.
        private CustomGroupBox _switches_groupBox = null;

        // "Expert" tab (next to "Properties", added by MainForm via GetExpertPanel()): one group per
        // tuner with gauges, lock LEDs and the I/Q constellation (see ExpertTunerView).
        private Panel _expert_panel = null;
        private ExpertTunerView _expert_1 = null;
        private ExpertTunerView _expert_2 = null;

        // "Special" tab: symbol rate buttons, derotator and the tuning trim per tuner
        private Panel _frequency_panel = null;
        private FrequencyTunerView _frequency_1 = null;
        private FrequencyTunerView _frequency_2 = null;

        public override List<KeyValuePair<string, Control>> GetExtraTabs()
        {
            var tabs = new List<KeyValuePair<string, Control>>();

            if (_expert_panel != null)
                tabs.Add(new KeyValuePair<string, Control>("Expert", _expert_panel));

            if (_frequency_panel != null)
                tabs.Add(new KeyValuePair<string, Control>("Special", _frequency_panel));

            return tabs;
        }

        private void BuildExpertPanel()
        {
            // All groups are Dock=Top, so each BringToFront() moves the group below the ones before it:
            // Tuner 1, Tuner 2, Switches (the views bring themselves to the front).
            _expert_1 = new ExpertTunerView("Tuner 1", _expert_panel);
            if (ts_devices == 2)
                _expert_2 = new ExpertTunerView("Tuner 2", _expert_panel);

            _switches_groupBox.BringToFront();

            // "Special" tab: Tuner 1, Tuner 2, then Minitiouner Properties
            _frequency_1 = new FrequencyTunerView("Tuner 1", _frequency_panel);
            _frequency_1.DefaultCorrectionPpm = _settings.DefaultFreqCorrectionPpm;
            _frequency_1.SetTrim(capture_range_khz[0], freq_correction_ppm[0], freq_offset_khz[0]);
            _frequency_1.TrimChanged += (capture, correction, offset) => ApplyTunerTrim(0, capture, correction, offset);
            _frequency_1.SymbolRateSelected += rate => { ChangeSymbolRate(0, rate); ResetVideo(0); };

            if (ts_devices == 2)
            {
                _frequency_2 = new FrequencyTunerView("Tuner 2", _frequency_panel);
                _frequency_2.DefaultCorrectionPpm = _settings.DefaultFreqCorrectionPpm;
                _frequency_2.SetTrim(capture_range_khz[1], freq_correction_ppm[1], freq_offset_khz[1]);
                _frequency_2.TrimChanged += (capture, correction, offset) => ApplyTunerTrim(1, capture, correction, offset);
                _frequency_2.SymbolRateSelected += rate => { ChangeSymbolRate(1, rate); ResetVideo(1); };
            }

            _source_properties.BringToFront();
        }

        private void BuildSwitchesPanel()
        {
            _switches_groupBox = new CustomGroupBox();
            _switches_groupBox.Dock = DockStyle.Top;
            _switches_groupBox.AutoSize = true;
            _switches_groupBox.Text = "Switches";
            _switches_groupBox.Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, (byte)0);
            _switches_groupBox.Padding = new Padding(8, 20, 8, 8);

            // RF control row - LNB-A/LNB-B power supply and 22kHz tone as real dropdowns, not
            // just the right-click context menu on the label further up (hard to discover).
            // These call the exact same command handlers as that context menu, just directly.
            string[] lnbOptions = { "OFF", "Vertical (12V)", "Horizontal (18V)" };

            var labelLnbA = new Label();
            labelLnbA.AutoSize = true;
            labelLnbA.Text = "LNB-A:";
            labelLnbA.Location = new Point(8, 27);
            _switches_groupBox.Controls.Add(labelLnbA);

            var comboLnbA = new ComboBox();
            comboLnbA.DropDownStyle = ComboBoxStyle.DropDownList;
            comboLnbA.Items.AddRange(lnbOptions);
            comboLnbA.SelectedIndex = current_lnba_psu;
            comboLnbA.Location = new Point(70, 24);
            comboLnbA.Width = 130;
            comboLnbA.SelectedIndexChanged += (sender, e) =>
            {
                var commands = new[] { MinitiounerPropertyCommands.LNBA_OFF, MinitiounerPropertyCommands.LNBA_VERTICAL, MinitiounerPropertyCommands.LNBA_HORIZONTAL };
                properties_OnPropertyMenuSelect(commands[comboLnbA.SelectedIndex], new int[] { 0, 0 });
            };
            _switches_groupBox.Controls.Add(comboLnbA);

            var labelLnbB = new Label();
            labelLnbB.AutoSize = true;
            labelLnbB.Text = "LNB-B:";
            labelLnbB.Location = new Point(216, 27);
            _switches_groupBox.Controls.Add(labelLnbB);

            var comboLnbB = new ComboBox();
            comboLnbB.DropDownStyle = ComboBoxStyle.DropDownList;
            comboLnbB.Items.AddRange(lnbOptions);
            comboLnbB.SelectedIndex = current_lnbb_psu;
            comboLnbB.Location = new Point(278, 24);
            comboLnbB.Width = 130;
            comboLnbB.SelectedIndexChanged += (sender, e) =>
            {
                var commands = new[] { MinitiounerPropertyCommands.LNBB_OFF, MinitiounerPropertyCommands.LNBB_VERTICAL, MinitiounerPropertyCommands.LNBB_HORIZONTAL };
                properties_OnPropertyMenuSelect(commands[comboLnbB.SelectedIndex], new int[] { 0, 0 });
            };
            _switches_groupBox.Controls.Add(comboLnbB);

            // 22kHz tone - independent per tuner (22K-A/22K-B = 22K_TX1/22K_TX2), stacked
            // directly under the matching LNB-A/LNB-B dropdown above.
            var labelToneA = new Label();
            labelToneA.AutoSize = true;
            labelToneA.Text = "22K-A:";
            labelToneA.Location = new Point(8, 63);
            _switches_groupBox.Controls.Add(labelToneA);

            var comboToneA = new ComboBox();
            comboToneA.DropDownStyle = ComboBoxStyle.DropDownList;
            comboToneA.Items.AddRange(new[] { "OFF", "ON" });
            comboToneA.SelectedIndex = current_tone_22kHz_0 ? 1 : 0;
            comboToneA.Location = new Point(70, 60);
            comboToneA.Width = 130;
            comboToneA.SelectedIndexChanged += (sender, e) =>
            {
                properties_OnPropertyMenuSelect(comboToneA.SelectedIndex == 1 ? MinitiounerPropertyCommands.TONE22K_ON : MinitiounerPropertyCommands.TONE22K_OFF, new int[] { 0 });
            };
            _switches_groupBox.Controls.Add(comboToneA);

            var labelToneB = new Label();
            labelToneB.AutoSize = true;
            labelToneB.Text = "22K-B:";
            labelToneB.Location = new Point(216, 63);
            _switches_groupBox.Controls.Add(labelToneB);

            var comboToneB = new ComboBox();
            comboToneB.DropDownStyle = ComboBoxStyle.DropDownList;
            comboToneB.Items.AddRange(new[] { "OFF", "ON" });
            comboToneB.SelectedIndex = current_tone_22kHz_1 ? 1 : 0;
            comboToneB.Location = new Point(278, 60);
            comboToneB.Width = 130;
            comboToneB.SelectedIndexChanged += (sender, e) =>
            {
                properties_OnPropertyMenuSelect(comboToneB.SelectedIndex == 1 ? MinitiounerPropertyCommands.TONE22K_ON : MinitiounerPropertyCommands.TONE22K_OFF, new int[] { 1 });
            };
            _switches_groupBox.Controls.Add(comboToneB);

            // One-shot pre-DiSEqC 1.0 tone burst ("mini-DiSEqC" A/B switching, see stv0910_send_tone_burst).
            // It forces DISEQC_MODE=3 itself for the pulse; NimThread.worker_thread puts the continuous
            // 22K-A/22K-B tone back afterwards (from current_config), so nothing needs doing here beyond
            // triggering it.
            var buttonBurstA = new Button();
            buttonBurstA.Text = "Send Burst A";
            buttonBurstA.Location = new Point(70, 96);
            buttonBurstA.Width = 130;
            buttonBurstA.Click += (sender, e) => SendToneBurst(0);
            _switches_groupBox.Controls.Add(buttonBurstA);

            var buttonBurstB = new Button();
            buttonBurstB.Text = "Send Burst B";
            buttonBurstB.Location = new Point(278, 96);
            buttonBurstB.Width = 130;
            buttonBurstB.Click += (sender, e) => SendToneBurst(1);
            _switches_groupBox.Controls.Add(buttonBurstB);

            int externTop = 132;

            if (!AuxAvailable)
            {
                var noticeLabel = new Label();
                noticeLabel.AutoSize = true;
                noticeLabel.Text = "EXTERN-0..7 not available (no AUX/TS1-A FT2232H detected)";
                noticeLabel.Location = new Point(8, externTop);
                _switches_groupBox.Controls.Add(noticeLabel);
                _switches_groupBox.Height = externTop + 32;
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    var checkBox = new CheckBox();
                    checkBox.AutoSize = true;
                    checkBox.Text = "EXTERN-" + i.ToString();
                    checkBox.Tag = i;
                    checkBox.Checked = GetExternOutput(i);
                    checkBox.Location = new Point(8 + (i % 4) * 110, externTop + (i / 4) * 26);
                    checkBox.CheckedChanged += (sender, e) =>
                    {
                        var cb = (CheckBox)sender;
                        SetExternOutput((int)cb.Tag, cb.Checked);
                    };
                    _switches_groupBox.Controls.Add(checkBox);
                }
                _switches_groupBox.Height = externTop + 60;
            }

            _expert_panel.Controls.Add(_switches_groupBox);
        }


        private DynamicPropertyGroup ConfigureTunerProperties(int tuner)
        {
            DynamicPropertyGroup dynamicPropertyGroup = new DynamicPropertyGroup("Tuner " +  tuner.ToString(), _parent);
            dynamicPropertyGroup.setID(tuner);
            dynamicPropertyGroup.OnSlidersChanged += DynamicPropertyGroup_OnSliderChanged;
            dynamicPropertyGroup.OnMediaButtonPressed += DynamicPropertyGroup_OnMediaButtonPressed;
            dynamicPropertyGroup.AddItem("demodstate", "Demod State", Color.PaleVioletRed);
            dynamicPropertyGroup.AddItem("ts_status", "TS Status", Color.Gray);
            dynamicPropertyGroup.AddItem("mer", "MER");
            dynamicPropertyGroup.AddItem("cn_needed", "C/N needed");
            //dynamicPropertyGroup.AddItem("db_margin", "db Margin");
            dynamicPropertyGroup.AddItem("rf_input_level", "RF Input Level");
            dynamicPropertyGroup.AddItem("rf_input", "RF Input", _genericContextStrip);
            dynamicPropertyGroup.AddItem("requested_freq_" + tuner.ToString(), "Requested Freq", _genericContextStrip);
            dynamicPropertyGroup.AddItem("symbol_rate", "Symbol Rate", _genericContextStrip); // requested, right click to choose
            dynamicPropertyGroup.AddItem("measured_sr", "Measured SR");                        // demodulator, only while locked
            dynamicPropertyGroup.AddItem("offset", "Freq Offset", _genericContextStrip);
            dynamicPropertyGroup.AddItem("modcod", "Modcod");
            dynamicPropertyGroup.AddItem("lna_gain", "LNA Gain");
            dynamicPropertyGroup.AddItem("ber", "BER");
            dynamicPropertyGroup.AddItem("freq_carrier_offset", "Freq Carrier Offset");
            dynamicPropertyGroup.AddItem("stream_format", "Stream Format");
            dynamicPropertyGroup.AddItem("service_name", "Service Name");
            dynamicPropertyGroup.SetValueBold("service_name");
            dynamicPropertyGroup.AddItem("service_name_provider", "Service Name Provider");
            dynamicPropertyGroup.AddItem("null_packets", "Null Packets");
            dynamicPropertyGroup.AddItem("video_codec", "Video Codec");
            dynamicPropertyGroup.AddItem("video_resolution", "Video Resolution");
            dynamicPropertyGroup.AddItem("audio_codec", "Audio Codec");
            dynamicPropertyGroup.AddItem("audio_rate", "Audio Rate");
            dynamicPropertyGroup.AddSlider("volume_slider_" + tuner.ToString(), "Volume", 0, 200);
            dynamicPropertyGroup.AddMediaControls("media_controls_" + tuner.ToString(), "Media Controls");
            return dynamicPropertyGroup;
        }

        private int[] preMute = new int[] { 50, 50 };
        private bool[] muted = new bool[] { true, true };
//        private int indicatorStatus1 = 0;
//        private int indicatorStatus2 = 0;

        

//        public void SetIndicator(ref int indicatorInput, PropertyIndicators indicator)
//        {
//            indicatorInput |= (byte)(1 << (int)indicator);
//        }

//        public void ClearIndicator(ref int indicatorInput, PropertyIndicators indicator)
//        {
//            indicatorInput &= (byte)~(1 << (int)indicator);
//        }

        private void DynamicPropertyGroup_OnMediaButtonPressed(string key, int function)
        {
            int tuner = 0;

            if (key == "media_controls_2")
                tuner = 1;

            switch (function)
            {
                case 0: // mute
                    if (tuner == 0)
                    {
                        if (!muted[0])
                        {
                            preMute[0] = _media_player[0].GetVolume();
                            UpdateVolume(0, 0);
                            _tuner1_properties.UpdateValue("volume_slider_1", "0");
                            _settings.DefaultVolume[0] = (byte)preMute[0];
                            _settings.DefaultMuted[0] = muted[0] = true;
                            _tuner1_properties.UpdateMuteButtonColor("media_controls_1", Color.PaleVioletRed);
                        }
                        else
                        {
                            _media_player[0].SetVolume(preMute[0]);
                            _tuner1_properties.UpdateValue("volume_slider_1", preMute[0].ToString());
                            _settings.DefaultMuted[0] = muted[0] = false;
                            _tuner1_properties.UpdateMuteButtonColor("media_controls_1", Color.Transparent);
                        }
                    }
                    else
                    {
                        if (!muted[1])
                        {
                            preMute[1] = _media_player[1].GetVolume();
                            _media_player[1].SetVolume(0);
                            _tuner2_properties.UpdateValue("volume_slider_2", "0");
                            _settings.DefaultVolume[1] = (byte)preMute[1];
                            _settings.DefaultMuted[1] = muted[1] = true;
                            _tuner2_properties.UpdateMuteButtonColor("media_controls_2", Color.PaleVioletRed);
                        }
                        else
                        {
                            _media_player[1].SetVolume(preMute[1]);
                            _tuner2_properties.UpdateValue("volume_slider_2", preMute[1].ToString());
                            _settings.DefaultMuted[1] = muted[1] = false;
                            _tuner2_properties.UpdateMuteButtonColor("media_controls_2", Color.Transparent);
                        }

                    }

                    break;
                case 1: // snaphost
                    {
                        string station = (tuner == 0 ? _tuner1_properties : _tuner2_properties).GetValue("service_name");
                        uint sr_ks = tuner == 0 ? current_sr_0 : current_sr_1;
                        string snapshot_filename = CommonFunctions.GenerateTimestampFilename(station, sr_ks) + ".png";
                        string snapshot_path = _mediapath + snapshot_filename;
                        Log.Information("Snapshot: tuner " + tuner.ToString() + ", " + snapshot_path);
                        _media_player[tuner].SnapShot(snapshot_path);
                        (tuner == 0 ? _tuner1_properties : _tuner2_properties).ShowNotice("media_controls_" + (tuner + 1).ToString(), "Saved: " + snapshot_path);
                    }
                    break;
                case 2: // record
                    Log.Information("Record: " + tuner.ToString());

                    if (_ts_recorders[tuner].record)
                    {
//                        ClearIndicator(ref ((tuner == 0) ? ref indicatorStatus1 : ref indicatorStatus2), PropertyIndicators.RecordingIndicator);
                        _ts_recorders[tuner].record = false;    // stop recording
                    }
                    else
                    {
//                        SetIndicator(ref ((tuner == 0) ? ref indicatorStatus1 : ref indicatorStatus2), PropertyIndicators.RecordingIndicator);
                        _ts_recorders[tuner].record = true;     // start recording
                    }

                    if (tuner == 0)
                    {
                        if (_ts_recorders[0].record == true)
                        {
                            _tuner1_properties.UpdateRecordButtonColor("media_controls_1", Color.PaleVioletRed);
                        }
                        else
                        {
                            _tuner1_properties.UpdateRecordButtonColor("media_controls_1", Color.Transparent);
                        }
//                        _tuner1_properties.UpdateValue("media_controls_1", indicatorStatus1.ToString());
                    }
                    else
                    {
                        if (_ts_recorders[1].record == true)
                        {
                            _tuner2_properties.UpdateRecordButtonColor("media_controls_2", Color.PaleVioletRed);
                        }
                        else
                        {
                            _tuner2_properties.UpdateRecordButtonColor("media_controls_2", Color.Transparent);
                        }
//                        _tuner2_properties.UpdateValue("media_controls_2", indicatorStatus2.ToString());
                    }
                    break;
                case 3: // udp stream
                    Log.Information("UDP Stream: " + tuner.ToString());

                    if (_ts_streamers[tuner].stream)
                    {
//                        ClearIndicator(ref ((tuner == 0) ? ref indicatorStatus1 : ref indicatorStatus2), PropertyIndicators.StreamingIndicator);
                        _ts_streamers[tuner].stream = false;
                    }
                    else
                    {
//                        SetIndicator(ref ((tuner == 0) ? ref indicatorStatus1 : ref indicatorStatus2), PropertyIndicators.StreamingIndicator);
                        _ts_streamers[tuner].stream = true;
                    }

                    if (tuner == 0)
                    {
                        if (_ts_streamers[0].stream == true)
                        {
                            _tuner1_properties.UpdateStreamButtonColor("media_controls_1", Color.PaleTurquoise);
                        }
                        else
                        {
                            _tuner1_properties.UpdateStreamButtonColor("media_controls_1", Color.Transparent);
                        }
//                        _tuner1_properties.UpdateValue("media_controls_1", indicatorStatus1.ToString());
                    }
                    else
                    {
                        if (_ts_streamers[1].stream == true)
                        {
                            _tuner2_properties.UpdateStreamButtonColor("media_controls_2", Color.PaleTurquoise);
                        }
                        else
                        {
                            _tuner2_properties.UpdateStreamButtonColor("media_controls_2", Color.Transparent);
                        }
//                        _tuner2_properties.UpdateValue("media_controls_2", indicatorStatus2.ToString());
                    }

                    break;
                case 4: // stop TS
                    Log.Information("Stop TS: " + tuner.ToString());
                    StopTuner(tuner);
                    break;
            }
        }

        private void DynamicPropertyGroup_OnSliderChanged(string key, int value)
        {
            switch (key)
            {
                case "volume_slider_1":
                    // cancel mute
                    _settings.DefaultMuted[0] = muted[0] = false;
                    if (_media_player.Count > 0) 
                    {
                        _media_player[0]?.SetVolume(value);
                    }
                    
                    _settings.DefaultVolume[0] = (byte)value;
                    _tuner1_properties.UpdateMuteButtonColor("media_controls_1", Color.Transparent);
                    break;
                case "volume_slider_2":
                    // cancel mute
                    _settings.DefaultMuted[1] = muted[1] = false;
                    if (_media_player.Count > 0)
                    {
                        _media_player[1]?.SetVolume(value);
                    }
                    _settings.DefaultVolume[1] = (byte)value;
                    _tuner2_properties.UpdateMuteButtonColor("media_controls_2", Color.Transparent);
                    break;
            }
        }

        // TS error counters of the last few seconds for the info line above the video (issue #19).
        private static void FillTsHealth(OTSourceData data, TSParserThread parser, TSThread ts_thread)
        {
            if (parser != null)
            {
                TSHealthCounts h = parser.Health.Get();
                data.ts_cc_errors = h.Continuity;
                data.ts_tei_errors = h.Transport;
                data.ts_sync_losses = h.Sync;
                data.ts_scrambled = h.Scrambled;
            }

            if (ts_thread != null)
                data.ts_buffer_overflows = ts_thread.Health.Get().Overflow;
        }

        private static string TsErrorText(TSParserThread parser, TSThread ts_thread)
        {
            TSHealthCounts h = parser != null ? parser.Health.Get() : new TSHealthCounts();

            if (ts_thread != null)
                h.Overflow = ts_thread.Health.Get().Overflow;

            return TSHealth.Describe(h);
        }

        private void UpdateTSProperties(int tuner, TSStatus ts_status)
        {
            DynamicPropertyGroup _tuner = (tuner == 1 ? _tuner1_properties : _tuner2_properties);

            _tuner.UpdateValue("service_name", ts_status.ServiceName);
            _tuner.UpdateValue("service_name_provider", ts_status.ServiceProvider);
            _tuner.UpdateValue("null_packets", ts_status.NullPacketsPerc.ToString() + "%");

            if (tuner == 1)
            {
                last_service_name_0 = ts_status.ServiceName;
                last_service_provider_0 = ts_status.ServiceProvider;
            }
            else
            {
                last_service_name_1 = ts_status.ServiceName;
                last_service_provider_1 = ts_status.ServiceProvider;
            }

            nim_thread?.UpdateServiceName(tuner - 1, ts_status.ServiceName);
        }

        private void UpdateMediaProperties(int player, MediaStatus media_status)
        {
            int tuner = player + 1;

            DynamicPropertyGroup _tuner = (tuner == 1 ? _tuner1_properties : _tuner2_properties);

            _tuner.UpdateTitle("Tuner " + tuner.ToString() + " - " + _media_player[player].GetName());

            string video_res = media_status.VideoWidth.ToString() + " x " + media_status.VideoHeight.ToString();
            string audio_rate = media_status.AudioRate.ToString() + " Hz, " + media_status.AudioChannels.ToString() + " channels";

            _tuner.UpdateValue("video_codec", MediaStatus.CodecDisplayName(media_status.VideoCodec));
            _tuner.UpdateValue("video_resolution", video_res);
            _tuner.UpdateValue("audio_codec", media_status.AudioCodec);
            _tuner.UpdateValue("audio_rate", audio_rate);

            if (player == 0) last_video_codec_0 = media_status.VideoCodec;
            else if (player == 1) last_video_codec_1 = media_status.VideoCodec;

            nim_thread?.UpdateVideoCodec(player, media_status.VideoCodec);
        }

        // "LED"-style live indicator (colored background, no separate value text needed) for the
        // decoded TSSTATUS bits (line_ok/error/nosync) - see stv0910_read_ts_status_decoded.
        // Green = TS line OK, Red = TS error, Orange = no sync, Gray = neither (e.g. not tuned).
        private void UpdateTsStatusLed(DynamicPropertyGroup tunerProperties, bool line_ok, bool error, bool nosync)
        {
            if (error)
            {
                tunerProperties.UpdateValue("ts_status", "ERROR");
                tunerProperties.UpdateColor("ts_status", Color.Red);
            }
            else if (line_ok)
            {
                tunerProperties.UpdateValue("ts_status", "OK");
                tunerProperties.UpdateColor("ts_status", Color.LimeGreen);
            }
            else if (nosync)
            {
                tunerProperties.UpdateValue("ts_status", "NO SYNC");
                tunerProperties.UpdateColor("ts_status", Color.Orange);
            }
            else
            {
                tunerProperties.UpdateValue("ts_status", "-");
                tunerProperties.UpdateColor("ts_status", Color.Gray);
            }
        }

        // LNA gain of the NIM's STVVGLNA from the raw readout ((SWLNAGAIN << 5) | VGO). With SWLNAGAIN = 3 (highest curve)
        // the gain is 12,3 dB at VGO 31 and falls 0,25 dB per VGO step (fitted to MiniTioune: VGO 31 = 12,3 dB, VGO 29 =
        // 11,8 dB). Other curves are not known, they show the raw number with a question mark.
        private static string LnaGainText(ushort raw)
        {
            double db;
            return stvvglna.stvvglna_gain_db(raw, out db) ? db.ToString("N1") + " dB (" + raw + ")" : raw.ToString() + " (?)";
        }

        // "C/N needed" for the received MODCOD (threshold table in lookups.cs) with the margin to the
        // measured MER in brackets, e.g. "4,7 dB [D 3,6]". "-" while there is no real MODCOD (not
        // locked, or a dummy frame, whose table entry is 0).
        private static string CnNeededText(byte demod_status, uint modcode, double mer)
        {
            double needed = CnNeededDb(demod_status, modcode);

            if (double.IsNaN(needed))
                return "-";

            return needed.ToString("N1") + " dB [D " + (mer - needed).ToString("N1") + "]";
        }

        // C/N in dB the received MODCOD needs, NaN if there is no real MODCOD.
        private static double CnNeededDb(byte demod_status, uint modcode)
        {
            double needed;

            if (demod_status == stv0910.DEMOD_S2 && modcode != 0 && lookups.modcod_lookup_dvbs2_threshold.TryGetValue(modcode, out needed))
                return needed;

            if (demod_status == stv0910.DEMOD_S && lookups.modcod_lookup_dvbs_threshold.TryGetValue(modcode, out needed) && needed != 0)
                return needed;

            return double.NaN;
        }

        private void UpdateTunerProperties(TunerStatus new_status)
        {
            // Marshal to the UI thread once here instead of once per DynamicPropertyItem/DynamicPropertyGroup
            // call below (see issue #6): each of the ~40 individual UpdateValue/UpdateColor/UpdateBigLabel
            // calls in this method otherwise does its own separate Control.Invoke, called every ~200ms from
            // NimThread while it holds HwLock - measured at up to ~2.3s of accumulated marshalling per status
            // update under load, stalling channel changes, closing the BATC chat window and app shutdown for
            // that whole time. Doing it once here makes every InvokeRequired check below false, so they fall
            // straight through to a direct (already-on-the-UI-thread) update.
            if (_parent != null && _parent.InvokeRequired)
            {
                try
                {
                    _parent.Invoke(new Action<TunerStatus>(UpdateTunerProperties), new_status);
                }
                catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException || ex is System.ComponentModel.InvalidAsynchronousStateException)
                {
                }
                return;
            }

            CheckSrFallback(new_status);

            double dbmargin = 0;
            double mer = Convert.ToDouble(new_status.T1P2_mer) / 10;
            double mer2 = Convert.ToDouble(new_status.T2P1_mer) / 10;

            // general
            _source_properties.UpdateValue("source_chip_id", "MID 0x" + new_status.chip_mid.ToString("X2") + " (ident " + (new_status.chip_mid >> 4) +
                                           ", release " + (new_status.chip_mid & 0x0F) + "), DID 0x" + new_status.chip_did.ToString("X2"));
            _source_properties.UpdateValue("source_pll_status", new_status.pll_locked ? "PLLLOCK: locked" : "PLLLOCK: NOT LOCKED");
            _source_properties.UpdateColor("source_pll_status", new_status.pll_locked ? Color.LimeGreen : Color.Red);

            // Expert tab: gauges, lock LEDs and I/Q constellation per tuner
            _expert_1?.Update(new_status.T1P2_demod_status, new_status.T1P2_input_power_level, mer, new_status.T1P2_dstatus,
                              new_status.T1P2_dstatus2, new_status.T1P2_ldi, new_status.T1P2_tmglock, new_status.T1P2_symbol_rate,
                              new_status.T1P2_constellation, new_status.T1P2_lock_time_ms,
                              CnNeededDb(new_status.T1P2_demod_status, new_status.T1P2_modcode),
                              new_status.T1P2_ldpc_iterations, new_status.T1P2_ldpc_max_iterations, new_status.T1P2_viterbi_error_rate,
                              new_status.T1P2_pdelstatus1, new_status.T1P2_spectrum_inverted, new_status.bcherr,
                              new_status.errors_ldpc_count, new_status.refresh_ms, new_status.T1P2_noise, new_status.T1P2_ts_bitrate_raw,
                              new_status.T1P2_agc1_gain, new_status.T1P2_agc2_gain);
            _frequency_1?.SetRequestedRate(current_sr_0);
            _frequency_1?.Update(new_status.T1P2_demod_status, new_status.T1P2_frequency_carrier_offset,
                                 new_status.T1P2_carrier_low_hz, new_status.T1P2_carrier_up_hz, new_status.T1P2_symbol_rate,
                                 (double)current_frequency_0 + current_offset_0, current_frequency_0, new_status.T1P2_agc2_gain);
            _expert_2?.Update(new_status.T2P1_demod_status, new_status.T2P1_input_power_level, mer2, new_status.T2P1_dstatus,
                              new_status.T2P1_dstatus2, new_status.T2P1_ldi, new_status.T2P1_tmglock, new_status.T2P1_symbol_rate,
                              new_status.T2P1_constellation, new_status.T2P1_lock_time_ms,
                              CnNeededDb(new_status.T2P1_demod_status, new_status.T2P1_modcode),
                              new_status.T2P1_ldpc_iterations, new_status.T2P1_ldpc_max_iterations, new_status.T2P1_viterbi_error_rate,
                              new_status.T2P1_pdelstatus1, new_status.T2P1_spectrum_inverted, new_status.bcherr,
                              new_status.errors_ldpc_count, new_status.refresh_ms, new_status.T2P1_noise, new_status.T2P1_ts_bitrate_raw,
                              new_status.T2P1_agc1_gain, new_status.T2P1_agc2_gain);
            _frequency_2?.SetRequestedRate(current_sr_1);
            _frequency_2?.Update(new_status.T2P1_demod_status, new_status.T2P1_frequency_carrier_offset,
                                 new_status.T2P1_carrier_low_hz, new_status.T2P1_carrier_up_hz, new_status.T2P1_symbol_rate,
                                 (double)current_frequency_1 + current_offset_1, current_frequency_1, new_status.T2P1_agc2_gain);

            // tuner 1 properties  *************
            _tuner1_properties.UpdateValue("demodstate", lookups.demod_state_lookup[new_status.T1P2_demod_status]);
            UpdateTsStatusLed(_tuner1_properties, new_status.T1P2_ts_line_ok, new_status.T1P2_ts_error, new_status.T1P2_ts_nosync);

            if (new_status.T1P2_demod_status > 1)
            {
                _tuner1_properties.UpdateColor("demodstate", Color.PaleGreen);
            }
            else
            {
                _tuner1_properties.UpdateColor("demodstate", Color.PaleVioletRed);
            }

            _tuner1_properties.UpdateValue("mer", mer.ToString() + " dB");
            _tuner1_properties.UpdateValue("lna_gain", LnaGainText(new_status.T1P2_lna_gain));
            _tuner1_properties.UpdateValue("rf_input_level", new_status.T1P2_input_power_level.ToString() + " dBm");
            // "Symbol Rate" is the rate the tuner is set to (right click: 33 / 25 / 20 ...), "Measured SR" what the
            // demodulator reads back - that is only meaningful while locked (unlocked it sits at its search limit)
            _tuner1_properties.UpdateValue("symbol_rate", current_sr_0.ToString());
            _tuner1_properties.UpdateValue("measured_sr", (new_status.T1P2_demod_status == stv0910.DEMOD_S || new_status.T1P2_demod_status == stv0910.DEMOD_S2)
                ? (new_status.T1P2_symbol_rate / 1000.0).ToString("N2") + " kS" : "-");
            _tuner1_properties.UpdateValue("ber", new_status.T1P2_ber.ToString());
            _tuner1_properties.UpdateValue("freq_carrier_offset", new_status.T1P2_frequency_carrier_offset.ToString());
            _tuner1_properties.UpdateValue("requested_freq_1", "(" + GetFrequency(0, true).ToString("N0") + ") (" + GetFrequency(0, false).ToString("N0") + ")");
            _tuner1_properties.UpdateValue("rf_input", (new_status.T1P2_rf_input == 1 ? "A" : "B"));
            _tuner1_properties.UpdateValue("stream_format", lookups.stream_format_lookups[Convert.ToInt32(new_status.T1P2_stream_format)].ToString());
            _tuner1_properties.UpdateValue("offset", current_offset_0.ToString());

            // clear ts data if not locked onto signal
            if (new_status.T1P2_demod_status < 2)
            {
                _tuner1_properties.UpdateValue("service_name_provider", "");
                _tuner1_properties.UpdateValue("service_name", "");
                _tuner1_properties.UpdateValue("stream_format", "");

                _tuner1_properties.UpdateValue("video_codec", "");
                _tuner1_properties.UpdateValue("video_resolution", "");                    
                _tuner1_properties.UpdateValue("audio_codec", "");
                _tuner1_properties.UpdateValue("audio_rate", "");

                // stop recording if recording
                // _ts_recorders/_ts_streamers can still be null here: the Minitiouner status-read
                // thread starts in Initialize() before MainForm wires up ConfigureTSRecorders/
                // ConfigureTSStreamers, so an early status update can race ahead of that setup.
                if (_ts_recorders != null && _ts_recorders[0].record)
                {
//                    ClearIndicator(ref indicatorStatus1, PropertyIndicators.RecordingIndicator);
                    _ts_recorders[0].record = false;    // stop recording
                    _tuner1_properties.UpdateRecordButtonColor("media_controls_1", Color.Transparent);
//                    _tuner1_properties.UpdateValue("media_controls_1", indicatorStatus1.ToString());
                }

                // stop streaming
                if (_ts_streamers != null && _ts_streamers[0].stream)
                {
//                    ClearIndicator(ref indicatorStatus1, PropertyIndicators.StreamingIndicator);
                    _ts_streamers[0].stream = false;    // stop streaming
                    _tuner1_properties.UpdateStreamButtonColor("media_controls_1", Color.Transparent);
//                    _tuner1_properties.UpdateValue("media_controls_1", indicatorStatus1.ToString());
                }


            }

            // db margin / modcod
            string modcod_text = "Unknown";
            string db_margin_text = "";
            try
            {
                switch (new_status.T1P2_demod_status)
                {
                    case 2:
                        modcod_text = lookups.modcod_lookup_dvbs2[new_status.T1P2_modcode];
                        dbmargin = (mer - lookups.modcod_lookup_dvbs2_threshold[new_status.T1P2_modcode]);
                        db_margin_text = "D" + dbmargin.ToString("N1");
                        break;
                    case 3:
                        modcod_text = lookups.modcod_lookup_dvbs[new_status.T1P2_modcode];
                        dbmargin = (mer - lookups.modcod_lookup_dvbs_threshold[new_status.T1P2_modcode]);
                        db_margin_text = "D" + dbmargin.ToString("N1");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "MiniTiouner: dB margin update failed");
            }

            last_dbm_0 = db_margin_text;
            last_mer_0 = mer.ToString();

            _tuner1_properties.UpdateBigLabel(db_margin_text);
            //_tuner1_properties.UpdateValue("db_margin", db_margin_text);
            _tuner1_properties.UpdateValue("modcod", modcod_text);
            _tuner1_properties.UpdateValue("cn_needed", CnNeededText(new_status.T1P2_demod_status, new_status.T1P2_modcode, mer));

            // var data1 = _tuner1_properties.GetAll();
            //data1.Add("frequency", GetFrequency(0, true).ToString());

            var source_data = new OTSourceData();
            source_data.frequency = GetFrequency(0, true);
            source_data.video_number = 0;
            source_data.mer = mer;
            source_data.db_margin = dbmargin;
            source_data.service_name = _tuner1_properties.GetValue("service_name");
            source_data.symbol_rate = (int)(new_status.T1P2_symbol_rate / 1000);
            source_data.demode_state = lookups.demod_state_lookup[new_status.T1P2_demod_status];
            source_data.modcode = modcod_text;

            if (_ts_recorders != null && _ts_recorders.Count > 0)
                _ts_recorders[0].StationInfo = opentuner.Utilities.CommonFunctions.StationSuffix(source_data.service_name, current_sr_0);

            if (_media_player.Count > 0)
            {
                if (_media_player[0] != null)
                    source_data.volume = _media_player[0].GetVolume();
            }
            
            source_data.demod_locked = (new_status.T1P2_demod_status >  1);

            FillTsHealth(source_data, ts_parser_thread, ts_thread);

            opentuner.Utilities.DiagnosticsHelper.MeasureSlow("Status update tuner 1 (OnSourceData)", 250, () => OnSourceData?.Invoke(0, source_data, "Tuner 1"));

            if (ts_devices == 2 && _tuner2_properties != null)
            {
                // tuner 2 properties  *************
                _tuner2_properties.UpdateValue("demodstate", lookups.demod_state_lookup[new_status.T2P1_demod_status]);
                UpdateTsStatusLed(_tuner2_properties, new_status.T2P1_ts_line_ok, new_status.T2P1_ts_error, new_status.T2P1_ts_nosync);

                if (new_status.T2P1_demod_status > 1)
                {
                    _tuner2_properties.UpdateColor("demodstate", Color.PaleGreen);
                }
                else
                {
                    _tuner2_properties.UpdateColor("demodstate", Color.PaleVioletRed);
                }

                _tuner2_properties.UpdateValue("mer", mer2.ToString() + " dB");
                _tuner2_properties.UpdateValue("lna_gain", LnaGainText(new_status.T2P1_lna_gain));
                _tuner2_properties.UpdateValue("rf_input_level", new_status.T2P1_input_power_level.ToString() + " dBm");
                _tuner2_properties.UpdateValue("symbol_rate", current_sr_1.ToString());
                _tuner2_properties.UpdateValue("measured_sr", (new_status.T2P1_demod_status == stv0910.DEMOD_S || new_status.T2P1_demod_status == stv0910.DEMOD_S2)
                    ? (new_status.T2P1_symbol_rate / 1000.0).ToString("N2") + " kS" : "-");
                _tuner2_properties.UpdateValue("ber", new_status.T2P1_ber.ToString());
                _tuner2_properties.UpdateValue("freq_carrier_offset", new_status.T2P1_frequency_carrier_offset.ToString());
                _tuner2_properties.UpdateValue("requested_freq_2", "(" + GetFrequency(1, true).ToString("N0") + ") (" + GetFrequency(1, false).ToString("N0") + ")");

                _tuner2_properties.UpdateValue("rf_input", (new_status.T2P1_rf_input == 1 ? "A" : "B"));
                _tuner2_properties.UpdateValue("stream_format", lookups.stream_format_lookups[Convert.ToInt32(new_status.T2P1_stream_format)].ToString());
                _tuner2_properties.UpdateValue("offset", current_offset_1.ToString());




                // clear ts data if not locked onto signal
                if (new_status.T2P1_demod_status < 2)
                {
                    _tuner2_properties.UpdateValue("service_provider_name", "");
                    _tuner2_properties.UpdateValue("service_name", "");
                    _tuner2_properties.UpdateValue("service_provider_name", "");
                    _tuner2_properties.UpdateValue("stream_format", "");

                    _tuner2_properties.UpdateValue("video_codec", "");
                    _tuner2_properties.UpdateValue("video_resolution", "");
                    _tuner2_properties.UpdateValue("audio_codec", "");
                    _tuner2_properties.UpdateValue("audio_rate", "");


                    // stop recording if recording
                    if (_ts_recorders != null && _ts_recorders[1].record)
                    {
//                        ClearIndicator(ref indicatorStatus2, PropertyIndicators.RecordingIndicator);
                        _ts_recorders[1].record = false;    // stop recording
                        _tuner2_properties.UpdateRecordButtonColor("media_controls_2", Color.Transparent);
//                        _tuner2_properties.UpdateValue("media_controls_2", indicatorStatus2.ToString());
                    }

                    // stop streaming
                    if (_ts_streamers != null && _ts_streamers[1].stream)
                    {
//                        ClearIndicator(ref indicatorStatus2, PropertyIndicators.StreamingIndicator);
                        _ts_streamers[1].stream = false;    // stop streaming
                        _tuner2_properties.UpdateStreamButtonColor("media_controls_2", Color.Transparent);
//                        _tuner2_properties.UpdateValue("media_controls_2", indicatorStatus2.ToString());
                    }


                }

                // db margin / modcod
                modcod_text = "Unknown";
                db_margin_text = "";
                try
                {
                    switch (new_status.T2P1_demod_status)
                    {
                        case 2:
                            modcod_text = lookups.modcod_lookup_dvbs2[new_status.T2P1_modcode];
                            dbmargin = (mer2 - lookups.modcod_lookup_dvbs2_threshold[new_status.T2P1_modcode]);
                            db_margin_text = "D" + dbmargin.ToString("N1");
                            break;
                        case 3:
                            modcod_text = lookups.modcod_lookup_dvbs[new_status.T2P1_modcode];
                            dbmargin = (mer2 - lookups.modcod_lookup_dvbs_threshold[new_status.T2P1_modcode]);
                            db_margin_text = "D" + dbmargin.ToString("N1");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.Debug(ex, "MiniTiouner: dB margin update failed");
                }

                last_dbm_1 = db_margin_text;
                last_mer_1 = mer2.ToString();

                _tuner2_properties.UpdateBigLabel(db_margin_text);
                //_tuner2_properties.UpdateValue("db_margin", db_margin_text);
                _tuner2_properties.UpdateValue("modcod", modcod_text);
                _tuner2_properties.UpdateValue("cn_needed", CnNeededText(new_status.T2P1_demod_status, new_status.T2P1_modcode, mer2));

                //var data2 = _tuner2_properties.GetAll();
                //data2.Add("frequency", GetFrequency(1, true).ToString());

                var source_data_2 = new OTSourceData();
                source_data_2.frequency = GetFrequency(1, true);
                source_data_2.video_number = 1;
                source_data_2.mer = mer2;
                source_data_2.db_margin = dbmargin;
                source_data_2.service_name = _tuner2_properties.GetValue("service_name");
                source_data_2.demod_locked = (new_status.T2P1_demod_status > 1);
                source_data_2.symbol_rate = (int)(new_status.T2P1_symbol_rate / 1000);
                source_data_2.demode_state = lookups.demod_state_lookup[new_status.T2P1_demod_status];
                source_data_2.modcode = modcod_text;

                FillTsHealth(source_data_2, ts_parser_thread2, ts_thread2);

                if (_ts_recorders != null && _ts_recorders.Count > 1)
                    _ts_recorders[1].StationInfo = opentuner.Utilities.CommonFunctions.StationSuffix(source_data_2.service_name, current_sr_1);

                if (_media_player.Count > 1)
                {
                    if (_media_player[1] != null)
                        source_data_2.volume = _media_player[1].GetVolume();
                }

                opentuner.Utilities.DiagnosticsHelper.MeasureSlow("Status update tuner 2 (OnSourceData)", 250, () => OnSourceData?.Invoke(1, source_data_2, "Tuner 2"));

            }
        }

        private void _genericContextStrip_Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            ContextMenuStrip contextMenuStrip = (ContextMenuStrip)sender;
            Log.Information("Opening Context Menu :" + contextMenuStrip.SourceControl.Name + " tag = " + contextMenuStrip.SourceControl.Tag.ToString());

            contextMenuStrip.Items.Clear();

            switch (contextMenuStrip.SourceControl.Name)
            {
                // change frequency
                case "requested_freq_1":
                    contextMenuStrip.Items.Add(ConfigureMenuItem("Tuner Control", MinitiounerPropertyCommands.SETFREQUENCY, new int[] {0}));
                    
                    if (_frequency_presets != null)
                    {
                        contextMenuStrip.Items.Add(new ToolStripSeparator());

                        for (int c = 0; c < _frequency_presets.Count; c++)
                        {
                            contextMenuStrip.Items.Add(ConfigureMenuItem(_frequency_presets[c].Name + " (" + _frequency_presets[c].Frequency + ")", MinitiounerPropertyCommands.SETPRESET, new int[] { 0, c }));
                        }
                    }

                    break;
                case "requested_freq_2":
                    contextMenuStrip.Items.Add(ConfigureMenuItem("Tuner Control", MinitiounerPropertyCommands.SETFREQUENCY, new int[] { 1 }));

                    if (_frequency_presets != null)
                    {
                        contextMenuStrip.Items.Add(new ToolStripSeparator());

                        for (int c = 0; c < _frequency_presets.Count; c++)
                        {
                            contextMenuStrip.Items.Add(ConfigureMenuItem(_frequency_presets[c].Name + " (" + _frequency_presets[c].Frequency + ")", MinitiounerPropertyCommands.SETPRESET, new int[] { 1, c }));
                        }
                    }

                    break;                  
                case "rf_input":
                    contextMenuStrip.Items.Add(ConfigureMenuItem("A", MinitiounerPropertyCommands.SETRFINPUTA, new int[] { (int)contextMenuStrip.SourceControl.Tag - 1 }));
                    contextMenuStrip.Items.Add(ConfigureMenuItem("B", MinitiounerPropertyCommands.SETRFINPUTB, new int[] { (int)contextMenuStrip.SourceControl.Tag - 1 }));
                    break;
                case "symbol_rate":
                    uint[] symbol_rates = new uint[] { 2000, 1500, 1000, 500, 333, 250, 125, 66, 33, 25, 20 };
                    foreach (uint rate in symbol_rates)
                        contextMenuStrip.Items.Add(ConfigureMenuItem(rate.ToString(), MinitiounerPropertyCommands.SETSYMBOLRATE, new int[] { (int)contextMenuStrip.SourceControl.Tag - 1, (int)rate}));
                    break;
                case "offset":
                    int tuner = (int)contextMenuStrip.SourceControl.Tag - 1;
                    contextMenuStrip.Items.Add(ConfigureMenuItem("Default: " + (tuner == 0 ? _settings.Offset1 : _settings.Offset2), MinitiounerPropertyCommands.SETOFFSET, new int[] { (int)contextMenuStrip.SourceControl.Tag - 1, 0 }));
                    contextMenuStrip.Items.Add(ConfigureMenuItem("Zero" , MinitiounerPropertyCommands.SETOFFSET, new int[] { (int)contextMenuStrip.SourceControl.Tag - 1, 1 }));
                    break;
            }

        }

        private ToolStripMenuItem ConfigureMenuItem(string Text, MinitiounerPropertyCommands command, int[] option)
        {
            ToolStripMenuItem item = new ToolStripMenuItem(Text);
            item.Click += (sender, e) =>
            {
                properties_OnPropertyMenuSelect(command, option);
            };

            return item;
        }

        private void properties_OnPropertyMenuSelect(MinitiounerPropertyCommands command, int[] options)
        {
            Log.Information("Config Change: " + command.ToString() + " - " + options.Length.ToString());

            int tuner = 0;

            switch (command)
            {
                case MinitiounerPropertyCommands.SETFREQUENCY:
                    tuner = options[0];
                    _tuner_forms[tuner].ShowTuner((tuner == 0 ? current_frequency_0 : current_frequency_1), (tuner == 0 ? current_sr_0 : current_sr_1), (tuner == 0 ? current_offset_0 : current_offset_1));
                    break;
                case MinitiounerPropertyCommands.SETRFINPUTA:
                    ChangeRFInput((byte)options[0], nim.NIM_INPUT_TOP);
                    ResetVideo(options[0]);
                    break;
                case MinitiounerPropertyCommands.SETRFINPUTB:
                    ChangeRFInput((byte)options[0], nim.NIM_INPUT_BOTTOM);
                    ResetVideo(options[0]);
                    break;
                case MinitiounerPropertyCommands.SETSYMBOLRATE:
                    ChangeSymbolRate((byte)options[0],(uint)options[1]);
                    ResetVideo(options[0]);
                    break;
                case MinitiounerPropertyCommands.SETOFFSET:
                    tuner = options[0];

                    if (options[1] == 0)    // set to default
                        ChangeOffset((byte)tuner, ((tuner == 0) ? (int)_settings.Offset1 : (int)_settings.Offset2));
                    if (options[1] == 1)    // zero out
                        ChangeOffset((byte)tuner, 0);
                    break;

                default:
                    Log.Information("Unconfigured Command Change - " + command.ToString());
                    break;

                case MinitiounerPropertyCommands.SETPRESET:
                    tuner = options[0];
                    int presetNumber = options[1];

                    if (_frequency_presets != null)
                    {
                        if (_frequency_presets.Count > presetNumber)
                        {
                            Log.Information("Tuning to preset " + presetNumber);
                            Log.Information("Preset Name: " + _frequency_presets[presetNumber].Name);
                            Log.Information("Preset Frequency: " + _frequency_presets[presetNumber].Frequency.ToString());
                            Log.Information("Preset Offset: " + _frequency_presets[presetNumber].Offset.ToString());
                            Log.Information("Preset SymbolRate: " + _frequency_presets[presetNumber].SymbolRate.ToString());
                            Log.Information("Preset RF Input: " + _frequency_presets[presetNumber].RFInput.ToString());

                            ChangeOffset((byte)tuner, (int)_frequency_presets[presetNumber].Offset);
                            ChangeRFInput((byte)tuner, _frequency_presets[presetNumber].RFInput == 1 ? nim.NIM_INPUT_TOP : nim.NIM_INPUT_BOTTOM);
                            SetFrequency((byte)tuner, _frequency_presets[presetNumber].Frequency, _frequency_presets[presetNumber].SymbolRate, true);
                        }
                    } 
                    break;

                case MinitiounerPropertyCommands.LNBA_OFF:
                    current_lnba_psu = 0;
                    RememberSwitchState();
                    change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    break;
                case MinitiounerPropertyCommands.LNBA_VERTICAL:
                    current_lnba_psu = 1;
                    RememberSwitchState();
                    change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    break;
                case MinitiounerPropertyCommands.LNBA_HORIZONTAL:
                    current_lnba_psu = 2;
                    RememberSwitchState();
                    change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    break;
                case MinitiounerPropertyCommands.LNBB_OFF:
                    current_lnbb_psu = 0;
                    RememberSwitchState();
                    change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    break;
                case MinitiounerPropertyCommands.LNBB_VERTICAL:
                    current_lnbb_psu = 1;
                    RememberSwitchState();
                    change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    break;
                case MinitiounerPropertyCommands.LNBB_HORIZONTAL:
                    current_lnbb_psu = 2;
                    RememberSwitchState();
                    change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    break;
                case MinitiounerPropertyCommands.TONE22K_OFF:
                case MinitiounerPropertyCommands.TONE22K_ON:
                    tuner = options[0];
                    bool tone_on = command == MinitiounerPropertyCommands.TONE22K_ON;
                    if (tuner == 0)
                    {
                        current_tone_22kHz_0 = tone_on;
                        RememberSwitchState();
                        change_frequency(0, current_frequency_0, current_sr_0, current_rf_input_0, current_tone_22kHz_0, current_lnba_psu, current_lnbb_psu);
                    }
                    else
                    {
                        current_tone_22kHz_1 = tone_on;
                        RememberSwitchState();
                        change_frequency(1, current_frequency_1, current_sr_1, current_rf_input_1, current_tone_22kHz_1, current_lnba_psu, current_lnbb_psu);
                    }
                    break;
            }
        }

        private void TunerControl_OnTunerChange(int id, uint freq)
        {
            Log.Information("Tuner: Set frequency : " + id.ToString() + "," + freq.ToString());
            SetFrequency(id, freq, id == 0 ? current_sr_0 : current_sr_1, false);
        }

        private void ResetVideo(int tuner)
        {
            if (VideoChangeCB != null)
            {
                VideoChangeCB(tuner + 1, false);
            }

        }

        public override Dictionary<string, string> GetSignalData(int device)
        {
            Dictionary<string, string> data = new Dictionary<string, string>();

            // Gets a NumberFormatInfo associated with the en-US culture.
            NumberFormatInfo nfi = new CultureInfo("en-US", false).NumberFormat;

            if (device == 0)
            {
                data.Add("ServiceName", last_service_name_0);
                data.Add("ServiceProvider", last_service_provider_0);
                data.Add("dbMargin", last_dbm_0);
                data.Add("Mer", last_mer_0);
                data.Add("SR", current_sr_0.ToString());
                data.Add("VideoCodec", last_video_codec_0);
                data.Add("Frequency", ((float)(current_frequency_0 + _settings.Offset1) / 1000.0f).ToString("F", nfi));
                data.Add("TSErrors", TsErrorText(ts_parser_thread, ts_thread));
            }

            if (device == 1)
            {
                data.Add("ServiceName", last_service_name_1);
                data.Add("ServiceProvider", last_service_provider_1);
                data.Add("dbMargin", last_dbm_1);
                data.Add("Mer", last_mer_1);
                data.Add("SR", current_sr_1.ToString());
                data.Add("VideoCodec", last_video_codec_1);
                data.Add("Frequency", ((float)(current_frequency_1 + _settings.Offset2) / 1000.0f).ToString("F", nfi));
                data.Add("TSErrors", TsErrorText(ts_parser_thread2, ts_thread2));
            }

            return data;
        }

        public override void UpdateFrequencyPresets(List<StoredFrequency> FrequencyPresets)
        {
            _frequency_presets = FrequencyPresets;
        }

        public override void UpdateVolume(int device, int volume)
        {
            if (device >= _media_player.Count || device < 0)
                return;

            if (_media_player[device] == null)
                return;

            int new_volume = _media_player[device].GetVolume() + volume;

            if (new_volume < 0) new_volume = 0;
            if (new_volume > 200) new_volume = 200;

            _media_player[device].SetVolume(new_volume);

            switch (device)
            {
                case 0: _tuner1_properties?.UpdateValue("volume_slider_" + (device + 1).ToString(), new_volume.ToString()); break;
                case 1: _tuner2_properties?.UpdateValue("volume_slider_" + (device + 1).ToString(), new_volume.ToString()); break;
            }
        }

        public override void ToggleMute(int device)
        {
            throw new NotImplementedException();
        }

        public override int GetVolume(int device)
        {
            if (device >= _media_player.Count || device < 0)
                return -1;

            if (_media_player[device] == null)
                return -1;
        
            return _media_player[device].GetVolume();   
        }
    }
}
