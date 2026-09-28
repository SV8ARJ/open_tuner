using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using opentuner.MediaPlayers;
using opentuner.Utilities;

namespace opentuner.MediaSources
{
    public abstract class OTSource
    {
        public delegate void VideoChangeCallback(int video_number, bool start);
        public delegate void SourceDataChange(int video_nr, OTSourceData properties, string description);
        public abstract event SourceDataChange OnSourceData;


        // Request the Source Name (eg. Minitiouner)
        public abstract string GetName();

        // Request Device Name (eg. FTDI, Picotuner, etc)
        public abstract string GetDeviceName();

        // Request Source Description (can also include some info regarding its current settings)
        public abstract string GetDescription();

        public abstract string GetMoreInfoLink();

        // Shows a Source specific setting screen. Called when user clicks "Settings" in source selection screen.
        public abstract void ShowSettings();

        // Optional extra tabs next to "Properties" (tab title, panel), e.g. "Expert" and "Frequency".
        // The default is none.
        public virtual List<KeyValuePair<string, Control>> GetExtraTabs() { return new List<KeyValuePair<string, Control>>(); }

        public abstract void SetFrequency(int device, uint frequency, uint symbol_rate, bool offset_included);

        // A signal clicked in the BATC spectrum (frequency incl. LNB offset, symbol rate estimated from its width).
        // Sources can retry with other rates if that estimate does not lock; the default just tunes.
        public virtual void SetFrequencyFromSpectrum(int device, uint frequency, uint symbol_rate)
        {
            SetFrequency(device, frequency, symbol_rate, true);
        }
        public abstract long GetFrequency(int device, bool offset_included);

        public abstract int GetVolume(int device);
        public abstract void UpdateVolume(int device, int volume_delta);
        public abstract void ToggleMute(int device);

        public abstract Dictionary<string, string> GetSignalData(int device);

        public abstract void StartStreaming(int device);
        public abstract void StopStreaming(int device);

        // Stops that tuner's demodulator (issue #9 "Stop TS") - a new signal has to be tuned in
        // afterwards, like at startup. Only MiniTiouner/PicoTuner implement this so far; a no-op
        // default lets MainForm call it on every source without a type check.
        public virtual void StopTuner(int device) { }
        public abstract int GetVideoSourceCount();
        public abstract CircularBuffer GetVideoDataQueue(int device);
        public abstract void RegisterTSConsumer(int device, CircularBuffer ts_buffer_queue);

        public abstract void Close();

        // initialize returns how many video players it need
        public abstract int Initialize(VideoChangeCallback VideoChangeCB, Control Parent);

        public abstract void ConfigureVideoPlayers(List<OTMediaPlayer> MediaPlayers);

        public abstract void ConfigureTSRecorders(List<TSRecorder> TSRecorders);

        public abstract void ConfigureTSStreamers(List<TSUdpStreamer> TSStreamers);

        public abstract void ConfigureMediaPath(string MediaPath);
        public abstract bool DeviceConnected { get; }

        //public abstract byte SelectHardwareInterface(int hardware_interface);

        public abstract void OverrideDefaultMuted(bool Override);

        public abstract void UpdateFrequencyPresets(List<StoredFrequency> FrequencyPresets);

    }
}
