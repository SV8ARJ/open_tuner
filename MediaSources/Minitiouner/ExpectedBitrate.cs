using System;
using System.Collections.Generic;

namespace opentuner.MediaSources.Minitiouner
{
    // The TS bit rate a signal can carry at most ("Bitrate expected" of MiniTioune), from symbol rate, modulation, code
    // rate, frame size and pilots. The sync bytes of the TS packets are part of it. Returns NaN if it can't be told.
    public static class ExpectedBitrate
    {
        // BCH-coded block Kbch (EN 302 307 table 5a / 5b) per code rate; 9/10 does not exist for short frames
        private static readonly Dictionary<string, int> KbchNormal = new Dictionary<string, int>
        {
            { "1/4", 16008 }, { "1/3", 21408 }, { "2/5", 25728 }, { "1/2", 32208 }, { "3/5", 38688 }, { "2/3", 43040 },
            { "3/4", 48408 }, { "4/5", 51648 }, { "5/6", 53840 }, { "8/9", 57472 }, { "9/10", 58192 },
        };

        private static readonly Dictionary<string, int> KbchShort = new Dictionary<string, int>
        {
            { "1/4", 3072 }, { "1/3", 5232 }, { "2/5", 6312 }, { "1/2", 7032 }, { "3/5", 9552 }, { "2/3", 10632 },
            { "3/4", 11712 }, { "4/5", 12432 }, { "5/6", 13152 }, { "8/9", 14232 },
        };

        private const int BchHeaderBits = 80;   // the BBHEADER in front of the data field
        private const int SlotSymbols = 90;

        // symbol_rate_ks in kS/s, modcod_name as in lookups.modcod_lookup_dvbs2 / _dvbs ("QPSK 4/5")
        public static double Get(bool dvb_s2, string modcod_name, uint symbol_rate_ks, bool short_frame, bool pilots)
        {
            if (string.IsNullOrEmpty(modcod_name) || symbol_rate_ks == 0)
                return double.NaN;

            string[] parts = modcod_name.Split(' ');
            if (parts.Length != 2)
                return double.NaN;

            string modulation = parts[0];
            string rate = parts[1];
            double symbols_per_second = symbol_rate_ks * 1000.0;

            if (!dvb_s2)
            {
                // QPSK, convolutional code rate and Reed-Solomon (204, 188)
                string[] fraction = rate.Split('/');
                if (fraction.Length != 2 || !double.TryParse(fraction[0], out double num) || !double.TryParse(fraction[1], out double den) || den == 0)
                    return double.NaN;

                return symbols_per_second * 2 * (num / den) * 188.0 / 204.0;
            }

            int bits_per_symbol;
            switch (modulation)
            {
                case "QPSK": bits_per_symbol = 2; break;
                case "8PSK": bits_per_symbol = 3; break;
                case "16APSK": bits_per_symbol = 4; break;
                case "32APSK": bits_per_symbol = 5; break;
                default: return double.NaN;
            }

            if (!(short_frame ? KbchShort : KbchNormal).TryGetValue(rate, out int kbch))
                return double.NaN;

            int fec_frame_bits = short_frame ? 16200 : 64800;
            int slots = fec_frame_bits / bits_per_symbol / SlotSymbols;
            int frame_symbols = (slots + 1) * SlotSymbols;   // + the PL header (one slot)
            if (pilots)
                frame_symbols += (slots - 1) / 16 * 36;      // 36 pilot symbols after every 16 slots

            return symbols_per_second * (kbch - BchHeaderBits) / frame_symbols;
        }
    }
}
