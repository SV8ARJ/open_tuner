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

            // the active tab is drawn orange, the 3D tab look was hard to read
            tabControl1.DrawMode = TabDrawMode.OwnerDrawFixed;
            tabControl1.ItemSize = new System.Drawing.Size(84, 26);
            tabControl1.DrawItem += TabControl1_DrawItem;

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

                tabControl1.Width = 100;
                DiagnosticsHelper.Measure("Connect: tabs, update (repaint)", () => tabControl1.Update());
            });
            videoSource.OnSourceData += VideoSource_OnSourceData;

            return true;
        }

        // Owner-drawn tab headers: the selected tab has a light orange background, the others are plain.
        private void TabControl1_DrawItem(object sender, DrawItemEventArgs e)
        {
            var tabs = (TabControl)sender;
            bool selected = e.Index == tabs.SelectedIndex;
            System.Drawing.Rectangle bounds = tabs.GetTabRect(e.Index);

            using (var back = new System.Drawing.SolidBrush(selected ? System.Drawing.Color.FromArgb(255, 204, 128) : System.Drawing.SystemColors.Control))
            {
                e.Graphics.FillRectangle(back, bounds);
            }

            TextRenderer.DrawText(e.Graphics, tabs.TabPages[e.Index].Text, tabs.Font, bounds,
                                  selected ? System.Drawing.Color.Black : System.Drawing.SystemColors.ControlText,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

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

        // Right click toggles the info line above the video and the choice is saved. Left click used to
        // do this too, so an accidental click in the picture hid it for good (issue #19).
        private void video_player_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right && e.Button != MouseButtons.Middle)
                return;

            int video_nr = (int)((Control)sender).Tag;

            // the wheel click always switches the info line at once, whatever the number of streams
            if (e.Button == MouseButtons.Middle)
            {
                ToggleInfoLine(video_nr);
                return;
            }

            // With three or more streams a menu offers the large view of this stream, next to the info line.
            // (In full screen, or with fewer streams, the right click still switches the info line at once.)
            if (CanChangeVideoLayout)
            {
                var menu = new ContextMenuStrip();
                if (_large_video != video_nr || _large_only)
                    menu.Items.Add("Show large, the others below", null, (s, ev) => ShowLargeVideo(video_nr, false));
                if (_large_video != video_nr || !_large_only)
                    menu.Items.Add("Show only this stream", null, (s, ev) => ShowLargeVideo(video_nr, true));
                if (_large_video >= 0)
                    menu.Items.Add("Show all streams (grid)", null, (s, ev) => ShowVideoGrid());
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add(InfoLineVisible(video_nr) ? "Hide info line (wheel click)" : "Show info line (wheel click)", null, (s, ev) => ToggleInfoLine(video_nr));
                menu.Closed += (s, ev) => BeginInvoke(new MethodInvoker(menu.Dispose));
                menu.Show(Cursor.Position);
                return;
            }

            ToggleInfoLine(video_nr);
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
                    batc_spectrum.OnSignalSelected += Batc_spectrum_OnSignalSelected;
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

            splitContainer1.SplitterDistance = _settings.gui_main_splitter_position;

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
        }

        private void checkBatcChat_CheckedChanged(object sender, EventArgs e)
        {
            _settings.enable_chatform_checkbox = checkBatcChat.Checked;
        }

        private void linkBatcWebchatSettings_Click(object sender, EventArgs e)
        {
            // webchat settings
            WebChatSettings wc_settings = new WebChatSettings();
            SettingsManager<WebChatSettings> wc_settingsManager = new SettingsManager<WebChatSettings>("qo100_webchat_settings");
            wc_settings = (wc_settingsManager.LoadSettings(wc_settings));

            WebChatSettngsForm wc_settings_form = new WebChatSettngsForm(ref wc_settings);

            if (wc_settings_form.ShowDialog() == DialogResult.OK)
            {
                wc_settingsManager.SaveSettings(wc_settings);
            }

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
            splitContainer3.SplitterDistance = (int)(splitContainer3.Height * 0.5);
            if (splitContainer4.Panel2Collapsed == false)
                splitContainer4.SplitterDistance = (int)(splitContainer4.Width * 0.5);
            if (splitContainer5.Panel2Collapsed == false)
                splitContainer5.SplitterDistance = (int)(splitContainer5.Width * 0.5);
        }

        private void splitContainer4_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            splitContainer3.SplitterDistance = (int)(splitContainer3.Height * 0.5);
            splitContainer4.SplitterDistance = (int)(splitContainer4.Width * 0.5);
            if (splitContainer5.Panel2Collapsed == false)
                splitContainer5.SplitterDistance = (int)(splitContainer5.Width * 0.5);
        }

        private void splitContainer5_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            splitContainer3.SplitterDistance = (int)(splitContainer3.Height * 0.5);
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
