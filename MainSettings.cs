using opentuner.SettingsManagement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opentuner
{
    public class MainSettings : GenericSettings
    {
        [Group("Settings 1")]
        [FriendlyName("Media Path")]
        public string media_path = "";
        public string media_video_path = "";

        // Folder containing the ffmpeg shared-library DLLs (avcodec-XX.dll etc.) used by
        // FlyleafLib/FFmpeg.AutoGen for playback. Kept user-configurable rather than pinning a
        // bundled copy in the repo, since the ffmpeg version has to match the referenced
        // FFmpeg.AutoGen NuGet package. Defaults to the setup this migration was built/tested
        // against - if that folder doesn't exist on a given machine, Program.cs falls back to
        // the bundled "ffmpeg\" folder and points at SETUP.md. Read once at startup (see
        // Program.cs) - changing it via Settings only takes effect on the next app start.
        public string ffmpeg_path = @"D:\Video\ffmpeg-9.0.1-full_build-shared\bin\";

        // Folder containing libmpv-2.dll (used by the MPV player option), added to the DLL search
        // path via SetDllDirectory() at startup. Not NuGet-managed, so this has to point at a
        // manually obtained build. Defaults to the setup this migration was built/tested against
        // - if that folder doesn't exist on a given machine, Program.cs falls back to the default
        // DLL search order and points at SETUP.md. Read once at startup (see Program.cs) -
        // changing it via Settings only takes effect on the next app start.
        public string libmpv_path = @"D:\Video\mpv-dev-x86_64-20260903\";

        // Shows/hides a console window alongside the GUI for live Serilog console-sink output and
        // raw Console.WriteLine() debug lines (both otherwise invisible - the app is built as a
        // GUI-subsystem exe). Read once at startup, before Serilog is configured (see Program.cs).
        public bool show_console_window = false;

        [Group("Settings 2")]
        public bool enable_spectrum_checkbox = true;
        public bool enable_chatform_checkbox = true;
        public bool enable_mqtt_checkbox = false;
        // Grays the "MQTT" checkbox out entirely (Enabled, not just Checked) - for users who only
        // want OpenTuner as receive software and never touch MQTT status publishing/Pluto control.
        public bool show_mqtt_feature = true;
        public bool enable_quicktune_checkbox = false;
        public bool enable_datvreporter_checkbox = false;

        public bool enable_plutoctrl_checkbox = false;
        // Grays the "Pluto Control (F5OEO)" checkbox out entirely (Enabled, not just Checked) -
        // it controls a separate DATV *transmitter*, out of scope for most OpenTuner (receive) users.
        public bool show_plutoctrl_feature = false;

        public int default_source = 0;
        public bool mute_at_startup = true;

        public bool auto_connect = false;

        public bool hide_properties = false; // can also be toggled with CTRL-P
        public bool hide_ExtraTool = false;  // can also be toggled with CTRL-E
        public bool[] show_video_info = { true, true, true, true };

        public int[] mediaplayer_preferences = { 1, 1, 1, 1 };
        public bool[] mediaplayer_windowed = { false, false, false, false };
        public string[] streamer_udp_hosts = { "127.0.0.1", "127.0.0.1", "127.0.0.1", "127.0.0.1" };
        public int[] streamer_udp_ports = { 5000, 5001, 5002, 5003 };

        // loaded on startup and updated on exit
        public int gui_window_width = -1;
        public int gui_window_height = -1;
        public int gui_window_x = -1;
        public int gui_window_y = -1;
        public int gui_window_state = 0;
        public int gui_main_splitter_position = 520;
    }
}
