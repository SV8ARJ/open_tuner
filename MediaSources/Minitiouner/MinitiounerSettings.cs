using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opentuner.MediaSources.Minitiouner
{
    public class MinitiounerSettings
    {
        public int Version = 2;

        public byte DefaultInterface = 0;   // 0 = always ask, 0 = FTDI, 2 = PicoTuner, 3 = Ethernet (Future)

        // With several MiniTiouner boards connected only one is used for now: the chip serial number (as shown in
        // Open Tuner > Hardware Info, without the last A/B letter is enough) of the wanted board, "" = automatic
        // (most TS streams first, so a Pro before a V2). PreferredBoardSerial names the board that comes first (tuner 1).
        public string PreferredBoardSerial = "";

        // With several boards connected: use all of them at once (up to 4 tuners in total), the first one as chosen
        // above. false = only that one board. JSON only, in the general file minitiouner_settings.json.
        public bool UseAllBoards = true;

        // false for a board that cannot switch the LNB voltage (the E-Tiouner has no RT5047 fitted): the LNB-A / LNB-B
        // dropdowns are disabled and the program never touches the LNB supply pins of that board. Per board.
        public bool LnbSupplyControl = true;

        public uint Offset1 = 9750000;
        public uint Offset2 = 9750000;

        public byte DefaultLnbASupply = 0;   // 0 = off, 1 = vert, 2 = horiz
        public byte DefaultLnbBSupply = 0;   // 0 = off, 1 = vert, 2 = horiz
        public bool[] Tone22kHz = new bool[2];   // last used 22kHz tone per tuner (Switches panel: 22K-A/22K-B)

        // Tuning trim per tuner (Frequency tab): derotator capture range in kHz on each side (0 = automatic,
        // 1.5 x symbol rate) and a correction in kHz for the frequency the tuner is really set to
        public uint[] CaptureRangeKHz = new uint[2];

        // Clicking a signal in the BATC spectrum tunes with the rate estimated from its width; without a lock the
        // next smaller standard rate is tried after 10 s (66 -> 33 -> 25 -> 20 kS)
        public bool AutoSrFallback = true;
        // Receiver settings as MiniTioune shows them in its Extra Panel; set to the longmynd values (2 / 6 / false) to go back.
        public byte CarrierPhaseAlgo = 0;   // CARCFG.PH_DET_ALGO of carrier loop 1: 0 costas (MiniTioune), 1 citroen 1, 2 citroen 2 (longmynd)
        public int BasebandGainDb = 8;      // STV6120 BBGAIN in steps of 2 dB, 0..16 (MiniTioune 8, longmynd 6)
        public bool IqSwap = false;         // TNRCFG2.TUN_IQSWAP. On flips the sign of the carrier offset (CFR): Adopt CFR ran the wrong way and the
                                            // low symbol rate carrier window (0 .. +3 x SR) missed the carrier. MiniTioune's "Swap: ON" is not proven to be this bit.
        public bool MiniTiouneInit = false; // write MiniTioune's startup values for timing / carrier loop, FEC (docs/MiniTioune_I2C_Analyse_25kS.md)
        public bool LowSrProfile = true;    // below 50 kS: MiniTioune's demodulator setup (tuner 1.5 x SR lower, SR scan, DVB-S2 only ...)
        public bool LowSrManualSfr = true;  // manual SFR window (+-5 %) in that profile; false = the chip's automatic window (-18 % / +12 %)
        public bool LowSrSrScan = false;    // symbol rate scan around the set rate in that profile (false = fixed rate, as MiniTioune's "fixed"; true only for a transmitter whose rate is not exact)
        public bool LowSrDvbS1 = false;     // keep the DVB-S1 search in that profile
        public bool LowSrClock = true; // lower the demodulator master clock to 30 MHz below 100 kS (JSON only)
        public int[] FreqCorrectionKHz = new int[2];      // old fixed correction in kHz, no longer used (replaced by FreqCorrectionPpm)
        // Tuner reference (crystal) error per tuner in ppm of the tuner frequency; the tuner is set that far off (kHz = ppm * IF / 1e6),
        // like MiniTioune's "ppm calib" (42 for the tested NIM: +31 kHz at 741 MHz, +48 kHz at 1138 MHz)
        public double[] FreqCorrectionPpm = new double[2];
        public double DefaultFreqCorrectionPpm = 0;   // where the Reset button of the Special tab puts the correction (42 for the tested NIM, MiniTioune's ppm calib)

        public byte DefaultRFInput = 0;     // 0 = both tuners fed through A, 1 = Tuner1 is A, Tuner2 is B

        public uint[] DefaultVolume = new uint[] { 50, 50 };
        public bool[] DefaultMuted = new bool[] { true, true };

        // Digole status display wired to JP3 ("I2C-NIM") on the NIM I2C bus.
        public bool EnableDigoleDisplay = false;
        public byte DigoleI2cAddress = 0x27;
        // Shown as a greeting screen until the first frequency is tuned on either channel,
        // and again on shutdown (so the display never sits blank between sessions).
        public string DigoleCallsign = "";
        // Optional extra lines for the Welcome/Final screens (empty = line not shown).
        public string DigoleLocator = "";   // Maidenhead locator, e.g. JN48
        public string DigoleName = "";      // operator name

        // EXTERN-0..7 LED outputs (AUX chip GPIO, MiniTiounerPro V2 only) - persisted so the
        // last state is restored on reconnect.
        public bool[] ExternState = new bool[8];
    }
}
