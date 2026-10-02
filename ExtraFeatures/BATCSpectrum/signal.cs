// From https://github.com/m0dts/QO-100-WB-Live-Tune - Rob Swinbank

using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace opentuner
{
    class signal
    {

        public int beacon_strength = -1;

        //struct for signal information
        public struct Sig
        {
            public int fft_start;
            public int fft_stop;
            public int fft_centre;
            public int fft_strength;
            public double frequency;
            public float sr;
            public string callsign;
            public bool overpower;
            public float dbb;
            public Sig(int _fft_start, int _fft_stop, int _fft_centre, int _fft_strength, double _frequency, float _sr, bool overpower, float _dbb)
            {
                this.fft_start = _fft_start;
                this.fft_stop = _fft_stop;
                this.fft_centre = _fft_centre;
                this.fft_strength = _fft_strength;
                this.frequency = _frequency;
                this.sr = _sr;
                this.callsign = "";
                this.overpower = overpower;
                this.dbb = _dbb;
            }

            public Sig(Sig old, string callsign)
            {
                this.fft_start = old.fft_start;
                this.fft_stop = old.fft_stop;
                this.fft_centre = old.fft_centre;
                this.fft_strength = old.fft_strength;
                this.frequency = old.frequency;
                this.sr = old.sr;
                this.callsign = callsign;
                this.overpower = old.overpower;
                this.dbb = old.dbb;
            }

            public Sig(Sig old, string callsign, float sr)
            {
                this.fft_start = old.fft_start;
                this.fft_stop = old.fft_stop;
                this.fft_centre = old.fft_centre;
                this.fft_strength = old.fft_strength;
                this.frequency = old.frequency;
                this.sr = sr;
                this.callsign = callsign;
                this.overpower = old.overpower;
                this.dbb = old.dbb;
            }


            public void updateCallsign(string callsign)
            {
                this.callsign = callsign;
            }

        }

        public Action<string> debug;


        Object list_lock;
        public List<Sig> signals = new List<Sig>();  //list of signals found: 
        public List<Sig> signalsData = new List<Sig>();
        double start_freq = 10490.5f;

        Random rnd = new Random();

        public signal(object _list_lock)
        {
            list_lock = _list_lock;
            //init nothing!


        }

        public bool diff_signals(Sig lastsig, Sig next)
        {
            // Log.Information(lastsig.frequency-next.frequency);
            float span;
            bool diff = true;
            if (lastsig.sr < 0.070 | next.sr < 0.070)
            {
                span = 0.01f;
            }
            else
            {
                span = 0.075f;
            }
            if (next.frequency > (lastsig.frequency - span) && next.frequency < (lastsig.frequency + span) && next.sr == lastsig.sr)      // +/- span, signal freq varies!
            {
                diff = false;
            }
            return diff;        //returns false if they are the same
        }

        public bool diff_signals(Sig sig, double frequency, float sr)
        {
            //debug(sig.frequency.ToString() + "," + frequency.ToString());
            // Log.Information(lastsig.frequency-next.frequency);
            float span;
            bool diff = true;
            if (sig.sr < 0.070 | sr < 0.070)
            {
                span = 0.01f;
            }
            else
            {
                span = 0.075f;
            }
            if (frequency > (sig.frequency - span) && frequency < (sig.frequency + span))      // +/- span, signal freq varies!
            {
                diff = false;
            }
            return diff;        //returns false if they are the same
        }

        public void updateSignalList()
        {
            // check current signal list
            //foreach (Sig s in signalsData.ToList())
            for (int x = 0; x < signalsData.Count; x++)
            {
                bool found = false;

                foreach (Sig sl in signals)
                {
                    if (diff_signals(signalsData[x], sl) == false) // same sig
                    {
                        signalsData[x] = new Sig(sl, signalsData[x].callsign);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    //debug("Removing a signal");
                    signalsData.Remove(signalsData[x]);
                }
            }

            // check new signal list
            foreach (Sig s in signals)
            {
                bool found = false;

                foreach (Sig sl in signalsData)
                {
                    if (diff_signals(s, sl) == false) // same sig
                    {
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    //debug("Adding a signal");
                    signalsData.Add(s);
                }
            }




        }

        public void updateCurrentSignal(string callsign, double freq, float sr)
        {
            lock (list_lock)
            {
                for (int x = 0; x < signalsData.Count; x++)
                {
                    if (diff_signals(signalsData[x], freq, sr) == false)
                    {
                        //debug("found - updating callsign");
                        signalsData[x] = new Sig(signalsData[x], callsign, sr);

                        break;
                    }
                }
            }
        }

        // ---- AutoTune: choosing the signal for a tuner ----------------------------------------------------------
        // The searches work on signalsData (kept over the frames, with the callsigns) and skip the signals the other
        // tuners are on. Frequency and symbol rate in MHz, like Sig.

        private const double BeaconLimitMHz = 10492.0;   // below this frequency is the beacon

        public struct TunedSignal
        {
            public int tuner;
            public double frequency;
            public float sr;

            public TunedSignal(int tuner, double frequency, float sr)
            {
                this.tuner = tuner;
                this.frequency = frequency;
                this.sr = sr;
            }
        }

        public static bool IsBeacon(double frequency)
        {
            return frequency < BeaconLimitMHz;
        }

        // a signal AutoTune may tune: not the beacon if that is to be avoided, strong enough (no dBb yet = no beacon seen,
        // accepted) and not one of the signals the other tuners are on
        private bool IsCandidate(Sig s, List<TunedSignal> in_use, bool avoid_beacon, float threshold)
        {
            if (avoid_beacon && IsBeacon(s.frequency))
                return false;

            if (!float.IsNaN(s.dbb) && s.dbb < threshold)
                return false;

            foreach (TunedSignal t in in_use)
            {
                if (!diff_signals(s, t.frequency, t.sr))
                    return false;
            }

            return true;
        }

        // the tuned signal is gone from the spectrum (or it is the beacon and that is to be avoided)
        public bool SignalLost(double frequency, float sr, bool avoid_beacon)
        {
            if (avoid_beacon && IsBeacon(frequency))
                return true;

            lock (list_lock)
            {
                foreach (Sig s in signalsData)
                {
                    if (!diff_signals(s, frequency, sr))
                        return false;
                }
            }

            return true;
        }

        // the signal at this frequency again (the symbol rate may have been measured differently), not on another tuner
        public Sig FindSameSignal(double frequency, float sr, List<TunedSignal> in_use, bool avoid_beacon, float threshold)
        {
            lock (list_lock)
            {
                foreach (Sig s in signalsData)
                {
                    // the tolerance is that of the wider of the two rates: a signal that was tuned as 33 kS and is
                    // measured as 125 kS now is still the same
                    double tolerance = Math.Max(s.sr, sr) < 0.070f ? 0.01 : 0.075;

                    if (Math.Abs(s.frequency - frequency) < tolerance && IsCandidate(s, in_use, avoid_beacon, threshold))
                        return s;
                }
            }

            return new Sig();
        }

        // the strongest free signal (start of AutoTune)
        public Sig FindStrongestSignal(List<TunedSignal> in_use, bool avoid_beacon, float threshold)
        {
            Sig best = new Sig();

            lock (list_lock)
            {
                foreach (Sig s in signalsData)
                {
                    if (!IsCandidate(s, in_use, avoid_beacon, threshold))
                        continue;

                    if (best.frequency == 0 || s.fft_strength > best.fft_strength)
                        best = s;
                }
            }

            return best;
        }

        // the free signal nearest to the frequency, only one without callsign if only_new; never the signal at that frequency
        public Sig FindNearestSignal(double frequency, float sr, List<TunedSignal> in_use, bool avoid_beacon, float threshold, bool only_new)
        {
            Sig best = new Sig();

            lock (list_lock)
            {
                foreach (Sig s in signalsData)
                {
                    if (!IsCandidate(s, in_use, avoid_beacon, threshold))
                        continue;

                    if (frequency > 0 && !diff_signals(s, frequency, sr))
                        continue;

                    if (only_new && !string.IsNullOrEmpty(s.callsign))
                        continue;

                    if (best.frequency == 0 || Math.Abs(s.frequency - frequency) < Math.Abs(best.frequency - frequency))
                        best = s;
                }
            }

            return best;
        }

        // the next free signal above the frequency, from the bottom again after the last one (Auto Timed)
        public Sig FindNextTimedSignal(double frequency, float sr, List<TunedSignal> in_use, bool avoid_beacon, float threshold)
        {
            Sig next = new Sig();
            Sig lowest = new Sig();

            lock (list_lock)
            {
                foreach (Sig s in signalsData)
                {
                    if (!IsCandidate(s, in_use, avoid_beacon, threshold))
                        continue;

                    if (frequency > 0 && !diff_signals(s, frequency, sr))
                        continue;   // the signal it is on

                    if (lowest.frequency == 0 || s.frequency < lowest.frequency)
                        lowest = s;

                    if (s.frequency > frequency && (next.frequency == 0 || s.frequency < next.frequency))
                        next = s;
                }
            }

            return next.frequency > 0 ? next : lowest;
        }

        // A signal is overpowering when it is stronger than the beacon minus this many FFT units (0.75 * 3276.8 of 65536,
        // about -0.71 dBb, the original value, matches the QO-100 wideband web spectrum). Fixed by agreement. The
        // exception for signals up to 333 kS (8PSK and higher) is covered by the 0.4 MHz minimum width in isOverPower;
        // the modulation is not known here.
        public const float OverpowerLimitUnits = -0.75f * 3276.8f;

        private const float DbbUnitsPerDb = 65536.0f * 0.052778f;     // see CalcDbb

        // FFT value above which a signal is overpowering (see isOverPower), 0 without beacon
        public int OverpowerLimit
        {
            get { return beacon_strength > 0 ? beacon_strength + Convert.ToInt32(OverpowerLimitUnits) : 0; }
        }

        // Signal strength in dB relative to the beacon (dBb), NaN as long as no beacon has been seen.
        // Formula by DL1RF, the QO-100 wideband web site's calculation without its canvas height:
        // a 15 dB window spans ((7/8) - (1/12)) of the height, i.e. 0.052778 per dB at height 1.
        public float CalcDbb(int strength_signal)
        {
            if (beacon_strength <= 0)
                return float.NaN;

            return (strength_signal - beacon_strength) / 65536.0f / 0.052778f;
        }

        public bool isOverPower(int beacon_strength, int signal_strength, float signal_bw)
        {
            if (beacon_strength != 0)
            {
                if (signal_bw < 0.4)
                {
                    return false;
                }

                if (signal_strength > beacon_strength + OverpowerLimitUnits)
                {
                    return true;
                }
            }
            else
            {
                Log.Information("Beacon Strength = 0");
            }


            return false;
        }

        public List<Sig> detect_signals(UInt16[] fft_data)
        {
            lock (list_lock)
            {
                signals.Clear();
                int i;
                int j;

                int noise_level = 11000;
                int signal_threshold = 16000;

                Boolean in_signal = false;
                int start_signal = 0;
                int end_signal;
                float mid_signal;
                int strength_signal;
                float signal_bw;
                double signal_freq;
                int acc;
                int acc_i;

                for (i = 2; i < fft_data.Length; i++)
                {
                    if (!in_signal)
                    {
                        if ((fft_data[i] + fft_data[i - 1] + fft_data[i - 2]) / 3.0 > signal_threshold)
                        {
                            in_signal = true;
                            start_signal = i;

                        }
                    }
                    else /* in_signal == true */
                    {
                        if ((fft_data[i] + fft_data[i - 1] + fft_data[i - 2]) / 3.0 < signal_threshold)
                        {
                            in_signal = false;

                            end_signal = i;

                            acc = 0;
                            acc_i = 0;
                            // Log.Information(Convert.ToInt16(start_signal + (0.3 * (end_signal - start_signal))));
                            //  Log.Information( start_signal + (0.7 * (end_signal - start_signal)));

                            for (j = Convert.ToInt16(start_signal + (0.3 * (end_signal - start_signal))) | 0; j < start_signal + (0.8 * (end_signal - start_signal)); j++)
                            {
                                acc = acc + fft_data[j];
                                acc_i = acc_i + 1;
                            }


                            strength_signal = acc / acc_i;

                            /* Find real start of top of signal */
                            for (j = start_signal; (fft_data[j] - noise_level) < 0.75 * (strength_signal - noise_level); j++)
                            {
                                start_signal = j;
                            }


                            /* Find real end of the top of signal */
                            for (j = end_signal; (fft_data[j] - noise_level) < 0.75 * (strength_signal - noise_level); j--)
                            {
                                end_signal = j;
                            }
                            //  Log.Information("Start:" + start_signal.ToString());
                            //  Log.Information("End:" + end_signal.ToString());
                            // Log.Information("Strength:" + strength_signal.ToString());
                            mid_signal = Convert.ToSingle(start_signal + ((end_signal - start_signal) / 2.0));

                            signal_freq = Convert.ToDouble(start_freq + (((mid_signal + 1) / (fft_data.Length)) * 9.0));

                            // narrow signals: half-power width in linear power, averaged over the frames;
                            // everything wider keeps the old measure (plenty of bins there)
                            float narrow_sr = NarrowSymbolrate(signal_freq, MeasureFwhmMHz(fft_data, start_signal - 5, end_signal + 5));
                            signal_bw = narrow_sr >= 0f
                                ? narrow_sr
                                : align_symbolrate(Convert.ToSingle((end_signal - start_signal) * (9.0 / (fft_data.Length))));


                            // Exclude signals in beacon band
                            if (signal_bw >= 0.019) // 20 kS is the smallest rate (0.020f is slightly below the double 0.020)
                            {
                                if (signal_freq < 10492000 && signal_bw > 1.0)
                                {
                                    beacon_strength = strength_signal;
                                    signals.Add(new Sig(start_signal, end_signal, Convert.ToInt32(mid_signal), strength_signal / 255, signal_freq, signal_bw, false, float.NaN));
                                }
                                else
                                {
                                    bool overpower = false;

                                    if (isOverPower(beacon_strength, strength_signal, signal_bw))
                                        overpower = true;

                                    signals.Add(new Sig(start_signal, end_signal, Convert.ToInt32(mid_signal), strength_signal / 255, signal_freq, signal_bw, overpower, CalcDbb(strength_signal)));
                                }
                            }


                        }
                    }
                }
                AgeWidthTracks();
                updateSignalList();

            }
            return signals;
        }

        // ---- Narrow signals (20 .. ~125 kS) ---------------------------------------------------------------
        // The FFT bins are 9.76 kHz wide, so a narrow signal is only a handful of bins. The old measure (75 % of the
        // peak of the log data, whole bins) gave e.g. 49 kHz for a 33 kS signal and one bucket "35" for everything
        // between 22 and 65 kHz. The spectrum values are 1/4096 dB (65535 = 16 dB), so they are converted back to
        // linear power, the noise is subtracted and the half-power width (FWHM) is measured with sub-bin
        // interpolation. That width does not depend on the level (measured on the live QO-100 downlink at
        // 8.5 .. 15 dB) and follows FWHM = sqrt(SR^2 + 10.7^2) kHz: 20 kS -> 23.1, 25 kS -> 27.1, 33 kS -> 34.7,
        // 66 kS -> 67.8 kHz measured. The width is averaged over the last frames of each signal.
        private const double FftUnitsPerDb = 4096.0;
        private const double TrackMatchMHz = 0.015;      // same signal if the frequency differs by less
        private const double WidthSmoothing = 0.25;      // exponential average over the frames
        private const double ClassHysteresisMHz = 0.0006; // a class only changes when the width is this far over the border
        private const int TrackMaxMissed = 40;           // frames a signal may be missing before its average is dropped

        private class WidthTrack
        {
            public double freq;
            public double width;
            public float cls;
            public bool seen;
            public int missed;
        }

        private readonly List<WidthTrack> width_tracks = new List<WidthTrack>();

        private static double FftToLinear(double fft_value)
        {
            return Math.Pow(10.0, fft_value / FftUnitsPerDb / 10.0);
        }

        // Half-power width in MHz of the signal in bins [first, last] (padded by the caller), 0 if it cannot be
        // measured. The noise floor is the median of the bins on both sides of the signal.
        private static double MeasureFwhmMHz(UInt16[] fft, int first, int last)
        {
            first = Math.Max(1, first);
            last = Math.Min(fft.Length - 2, last);
            if (last - first < 2)
                return 0;

            var noise_samples = new List<double>();
            for (int i = Math.Max(0, first - 45); i <= first - 8; i++)
                noise_samples.Add(fft[i]);
            for (int i = last + 8; i <= Math.Min(fft.Length - 1, last + 45); i++)
                noise_samples.Add(fft[i]);

            double noise_units = 11500; // typical floor if there is nothing to measure it on
            if (noise_samples.Count >= 6)
            {
                noise_samples.Sort();
                noise_units = noise_samples[noise_samples.Count / 2];
            }

            double noise_linear = FftToLinear(noise_units);
            int count = last - first + 1;
            var power = new double[count];
            int peak = 0;
            for (int i = 0; i < count; i++)
            {
                power[i] = Math.Max(0, FftToLinear(fft[first + i]) - noise_linear);
                if (power[i] > power[peak])
                    peak = i;
            }

            if (power[peak] <= 0)
                return 0;

            double level = 0.5 * power[peak];

            int left = peak;
            while (left > 0 && power[left] >= level)
                left--;
            int right = peak;
            while (right < count - 1 && power[right] >= level)
                right++;

            // the window must be wide enough that the signal falls below half power on both sides
            if (power[left] >= level || power[right] >= level)
                return 0;

            double left_edge = left + (level - power[left]) / (power[left + 1] - power[left]);
            double right_edge = right - (level - power[right]) / (power[right - 1] - power[right]);

            return (right_edge - left_edge) * (9.0 / fft.Length);
        }

        // Symbol rate in MHz for a half-power width in MHz: 0 = too narrow to be a signal, -1 = not a narrow signal
        // (use the old measure). The borders are half way between the expected widths sqrt(SR^2 + 10.7^2).
        private static float ClassifyNarrow(double fwhm_mhz)
        {
            if (fwhm_mhz < 0.0170) return 0f;
            if (fwhm_mhz < 0.0250) return 0.020f;
            if (fwhm_mhz < 0.0309) return 0.025f;
            if (fwhm_mhz < 0.0508) return 0.033f;
            if (fwhm_mhz < 0.0966) return 0.066f;
            if (fwhm_mhz < 0.1870) return 0.125f;
            return -1f;
        }

        // Smoothed narrow symbol rate of the signal at signal_freq (MHz), -1 if it is not narrow.
        private float NarrowSymbolrate(double signal_freq, double fwhm_mhz)
        {
            // not measurable (0) or wide: the caller uses the old measure
            if (fwhm_mhz <= 0 || ClassifyNarrow(fwhm_mhz) < 0)
                return -1f;

            WidthTrack track = null;
            double best = TrackMatchMHz;
            foreach (var candidate in width_tracks)
            {
                double distance = Math.Abs(candidate.freq - signal_freq);
                if (distance < best)
                {
                    best = distance;
                    track = candidate;
                }
            }

            if (track == null)
            {
                track = new WidthTrack { freq = signal_freq, width = fwhm_mhz, cls = ClassifyNarrow(fwhm_mhz) };
                width_tracks.Add(track);
            }
            else
            {
                track.freq = signal_freq;
                track.width = track.width * (1.0 - WidthSmoothing) + fwhm_mhz * WidthSmoothing;

                // hysteresis: keep the class while the width is still inside its band +- margin
                float low = ClassifyNarrow(track.width - ClassHysteresisMHz);
                float high = ClassifyNarrow(track.width + ClassHysteresisMHz);
                if (track.cls != low && track.cls != high)
                    track.cls = ClassifyNarrow(track.width);
            }

            track.seen = true;
            return track.cls;
        }

        // Called once per frame after all signals were measured: forget signals that are gone.
        private void AgeWidthTracks()
        {
            for (int i = width_tracks.Count - 1; i >= 0; i--)
            {
                WidthTrack track = width_tracks[i];

                if (track.seen)
                {
                    track.missed = 0;
                }
                else if (++track.missed > TrackMaxMissed)
                {
                    width_tracks.RemoveAt(i);
                    continue;
                }

                track.seen = false;
            }
        }

        public float align_symbolrate(float width)
        {
            if (width < 0.022)
            {
                return 0;
            }
            else if (width < 0.065)
            {
                return 0.035f;
            }
            else if (width < 0.086)
            {
                return 0.066f;
            }
            else if (width < 0.195)
            {
                return 0.125f;
            }
            else if (width < 0.277)
            {
                return 0.250f;
            }
            else if (width < 0.388)
            {
                return 0.333f;
            }
            else if (width < 0.700)
            {
                return 0.500f;
            }
            else if (width < 1.2)
            {
                return 1.000f;
            }
            else if (width < 1.6)
            {
                return 1.500f;
            }
            else if (width < 2.2)
            {
                return 2.000f;
            }
            else
            {
                return Convert.ToSingle(Math.Round(width * 5) / 5.0);
            }
        }
    }
}
