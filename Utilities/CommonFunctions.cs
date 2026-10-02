using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace opentuner.Utilities
{
    public static class CommonFunctions
    {
        // .NET (Core) needs UseShellExecute = true to open a URL in the default browser
        public static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Unable to open URL {Url}", url);
            }
        }

        public static string GenerateTimestampFilename()
        {
            // Was "yyyy-dd-M--HH-mm-ss" (day and month swapped, e.g. "2026-28-9" for Sept 28) - fixed.
            return DateTime.Now.ToString("yyyy-MM-dd_HH.mm.ss");
        }

        // e.g. "2026-09-28_20.51.57_A71A-1500KS" - station is sanitized (invalid filename
        // characters stripped) and dropped entirely (no dangling "_") if empty/unknown.
        public static string GenerateTimestampFilename(string station, uint symbol_rate_ks)
        {
            string suffix = StationSuffix(station, symbol_rate_ks);
            return string.IsNullOrEmpty(suffix) ? GenerateTimestampFilename() : GenerateTimestampFilename() + "_" + suffix;
        }

        // The "<Station>-<SR>KS" part on its own, e.g. "A71A-1500KS" - for callers (like TSRecorder)
        // that build the filename themselves. "" if the station is empty/unknown (not locked yet).
        public static string StationSuffix(string station, uint symbol_rate_ks)
        {
            if (string.IsNullOrWhiteSpace(station))
                return "";

            char[] invalid = System.IO.Path.GetInvalidFileNameChars();
            string safe_station = new string(station.Trim().Where(c => !invalid.Contains(c)).ToArray());

            return string.IsNullOrEmpty(safe_station) ? "" : safe_station + "-" + symbol_rate_ks + "KS";
        }

        // IPv4 multicast range 224.0.0.0 - 239.255.255.255
        public static bool IsMulticast(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
                return false;

            byte first = address.GetAddressBytes()[0];
            return first >= 224 && first <= 239;
        }

        public static List<string> determineIP()
        {
            List<string> detected_ips = new List<string>();

            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    detected_ips.Add(ip.ToString());
                }
            }

            return detected_ips;
        }
        

    }
}
