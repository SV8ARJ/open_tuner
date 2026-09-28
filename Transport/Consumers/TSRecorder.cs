using opentuner.MediaSources;
using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocketSharp;

namespace opentuner
{
    public class TSRecorder
    {

        public CircularBuffer ts_data_queue = new CircularBuffer(GlobalDefines.CircularBufferStartingCapacity);

        object locker = new object();

        public event EventHandler<bool> onRecordStatusChange;

        protected bool _record;
        public bool record
        {
            get
            {
                lock (locker)
                    return _record;
            }
            set
            {
                lock (locker)
                    _record =value;
            }
        }

        public int ID { get { return _id; } }
        private int _id = 0;

        bool recording = false;
        string media_path = "";
        public string Path => media_path;

        // "<Station>-<SR>KS" (see CommonFunctions.StationSuffix), kept up to date by the owning
        // source's status update so it is current at the moment a new recording file is opened
        // below. Falls back to the tuner id when empty (not locked yet) so files stay unique.
        public string StationInfo = "";

        private bool _running = false;
        private Thread _recorderThread = null;

        public TSRecorder(string _media_path, int id, OTSource TSSource)
        {
            media_path = _media_path;
            this._id = id;

            // register for TS Stream
            TSSource.RegisterTSConsumer(id, ts_data_queue);

            recording = false;

            _recorderThread = new Thread(worker_thread);
            _recorderThread.IsBackground = true;
            _recorderThread.Start();
        }

        public void Close()
        {
            // TODO: close file properly if recording when closing
            // Thread.Abort() doesn't exist on modern .NET (throws PlatformNotSupportedException) -
            // _running=false already stops worker_thread's loop cooperatively without it.
            _running = false;
        }

        public void worker_thread()
        {
            _running = true;
            BinaryWriter binWriter = null;
            byte data;
            bool ts_sync = true;
            try
            {
                while (_running)
                {
                    if (recording == false && record == true)
                    {
                        // open a new file
                        Log.Information("recording");

                        string name_suffix = string.IsNullOrEmpty(StationInfo) ? _id.ToString() : StationInfo;
                        string filename = opentuner.Utilities.CommonFunctions.GenerateTimestampFilename() + "_" + name_suffix + ".ts";

                        // if path doesn't exist then save in same folder
                        if (Directory.Exists(media_path))
                        {
                            filename = this.media_path + filename;
                        }
                        
                        binWriter = new BinaryWriter(File.Open(filename, FileMode.Create));
                        recording = true;
                        ts_sync = true;

                        ts_data_queue.Clear();

                        onRecordStatusChange?.Invoke(this, true);
                    }
                    else
                    {
                        if (recording == true && record == false)
                        {
                            Log.Information("stop recording");
                            recording = false;

                            // stop record
                            binWriter.Close();
                            binWriter.Dispose();
                            binWriter = null;

                            onRecordStatusChange?.Invoke(this, false);
                        }
                    }

                    int ts_data_count = ts_data_queue.Count;

                    if (ts_data_count > 0)
                    {
                        //if (_ts_data_queue.TryDequeue(out data))
                        //{                        
                        data = ts_data_queue.Dequeue();

                            if (record == true)
                            {

                                if (binWriter != null)
                                {
                                    if (ts_sync == true && data == 0x47)
                                    {
                                        Log.Information("TS Header Sync");
                                        ts_sync = false;
                                    }

                                    if (ts_sync == false)
                                    { 
                                        binWriter.Write(data);
                                    }
                                }
                            }
                        //}
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //Log.Information("TS Recorder Thread: Closed");
                Thread.ResetAbort();
            }
        }
    }
}
