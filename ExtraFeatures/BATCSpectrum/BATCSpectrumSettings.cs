namespace opentuner.ExtraFeatures.BATCSpectrum
{
    // Settings of the BATC spectrum, stored in settings\spectrumSettings.json. AutoTune design and the settings window
    // after DL1RF's version (upstream tomvdb/open_tuner#105).
    public class BATCSpectrumSettings
    {
        public const int ModeManual = 0;
        public const int ModeAutoHold = 1;      // stay on the signal, go to the nearest one after it was lost for the hold time
        public const int ModeAutoNextNew = 2;   // like hold, and after the timed interval to the nearest signal without callsign
        public const int ModeAutoTimed = 3;     // to the next signal (ascending frequency) after the timed interval

        // per tuner (RX 1 to 4)
        public int[] tuneMode = { ModeManual, ModeManual, ModeManual, ModeManual };
        public bool[] avoidBeacon = { true, true, true, true };

        // only signals at least this strong relative to the beacon (dBb) are tuned by AutoTune
        public float threshold = -6.0f;

        public int autoHoldTimeValue = 5;       // seconds
        public int autoTuneTimeValue = 30;      // seconds

        // 0 classic, 1 classic + line, 2 box from top to line, 3 box from line to bottom, 4 line
        public int overPowerIndicatorLayout = 0;
    }
}
