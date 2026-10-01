using System;
using System.Collections.Generic;
using System.Text;

namespace opentuner
{
    // Plain-text content of the PSI / SI tables for the hover info of the TS Info legend (issue #63). The methods get a
    // section that lies completely in one packet: start = table_id, end = behind the last byte before the CRC. Every read
    // is checked against end, a damaged section gives a shorter text and never an exception.
    public static class TSTableDecoder
    {
        // "program 1 -> PMT 256" per program of the PAT
        public static string Pat(byte[] p, int start, int end)
        {
            var programs = new List<string>();

            for (int i = start + 8; i + 4 <= end; i += 4)
            {
                int program = (p[i] << 8) | p[i + 1];
                int pid = ((p[i + 2] & 0x1F) << 8) | p[i + 3];

                programs.Add(program == 0 ? "network PID " + pid : "program " + program + " -> PMT " + pid);
            }

            return string.Join(", ", programs);
        }

        // "program 1, PCR 259, 2 streams"
        public static string Pmt(byte[] p, int start, int end, out int program, out int pcr_pid, out int streams)
        {
            program = pcr_pid = streams = 0;

            if (start + 12 > end)
                return "";

            program = (p[start + 3] << 8) | p[start + 4];
            pcr_pid = ((p[start + 8] & 0x1F) << 8) | p[start + 9];

            int program_info_length = ((p[start + 10] & 0x0F) << 8) | p[start + 11];
            int i = start + 12 + program_info_length;

            while (i + 5 <= end)
            {
                streams++;
                i += 5 + (((p[i + 3] & 0x0F) << 8) | p[i + 4]);
            }

            return "program " + program + ", PCR " + pcr_pid + ", " + streams + (streams == 1 ? " stream" : " streams");
        }

        // language of an elementary stream from its ISO 639 descriptor (0x0A), "" if there is none
        public static string EsLanguage(byte[] p, int descriptors, int end)
        {
            for (int d = descriptors; d + 2 <= end; d += 2 + p[d + 1])
            {
                if (p[d] == 0x0A && p[d + 1] >= 3 && d + 5 <= end)
                    return Encoding.ASCII.GetString(p, d + 2, 3);
            }

            return "";
        }

        // "A71A, QARS, digital TV, running, free" for every service of the SDT
        public static string Sdt(byte[] p, int start, int end)
        {
            var services = new List<string>();
            int i = start + 11;

            while (i + 5 <= end)
            {
                int running = (p[i + 3] >> 5) & 0x07;
                bool scrambled = ((p[i + 3] >> 4) & 0x01) != 0;
                int loop_end = Math.Min(i + 5 + (((p[i + 3] & 0x0F) << 8) | p[i + 4]), end);

                string name = "", provider = "", type = "";
                for (int d = i + 5; d + 2 <= loop_end; d += 2 + p[d + 1])
                {
                    if (p[d] == 0x48 && d + 5 <= loop_end)   // service descriptor
                    {
                        type = ServiceType(p[d + 2]);
                        int provider_length = p[d + 3];
                        if (d + 4 + provider_length >= loop_end)
                            break;

                        provider = Text(p, d + 4, provider_length, loop_end);
                        int name_length = p[d + 4 + provider_length];
                        name = Text(p, d + 5 + provider_length, name_length, loop_end);
                    }
                }

                var parts = new List<string>();
                if (name != "") parts.Add(name);
                if (provider != "") parts.Add(provider);
                if (type != "") parts.Add(type);
                parts.Add(running == 4 ? "running" : running == 1 ? "not running" : running == 2 ? "starts soon" : running == 3 ? "pausing" : running == 5 ? "off air" : "status " + running);
                parts.Add(scrambled ? "scrambled" : "free");
                services.Add(string.Join(", ", parts));

                i = loop_end;
            }

            return string.Join(" | ", services);
        }

        private static string ServiceType(byte type)
        {
            switch (type)
            {
                case 0x01: return "digital TV";
                case 0x02: return "digital radio";
                case 0x03: return "teletext";
                case 0x0C: return "data";
                case 0x11: return "MPEG-2 HD TV";
                case 0x16: return "advanced codec SD TV";
                case 0x19: return "advanced codec HD TV";
                case 0x1F: return "UHD TV";
                default: return "service type 0x" + type.ToString("X2");
            }
        }

        // network name and the satellite delivery system of the first transport stream:
        // "QARS; 10491.525 MHz, V, 1500 kS, DVB-S2 QPSK 4/5, 25.5 E"
        public static string Nit(byte[] p, int start, int end)
        {
            if (start + 12 > end)
                return "";

            var parts = new List<string>();

            int network_length = ((p[start + 8] & 0x0F) << 8) | p[start + 9];
            int i = start + 10;
            int network_end = Math.Min(i + network_length, end);

            for (int d = i; d + 2 <= network_end; d += 2 + p[d + 1])
            {
                if (p[d] == 0x40)   // network name
                    parts.Add(Text(p, d + 2, p[d + 1], network_end));
            }

            i = network_end;
            if (i + 2 <= end)
            {
                int loop_end = Math.Min(i + 2 + (((p[i] & 0x0F) << 8) | p[i + 1]), end);
                i += 2;

                while (i + 6 <= loop_end)
                {
                    int desc_end = Math.Min(i + 6 + (((p[i + 4] & 0x0F) << 8) | p[i + 5]), loop_end);

                    for (int d = i + 6; d + 2 <= desc_end; d += 2 + p[d + 1])
                    {
                        if (p[d] == 0x43 && p[d + 1] >= 11 && d + 13 <= desc_end)
                        {
                            parts.Add(SatelliteDelivery(p, d + 2));
                            return string.Join("; ", parts);   // the first one is enough
                        }
                    }

                    i = desc_end;
                }
            }

            return string.Join("; ", parts);
        }

        // satellite_delivery_system_descriptor (EN 300 468), p[d] = first byte behind tag and length
        private static string SatelliteDelivery(byte[] p, int d)
        {
            double frequency_mhz = Bcd(p, d, 4) / 100.0;                 // 8 digits: GHz with 5 decimals
            double orbital = Bcd(p, d + 4, 2) / 10.0;                    // 4 digits: degrees with 1 decimal
            bool east = (p[d + 6] & 0x80) != 0;
            string polarization = new[] { "H", "V", "L", "R" }[(p[d + 6] >> 5) & 0x03];
            bool dvbs2 = (p[d + 6] & 0x04) != 0;
            int modulation = p[d + 6] & 0x03;
            long symbol_digits = Bcd(p, d + 7, 3) * 10 + (p[d + 10] >> 4);   // 7 digits (28 bit): Msymbol/s with 4 decimals
            double symbol_rate_ks = symbol_digits / 10.0;
            int fec = p[d + 10] & 0x0F;

            string[] fecs = { "", "1/2", "2/3", "3/4", "5/6", "7/8", "8/9", "3/5", "4/5", "9/10" };
            string modulation_text = modulation == 1 ? "QPSK" : modulation == 2 ? "8PSK" : modulation == 3 ? "16QAM" : "";

            return frequency_mhz.ToString("0.###") + " MHz, " + polarization + ", " + symbol_rate_ks.ToString("0.#") + " kS, " +
                   (dvbs2 ? "DVB-S2" : "DVB-S") + (modulation_text == "" ? "" : " " + modulation_text) +
                   (fec >= 1 && fec < fecs.Length ? " " + fecs[fec] : "") + ", " + orbital.ToString("0.#") + (east ? " E" : " W");
        }

        // event name of the first event of a present / following EIT section (section_number 0 = now, 1 = next)
        public static string EitEvent(byte[] p, int start, int end, out int section_number)
        {
            section_number = start + 7 <= end ? p[start + 6] : -1;

            int i = start + 14;
            if (i + 12 > end)
                return "";

            int desc_end = Math.Min(i + 12 + (((p[i + 10] & 0x0F) << 8) | p[i + 11]), end);

            for (int d = i + 12; d + 2 <= desc_end; d += 2 + p[d + 1])
            {
                if (p[d] == 0x4D && d + 6 <= desc_end)   // short event descriptor: language, name, text
                    return Text(p, d + 6, p[d + 5], desc_end);
            }

            return "";
        }

        // TDT / TOT: "2026-10-01 12:34:56 UTC"
        public static string Time(byte[] p, int start, int end)
        {
            if (start + 8 > TSParserThread.TS_PACKET_SIZE)   // a TDT has no CRC, so end says nothing here: the five time bytes are needed
                return "";

            int mjd = (p[start + 3] << 8) | p[start + 4];
            int y = (int)((mjd - 15078.2) / 365.25);
            int m = (int)((mjd - 14956.1 - (int)(y * 365.25)) / 30.6001);
            int day = mjd - 14956 - (int)(y * 365.25) - (int)(m * 30.6001);
            int k = (m == 14 || m == 15) ? 1 : 0;
            y += k;
            m = m - 1 - k * 12;

            return (1900 + y).ToString("0000") + "-" + m.ToString("00") + "-" + day.ToString("00") + " " +
                   BcdByte(p[start + 5]).ToString("00") + ":" + BcdByte(p[start + 6]).ToString("00") + ":" + BcdByte(p[start + 7]).ToString("00") + " UTC";
        }

        private static long Bcd(byte[] p, int offset, int bytes)
        {
            long value = 0;
            for (int i = 0; i < bytes; i++)
                value = value * 100 + BcdByte(p[offset + i]);
            return value;
        }

        private static int BcdByte(byte b)
        {
            return (b >> 4) * 10 + (b & 0x0F);
        }

        // DVB text: a first byte below 0x20 selects a character table (not decoded, Latin-1 is used)
        private static string Text(byte[] p, int offset, int length, int limit)
        {
            if (length <= 0 || offset + length > limit)
                return "";

            if (p[offset] < 0x20)
            {
                int skip = p[offset] == 0x10 ? 3 : 1;
                offset += skip;
                length -= skip;
                if (length <= 0)
                    return "";
            }

            return Encoding.Latin1.GetString(p, offset, length).Trim('\0', ' ');
        }
    }
}
