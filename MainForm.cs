using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

using opentuner.MediaSources;
using opentuner.MediaSources.Minitiouner;
using opentuner.MediaSources.Longmynd;
using opentuner.MediaSources.WinterHill;

using opentuner.MediaPlayers;
using opentuner.MediaPlayers.MPV;
using opentuner.MediaPlayers.FFMPEG;
using opentuner.MediaPlayers.VLC;

using opentuner.Utilities;
using opentuner.Transmit;
using opentuner.ExtraFeatures.BATCSpectrum;
using opentuner.ExtraFeatures.BATCWebchat;
using opentuner.ExtraFeatures.MqttClient;
using opentuner.ExtraFeatures.QuickTuneControl;
using opentuner.ExtraFeatures.DATVReporter;

using Serilog;
using Serilog.Events;

namespace opentuner
{
    delegate void updateNimStatusGuiDelegate(MainForm gui, TunerStatus new_status);
    delegate void updateTSStatusGuiDelegate(int device, MainForm gui, TSStatus new_status);
    delegate void updateMediaStatusGuiDelegate(int tuner, MainForm gui, MediaStatus new_status);
    delegate void UpdateLBDelegate(ListBox LB, Object obj);
    delegate void UpdateLabelDelegate(Label LB, Object obj);
    delegate void updateRecordingStatusDelegate(MainForm gui, bool recording_status, string id);

    delegate void UpdateInfoDelegate(StreamInfoContainer info_object, OTSourceData info);

    public partial class MainForm : Form, IMessageFilter
    {
        // extras
        MqttManager mqtt_client;
        F5OEOPlutoControl pluto_client;
        BATCSpectrum batc_spectrum = null;
        BATCChat batc_chat;
        QuickTuneControl quickTune_control;
        DATVReporter datv_reporter = new DATVReporter();

        private static List<OTMediaPlayer> _mediaPlayers;
        private static List<OTSource> _availableSources = new List<OTSource>();
        private static List<TSRecorder> _ts_recorders = new List<TSRecorder>();
        private static List<TSUdpStreamer> _ts_streamers = new List<TSUdpStreamer>();

        private static OTSource videoSource;

        private MainSettings _settings;
        private SettingsManager<MainSettings> _settingsManager;

        private bool _settings_save = true;

        SettingsManager<List<StoredFrequency>> frequenciesManager;

        List<StoredFrequency> stored_frequencies = new List<StoredFrequency>();
        List<ExternalTool> external_tools = new List<ExternalTool>();

        List<VolumeInfoContainer> volume_display = new List<VolumeInfoContainer>();
        List<StreamInfoContainer> info_display = new List<StreamInfoContainer>();

        private bool source_connected = false;

        SplitterPanel[] video_panels = new SplitterPanel[4];

        bool properties_hidden = false;
        bool ExtraTool_hidden = false;

        public void UpdateInfo(StreamInfoContainer info_object, OTSourceData info)
        {

            if (info_object == null)
                return;

            if (info == null)
                return;

            if (info_object.InvokeRequired)
            {
                UpdateInfoDelegate ulb = new UpdateInfoDelegate(UpdateInfo);
                info_object?.Invoke(ulb, new object[] { info_object, info });
            }
            else
            {
                info_object.UpdateInfo(info);
            }
        }

        void ParseCommandLineOptions(string[] args)
        {
            int i = 0;

            while (i < args.Length)
            {
                switch (args[i])
                {
                    case "--nosave":
                        _settings_save = false;
                        break;

                    case "--autoconnect":
                        _settings.auto_connect = true;
                        break;

                    case "--enablebatcspectrum":
                        _settings.enable_spectrum_checkbox = true;
                        break;

                    case "--disablebatcspectrum":
                        _settings.enable_spectrum_checkbox = false;
                        break;

                    case "--enablebatcchat":
                        _settings.enable_chatform_checkbox = true;
                        break;

                    case "--disablebatcchat":
                        _settings.enable_chatform_checkbox = false;
                        break;

                    case "--enablemqtt":
                        _settings.enable_mqtt_checkbox = true;
                        break;

                    case "--disablemqtt":
                        _settings.enable_mqtt_checkbox = false;
                        break;

                    case "--enableplutoctrl":
                        _settings.enable_plutoctrl_checkbox = true;
                        break;

                    case "--disableplutoctrl":
                        _settings.enable_plutoctrl_checkbox = false;
                        break;

                    case "--enablequicktune":
                        _settings.enable_quicktune_checkbox = true;
                        break;

                    case "--disablequicktune":
                        _settings.enable_quicktune_checkbox = false;
                        break;

                    case "--enabledatvreporter":
                        _settings.enable_datvreporter_checkbox = true;
                        break;

                    case "--disabledatvreporter":
                        _settings.enable_datvreporter_checkbox = false;
                        break;

                    case "--hideproperties":
                        _settings.hide_properties = true;
                        break;

                    case "--showproperties":
                        _settings.hide_properties = false;
                        break;

                    case "--showextratools":
                        _settings.hide_ExtraTool = false;
                        break;

                    case "--hideextratools":
                        _settings.hide_ExtraTool = true;
                        break;

                    case "--hidevideoinfo":
                        for (int j = 0; j < 4; j++)
                            _settings.show_video_info[j] = false;
                        break;

                    case "--showvideoinfo":
                        for (int j = 0; j < 4; j++)
                            _settings.show_video_info[j] = true;
                        break;

                    case "--windowwidth":
                        int new_window_width = 0;

                        if (int.TryParse(args[i + 1], out new_window_width))
                        {
                            _settings.gui_window_width = new_window_width;
                        }
                        else
                        {
                            Log.Error(args[i + 1] + " is not a valid window width. Integer Expected");
                        }
                        i += 1;
                        break;

                    case "--windowheight":
                        int new_window_height = 0;

                        if (int.TryParse(args[i + 1], out new_window_height))
                        {
                            _settings.gui_window_height = new_window_height;
                        }
                        else
                        {
                            Log.Error(args[i + 1] + " is not a valid window height. Integer Expected");
                        }
                        i += 1;
                        break;

                    case "--windowx":
                        int new_window_x = 0;

                        if (int.TryParse(args[i + 1], out new_window_x))
                        {
                            _settings.gui_window_x = new_window_x;
                        }
                        else
                        {
                            Log.Error(args[i + 1] + " is not a valid window x. Integer Expected");
                        }
                        i += 1;
                        break;

                    case "--windowy":
                        int new_window_y = 0;

                        if (int.TryParse(args[i + 1], out new_window_y))
                        {
                            _settings.gui_window_y = new_window_y;
                        }
                        else
                        {
                            Log.Error(args[i + 1] + " is not a valid window y. Integer Expected");
                        }
                        i += 1;
                        break;

                    case "--defaultsource":
                        int new_default_source = 0;

                        if (int.TryParse(args[i + 1], out new_default_source))
                        {
                            if (new_default_source < 3 && new_default_source >= 0)
                            {
                                _settings.default_source = new_default_source;
                            }
                            else
                            {
                                Log.Error(args[i + 1] + " is not a valid default source parameter. 0-2 Expected");
                            }
                        }
                        else
                        {
                            Log.Error(args[i + 1] + " is not a valid default source parameter. 0-2 Expected");
                        }
                        i += 1;
                        break;

                    case "--hideconsolewindow":
                        break;

                    case "--debuglevel":
                        i += 1;
                        break;

                    case "--breakonstall":
                        break;

                    default:
                        Log.Warning("Unknown command line param: " + args[i]);
                        break;
                }
                // grab next param
                i += 1;
            }
        }

        // "" on main, "-#13" on branch issue-13_..., "-<branch>" on any other branch.
        private static string BranchSuffix()
        {
            string b = Builtin.GitBranch;
            if (b == "main" || b == "unknown") return "";
            var m = System.Text.RegularExpressions.Regex.Match(b, @"^issue-(\d+)");
            return m.Success ? "-#" + m.Groups[1].Value : "-" + b;
        }

        public MainForm(string[] args)
        {
            var compileTime = new DateTime(Builtin.CompileTime, DateTimeKind.Utc);
            DateTimeFormatInfo usDateFormat = new CultureInfo("en-US", false).DateTimeFormat;
            string compileTime_usFormat = compileTime.ToString("u", usDateFormat);

            ThreadPool.GetMinThreads(out int workers, out int ports);
            ThreadPool.SetMinThreads(workers + 6, ports + 6);

            InitializeComponent();

            // Must run AFTER InitializeComponent(): the designer bakes a stale "$this.Text" value
            // into MainForm.resx (last saved 2024-07 as "Open Tuner (ZR6TG) - 0.B Version -
            // 2024/07/09"), and InitializeComponent() applies it via resources.ApplyResources(this,
            // "$this") - setting Text before that call gets silently overwritten by the resx value
            // every time, regardless of what it's set to.
            Text = "Open Tuner (" + Builtin.GitUser + " - " + Builtin.GitDescribe + BranchSuffix() + " - " + compileTime_usFormat + ")";

            // Always log the version information
            // swith logging level to Information
            LogEventLevel lastMinimumLevel = Program.levelSwitch.MinimumLevel;
            Program.levelSwitch.MinimumLevel = LogEventLevel.Information;

            Log.Information(Text);

            // swith logging level back
            Program.levelSwitch.MinimumLevel = lastMinimumLevel;

            // The tab headers of the TabControl are hidden, TabRowStrip draws them instead: the active tab orange (the 3D
            // tab look was hard to read), one row per board when there are several (Pro, V2).
            tabControl1.SizeMode = TabSizeMode.Fixed;
            tabControl1.ItemSize = new System.Drawing.Size(0, 1);
            splitContainer3.SizeChanged += (s, e) => FitLargeVideo();   // the large stream keeps its format when the window changes
            _tab_strip = new TabRowStrip(tabControl1);
            splitContainer1.Panel1.Controls.Add(_tab_strip);   // added after the TabControl (Dock Fill), so it docks at the top

            Application.AddMessageFilter(this);

            _settings = new MainSettings();

            //SettingsFormBuilder test = new SettingsFormBuilder(_settings);

            _settingsManager = new SettingsManager<MainSettings>("open_tuner_settings");
            _settings = (_settingsManager.LoadSettings(_settings));

            // reset autoconnect. Will be updated in the next step
            _settings.auto_connect = false;

            // parse command line options
            ParseCommandLineOptions(args);

            //setup
            splitContainer2.Panel2Collapsed = true;
            splitContainer2.Panel2.Enabled = false;
            splitContainer2.Panel2.Hide();

            checkBatcSpectrum.Checked = _settings.enable_spectrum_checkbox;
            checkBatcChat.Checked = _settings.enable_chatform_checkbox;
            UpdateSpectrumSettingsLink();
            checkMqttClient.Checked = _settings.enable_mqtt_checkbox;
            checkMqttClient.Enabled = _settings.show_mqtt_feature;
            checkPlutoCtrl.Checked = _settings.enable_plutoctrl_checkbox;
            checkPlutoCtrl.Enabled = _settings.show_plutoctrl_feature;
            checkQuicktune.Checked = _settings.enable_quicktune_checkbox;
            checkDATVReporter.Checked = _settings.enable_datvreporter_checkbox;

            // load available sources
            _availableSources.Add(new MinitiounerMultiSource());
            _availableSources.Add(new LongmyndSource());
            _availableSources.Add(new WinterHillSource());

            comboAvailableSources.Items.Clear();

            for (int c = 0; c < _availableSources.Count; c++)
            {
                comboAvailableSources.Items.Add(_availableSources[c].GetName());
            }

            comboAvailableSources.SelectedIndex = _settings.default_source;
            sourceInfo.Text = _availableSources[_settings.default_source].GetDescription();

            // load stored presets
            frequenciesManager = new SettingsManager<List<StoredFrequency>>("frequency_presets");
            stored_frequencies = frequenciesManager.LoadSettings(stored_frequencies);
        }

        /// <summary>
        /// Connect to Media Source and configure Media Players + extra's based on Media Source initialization.
        /// </summary>
        /// <param name="MediaSource"></param>
        private bool SourceConnect(OTSource MediaSource)
        {
            Log.Information("Connecting to " + MediaSource.GetName());

            videoSource = MediaSource;

            int video_players_required = 0;
            DiagnosticsHelper.Measure("Connect: source Initialize", () => video_players_required = videoSource.Initialize(ChangeVideo, PropertiesPage));

            if (video_players_required < 0)
            {
                Log.Error("Error Connecting MediaSource: " + videoSource.GetName());
                MessageBox.Show("Error Connecting MediaSource: " + videoSource.GetName());
                return false;
            }

            this.Text = this.Text += " - " + videoSource.GetDeviceName();

            // preferred player to use for each video view
            // 0 = vlc, 1 = ffmpeg, 2 = mpv
            Log.Information("Configuring Media Players");
            DiagnosticsHelper.Measure("Connect: media players", () =>
            {
                _mediaPlayers = ConfigureMediaPlayers(videoSource.GetVideoSourceCount(), _settings.mediaplayer_preferences, _settings.mediaplayer_windowed );
                videoSource.ConfigureVideoPlayers(_mediaPlayers);

                // the video size of every stream, for the large view (FitLargeVideo)
                _video_sizes.Clear();
                for (int i = 0; i < _mediaPlayers.Count; i++)
                {
                    int video_nr = i;
                    if (_mediaPlayers[i] != null)
                        _mediaPlayers[i].onVideoOut += (s, status) => VideoSizeReported(video_nr, status);
                }
            });
            videoSource.ConfigureMediaPath(_settings.media_path);

            // set recorders
            DiagnosticsHelper.Measure("Connect: TS recorders and streamers", () =>
            {
                _ts_recorders = ConfigureTSRecorders(videoSource, _settings.media_video_path);
                videoSource.ConfigureTSRecorders(_ts_recorders);

                // set udp streamers
                _ts_streamers = ConfigureTSStreamers(videoSource, _settings.streamer_udp_hosts, _settings.streamer_udp_ports);
                videoSource.ConfigureTSStreamers(_ts_streamers);
            });

            // update gui
            DiagnosticsHelper.Measure("Connect: tabs", () =>
            {
                DiagnosticsHelper.Measure("Connect: tabs, switch to properties page", () =>
                {
                    SourcePage.Hide();
                    tabControl1.TabPages.Remove(SourcePage);
                    tabControl1.TabPages.Add(PropertiesPage);
                });

                // extra tabs next to "Properties" (e.g. "Expert", "Frequency") if the source provides any
                List<KeyValuePair<string, Control>> extra_tabs = null;
                DiagnosticsHelper.Measure("Connect: tabs, create extra tabs", () => extra_tabs = videoSource.GetExtraTabs());

                DiagnosticsHelper.Measure("Connect: tabs, add extra tabs", () =>
                {
                    foreach (var extra_tab in extra_tabs)
                    {
                        TabPage extra_page = new TabPage(extra_tab.Key);
                        extra_page.Controls.Add(extra_tab.Value);
                        tabControl1.TabPages.Add(extra_page);
                    }
                });

                _tab_strip.Rebuild();

                tabControl1.Width = 100;
                DiagnosticsHelper.Measure("Connect: tabs, update (repaint)", () => tabControl1.Update());
            });
            videoSource.OnSourceData += VideoSource_OnSourceData;

            return true;
        }

        private TabRowStrip _tab_strip;

        private void VideoSource_OnSourceData(int video_nr, OTSourceData properties, string description)
        {
            if (video_nr < info_display.Count && video_nr >= 0)
            {
                if (info_display[video_nr] != null)
                {
                    UpdateInfo(info_display[video_nr], properties);
                }
                else
                {
                    Log.Error("info_display is a NULL ponter");
                }
            }
            else
            {
                Log.Error("info_display count does not fit video_nr");
            }

            if (datv_reporter != null)
            {
                if (properties.demod_locked)
                {
                    bool result = datv_reporter.SendISawMessage(new ISawMessage(
                        properties.service_name,
                        properties.db_margin,
                        properties.mer,
                        properties.frequency,
                        properties.symbol_rate,
                        videoSource.GetDeviceName()
                        ));

                    /*
                    if (!result)
                    {
                        if (!datv_reporter.Connected)
                        {
                            datv_reporter.Connect();
                        }
                    }
                    */
                }
            }

            if (batc_spectrum != null)
            {
                float freq = properties.frequency;
                float sr = properties.symbol_rate;
                string callsign = properties.service_name;

                //Log.Information(callsign.ToString() + "," + freq.ToString() + "," + sr.ToString());

                batc_spectrum.updateSignalCallsign(callsign, freq/1000, sr/1000);
            }

            if (mqtt_client != null)
            {
                // send mqtt data
                mqtt_client.SendProperties(properties, videoSource.GetName() + "/" + description);
            }
        }

        public static void UpdateLB(ListBox LB, Object obj)
        {
            if (LB == null)
                return;

            if (LB.InvokeRequired)
            {
                UpdateLBDelegate ulb = new UpdateLBDelegate(UpdateLB);
                if (LB != null)
                {
                    LB?.Invoke(ulb, new object[] { LB, obj });
                }
            }
            else
            {
                if (LB.Items.Count > 1000)
                {
                    LB.Items.Remove(0);
                }

                int i = LB.Items.Add(DateTime.Now.ToShortTimeString() + " : " + obj);
                LB.TopIndex = i;
            }
        }

        public void start_video(int video_number)
        {
            if (_mediaPlayers == null)
            {
                Log.Debug("Media player is still null");
                return;
            }

            if (video_number < _mediaPlayers.Count)
            {
                if (video_number == 0)
                {
                    Log.Information("Ping");
                }

                opentuner.Utilities.DiagnosticsHelper.Measure("Source StartStreaming " + video_number, () => videoSource.StartStreaming(video_number));
                opentuner.Utilities.DiagnosticsHelper.Measure("Player " + _mediaPlayers[video_number].GetName() + " Play " + video_number, () => _mediaPlayers[video_number].Play());
                // Start with volume "muted".
                // The real audio volume is set later.
                // Reason for muting here:
                //   If audio is muted for the tuner and stream
                //   we start with Volume 100 or so here and get an annoying audio glitch.
                //   (detected with MPV player)
                _mediaPlayers[video_number].SetVolume(0);
            }
        }

        public void stop_video(int video_number)
        {
            if (_mediaPlayers == null)
            {
                return;
            }

            if (video_number < _mediaPlayers.Count)
            {
                opentuner.Utilities.DiagnosticsHelper.Measure("Source StopStreaming " + video_number, () => videoSource.StopStreaming(video_number));
                opentuner.Utilities.DiagnosticsHelper.Measure("Player " + _mediaPlayers[video_number].GetName() + " Stop " + video_number, () => _mediaPlayers[video_number].Stop());
            }
        }

        private void ChangeVideo(int video_number, bool start)
        {
            Log.Information("Change Video " + video_number.ToString());

            if (start)
                start_video(video_number-1);
            else 
                stop_video(video_number-1);
        }

        private bool _closing;

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Closing the source pumps messages (Application.DoEvents), a second close request (e.g. another
            // click on the close button while the first one is still working) would run all of this a second time.
            if (_closing)
            {
                Log.Information("Exiting: already closing, ignoring the second close request");
                e.Cancel = true;
                return;
            }
            _closing = true;

            Log.Information("Exiting...");

            _uiWatchdog?.Dispose();
            _uiWatchdog = null;

            Application.RemoveMessageFilter(this);

            Log.Information("* Saving Settings");

            // save current windows properties
            LeaveVideoFullScreen();
            _settings.gui_window_state = (int)this.WindowState;
            if (this.WindowState == FormWindowState.Minimized)
            {
                _settings.gui_window_state = (int)FormWindowState.Normal;
            }
            this.WindowState = FormWindowState.Normal;
            _settings.gui_window_width = this.Width;
            _settings.gui_window_height = this.Height;
            _settings.gui_window_x = this.Left;
            _settings.gui_window_y = this.Top;
            _settings.gui_main_splitter_position = splitContainer1.SplitterDistance;

            if (_settings_save)
                _settingsManager.SaveSettings(_settings);

            try
            {
                if (mqtt_client != null)
                {
                    mqtt_client.Disconnect();
                }

                if (pluto_client != null)
                    pluto_client.Close();

                if (batc_spectrum != null)
                    batc_spectrum.Close();

                if (batc_chat != null)
                    batc_chat.Close();

                if (_tuner_highlight_timer != null)
                    _tuner_highlight_timer.Dispose();

                if (quickTune_control != null)
                    quickTune_control.Close();

                if (datv_reporter != null)
                    datv_reporter.Close();

                if (videoSource != null)
                {
                    // Stop TS (issue #9): put the tuner(s) into the same idle state a "Stop TS"
                    // button click would, before tearing anything else down on exit.
                    for (int i = 0; i < videoSource.GetVideoSourceCount(); i++)
                    {
                        videoSource.StopTuner(i);
                    }
                }

                Log.Information("* Stopping Playing Video");

                if (videoSource != null)
                {
                    for (int i = 0; i < videoSource.GetVideoSourceCount(); i++)
                    {
                        ChangeVideo(i+1, false);
                    }
                }

                Log.Information("* Closing Extra TS Threads");

                // Every step is logged before and after, so a hang on exit shows where it stands (issue #34).
                // close ts streamers
                for (int c = 0; c < _ts_streamers.Count; c++)
                {
                    Log.Information("* Closing TS streamer " + c + "...");
                    _ts_streamers[c].Close();
                    Log.Information("* TS streamer " + c + " closed");
                }

                // close ts recorders
                for (int c = 0; c < _ts_recorders.Count; c++)
                {
                    Log.Information("* Closing TS recorder " + c + "...");
                    _ts_recorders[c].Close();
                    Log.Information("* TS recorder " + c + " closed");
                }

                // close available media sources
                for (int c = 0; c < _availableSources.Count; c++)
                {
                    string source_name = _availableSources[c].GetName();
                    Log.Information("* Closing source " + source_name + "...");
                    var close_sw = System.Diagnostics.Stopwatch.StartNew();
                    _availableSources[c].Close();
                    Log.Information("* Source " + source_name + " closed in " + close_sw.ElapsedMilliseconds + " ms");
                }
            }
            catch (Exception ex)
            {
                // we are closing, we don't really care about exceptions at this point
                Log.Error(ex, "Closing Exception");
            }

            // swith logging level to Information
            LogEventLevel lastMinimumLevel = Program.levelSwitch.MinimumLevel;
            Program.levelSwitch.MinimumLevel = LogEventLevel.Information;

            Log.Information("Bye!");

            // swith logging level back
            Program.levelSwitch.MinimumLevel = lastMinimumLevel;
        }

        private opentuner.Utilities.UiWatchdog _uiWatchdog;

        private void Form1_Load(object sender, EventArgs e)
        {
            _uiWatchdog = new opentuner.Utilities.UiWatchdog();

            if (_settings.media_path.Length == 0 || _settings.media_path == AppDomain.CurrentDomain.BaseDirectory)
            {
                _settings.media_path = AppDomain.CurrentDomain.BaseDirectory + "Screenshots\\";
            }

            if (!Directory.Exists(_settings.media_path))
            {
                try
                {
                    DirectoryInfo di;
                    di = Directory.CreateDirectory(_settings.media_path);
                    Log.Information("directory " + _settings.media_path + " created");
                }
                catch
                {
                    Log.Error("Can not create directory: " + _settings.media_path);
                }
            }

            if (_settings.media_video_path.Length == 0 || _settings.media_video_path == AppDomain.CurrentDomain.BaseDirectory)
            {
                _settings.media_video_path = AppDomain.CurrentDomain.BaseDirectory + "Videos\\";
            }

            if (!Directory.Exists(_settings.media_video_path))
            {
                try
                {
                    DirectoryInfo di;
                    di = Directory.CreateDirectory(_settings.media_video_path);
                    Log.Information("directory " + _settings.media_video_path + " created");
                }
                catch
                {
                    Log.Error("Can not create directory: " + _settings.media_video_path);
                }
            }

            if (_settings.gui_window_width != -1)
            {
                Log.Information("Restoring Window Position:");
                Log.Information(" Size: (h = " + _settings.gui_window_height.ToString() + ", w = " + _settings.gui_window_width.ToString() + ")");
                Log.Information(" Position: (x = " + _settings.gui_window_x.ToString() + ", y = " + _settings.gui_window_y.ToString() + ")");

                this.Height = _settings.gui_window_height;
                this.Width = _settings.gui_window_width;

                this.Left = _settings.gui_window_x;
                this.Top = _settings.gui_window_y;

                this.WindowState = (FormWindowState)_settings.gui_window_state;
            }

            // auto connect if specified
            if (_settings.auto_connect)
            {
                source_connected = ConnectSelectedSource();
            }
            // hide/show video overlay
        }

        private void Batc_spectrum_OnSignalSelected(int Receiver, uint Freq, uint SymbolRate)
        {
            // the rate is only estimated from the width of the signal: the source tries smaller standard rates if it
            // does not lock; the rate can be chosen per tuner in the tuner properties (Symbol Rate, right click)
            videoSource.SetFrequencyFromSpectrum(Receiver, Freq, SymbolRate);
        }

        // Right click in the signal area of the spectrum: tune the tuner of that row to the frequency and show the
        // tab with its symbol rate buttons and fine tuning, where the rate can be corrected. The rate is that of the
        // signal under the mouse; without a signal it stays the one the tuner has.
        private void Batc_spectrum_OnSpectrumRightClick(int Receiver, uint Freq, uint SignalSymbolRate)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new MethodInvoker(() => Batc_spectrum_OnSpectrumRightClick(Receiver, Freq, SignalSymbolRate)));
                return;
            }

            if (videoSource == null || Receiver >= videoSource.GetVideoSourceCount())
                return;

            // a detected signal brings its rate (like a left click); the rate of the tuner only stays if there is no
            // signal under the mouse (narrow signals are often not detected)
            uint symbol_rate = SignalSymbolRate;
            if (symbol_rate == 0)
                symbol_rate = videoSource.GetSymbolRate(Receiver);

            if (symbol_rate == 0)
            {
                Log.Information("Spectrum right click: no symbol rate known for tuner " + (Receiver + 1));
                return;
            }

            videoSource.SetFrequency(Receiver, Freq, symbol_rate, true);
            batc_spectrum?.MarkTuner(Receiver, Freq, symbol_rate);

            HighlightTuner(Receiver);
            ShowTunerTab(videoSource.GetSpecialTabTitle(Receiver));
        }

        // shows the tab with that title, false if there is none
        private bool ShowTunerTab(string tab_title)
        {
            if (tab_title == null)
                return false;

            foreach (TabPage page in tabControl1.TabPages)
            {
                if (page.Text == tab_title)
                {
                    tabControl1.SelectedTab = page;
                    return true;
                }
            }

            return false;
        }

        // one colour per tuner for the highlight of the selected tuner (no red: that is the overpower indicator)
        private static readonly System.Drawing.Color[] TunerColors =
        {
            System.Drawing.Color.FromArgb(0, 120, 215),     // RX 1 blue
            System.Drawing.Color.FromArgb(0, 153, 0),       // RX 2 green
            System.Drawing.Color.FromArgb(255, 140, 0),     // RX 3 orange
            System.Drawing.Color.FromArgb(148, 0, 211)      // RX 4 violet
        };

        // The highlight is shown for a while only, then everything looks as before.
        private const int TunerHighlightSeconds = 10;
        private System.Windows.Forms.Timer _tuner_highlight_timer;

        private void ClearTunerHighlight()
        {
            if (_tuner_highlight_timer != null)
                _tuner_highlight_timer.Stop();

            if (videoSource != null)
                videoSource.SetTunerHighlight(-1, System.Drawing.Color.Empty);

            foreach (StreamInfoContainer info in info_display)
            {
                if (info != null)
                    info.HighlightColor = System.Drawing.Color.Empty;
            }
        }

        // Left click on a video or in the row of a tuner in the spectrum: this tuner is the selected one. Its groups in the
        // tabs and the frame of its info line get the colour of the tuner for a while (the others keep the normal look),
        // and its Properties tab is shown. Only the right click in the spectrum shows the Special tab.
        private void SelectTuner(int video_nr)
        {
            if (!HighlightTuner(video_nr))
                return;

            if (!ShowTunerTab(videoSource.GetPropertiesTabTitle(video_nr)) && tabControl1.TabPages.Contains(PropertiesPage))
                tabControl1.SelectedTab = PropertiesPage;
        }

        // colours the groups of the tuner in the tabs and the frame of its info line for a while, false if there is no such tuner
        private bool HighlightTuner(int video_nr)
        {
            if (videoSource == null || _fullscreen_form != null || video_nr < 0 || video_nr >= videoSource.GetVideoSourceCount())
                return false;

            System.Drawing.Color color = TunerColors[video_nr % TunerColors.Length];

            videoSource.SetTunerHighlight(video_nr, color);

            for (int i = 0; i < info_display.Count; i++)
            {
                if (info_display[i] != null)
                    info_display[i].HighlightColor = (i == video_nr) ? color : System.Drawing.Color.Empty;
            }

            if (_tuner_highlight_timer == null)
            {
                _tuner_highlight_timer = new System.Windows.Forms.Timer();
                _tuner_highlight_timer.Tick += (s, ev) => ClearTunerHighlight();
            }
            _tuner_highlight_timer.Stop();
            _tuner_highlight_timer.Interval = TunerHighlightSeconds * 1000;
            _tuner_highlight_timer.Start();

            return true;
        }


        private void quitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            settingsForm settings_form = new settingsForm(ref _settings);

            if (settings_form.ShowDialog() == DialogResult.OK)
            {
                _settingsManager.SaveSettings(_settings);
            }
        }

        private void sourceSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Once a source is connected the source selection panel (with its Source Settings
            // button) is gone, so this is the way to reach the connected source's settings.
            var source = (source_connected && videoSource != null) ? videoSource : _availableSources[comboAvailableSources.SelectedIndex];
            source.ShowSettings();
        }

        private void hardwareInfoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // FTDI devices as the USB driver lists them, plus what the connected source knows (none before connect)
            using (var info = new HardwareOverviewForm((source_connected && videoSource != null) ? videoSource : null))
            {
                info.ShowDialog(this);
            }
        }

        private void qO100WidebandChatToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (batc_chat != null)
                batc_chat.Show();
        }

        private void configureCallsignToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // The Pluto menu is always visible regardless of the "Pluto Control (F5OEO)" checkbox,
            // so pluto_client can still be null here (not connected, or Pluto Control unchecked).
            if (pluto_client == null)
            {
                Log.Warning("Pluto Control is not active - enable \"Pluto Control (F5OEO)\" and connect first");
                return;
            }
            pluto_client.ConfigureCallsignAndReboot("ZR6TG");
        }

        private void LoadSeperateWindow(Control VideoControl, string Title, int Nr, Control[] extraControls)
        {
            var external_video_form = new VideoViewForm(VideoControl, Title, Nr, videoSource, extraControls);
            external_video_form.Show();
        }

        private OTMediaPlayer ConfigureVideoPlayer(int nr, int preference, bool seperate_window)
        {
            OTMediaPlayer player = null;

            VolumeInfoContainer video_volume_display = null;
            StreamInfoContainer video_info_display = null;

            video_volume_display = new VolumeInfoContainer();
            video_volume_display.Tag = nr;

            video_info_display = new StreamInfoContainer(_settings.show_video_info[nr]);
            video_info_display.Tag = nr;


            switch (preference)
            {
                case 0: // vlc
                    Log.Information(nr.ToString() + " - " + "VLC");
                    var vlc_video_player = new LibVLCSharp.WinForms.VideoView();
                    vlc_video_player.Dock = DockStyle.Fill;
                    vlc_video_player.MouseClick += video_player_MouseClick;
                    vlc_video_player.MouseWheel += video_player_MouseWheel;
                    vlc_video_player.MouseDoubleClick += video_player_MouseDoubleClick;
                    vlc_video_player.Tag = nr;


                    if (seperate_window)
                    {
                        LoadSeperateWindow(vlc_video_player, "VLC - " + (nr + 1).ToString(), nr, new Control[] {video_volume_display, video_info_display});
                    }
                    else
                    {
                        video_panels[nr].Controls.Add(video_volume_display);
                        video_panels[nr].Controls.Add(video_info_display);
                        video_panels[nr].Controls.Add(vlc_video_player);
                    }

                    player = new VLCMediaPlayer(vlc_video_player);
                    player.Initialize(videoSource.GetVideoDataQueue(nr), nr);
                    break;
                    
                case 1: // ffmpeg
                    Log.Information(nr.ToString() + " - " + "FFMPEG");
                    var ffmpeg_video_player = new FlyleafLib.Controls.WinForms.FlyleafHost();
                    ffmpeg_video_player.Dock = DockStyle.Fill;
                    ffmpeg_video_player.MouseClick += video_player_MouseClick;
                    ffmpeg_video_player.MouseWheel += video_player_MouseWheel;
                    ffmpeg_video_player.MouseDoubleClick += video_player_MouseDoubleClick;
                    ffmpeg_video_player.ToggleFullScreenOnDoubleClick = false; // own full screen handling, see issue #22
                    ffmpeg_video_player.Tag = nr;


                    if (seperate_window)
                    {
                        LoadSeperateWindow(ffmpeg_video_player, "FFMPEG - " + (nr + 1).ToString(), nr, new Control[] { video_volume_display, video_info_display });
                    }
                    else
                    {
                        video_panels[nr].Controls.Add(video_volume_display);
                        video_panels[nr].Controls.Add(video_info_display);
                        video_panels[nr].Controls.Add(ffmpeg_video_player);
                    }

                    player = new FFMPEGMediaPlayer(ffmpeg_video_player);
                    player.Initialize(videoSource.GetVideoDataQueue(nr), nr);
                    break;

                case 2: // mpv
                    Log.Information(nr.ToString() + " - " + "MPV");
                    var mpv_video_player = new PictureBox();
                    mpv_video_player.Dock = DockStyle.Fill;
                    mpv_video_player.MouseClick += video_player_MouseClick;
                    mpv_video_player.MouseWheel += video_player_MouseWheel;
                    mpv_video_player.MouseDoubleClick += video_player_MouseDoubleClick;
                    mpv_video_player.Tag = nr;



                    if (seperate_window)
                    {
                        LoadSeperateWindow(mpv_video_player, "MPV - " + (nr + 1).ToString(), nr, new Control[] { video_volume_display, video_info_display});
                    }
                    else
                    {
                        video_panels[nr].Controls.Add(video_volume_display);
                        video_panels[nr].Controls.Add(video_info_display);
                        video_panels[nr].Controls.Add(mpv_video_player);
                    }

                    player = new MPVMediaPlayer(mpv_video_player.Handle.ToInt64());
                    player.Initialize(videoSource.GetVideoDataQueue(nr), nr);
                    break;
            }


            if (video_volume_display != null)
                volume_display.Add(video_volume_display);

            if (video_info_display != null)
                info_display.Add(video_info_display);

            // the controls of this video field (volume display, info line, player), for the large view of one stream
            while (_video_parts.Count <= nr)
                _video_parts.Add(null);
            _video_parts[nr] = seperate_window ? null : video_panels[nr].Controls.Cast<Control>().ToArray();

            return player;
        }

        // Double click on the video shows only the video full screen: the video control is moved into a
        // borderless form and put back into its panel afterwards. Done here instead of by Flyleaf, which
        // restored the window a few pixels smaller on every exit (issue #22).
        private Form _fullscreen_form;
        private Control _fullscreen_video;
        private Control _fullscreen_parent;
        private int _fullscreen_index;
        private Control _fullscreen_info; // the info line above the video, shown in full screen too
        private int _fullscreen_info_index;

        private void video_player_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            if (_fullscreen_form != null)
            {
                LeaveVideoFullScreen();
                return;
            }

            Control video = (Control)sender;
            if (video.Parent == null)
                return;

            _fullscreen_video = video;
            _fullscreen_parent = video.Parent;
            _fullscreen_index = _fullscreen_parent.Controls.GetChildIndex(video);

            var form = new Form();
            form.FormBorderStyle = FormBorderStyle.None;
            form.BackColor = System.Drawing.Color.Black;
            form.ShowInTaskbar = false;
            form.KeyPreview = true;
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = Screen.FromControl(video).Bounds;
            form.KeyDown += (s, ke) =>
            {
                if (ke.KeyCode == Keys.Escape)
                    LeaveVideoFullScreen();
            };
            _fullscreen_form = form;

            _fullscreen_info = null;
            foreach (Control c in _fullscreen_parent.Controls)
            {
                if (c is StreamInfoContainer && c.Tag is int && c.Tag.Equals(video.Tag))
                    _fullscreen_info = c;
            }

            // info first, then video: keeps the dock order of the panel
            if (_fullscreen_info != null)
            {
                _fullscreen_info_index = _fullscreen_parent.Controls.GetChildIndex(_fullscreen_info);
                _fullscreen_info.Parent = form;
            }
            video.Parent = form;
            form.Show(this);
        }

        private void LeaveVideoFullScreen()
        {
            Form form = _fullscreen_form;
            if (form == null)
                return;

            _fullscreen_form = null;

            // lower index first
            if (_fullscreen_info != null && _fullscreen_info_index < _fullscreen_index)
            {
                _fullscreen_info.Parent = _fullscreen_parent;
                _fullscreen_parent.Controls.SetChildIndex(_fullscreen_info, _fullscreen_info_index);
            }
            _fullscreen_video.Parent = _fullscreen_parent;
            _fullscreen_parent.Controls.SetChildIndex(_fullscreen_video, _fullscreen_index);
            if (_fullscreen_info != null && _fullscreen_info.Parent != _fullscreen_parent)
            {
                _fullscreen_info.Parent = _fullscreen_parent;
                _fullscreen_parent.Controls.SetChildIndex(_fullscreen_info, _fullscreen_info_index);
            }
            _fullscreen_info = null;
            form.Close();
            form.Dispose();
        }

        private void video_player_MouseWheel(object sender, MouseEventArgs e)
        {
            int wheel_volume_rate = 10;  // todo, maybe turn this into a setting

            int video_nr = (int)((Control)sender).Tag;

            if (e.Delta < 0)
            {
                videoSource.UpdateVolume(video_nr, -1 * wheel_volume_rate);
            }
            if (e.Delta > 0)
            {
                videoSource.UpdateVolume(video_nr, wheel_volume_rate);
            }

            if (volume_display.Count > video_nr)
            {
                volume_display[video_nr].UpdateVolume(videoSource.GetVolume(video_nr));
            }
        }

        // Left click selects the tuner (SelectTuner). Right click toggles the info line above the video and the choice is
        // saved. Left click used to do this too, so an accidental click in the picture hid it for good (issue #19).
        private void video_player_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                SelectTuner((int)((Control)sender).Tag);
                return;
            }

            if (e.Button != MouseButtons.Right && e.Button != MouseButtons.Middle)
                return;

            int video_nr = (int)((Control)sender).Tag;

            // the wheel click always switches the info line at once, whatever the number of streams
            if (e.Button == MouseButtons.Middle)
            {
                ToggleInfoLine(video_nr);
                return;
            }

            // A menu offers the tabs of this tuner (Properties, Expert, TS Info, Special), the large view of this stream
            // (with three or more streams) and the info line. (In full screen, or when there is nothing but the info line to
            // choose, the right click still switches the info line at once.)
            var tab_pages = TunerTabPages(video_nr);
            if (CanChangeVideoLayout || tab_pages.Count > 0)
            {
                var menu = new ContextMenuStrip();
                if (tab_pages.Count > 0)
                {
                    foreach (var entry in tab_pages)
                    {
                        TabPage page = entry.Value;
                        var item = new ToolStripMenuItem(entry.Key, null, (s, ev) => { HighlightTuner(video_nr); tabControl1.SelectedTab = page; });
                        item.Checked = tabControl1.SelectedTab == page;
                        menu.Items.Add(item);
                    }
                }

                // audio shortcuts as a group of their own below the tabs: this stream on / off, only this stream, all streams off
                bool? muted = videoSource.IsMuted(video_nr);
                if (muted.HasValue && _fullscreen_form == null)
                {
                    if (menu.Items.Count > 0)
                        menu.Items.Add(new ToolStripSeparator());

                    menu.Items.Add(muted.Value ? "Unmute this stream" : "Mute this stream", null, (s, ev) => videoSource.ToggleMute(video_nr));

                    if (videoSource.GetVideoSourceCount() > 1)
                    {
                        menu.Items.Add("Audio only this stream", null, (s, ev) => SetAudioOnly(video_nr));
                        menu.Items.Add("Mute all streams", null, (s, ev) => SetAudioOnly(-1));
                    }
                }

                if (menu.Items.Count > 0)
                    menu.Items.Add(new ToolStripSeparator());

                if (CanChangeVideoLayout)
                {
                    if (_large_video != video_nr || _large_only)
                        menu.Items.Add("Show large, the others below", null, (s, ev) => ShowLargeVideo(video_nr, false));
                    if (_large_video != video_nr || !_large_only)
                        menu.Items.Add("Show only this stream", null, (s, ev) => ShowLargeVideo(video_nr, true));
                    if (_large_video >= 0)
                        menu.Items.Add("Show all streams (grid)", null, (s, ev) => ShowVideoGrid());
                    menu.Items.Add(new ToolStripSeparator());
                }

                menu.Items.Add(InfoLineVisible(video_nr) ? "Hide info line (wheel click)" : "Show info line (wheel click)", null, (s, ev) => ToggleInfoLine(video_nr));
                menu.Closed += (s, ev) => BeginInvoke(new MethodInvoker(menu.Dispose));
                menu.Show(Cursor.Position);
                CloseMenuWhenMouseLeaves(menu, video_nr, (Control)sender);
                return;
            }

            ToggleInfoLine(video_nr);
        }

        // The menu of the video closes by itself when the mouse is more than this far (pixels) from it, or when it
        // moves over the field of another stream.
        private const int VideoMenuCloseDistance = 100;

        private void CloseMenuWhenMouseLeaves(ContextMenuStrip menu, int video_nr, Control own_video)
        {
            var timer = new System.Windows.Forms.Timer { Interval = 100 };
            timer.Tick += (s, ev) =>
            {
                if (menu.IsDisposed || !menu.Visible)
                {
                    timer.Stop();
                    return;
                }

                System.Drawing.Point mouse = Cursor.Position;
                System.Drawing.Rectangle bounds = menu.Bounds;   // screen coordinates

                if (bounds.Contains(mouse))
                    return;

                int dx = Math.Max(Math.Max(bounds.Left - mouse.X, mouse.X - bounds.Right), 0);
                int dy = Math.Max(Math.Max(bounds.Top - mouse.Y, mouse.Y - bounds.Bottom), 0);
                bool too_far = dx * dx + dy * dy > VideoMenuCloseDistance * VideoMenuCloseDistance;

                bool over_other_stream = false;
                for (int i = 0; i < _video_parts.Count && i < video_panels.Length; i++)
                {
                    if (i == video_nr || _video_parts[i] == null)
                        continue;

                    if (video_panels[i].RectangleToScreen(video_panels[i].ClientRectangle).Contains(mouse) && video_panels[i].Visible)
                        over_other_stream = true;
                }

                if (too_far || over_other_stream)
                    menu.Close();
            };
            menu.Closed += (s, ev) => { timer.Stop(); timer.Dispose(); };
            timer.Start();
        }

        // The tabs of this tuner (name without the board suffix, page) for the right click menu: Properties, Expert, TS Info
        // and Special, as far as the source has them. With several boards the pages carry the board name ("Expert-V2").
        private List<KeyValuePair<string, TabPage>> TunerTabPages(int video_nr)
        {
            var result = new List<KeyValuePair<string, TabPage>>();

            string properties_title = videoSource?.GetPropertiesTabTitle(video_nr);
            if (_fullscreen_form != null || properties_title == null || !properties_title.StartsWith("Properties"))
                return result;

            string suffix = properties_title.Substring("Properties".Length);   // "" or "-Pro"

            foreach (string name in new[] { "Properties", "Expert", "TS Info", "Special" })
            {
                foreach (TabPage page in tabControl1.TabPages)
                {
                    if (page.Text == name + suffix)
                    {
                        result.Add(new KeyValuePair<string, TabPage>(name, page));
                        break;
                    }
                }
            }

            return result;
        }

        // Only the audio of this stream is on, all others are muted; -1 mutes everything.
        private void SetAudioOnly(int video_nr)
        {
            for (int device = 0; device < videoSource.GetVideoSourceCount(); device++)
            {
                bool? muted = videoSource.IsMuted(device);
                if (!muted.HasValue)
                    continue;

                bool want_muted = device != video_nr;
                if (muted.Value != want_muted)
                    videoSource.ToggleMute(device);
            }
        }

        private bool InfoLineVisible(int video_nr)
        {
            return info_display.Count > video_nr && info_display[video_nr] != null && info_display[video_nr].Visible;
        }

        private void ToggleInfoLine(int video_nr)
        {
            if (info_display.Count > video_nr)
            {
                if (info_display[video_nr] != null)
                    _settings.show_video_info[video_nr] = info_display[video_nr].Visible = !info_display[video_nr].Visible;
            }
        }

        // ---- large view of one stream (right click on the video) ----------------------------------------------------
        // The controls of every video field are moved between the panels of the 2x2 grid (like the full screen does with
        // the video). One stream on the whole top row and the others below it (one to three fields, the third split in
        // two), or only that stream on the whole video area; "grid" puts everything back.

        private readonly List<Control[]> _video_parts = new List<Control[]>();
        private int _large_video = -1;          // the stream shown large, -1 = the grid
        private bool _large_only = false;       // only that stream is shown
        private SplitContainer _extra_bottom_split;
        private int _grid_splitter = -1;        // where the splitter between the two rows of the grid was, for "grid"
        private readonly Dictionary<int, System.Drawing.Size> _video_sizes = new Dictionary<int, System.Drawing.Size>();

        // A player reports the size of its video (any thread).
        private void VideoSizeReported(int video_nr, MediaStatus status)
        {
            if (status == null || status.VideoWidth == 0 || status.VideoHeight == 0 || !IsHandleCreated)
                return;

            var size = new System.Drawing.Size((int)status.VideoWidth, (int)status.VideoHeight);

            try
            {
                BeginInvoke(new MethodInvoker(() =>
                {
                    if (_video_sizes.TryGetValue(video_nr, out var known) && known == size)
                        return;

                    _video_sizes[video_nr] = size;
                    if (video_nr == _large_video)
                        FitLargeVideo();
                }));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException)
            {
            }
        }

        // "Show large, the others below": the row of the large stream is as high as its video needs at the full width (the
        // aspect ratio of the video, 16:9 until the player has reported the size), so it is shown completely. The other
        // streams get the rest, but at least a fifth of the area.
        private void FitLargeVideo()
        {
            if (_large_video < 0 || _large_only || splitContainer3.Panel2Collapsed || splitContainer3.Width <= 0)
                return;

            double ratio = 9.0 / 16.0;
            if (_video_sizes.TryGetValue(_large_video, out var size) && size.Width > 0 && size.Height > 0)
                ratio = (double)size.Height / size.Width;

            int wanted = (int)Math.Round(splitContainer3.Width * ratio);
            int smallest_bottom = Math.Max(splitContainer3.Panel2MinSize, splitContainer3.Height / 5);
            int most = splitContainer3.Height - splitContainer3.SplitterWidth - smallest_bottom;
            int distance = Math.Max(splitContainer3.Panel1MinSize, Math.Min(wanted, most));

            if (distance > 0 && distance != splitContainer3.SplitterDistance)
            {
                try
                {
                    splitContainer3.SplitterDistance = distance;
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        // double click on a splitter: the rows get the same height again - except in the large view, where the large stream
        // gets the height of its format
        private void ResetRowSplitter()
        {
            if (_large_video >= 0 && !_large_only)
                FitLargeVideo();
            else
                splitContainer3.SplitterDistance = (int)(splitContainer3.Height * 0.5);
        }

        private bool CanChangeVideoLayout => _fullscreen_form == null && _video_parts.Count >= 3 && _video_parts.All(p => p != null);

        private static void PlaceVideo(Control parent, Control[] parts)
        {
            foreach (var part in parts)
                parent.Controls.Add(part);
        }

        private void TakeOutVideos()
        {
            foreach (var parts in _video_parts)
            {
                foreach (var part in parts)
                    part.Parent?.Controls.Remove(part);
            }

            if (_extra_bottom_split != null)
            {
                _extra_bottom_split.Parent?.Controls.Remove(_extra_bottom_split);
                _extra_bottom_split.Dispose();
                _extra_bottom_split = null;
            }
        }

        private void ShowLargeVideo(int video_nr, bool only_this)
        {
            if (!CanChangeVideoLayout || video_nr < 0 || video_nr >= _video_parts.Count)
                return;

            int count = _video_parts.Count;
            var others = Enumerable.Range(0, count).Where(i => i != video_nr).ToList();

            if (_large_video < 0)
                _grid_splitter = splitContainer3.SplitterDistance;   // the grid comes back with the splitter where it was

            SuspendLayout();
            try
            {
                TakeOutVideos();
                _large_video = video_nr;
                _large_only = only_this;

                // the top row holds only this stream, over its whole width
                splitContainer4.Panel2Collapsed = true;
                PlaceVideo(splitContainer4.Panel1, _video_parts[video_nr]);

                if (only_this)
                {
                    // no bottom row; the other streams stay in it, hidden, and keep running
                    splitContainer3.Panel2Collapsed = true;
                    splitContainer5.Panel2Collapsed = false;
                    for (int i = 0; i < others.Count; i++)
                        PlaceVideo(i == 0 ? splitContainer5.Panel1 : splitContainer5.Panel2, _video_parts[others[i]]);
                }
                else
                {
                    splitContainer3.Panel2Collapsed = false;

                    if (others.Count == 1)
                    {
                        splitContainer5.Panel2Collapsed = true;
                        PlaceVideo(splitContainer5.Panel1, _video_parts[others[0]]);
                    }
                    else if (others.Count == 2)
                    {
                        splitContainer5.Panel2Collapsed = false;
                        PlaceVideo(splitContainer5.Panel1, _video_parts[others[0]]);
                        PlaceVideo(splitContainer5.Panel2, _video_parts[others[1]]);
                    }
                    else
                    {
                        // three streams under the large one: the right field of the bottom row is split in two
                        splitContainer5.Panel2Collapsed = false;
                        _extra_bottom_split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Vertical };
                        splitContainer5.Panel2.Controls.Add(_extra_bottom_split);
                        PlaceVideo(splitContainer5.Panel1, _video_parts[others[0]]);
                        PlaceVideo(_extra_bottom_split.Panel1, _video_parts[others[1]]);
                        PlaceVideo(_extra_bottom_split.Panel2, _video_parts[others[2]]);

                        splitContainer5.SplitterDistance = Math.Max(1, splitContainer5.Width / 3);
                        _extra_bottom_split.SplitterDistance = Math.Max(1, _extra_bottom_split.Width / 2);
                    }
                }
            }
            finally
            {
                ResumeLayout(true);
            }

            FitLargeVideo();
        }

        private void ShowVideoGrid()
        {
            if (_large_video < 0)
                return;

            SuspendLayout();
            try
            {
                TakeOutVideos();
                _large_video = -1;
                _large_only = false;

                splitContainer3.Panel2Collapsed = false;
                splitContainer4.Panel2Collapsed = false;
                splitContainer5.Panel2Collapsed = false;

                for (int i = 0; i < _video_parts.Count; i++)
                    PlaceVideo(video_panels[i], _video_parts[i]);
            }
            finally
            {
                ResumeLayout(true);
            }

            if (_grid_splitter > 0)
            {
                try
                {
                    splitContainer3.SplitterDistance = _grid_splitter;
                }
                catch (InvalidOperationException)
                {
                }
            }
        }

        // configure TS recorders
        private List<TSRecorder> ConfigureTSRecorders(OTSource video_source, string video_path)
        {
            List<TSRecorder> tSRecorders = new List<TSRecorder>();

            for (int c = 0; c < video_source.GetVideoSourceCount(); c++)
            {
                var ts_recorder = new TSRecorder(video_path, c, video_source);
                tSRecorders.Add(ts_recorder);
            }

            return tSRecorders;
        }

        // configure TS streamers
        private List<TSUdpStreamer> ConfigureTSStreamers(OTSource video_source, string[] udpHosts, int[] udpPorts )
        {
            List<TSUdpStreamer> tsStreamers = new List<TSUdpStreamer>();

            for (int c= 0; c < videoSource.GetVideoSourceCount(); c++)
            {
                var ts_streamer = new TSUdpStreamer(udpHosts[c], udpPorts[c], c, video_source);
                tsStreamers.Add(ts_streamer);   
            }

            return tsStreamers;
        }

        // configure media players
        private List<OTMediaPlayer> ConfigureMediaPlayers(int amount, int[] playerPreference, bool[] windowed)
        {
            List<OTMediaPlayer> mediaPlayers = new List<OTMediaPlayer>();

            _video_parts.Clear();
            _large_video = -1;

            if (amount == 4)
            {
                video_panels[0] = splitContainer4.Panel1;
                video_panels[2] = splitContainer5.Panel1;
                video_panels[1] = splitContainer4.Panel2;
                video_panels[3] = splitContainer5.Panel2;
            }
            else
            {
                video_panels[0] = splitContainer4.Panel1;
                video_panels[1] = splitContainer5.Panel1;
                video_panels[2] = splitContainer4.Panel2;
                video_panels[3] = splitContainer5.Panel2;
            }

            for (int c = 0; c < amount; c++)
            {
                var media_player = ConfigureVideoPlayer(c, playerPreference[c], windowed[c]);
                mediaPlayers.Add(media_player);
            }

            ConfigureVideoLayout(amount);

            return mediaPlayers;
        }


        // configure video layout - only for 1, 2 or 4 video players 
        // todo: reset for changing sources
        private void ConfigureVideoLayout(int amount)
        {
            splitContainer3.Visible = true;
            splitContainer4.Visible = true;
            splitContainer5.Visible = true;

            switch (amount)
            {
                case 1:
                    splitContainer4.Panel2Collapsed = true;
                    splitContainer4.Panel2.Hide();
                    splitContainer3.Panel2Collapsed = true;
                    splitContainer3.Panel2.Hide();
                    break;
                case 2:
                    splitContainer4.Panel2Collapsed = true;
                    splitContainer4.Panel2.Hide();
                    splitContainer5.Panel2Collapsed = true;
                    splitContainer5.Panel2.Hide();
                    break;
            }
        }

        private void DisconnectCurrentSource()
        {
            toolstripConnectToggle.Text = "Connect Source";
        }

        private bool ConnectSelectedSource()
        {
            if (!SourceConnect(_availableSources[comboAvailableSources.SelectedIndex]))
                return false;

            hidePanelToolStripMenuItem.Visible = true;

            if (checkBatcSpectrum.Checked)
            {
                hideExtraToolStripMenuItem.Visible = true;
                // show spectrum
                splitContainer2.Panel2Collapsed = false;
                splitContainer2.Panel2.Enabled = true;
                splitContainer2.Panel2.Show();

                this.DoubleBuffered = true;
                DiagnosticsHelper.Measure("Connect: BATC spectrum", () =>
                {
                    batc_spectrum = new BATCSpectrum(spectrum, videoSource.GetVideoSourceCount());
                    batc_spectrum.QuickTuneActive = checkQuicktune.Checked;
                    batc_spectrum.OnSignalSelected += Batc_spectrum_OnSignalSelected;
                    batc_spectrum.OnSpectrumRightClick += Batc_spectrum_OnSpectrumRightClick;
                    batc_spectrum.OnTunerSelected += SelectTuner;
                });
            }

            if (checkBatcChat.Checked)
            {
                qO100WidebandChatToolStripMenuItem.Visible = true;
                DiagnosticsHelper.Measure("Connect: BATC chat", () => batc_chat = new BATCChat(videoSource));
            }

            if (checkQuicktune.Checked)
            {
                DiagnosticsHelper.Measure("Connect: QuickTune", () => quickTune_control = new QuickTuneControl(videoSource));
            }

            if (checkMqttClient.Checked)
            {
                DiagnosticsHelper.Measure("Connect: MQTT", () => mqtt_client = new MqttManager());
            }

            if (checkPlutoCtrl.Checked)
            {
                if (mqtt_client != null)
                {
                    DiagnosticsHelper.Measure("Connect: Pluto Control", () => pluto_client = new F5OEOPlutoControl(mqtt_client));
                }
                else
                {
                    Log.Warning("Pluto Control requires MQTT to be enabled too - not started");
                }
            }

            if (checkDATVReporter.Checked)
            {
                DiagnosticsHelper.Measure("Connect: DATV reporter", () =>
                {
                    if (!datv_reporter.Connect())
                    {
                        Log.Error("DATV Reporter can't connect - check your settings");
                    }
                });
            }

            if (_settings.mute_at_startup)
            {
                _availableSources[comboAvailableSources.SelectedIndex].OverrideDefaultMuted(_settings.mute_at_startup);
            }

            // the saved width of the Properties panel, but wide enough for the tabs of all boards in one row
            splitContainer1.SplitterDistance = Math.Max(_settings.gui_main_splitter_position, _tab_strip.PreferredWidth + 4);

            videoSource.UpdateFrequencyPresets(stored_frequencies);

            //toolstripConnectToggle.Text = "Disconnect Source";
            toolstripConnectToggle.Visible = false;

            // hide/show panels
            TogglePropertiesPanel(_settings.hide_properties);
            if (checkBatcSpectrum.Checked)
            {
                ToggleExtraToolPanel(_settings.hide_ExtraTool);
            }
            this.Focus();
            return true;
        }

        private void btnSourceConnect_Click(object sender, EventArgs e)
        {
            source_connected = ConnectSelectedSource();
        }

        private void btnSourceSettings_Click(object sender, EventArgs e)
        {
            _availableSources[comboAvailableSources.SelectedIndex].ShowSettings();
            sourceInfo.Text = _availableSources[comboAvailableSources.SelectedIndex].GetDescription();
        }

        private void comboAvailableSources_SelectedIndexChanged(object sender, EventArgs e)
        {
            sourceInfo.Text = _availableSources[comboAvailableSources.SelectedIndex].GetDescription();
        }

        private void checkMqttClient_CheckedChanged(object sender, EventArgs e)
        {
            _settings.enable_mqtt_checkbox = checkMqttClient.Checked;
        }

        private void checkPlutoCtrl_CheckedChanged(object sender, EventArgs e)
        {
            _settings.enable_plutoctrl_checkbox = checkPlutoCtrl.Checked;
        }

        private void checkQuicktune_CheckedChanged(object sender, EventArgs e)
        {
            _settings.enable_quicktune_checkbox = checkQuicktune.Checked;
        }

        private void checkBatcSpectrum_CheckedChanged(object sender, EventArgs e)
        {
            _settings.enable_spectrum_checkbox = checkBatcSpectrum.Checked;
            UpdateSpectrumSettingsLink();
        }

        // no spectrum, no AutoTune: the settings of the spectrum are shown but cannot be used without it; the same for the
        // chat (link on the source page, the menu "Open Tuner" is reachable while the source is connected)
        private void UpdateSpectrumSettingsLink()
        {
            linkBatcSpectrumSettings.Enabled = checkBatcSpectrum.Checked;
            linkBatcSpectrumSettings.Cursor = checkBatcSpectrum.Checked ? Cursors.Hand : Cursors.Default;
            spectrumSettingsToolStripMenuItem.Enabled = checkBatcSpectrum.Checked;
            chatSettingsToolStripMenuItem.Enabled = checkBatcChat.Checked;
        }

        private void linkBatcSpectrumSettings_Click(object sender, EventArgs e)
        {
            ShowSpectrumSettings();
        }

        private void spectrumSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowSpectrumSettings();
        }

        private void ShowSpectrumSettings()
        {
            if (!checkBatcSpectrum.Checked)
                return;

            // tuning modes (AutoTune), threshold, times and the overpower layout of the BATC spectrum
            var spectrum_settingsManager = new SettingsManager<BATCSpectrumSettings>("spectrumSettings");
            BATCSpectrumSettings spectrum_settings = spectrum_settingsManager.LoadSettings(new BATCSpectrumSettings());

            using (var spectrum_settings_form = new BATCSpectrumSettingsForm(spectrum_settings))
            {
                if (spectrum_settings_form.ShowDialog() == DialogResult.OK)
                {
                    spectrum_settingsManager.SaveSettings(spectrum_settings);
                    batc_spectrum?.ReloadSettings();
                }
            }
        }

        private void checkBatcChat_CheckedChanged(object sender, EventArgs e)
        {
            UpdateSpectrumSettingsLink();
            _settings.enable_chatform_checkbox = checkBatcChat.Checked;
        }

        private void linkBatcWebchatSettings_Click(object sender, EventArgs e)
        {
            ShowChatSettings();
        }

        private void chatSettingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowChatSettings();
        }

        private void ShowChatSettings()
        {
            // webchat settings; with a running chat its own settings are edited: they apply at once, and the chat does not
            // write its old values over the new ones when it is closed
            SettingsManager<WebChatSettings> wc_settingsManager = new SettingsManager<WebChatSettings>("qo100_webchat_settings");
            WebChatSettings wc_settings = batc_chat != null
                ? batc_chat.Settings
                : wc_settingsManager.LoadSettings(new WebChatSettings());

            int old_font_size = wc_settings.chat_font_size;

            WebChatSettngsForm wc_settings_form = new WebChatSettngsForm(ref wc_settings);

            if (wc_settings_form.ShowDialog() == DialogResult.OK)
            {
                if (batc_chat != null)
                {
                    batc_chat.ApplySettings();

                    // the lines already written keep their font: build the chat again, it gets the history from the server
                    if (wc_settings.chat_font_size != old_font_size)
                        RestartChat();
                }
                else
                {
                    wc_settingsManager.SaveSettings(wc_settings);
                }
            }

        }

        // a new chat window with the saved settings, shown again if it was visible (the login has to be done again unless
        // auto login is on)
        private void RestartChat()
        {
            if (batc_chat == null || videoSource == null)
                return;

            bool was_visible = batc_chat.Visible;

            batc_chat.CloseForRestart();
            batc_chat = new BATCChat(videoSource);

            if (was_visible)
                batc_chat.Show();
        }

        private void linkDocumentation_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/opentuner-documentation/");
        }

        private void linkMqttSettings_Click(object sender, EventArgs e)
        {
            // mqtt settings
            MqttManagerSettings mqtt_settings = new MqttManagerSettings();
            SettingsManager<MqttManagerSettings> mqtt_settingsManager = new SettingsManager<MqttManagerSettings>("mqttclient_settings");
            mqtt_settings = mqtt_settingsManager.LoadSettings(mqtt_settings);

            MqttSettingsForm mqtt_settings_form = new MqttSettingsForm(ref mqtt_settings);

            if (mqtt_settings_form.ShowDialog() == DialogResult.OK)
            {
                mqtt_settingsManager.SaveSettings(mqtt_settings);
            }
        }

        private void linkQuickTuneSettings_Click(object sender, EventArgs e)
        {
            // quick tune settings
            QuickTuneControlSettings quicktune_settings = new QuickTuneControlSettings();
            SettingsManager<QuickTuneControlSettings> quicktune_settingsManager = new SettingsManager<QuickTuneControlSettings>("quicktune_settings");
            quicktune_settings = quicktune_settingsManager.LoadSettings(quicktune_settings);

            QuickTuneControlSettingsForm quicktune_settings_form = new QuickTuneControlSettingsForm(ref quicktune_settings);

            if (quicktune_settings_form.ShowDialog() == DialogResult.OK)
            {
                quicktune_settingsManager.SaveSettings(quicktune_settings);
            }
        }

        private void linkSpectrumDocumentation_Click(object sender, EventArgs e)
        {           
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/opentuner-spectrum/");
        }

        private void LinkMqttDocumentation_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/opentuner-mqtt-client/");
        }

        private void linkQuickTuneDocumentation_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/opentuner-quicktune-control/");
        }

        private void linkBatcWebchatDocumentation_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/opentuner-webchat/");
        }

        private void linkOpenTunerUpdates_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/open-tuner/");
        }

        private void linkSourceMoreInfo_Click(object sender, EventArgs e)
        {
            if (_availableSources[comboAvailableSources.SelectedIndex].GetMoreInfoLink().Length > 0 ) 
            {
                opentuner.Utilities.CommonFunctions.OpenUrl(_availableSources[comboAvailableSources.SelectedIndex].GetMoreInfoLink());
            }
        }

        private void linkGithubIssues_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://github.com/tomvdb/open_tuner/issues");

        }

        private void linkForum_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://forum.batc.org.uk/viewforum.php?f=142");

        }

        private void linkSupport_Click(object sender, EventArgs e)
        {
            // Fork only: offer both the original author and the fork maintainer
            var menu = new ContextMenuStrip();
            menu.Items.Add("Original author (ZR6TG)", null, (s, a) => opentuner.Utilities.CommonFunctions.OpenUrl("https://www.buymeacoffee.com/zr6tg/"));
            menu.Items.Add("This fork (DH1RK)", null, (s, a) => opentuner.Utilities.CommonFunctions.OpenUrl("https://buymeacoffee.com/dh1rk"));
            menu.Show(linkSupport, new System.Drawing.Point(0, linkSupport.Height));
        }

        private void linkBatc_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://batc.org.uk/");

        }

        private void link2ndTS_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/adding-2nd-transport-to-batc-minitiouner-v2/");
        }

        private void linkPicoTuner_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/2024/02/11/picotuner-an-experimental-dual-ts-alternative/");
        }

        private void menuManageFrequencyPresets_Click(object sender, EventArgs e)
        {
            frequencyManagerForm freqManager = new frequencyManagerForm(stored_frequencies);

            freqManager.ShowDialog();

            frequenciesManager.SaveSettings(stored_frequencies);

            videoSource?.UpdateFrequencyPresets(stored_frequencies);
        }

        private void TogglePropertiesPanel(bool hide)
        {
            if (hide)
            {
                splitContainer1.Panel1.Hide();
                splitContainer1.Panel1Collapsed = true;
                properties_hidden = true;
                hidePanelToolStripMenuItem.Text = "Show Properties";
            }
            else
            {
                splitContainer1.Panel1.Show();
                splitContainer1.Panel1Collapsed = false;
                properties_hidden = false;
                hidePanelToolStripMenuItem.Text = "Hide Properties";
            }
        }

        private void ToggleExtraToolPanel(bool hide)
        {
            if (hide)
            {
                splitContainer2.Panel2.Hide();
                splitContainer2.Panel2Collapsed = true;
                ExtraTool_hidden = true;
                hideExtraToolStripMenuItem.Text = "Show Spectrum";
            }
            else
            {
                splitContainer2.Panel2.Show();
                splitContainer2.Panel2Collapsed = false;
                ExtraTool_hidden = false;
                hideExtraToolStripMenuItem.Text = "Hide Spectrum";
            }
        }

        // Logs the exact moment Windows delivers the close request, before any of our own
        // FormClosing code runs - lets us tell apart "Windows took a while to deliver WM_CLOSE"
        // (something outside our message loop, e.g. a hung child/owned window blocking window
        // activation) from "our own handler took a while to start" (see issue #6).
        protected override void WndProc(ref Message m)
        {
            const int WM_CLOSE = 0x0010;
            if (m.Msg == WM_CLOSE)
            {
                Log.Information("MainForm: WM_CLOSE received (thread " + Environment.CurrentManagedThreadId + ")");
            }
            base.WndProc(ref m);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg == 0X0100 && (Keys)m.WParam.ToInt32() == Keys.P && ModifierKeys == Keys.Control)
            {
                TogglePropertiesPanel(!properties_hidden);
                _settings.hide_properties = properties_hidden;
                return true;
            }
            if (m.Msg == 0X0100 && (Keys)m.WParam.ToInt32() == Keys.E && ModifierKeys == Keys.Control)
            {
                if (checkBatcSpectrum.Checked)
                {
                    ToggleExtraToolPanel(!ExtraTool_hidden);
                    _settings.hide_ExtraTool = ExtraTool_hidden;
                }
                return true;
            }

            return false;
        }

        private void toolstripConnectToggle_Click(object sender, EventArgs e)
        {
            if (!source_connected)
            {
                source_connected = ConnectSelectedSource();
            }
        }

        private void linkDATVReporterSettings_Click(object sender, EventArgs e)
        {
            datv_reporter.ShowSettings();
        }

        private void checkDATVReporter_CheckedChanged(object sender, EventArgs e)
        {
            _settings.enable_datvreporter_checkbox = checkDATVReporter.Checked;
        }

        private void LinkDatvReportMoreInfo_Click(object sender, EventArgs e)
        {
            opentuner.Utilities.CommonFunctions.OpenUrl("https://www.zr6tg.co.za/opentuner-datv-reporter/");
        }

        private void ExtraToolsTab_DrawItem(object sender, DrawItemEventArgs e)
        {

        }

        private void splitContainer3_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ResetRowSplitter();
            if (splitContainer4.Panel2Collapsed == false)
                splitContainer4.SplitterDistance = (int)(splitContainer4.Width * 0.5);
            if (splitContainer5.Panel2Collapsed == false)
                splitContainer5.SplitterDistance = (int)(splitContainer5.Width * 0.5);
        }

        private void splitContainer4_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ResetRowSplitter();
            splitContainer4.SplitterDistance = (int)(splitContainer4.Width * 0.5);
            if (splitContainer5.Panel2Collapsed == false)
                splitContainer5.SplitterDistance = (int)(splitContainer5.Width * 0.5);
        }

        private void splitContainer5_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ResetRowSplitter();
            if (splitContainer4.Panel2Collapsed == false)
                splitContainer4.SplitterDistance = (int)(splitContainer4.Width * 0.5);
            splitContainer5.SplitterDistance = (int)(splitContainer5.Width * 0.5);
        }

        private void hidePanelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TogglePropertiesPanel(!properties_hidden);
        }

        private void hideExtraToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToggleExtraToolPanel(!ExtraTool_hidden);
        }
    }
}
