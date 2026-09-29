using Serilog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace opentuner
{
    // this thread will read TS data using the provided callback functions and pass it along to the TS consumers (eg. TSParserThread)
    // this should be device agnostic
    // callback functions required for reading TS data from source
    public delegate void FlushTS();
    public delegate byte ReadTS(ref byte[] data, ref uint BytesRead);

    public class TSThread
    {
        // volatile: written by the UI thread (stop_ts / start_ts / Stop) and read by the worker thread and the other way
        // round (worker_thread_stopped) - without it the JIT may keep a stale value in a loop (issue #34)
        private volatile bool ts_build_queue = false;
        private List<CircularBuffer> registered_consumers = new List<CircularBuffer>();
        private FlushTS flush_ts_callback = null;
        private ReadTS read_ts_callback = null;
        private string identifier = "";

        private EventWaitHandle thread_wait_event_handle;
        private volatile bool worker_thread_stopped = false;
        private volatile bool shutdown_worker_thread = false;

        // Buffer overflow events of the consumer queues (issue #19).
        public readonly TSHealth Health = new TSHealth();

        private long DroppedBytesOfConsumers()
        {
            long dropped = 0;
            for (int consumers = 0; consumers < registered_consumers.Count; consumers++)
            {
                if (registered_consumers[consumers] != null)
                    dropped += registered_consumers[consumers].DroppedBytes;
            }
            return dropped;
        }

        public TSThread(CircularBuffer _raw_ts_data_queue, FlushTS _flush_ts_callback, ReadTS _read_ts_callback, string _identifier)
        {
            Log.Information(" >> Starting TS Thread <<");
            Log.Information(" >> Registering Raw TS Queue << ");

            thread_wait_event_handle = new EventWaitHandle(false, EventResetMode.AutoReset);

            registered_consumers.Add(_raw_ts_data_queue);

            flush_ts_callback = _flush_ts_callback;
            read_ts_callback = _read_ts_callback;
            identifier = _identifier;
        }

        public void NewDataPresent()
        {
            thread_wait_event_handle.Set();
        }

        public void RegisterTSConsumer(CircularBuffer raw_ts_data_queue)
        {
            Log.Information(" >> Registering New Queue << ");
            registered_consumers.Add(raw_ts_data_queue);
        }

        public void stop_ts()
        {
            Log.Information("Stopping TS: " + identifier);
            ts_build_queue = false;
            thread_wait_event_handle.Set(); // fire worker thread to handle stop event
        }

        public void start_ts()
        {
            Log.Information("Starting TS:" + identifier);
            ts_build_queue = true;
            thread_wait_event_handle.Set(); // fire worker thread to handle start event
        }

        public void Stop(ref bool stopped)
        {
            Log.Information("TS Thread " + identifier + ": stopping worker thread...");
            ts_build_queue = false;
            shutdown_worker_thread = true;
            thread_wait_event_handle.Set(); // fire worker thread to handle stop event

            // Wait for the worker thread, but not forever: this runs on the UI thread while the program closes. (The
            // old loop never waited - Task.Delay was not awaited and its counter never counted down - and spun for as
            // long as the worker did not report that it had stopped, issue #34.)
            var sw = System.Diagnostics.Stopwatch.StartNew();
            while (!worker_thread_stopped && sw.ElapsedMilliseconds < 1000)
            {
                Thread.Sleep(10);
            }
            stopped = worker_thread_stopped;
            Log.Information("TS Thread " + identifier + ": worker thread " + (stopped ? "stopped" : "DID NOT STOP within 1 s") + " (" + sw.ElapsedMilliseconds + " ms)");
        }

        public void worker_thread()
        {
            bool bufferingData = false;
            Log.Information(">> Starting TS Worked Thread <<");

            try
            {
                Log.Information("TS Thread: Starting...");

                byte[] data = new byte[4096];
                uint dataRead = 0;

                while (true)
                {
                    // Wait on an Event.
                    thread_wait_event_handle.WaitOne();

                    if (ts_build_queue == false)
                    {
                        if (flush_ts_callback != null)
                            flush_ts_callback();

                        bufferingData = false;
                        registered_consumers[0].Clear();
                        if (shutdown_worker_thread)
                        {
                            worker_thread_stopped = true;
                            break;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    if (ts_build_queue == true && bufferingData == false)
                    {
                        if (flush_ts_callback != null)
                            flush_ts_callback();

                        registered_consumers[0].Clear();
                        bufferingData = true;
                    }

                    if (bufferingData == true)
                    {
                        dataRead = 0;

                        if (read_ts_callback(ref data, ref dataRead) != 0)
                            Log.Information("Read Error");

                        if (dataRead > 0)
                        {
                            long dropped_before = DroppedBytesOfConsumers();

                            for (int c = 0; c < dataRead; c++)
                            {
                                for (int consumers = 0; consumers < registered_consumers.Count; consumers++)
                                {
                                    if (registered_consumers[consumers] != null)
                                        registered_consumers[consumers].Enqueue(data[c]);
                                }
                            }

                            if (DroppedBytesOfConsumers() > dropped_before)
                                Health.AddBufferOverflow();
                        }
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //Log.Information("TS Thread: Closed");
                Thread.ResetAbort();
            }
        }
    }
}
