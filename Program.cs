using FlyleafLib;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using opentuner.Utilities;

namespace opentuner
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        public static LoggingLevelSwitch levelSwitch;
        public static bool BreakOnStall;

        [DllImport("user32.dll")]
        private static extern bool ShowWindow([In] IntPtr hWnd, [In] int nCmdShow);

        [DllImport("kernel32.dll")]
        static extern IntPtr GetConsoleWindow();

        [DllImport("kernel32.dll")]
        static extern bool AllocConsole();

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool SetDllDirectory(string lpPathName);

        [STAThread]

        static void Main(string[] args)
        {
            // Before any Form (including the first-run PlaybackPathsSetupForm below) is shown.
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            int i = 0;
            int debugLevel = 3; // Warning
            levelSwitch = new LoggingLevelSwitch();

            while (i < args.Length)
            {
                switch (args[i])
                {
                    case "--debuglevel":
                        int new_debug_level = -1;

                        if (int.TryParse(args[i + 1], out new_debug_level))
                        {
                            if (new_debug_level < 6 && new_debug_level >= 0)
                            {
                                debugLevel = new_debug_level;
                            }
                            i += 1;
                        }
                        break;

                    case "--breakonstall":
                        // Visual Studio only: break into the debugger when the UI thread stops responding
                        BreakOnStall = true;
                        break;

                    case "--hideconsolewindow":
                        // minimize console window
                        IntPtr handle = GetConsoleWindow();
                        if (handle != IntPtr.Zero)
                        {
                            ShowWindow(handle, 0);
                        }
                        break;

                    default:
                        break;
                }
                // grab next param
                i += 1;
            }

            switch (debugLevel)
            {
                case 0: // Verbose
                    levelSwitch.MinimumLevel = LogEventLevel.Verbose;
                    break;

                case 1: // Debug
                    levelSwitch.MinimumLevel = LogEventLevel.Debug;
                    break;

                case 2: // Information
                    levelSwitch.MinimumLevel = LogEventLevel.Information;
                    break;

                case 3: // Warning
                    levelSwitch.MinimumLevel = LogEventLevel.Warning;
                    break;

                case 4: // Error
                    levelSwitch.MinimumLevel = LogEventLevel.Error;
                    break;

                case 5: // Fatal
                    levelSwitch.MinimumLevel = LogEventLevel.Fatal;
                    break;

                default:
                    levelSwitch.MinimumLevel = LogEventLevel.Warning;
                    break;
            }

            // Loaded once, this early, so show_console_window can take effect before Serilog's
            // Console sink is built below (AllocConsole() after that point wouldn't retroactively
            // redirect a sink that already captured the old, console-less stdout handle) - reused
            // further down for ffmpeg_path instead of loading settings a second time.
            var settingsManager = new SettingsManager<MainSettings>("open_tuner_settings");
            MainSettings early_settings = settingsManager.LoadSettings(new MainSettings());

            if (early_settings.show_console_window)
            {
                AllocConsole();
            }

            // First-run guidance: ffmpeg is needed unconditionally (Engine.Start below always
            // loads it, regardless of which player any tuner is set to), libmpv only if MPV is
            // actually selected as a player. Shows one dialog instead of two separate silent
            // MessageBox warnings further down, with a download link and a folder picker that
            // writes straight into Settings - "Skip" leaves the existing fallback behavior alone.
            bool ffmpegOk = PlaybackPathsSetupForm.FfmpegPathValid(early_settings.ffmpeg_path);
            bool libmpvNeeded = early_settings.mediaplayer_preferences != null && Array.IndexOf(early_settings.mediaplayer_preferences, 2) >= 0;
            bool libmpvOk = !libmpvNeeded || PlaybackPathsSetupForm.LibmpvPathValid(early_settings.libmpv_path);

            if (!ffmpegOk || !libmpvOk)
            {
                using (var setupForm = new PlaybackPathsSetupForm(early_settings.ffmpeg_path, early_settings.libmpv_path, !ffmpegOk, !libmpvOk))
                {
                    if (setupForm.ShowDialog() == DialogResult.OK)
                    {
                        early_settings.ffmpeg_path = setupForm.FfmpegPath;
                        early_settings.libmpv_path = setupForm.LibmpvPath;
                        settingsManager.SaveSettings(early_settings);
                    }
                }
            }

            // Must run before any P/Invoke call reaches libmpv-2.dll (MPVMediaPlayer is only
            // instantiated on demand, but SetDllDirectory has to be in place before that first
            // call, so it's simplest to just always set it here, this early). Falls back to the
            // default DLL search order if the configured folder still doesn't exist (e.g. the
            // setup dialog above was skipped).
            if (!string.IsNullOrWhiteSpace(early_settings.libmpv_path) && Directory.Exists(early_settings.libmpv_path))
            {
                SetDllDirectory(early_settings.libmpv_path);
            }

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch)
                .WriteTo.Console()
                .WriteTo.File("logs\\ot_log_" + DateTime.Now.ToString("yyyy-dd-M--HH-mm-ss") + ".txt")
                .CreateLogger();

            opentuner.Utilities.DiagnosticsHelper.Install();

            // Always log the starting information
            // swith logging level to Information
            LogEventLevel lastMinimumLevel = levelSwitch.MinimumLevel;
            levelSwitch.MinimumLevel = LogEventLevel.Information;

            Log.Information("Starting OpenTuner");

            // swith logging level back
            levelSwitch.MinimumLevel = lastMinimumLevel;

            string logDirectory = AppDomain.CurrentDomain.BaseDirectory + "logs\\";

            if (Directory.Exists(logDirectory))
            {
                var logFiles = Directory.GetFiles(logDirectory, "*.txt").Select(f => new FileInfo(f)).OrderByDescending(f => f.CreationTime);
                int fileCount = logFiles.Count();
                if (fileCount > 10)
                {
                    i = 0;
                    foreach (var file in logFiles)
                    {
                        if (i > 9)
                        {
                            try
                            {
                                File.Delete(file.FullName);
                                Log.Debug("Log file deleted: " + file.Name);
                            }
                            catch
                            {
                                Log.Warning("Log file for deletion not found: " + file.Name);
                            }
                        }
                        i++;
                    }
                }
            }

            try
            {
                // ffmpeg_path is user-configurable (Settings > Playback Paths > ffmpeg Path)
                // since the shared-library ffmpeg build has to match the FFmpeg.AutoGen NuGet
                // package version. The first-run setup dialog above already asked for this if it
                // was missing; falls back to the bundled "ffmpeg\" folder if still not set/found
                // (e.g. the dialog was skipped). (early_settings was already loaded above.)
                string ffmpeg_path = !string.IsNullOrWhiteSpace(early_settings.ffmpeg_path) && Directory.Exists(early_settings.ffmpeg_path)
                    ? early_settings.ffmpeg_path
                    : @"ffmpeg\";

                Engine.Start(new EngineConfig()
                {
                    FFmpegPath = ffmpeg_path,
                    // FFmpegDevices removed in FlyleafLib 3.11.5's EngineConfig - avdevice/avfilter
                    // loading is no longer a manual opt-out here (we never used dshow/gdigrab).
                    //LogLevel = LogLevel.Debug,
                                              //LogOutput = ":console",
                                              //LogOutput = @"C:\temp2\ffmpeg.log",

                    /*
                    UIRefresh = true,    // Required for Activity, BufferedDuration, Stats in combination with Config.Player.Stats = true
                    UIRefreshInterval = 250,      // How often (in ms) to notify the UI
                    UICurTimePerSecond = false,     // Whether to notify UI for CurTime only when it's second changed or by UIRefreshInterval
                    */
                });

                Application.Run(new MainForm(args));
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Program.Main: Uncaught Exception");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}
