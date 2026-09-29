using System;
using System.Globalization;
using System.Linq;
using System.Text;
using opentuner.MediaSources.Minitiouner.HardwareInterfaces;

namespace opentuner.MediaSources.Minitiouner
{
    // Drives a Digole serial OLED/LCD wired in I2C mode to JP3 ("I2C-NIM") on a
    // MiniTiounerPro V2 board - the same physical I2C bus as the STV0910/STV6120 NIM chips
    // (see MiniTiounerPro V2 schematic sheet 3). Digole's I2C command set is the same ASCII
    // protocol as its UART mode, just framed as a plain I2C write instead of a serial byte
    // stream - see https://www.digole.com (Serial Display Adapter Manual).
    public class DigoleDisplay
    {
        private static readonly CultureInfo de = CultureInfo.GetCultureInfo("de-DE");

        private readonly MTHardwareInterface hw;
        private readonly byte i2c_address;
        private string last_rendered = null;
        private string[] last_lines = null;    // the lines on the screen after the last successful draw (null = unknown)

        public DigoleDisplay(MTHardwareInterface hardware, byte i2cAddress)
        {
            hw = hardware;
            i2c_address = i2cAddress;
        }

        // device_name: e.g. "MiniTiouner-V2" / "MiniTiouner-PRO2" (MinitiounerSource.HardwareDevice)
        // tuner_label: "TUNER A" / "TUNER B"
        // frequency_kHz / symbol_rate_kS: the currently tuned values (not live-measured, matches
        // MiniTioune's own display, which shows the requested tuning, not a jittering live read)
        // rf_level_dBm: NimStatus.T1P2_input_power_level / T2P1_input_power_level
        // mer_dB: NimStatus.T1P2_mer / T2P1_mer already divided by 10 (see MinitiounerProperties.cs)
        // service_name: decoded TS service name (SDT), truncated to 14 chars - matches the
        // "TSStatus.ServiceName"/"last_service_name_0/1" fields in MinitiounerSource
        // video_codec / modcod_name: e.g. "h264" / "8PSK 3/4" - combined into one "Inf:" line
        // Every redraw clears the screen, so it flickers. MER and the RF level jitter all the time; a small change of them
        // alone is not drawn (the shown value is kept), only a change beyond these limits, of another value, or after
        // the maximum age.
        private const double MerHysteresisDb = 0.3;
        private const short RfHysteresisDbm = 2;
        private static readonly TimeSpan MaxValueAge = TimeSpan.FromSeconds(5);
        private double _drawn_mer = double.NaN;
        private short _drawn_rf;
        private DateTime _drawn_at = DateTime.MinValue;

        public void UpdateStatus(string device_name, string tuner_label, long frequency_kHz, uint symbol_rate_kS, short rf_level_dBm, double mer_dB, string service_name, string video_codec, string modcod_name)
        {
            if (!double.IsNaN(_drawn_mer)
                && Math.Abs(mer_dB - _drawn_mer) < MerHysteresisDb
                && Math.Abs(rf_level_dBm - _drawn_rf) < RfHysteresisDbm
                && DateTime.UtcNow - _drawn_at < MaxValueAge)
            {
                mer_dB = _drawn_mer;
                rf_level_dBm = _drawn_rf;
            }

            double frequency_MHz = frequency_kHz / 1000.0;

            string service_short = service_name ?? "";
            if (service_short.Length > 14)
                service_short = service_short.Substring(0, 14);

            string info = string.Join(" ", new[] { MediaStatus.CodecDisplayName(video_codec), modcod_name }.Where(s => !string.IsNullOrEmpty(s)));

            string line0 = device_name ?? "";
            string line1 = "Serit FTS-4334L";
            string line2 = "    " + tuner_label;
            string line3 = "Frq: " + frequency_MHz.ToString("N3", de) + " MHz";
            string line4 = "SR:  " + symbol_rate_kS.ToString(de) + " KS/s";
            string line5 = "RF:  " + rf_level_dBm.ToString(de) + "dBm";
            string line6 = "MER: " + mer_dB.ToString("F1", de) + "dB";
            string line7 = "Srv: " + service_short;
            string line8 = "Inf: " + info;

            string[] lines = { line0, line1, line2, line3, line4, line5, line6, line7, line8 };

            string rendered = string.Join("\n", lines);

            // Change detection - avoid needless traffic on the shared NIM I2C bus (this is
            // called every ~200ms from NimThread's polling loop, most values don't change
            // that often).
            if (rendered == last_rendered)
                return;

            last_rendered = rendered;
            _drawn_mer = mer_dB;
            _drawn_rf = rf_level_dBm;
            _drawn_at = DateTime.UtcNow;

            var cmd = new System.Collections.Generic.List<byte>();

            // The first draw (and the first after a greeting or a clear) is exactly as in commit 550fa07: CL (which
            // resets the font to 0 - see Digole docs) followed by the lines in the SAME transaction, no font selection
            // and no pauses. Later attempts (SC/SF 10, ETP pixel positions, CL in its own write + pauses) did not change
            // the font size on the real display and only made the screen flicker.
            //
            // After that only the lines that changed are written again, padded with spaces up to the old length so no
            // rest of the old text stays: a new MER value must not clear and redraw the whole screen (flicker).
            bool full = last_lines == null || last_lines.Length != lines.Length;
            if (full)
                cmd.AddRange(Encoding.ASCII.GetBytes("CL")); // clear screen

            for (byte row = 0; row < lines.Length; row++)
            {
                if (!full && lines[row] == last_lines[row])
                    continue;

                string text = lines[row];
                if (!full && last_lines[row].Length > text.Length)
                    text = text.PadRight(last_lines[row].Length);

                cmd.AddRange(Encoding.ASCII.GetBytes("TP"));
                cmd.Add(0);   // column
                cmd.Add(row); // row

                cmd.AddRange(Encoding.ASCII.GetBytes("TT"));
                cmd.AddRange(Encoding.ASCII.GetBytes(text));
                cmd.Add(0); // null terminator, per Digole's "TT" command
            }

            byte err = hw.i2c_write_raw(i2c_address, cmd.ToArray());

            // a failed write leaves the screen in an unknown state: draw everything again next time
            last_lines = err == 0 ? lines : null;
            if (err != 0)
                last_rendered = null;
        }

        // Clears the display and switches its backlight off - call on shutdown so a stale reading isn't left on screen
        // after OpenTuner closes. Bypasses change detection (always sends). Returns the
        // i2c_write_raw error code (0 = ok) so callers can log a shutdown failure instead
        // of it being silently discarded.
        public byte Clear()
        {
            last_rendered = null;
            last_lines = null;
            _drawn_mer = double.NaN;
            // "BL" + brightness byte 0 switches the backlight off too, so it isn't left shining on a blank screen
            // (ShowGreeting turns it back on)
            var cmd = new System.Collections.Generic.List<byte>();
            cmd.AddRange(Encoding.ASCII.GetBytes("CL"));
            AddCmd(cmd, "BL", 0);
            return hw.i2c_write_raw(i2c_address, cmd.ToArray());
        }

        // Panel (BTG-160120D, monochrome graphic LCD) and font metrics in pixels. Width 160 and the
        // char widths of fonts 10 (6) and 18 (9) were measured; the visible height (~96) is derived
        // from the old 9-row live screen; font 120 (callsign) width/height are still GUESSES - tune
        // here after looking at the real display. Text is placed with "ETP" (pixel position);
        // y is the BOTTOM edge (baseline) of the text, not the top - confirmed on the real display
        // (callsign at y=2 showed only ~2 px at the very top). So y = top of text + font height.
        private const int DisplayWidth = 160;
        private const byte FontMedium = 18;
        private const byte FontBig = 120;
        // Character width in pixels of the big font (callsign): only a guess so far, so it can be set (setting
        // DigoleCallsignCharWidth) until the callsign is centered. The width of the medium font was measured.
        public byte BigCharWidth { get; set; } = 24;

        private int CharWidth(byte font) => font == FontMedium ? 9 : BigCharWidth;

        // Greeting screen shown before the first frequency is tuned on either channel, and
        // again on shutdown instead of a blank Clear() - so the display always shows something
        // meaningful rather than sitting blank/stale between sessions. Bypasses change detection
        // (always sends) since it's called at most once per state transition, not every poll tick.
        // Layout: callsign big (font 120, centred), locator and name below it (font 18, centred,
        // each line omitted when empty). START / NO SIGNAL / END therefore look identical now;
        // `phase` ("START", "NO SIGNAL", "END") is still passed by the callers but no longer drawn
        // (a status line was tried at the bottom in font 10 and dropped on request).
        public byte ShowGreeting(string device_name, string callsign, string locator, string name, string phase = "")
        {
            last_rendered = null;
            last_lines = null;
            _drawn_mer = double.NaN;

            var cmd = new System.Collections.Generic.List<byte>();
            cmd.AddRange(Encoding.ASCII.GetBytes("CL"));
            AddCmd(cmd, "BL", 100); // backlight back to full after Clear() switched it off
            AddCmd(cmd, "SC", 1);

            // No callsign configured: fall back to the device name so the screen isn't empty.
            // Bottom edges, stacked from the bottom of the ~96 px display: name 74-92, locator
            // 54-72, callsign ~8-48 (font 120 height ~40 is a guess).
            AddCentered(cmd, FontBig, 48, string.IsNullOrEmpty(callsign) ? device_name : callsign);
            AddCentered(cmd, FontMedium, 72, locator);
            AddCentered(cmd, FontMedium, 92, name);

            return hw.i2c_write_raw(i2c_address, cmd.ToArray());
        }

        // Sets font `font`, then draws `text` horizontally centred with its bottom edge at pixel row y
        // (skipped if empty).
        private void AddCentered(System.Collections.Generic.List<byte> cmd, byte font, byte y, string text)
        {
            text = ToAscii(text);
            if (string.IsNullOrEmpty(text))
                return;

            // Spaces at the start or the end are not drawn (the display ignores a leading space, or the font has none of any
            // width): each one moves the text by half a character width instead - a leading space to the right, a trailing
            // one to the left - so a callsign can be nudged into the middle by hand.
            int leading = text.Length - text.TrimStart(' ').Length;
            int trailing = text.Length - text.TrimEnd(' ').Length;
            text = text.Trim(' ');
            if (text.Length == 0)
                return;

            int shift = (leading - trailing) * CharWidth(font) / 2;
            int x = Math.Min(DisplayWidth - 1, Math.Max(0, (DisplayWidth - text.Length * CharWidth(font)) / 2 + shift));
            AddCmd(cmd, "SF", font);
            AddText(cmd, "ETP", (byte)x, y, text);
        }

        // The Digole gets plain ASCII only: transliterate umlauts, strip other accents, and
        // replace anything else non-printable so no "?" ends up in a name.
        private static string ToAscii(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            s = s.Replace("ä", "ae").Replace("ö", "oe").Replace("ü", "ue")
                 .Replace("Ä", "Ae").Replace("Ö", "Oe").Replace("Ü", "Ue").Replace("ß", "ss");

            var sb = new StringBuilder();
            foreach (char c in s.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                    continue;
                sb.Append(c >= ' ' && c < 127 ? c : '?');
            }
            return sb.ToString();
        }

        private static void AddCmd(System.Collections.Generic.List<byte> cmd, string name, params byte[] args)
        {
            cmd.AddRange(Encoding.ASCII.GetBytes(name));
            cmd.AddRange(args);
        }

        // Position command (TP/ETP) followed by "TT<text>\0".
        private static void AddText(System.Collections.Generic.List<byte> cmd, string pos_cmd, byte x, byte y, string text)
        {
            AddCmd(cmd, pos_cmd, x, y);
            cmd.AddRange(Encoding.ASCII.GetBytes("TT"));
            cmd.AddRange(Encoding.ASCII.GetBytes(text));
            cmd.Add(0);
        }
    }
}
