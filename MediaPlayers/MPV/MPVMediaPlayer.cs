using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static opentuner.MediaPlayers.MPV.LibMpv;

namespace opentuner.MediaPlayers.MPV
{
    public class MPVMediaPlayer : OTMediaPlayer
    {

        public delegate Int64 MyStreamCbReadFn(IntPtr cookie, IntPtr buf, Int64 numbytes);
        public delegate Int64 MyStreamCbSeekFn(IntPtr cookie, Int64 offset);
        public delegate void MyStreamCbCloseFn(IntPtr cookie);
        public delegate Int64 MyStreamCbSizeFn(IntPtr cookie);

        private IntPtr handler_ptr;
        private MyStreamCbOpenFn handler;

        private MyStreamCbReadFn readfn;
        private MyStreamCbCloseFn closefn;

        private CircularBuffer _videoBuffer;

        bool stopFlag = false;

        // Every Play() starts one instance of mpv. The read callback of an instance only serves while its generation
        // (the cookie) is the current one, so an old instance that is still ending cannot take data from the new one.
        private long _generation = 0;
        private readonly object _lifecycleLock = new object();
        private EventLoopToken _eventLoop;
        private Task _terminationTask = Task.CompletedTask;

        private class EventLoopToken
        {
            public volatile bool Cancel;
            public Task Task;
        }

        public override event EventHandler<MediaStatus> onVideoOut;
        private IntPtr _mpvHandle;

        private Int64 _videoViewHandle;
        private long _volume = 100;

        private int _id = 0;

        public override string GetName()
        {
            return "MPV";
        }

        private void debug(string msg)
        {
            //Log.Information("MPVMediaPlayer: " + msg);
        }

        private EventLoopToken StartEventLoop(IntPtr handle)
        {
            var token = new EventLoopToken();
            token.Task = Task.Run(() => {
                try
                {
                    EventLoop(handle, token);
                }
                catch (Exception ex)
                {
                    debug("Eventloop Exception : " + ex.ToString());
                }
            });

            return token;
        }

        // handle: the instance asked, default = the current one
        public int GetPropertyInt(string propertyName, IntPtr handle = default)
        {
            mpv_get_property(handle != default ? handle : _mpvHandle, GetUtf8Bytes(propertyName), mpv_format.MPV_FORMAT_INT64, out IntPtr buffer);
            return buffer.ToInt32();
        }

        public string GetPropertyString(string propertyName, IntPtr handle = default)
        {
            mpv_error error = mpv_get_property(handle != default ? handle : _mpvHandle, GetUtf8Bytes(propertyName), mpv_format.MPV_FORMAT_STRING, out IntPtr buffer);

            if (error == mpv_error.MPV_ERROR_SUCCESS)
            {
                string ret = ConvertFromUtf8(buffer);
                mpv_free(buffer);
                return ret;
            }

            return "";
        }


        // Runs for one instance (handle) until it is cancelled: it must not touch the handle after that, because the
        // core is destroyed right then (hence the short timeout instead of waiting for ever).
        private void EventLoop(IntPtr handle, EventLoopToken token)
        {
            while (!token.Cancel)
            {
                IntPtr ptr = LibMpv.mpv_wait_event(handle, 0.2);
                if (token.Cancel)
                    break;

                mpv_event evt = (mpv_event)Marshal.PtrToStructure(ptr, typeof(mpv_event));

                if (evt.event_id == mpv_event_id.MPV_EVENT_SHUTDOWN)
                    break;

                try
                {
                    switch (evt.event_id)
                    {
                        case (mpv_event_id.MPV_EVENT_VIDEO_RECONFIG):
                            //reconfigureVideo();
                            break;

                        case (mpv_event_id.MPV_EVENT_LOG_MESSAGE):
                            var logdata = (mpv_event_log_message)Marshal.PtrToStructure(evt.data, typeof(mpv_event_log_message));
                            debug(ConvertFromUtf8(logdata.text).Trim());
                            break;

                        case (mpv_event_id.MPV_EVENT_PLAYBACK_RESTART):
                            MediaStatus info = new MediaStatus();

                            int width = GetPropertyInt("dwidth", handle);
                            int height = GetPropertyInt("dheight", handle);

                            info.VideoHeight = (uint)height;
                            info.VideoWidth = (uint)width;

                            //string data = GetPropertyString("audio-device-list");
                            string data = "";

                            //data = GetPropertyString("video-format");
                            
                            data = GetPropertyString("video-codec", handle);
                            info.VideoCodec = data;
                            data = GetPropertyString("audio-codec-name", handle);
                            info.AudioCodec = data;
                            data = GetPropertyString("audio-device", handle);
                            debug(data);
                            data = GetPropertyString("audio-params/channel-count", handle);
                            uint.TryParse(data, out info.AudioChannels);
                            data = GetPropertyString("audio-params/samplerate", handle);
                            uint.TryParse(data, out info.AudioRate);

                            onVideoOut?.Invoke(this,info);
                            break;
                        default: debug("MPV Event: " + evt.event_id.ToString()); break;
                    }
                }
                catch (Exception ex)
                {
                    debug(ex.ToString());
                }
            }

            debug("Closing Event Loop");

        }

        public MPVMediaPlayer(Int64 VideoViewHandle)
        {
            handler = StreamCBOpenFN;
            handler_ptr = Marshal.GetFunctionPointerForDelegate(handler);
            _videoViewHandle = VideoViewHandle;
        }

        bool ts_sync = false;

        Int64 MyStreamReadFn(IntPtr cookie, IntPtr buf, Int64 numbytes)
        {
            if (stopFlag == true || (long)cookie != Interlocked.Read(ref _generation))
                return 0;

            try
            {
                int timeout = 0;

                while (_videoBuffer.Count < 2000)
                {

                    if (stopFlag == true || (long)cookie != Interlocked.Read(ref _generation))
                    {
                        //Log.Information("Stop Requested");
                        return 0;
                    }

                    if (timeout > 5000)
                    {
                        Log.Information("MyStream : Read Timeout");
                        return 0;
                    }

                    Thread.Sleep(5);
                    timeout += 5;
                }

                int queue_count = _videoBuffer.Count;

                if (queue_count > 0)
                {
                    //RawTSData raw_ts_data = null;
                    byte raw_ts_data = 0;

                    Int64 buildLen = numbytes;

                    if (queue_count < buildLen)
                    {
                        buildLen = queue_count;
                    }

                    byte[] ts_data = new byte[buildLen];

                    int counter = 0;

                    while (counter < buildLen)
                    {
                        if (_videoBuffer.Count > 0)
                        {
                            raw_ts_data = _videoBuffer.Dequeue();


                            if (ts_sync == false && raw_ts_data != 0x47)
                            {
                                buildLen--;
                                continue;
                            }
                            else
                            {
                                ts_sync = true;
                                ts_data[counter++] = raw_ts_data;
                            }
                        }
                        else
                        {
                            Log.Information("Warning: Failing to dequeue, nothing to dequeue: TSStream");
                        }
                    }

                    Marshal.Copy(ts_data.ToArray(), 0, buf, ts_data.Length);
                    return ts_data.Length;

                }
            }
            catch (Exception ex)
            {
                Log.Information("Stream Read Callback Exception: " + ex.Message);
            }

            Log.Information("TS StreamInput: Shouldn't be here");

            return 0;
        }

        void MyStreamCloseFn(IntPtr cookie)
        {
            debug("Close Callback Called");
        }


        int StreamCBOpenFN(String userdata, String uri, ref MPV_STREAM_CB_INFO info)
        {
            debug("StreamCBOpenFN Called");
            //debug(userdata);
            //debug(uri);

            readfn = MyStreamReadFn;
            closefn = MyStreamCloseFn;

            info.ReadFn = Marshal.GetFunctionPointerForDelegate(readfn);
            info.CloseFn = Marshal.GetFunctionPointerForDelegate(closefn);
            info.Cookie = (IntPtr)Interlocked.Read(ref _generation);

            return 0;
        }

        public static IntPtr AllocateUtf8IntPtrArrayWithSentinel(string[] arr, out IntPtr[] byteArrayPointers)
        {
            int numberOfStrings = arr.Length + 1; // add extra element for extra null pointer last (sentinel)
            byteArrayPointers = new IntPtr[numberOfStrings];
            IntPtr rootPointer = Marshal.AllocCoTaskMem(IntPtr.Size * numberOfStrings);
            for (int index = 0; index < arr.Length; index++)
            {
                var bytes = LibMpv.GetUtf8Bytes(arr[index]);
                IntPtr unmanagedPointer = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, unmanagedPointer, bytes.Length);
                byteArrayPointers[index] = unmanagedPointer;
            }
            Marshal.Copy(byteArrayPointers, 0, rootPointer, numberOfStrings);
            return rootPointer;
        }

        private void DoMpvCommand(params string[] args)
        {
            IntPtr[] byteArrayPointers;
            var mainPtr = AllocateUtf8IntPtrArrayWithSentinel(args, out byteArrayPointers);
            LibMpv.mpv_command(_mpvHandle, mainPtr);
            foreach (var ptr in byteArrayPointers)
            {
                Marshal.FreeHGlobal(ptr);
            }
            Marshal.FreeHGlobal(mainPtr);
        }

        public override void Close()
        {
            lock (_lifecycleLock)
            {
                StopLocked();
                _terminationTask.Wait(500);
            }
        }

        public override int GetVolume()
        {
            return (int)_volume;
        }

        public override void Initialize(CircularBuffer TSDataQueue)
        {
            _videoBuffer = TSDataQueue;
        }

        public override void Play()
        {
            lock (_lifecycleLock)
            {
                PlayLocked();
            }
        }

        private void PlayLocked()
        {
            // a running instance is stopped first and has to be gone before the next one starts
            StopLocked();
            if (!_terminationTask.Wait(2000))
                Log.Warning("MPV: the previous instance did not end within 2 s");

            ts_sync = false;
            stopFlag = false;
            Interlocked.Increment(ref _generation);

            _mpvHandle = LibMpv.mpv_create();

            debug("start event loop");
            _eventLoop = StartEventLoop(_mpvHandle);


            mpv_initialize(_mpvHandle);
            mpv_request_log_messages(_mpvHandle, "error");
            mpv_set_option_string(_mpvHandle, LibMpv.GetUtf8Bytes("keep-open"), LibMpv.GetUtf8Bytes("always"));
            //var windowID = _videoView.Handle.ToInt64();
            var windowID = _videoViewHandle;
            LibMpv.mpv_set_option(_mpvHandle, LibMpv.GetUtf8Bytes("wid"), LibMpv.mpv_format.MPV_FORMAT_INT64, ref windowID);

            //mpv_stream_cb_add_ro(_mpvHandle, "myprotocol", "", handler_ptr);
            mpv_stream_cb_add_ro(_mpvHandle, "myprotocol", "", handler);

            _videoBuffer.Clear();

            DoMpvCommand("loadfile", "myprotocol://fake");
            //DoMpvCommand("loadfile", "udp://127.0.0.1:4003");        }
        }

        public override void SetVolume(int Volume)
        {
            _volume = Volume;
            if (_mpvHandle != IntPtr.Zero)
            {
                try
                {
                    LibMpv.mpv_set_option(_mpvHandle, LibMpv.GetUtf8Bytes("volume"), mpv_format.MPV_FORMAT_INT64, ref _volume);
                }
                catch (Exception ex)
                {
                    Log.Information("Error setting volume for MediaPlayer MPV: " + ex.Message);
                }
            }
        }

        public override void SnapShot(string FileName)
        {
            Log.Information("MPV Snapshot: " + Path.GetDirectoryName(FileName) + "\\");

            if (_mpvHandle != IntPtr.Zero)
            {
                LibMpv.mpv_set_option_string(_mpvHandle, LibMpv.GetUtf8Bytes("screenshot-directory"), LibMpv.GetUtf8Bytes(Path.GetDirectoryName(FileName) + "\\"));
                LibMpv.mpv_set_option_string(_mpvHandle, LibMpv.GetUtf8Bytes("screenshot-template"), LibMpv.GetUtf8Bytes("ot_mpv_%n"));
                LibMpv.mpv_set_option_string(_mpvHandle, LibMpv.GetUtf8Bytes("screenshot-format"), LibMpv.GetUtf8Bytes("png"));
                DoMpvCommand("screenshot", "video");
            }
        }

        public override void Stop()
        {
            lock (_lifecycleLock)
            {
                StopLocked();
            }
        }

        // Ends the running instance without waiting for it. The event loop is cancelled first (it must not touch the
        // handle any more), then the core is ended on a background thread. mpv_destroy alone returns at once and
        // leaves the old core running with its video window and its read callback: the old instance then keeps its
        // last picture and takes data from the new one. mpv_terminate_destroy waits for the end of the core, which on
        // the UI thread could block against the window of the core, so it never runs there.
        private void StopLocked()
        {
            stopFlag = true;
            Interlocked.Increment(ref _generation);   // the read callback of the old instance returns 0 from now on

            IntPtr handle = _mpvHandle;
            EventLoopToken token = _eventLoop;
            _mpvHandle = IntPtr.Zero;
            _eventLoop = null;

            if (handle == IntPtr.Zero)
                return;

            if (token != null)
                token.Cancel = true;

            _terminationTask = Task.Run(() =>
            {
                try
                {
                    token?.Task?.Wait(1000);
                    LibMpv.mpv_terminate_destroy(handle);
                }
                catch (Exception ex)
                {
                    Log.Warning("MPV: ending the instance failed: " + ex.Message);
                }
            });
        }

        public override int getID()
        {
            return _id;
        }

        public override void Initialize(CircularBuffer TSDataQueue, int ID)
        {
            _id = ID;
            Initialize(TSDataQueue);
        }
    }
}
