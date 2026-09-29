using Serilog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace opentuner
{
    public delegate void TSDataCallback(TSStatus ts_status);

    public class TSParserThread
    {
        public const uint MAX_PID = 8192;

        public const byte TS_PACKET_SIZE = 188;
        public const byte TS_HEADER_SYNC = 0x47;

        public const ushort TS_PID_PAT = 0x0000;
        public const ushort TS_PID_SDT = 0x0011;
        public const ushort TS_PID_NULL = 0x1FFF;

        public const byte TS_TABLE_PAT = 0x00;
        public const byte TS_TABLE_PMT = 0x02;
        public const byte TS_TABLE_SDT = 0x42;

        TSDataCallback ts_data_callback = null;

        //ConcurrentQueue<byte> parser_ts_data_queue = null;
        //CircularBuffer parser_ts_data_queue = null;

        public CircularBuffer parser_ts_data_queue = new CircularBuffer(GlobalDefines.CircularBufferStartingCapacity);

        // Continuity/transport error/sync loss counters over the last few seconds (issue #19).
        public readonly TSHealth Health = new TSHealth();

        // Last continuity counter seen per PID, -1 = none yet.
        private readonly sbyte[] _last_cc = CreateLastCc();

        private static sbyte[] CreateLastCc()
        {
            var last = new sbyte[MAX_PID];
            Array.Fill(last, (sbyte)-1);
            return last;
        }

        // Says on which PID the errors are (a muxer that doesn't maintain the counter of one side stream
        // looks different from real packet loss), at most one line per 2 s so a bad stream can't flood the log.
        private long _last_cc_log_ms = 0;
        private int _cc_log_suppressed = 0;

        private void LogContinuityError(uint pid, int expected, int found)
        {
            long now = Environment.TickCount64;

            if (now - _last_cc_log_ms < 2000)
            {
                _cc_log_suppressed++;
                return;
            }

            Log.Warning("TS continuity error: PID 0x" + pid.ToString("X4") + " expected " + expected + " got " + found +
                        (_cc_log_suppressed > 0 ? " (+" + _cc_log_suppressed + " more since the last line)" : ""));
            _last_cc_log_ms = now;
            _cc_log_suppressed = 0;
        }

        // Continuity check for one packet (ISO 13818-1 2.4.3.3): the counter goes up by one for
        // every packet of a PID that carries a payload; a repeat of the same value is a legal
        // duplicate. Packets flagged with TEI or the discontinuity indicator can't be judged.
        private void CheckContinuity(uint pid, byte[] packet)
        {
            bool has_payload = (packet[3] & 0x10) != 0;
            bool transport_error = (packet[1] & 0x80) != 0;

            if (!has_payload || transport_error)
                return;

            int cc = packet[3] & 0x0F;
            int last = _last_cc[pid];
            bool discontinuity = (packet[3] & 0x20) != 0 && packet[4] > 0 && (packet[5] & 0x80) != 0;

            if (!discontinuity && last >= 0 && cc != last && cc != ((last + 1) & 0x0F))
            {
                Health.AddContinuityError();
                LogContinuityError(pid, (last + 1) & 0x0F, cc);
            }

            _last_cc[pid] = (sbyte)cc;
        }

        // Thread.Abort() doesn't exist on modern .NET (throws PlatformNotSupportedException) -
        // worker_thread() checks this cooperatively instead.
        private volatile bool _stopRequested = false;
        public void Stop() { _stopRequested = true; }

        //public TSParserThread(TSDataCallback _ts_data_callback, CircularBuffer _parser_ts_data_queue)
        public TSParserThread(TSDataCallback _ts_data_callback)
        {
            ts_data_callback = _ts_data_callback;
        }

        public void worker_thread()
        {
            uint ts_packet_total_count = 0;
            uint ts_packet_null_count = 0;
            uint ts_invalid_packet_count = 0;
            bool sync_lost = false;

            string prevServiceName = "";
            string prevServiceProvider = "";

            try
            {
                while (!_stopRequested)
                {
                    int ts_data_count = parser_ts_data_queue.Count;

                    if (ts_data_count > TS_PACKET_SIZE)
                    {
                        byte check = 0;

                        if (parser_ts_data_queue.Count > 0)
                        {
                            check = parser_ts_data_queue.Peek();

                            if (check == TS_HEADER_SYNC)
                            {
                                sync_lost = false;

                                // get the complete packet
                                byte[] ts_packet = new byte[TS_PACKET_SIZE];

                                int byte_counter = 0;

                                while (byte_counter < TS_PACKET_SIZE)
                                {
                                    byte data;

                                    //if (parser_ts_data_queue.TryDequeue(out data))
                                    if (parser_ts_data_queue.Count > 0)
                                    {
                                        data = parser_ts_data_queue.Dequeue();
                                        ts_packet[byte_counter++] = data;
                                    }

                                }

                                //Log.Information("TS Packet Received");

                                // parse ts packet
                                ts_packet_total_count += 1;

                                UInt32 ts_pid = (UInt32)((ts_packet[1] & 0x1F) << 8) | (UInt32)ts_packet[2];

                                //Log.Information("TS Pid: " + ts_pid.ToString("X"));

                                // TS health (issue #19)
                                if ((ts_packet[1] & 0x80) != 0)
                                    Health.AddTransportError();

                                if (ts_pid != TS_PID_NULL)
                                    CheckContinuity(ts_pid, ts_packet);

                                if (ts_pid != TS_PID_NULL && (ts_packet[3] & 0xC0) != 0)   // transport_scrambling_control
                                    Health.AddScrambled();

                                UInt32 ts_adaption_field_flag = (UInt32)(ts_packet[3] & 0x20) >> 5;

                                byte ts_payload_content_offset = 4;
                                byte ts_adaption_field_length = 0;

                                if (ts_adaption_field_flag > 0)
                                {
                                    ts_adaption_field_length = ts_packet[4];

                                    if (ts_adaption_field_length == 0 || ts_adaption_field_length > 183)
                                    {
                                        //Log.Information("Length Invalid: Packet likely Invalid");
                                        ts_invalid_packet_count += 1;
                                        continue;
                                    }

                                }

                                ts_payload_content_offset += ts_adaption_field_length;

                                if (ts_pid == TS_PID_NULL)
                                {
                                    //Log.Information("Null Packet");
                                    ts_packet_null_count += 1;
                                    continue;
                                }

                                if (ts_pid == TS_PID_SDT)   // service description table
                                {
                                    int ts_payload_offset = ts_payload_content_offset + 1 + ts_packet[ts_payload_content_offset];

                                    try
                                    {
                                        if (ts_packet[ts_payload_offset] != TS_TABLE_SDT)
                                        {
                                            continue;
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Warning("TS_TABLE_SDT Exception : " + ex.Message);
                                        continue;
                                    }

                                    UInt32 ts_payload_section_length = ((UInt32)(ts_packet[ts_payload_offset + 1] & 0x0F) << 8) | (UInt32)ts_packet[ts_payload_offset + 2];

                                    if (ts_payload_section_length < 1)
                                    {
                                        continue;
                                    }

                                    //Log.Information("SDT Section Len: " + ts_payload_section_length);

                                    Int32 ts_service_provider_name_length = ts_packet[ts_payload_offset + 19];

                                    string service_provider = "";
                                    try
                                    {
                                        service_provider = System.Text.Encoding.ASCII.GetString(ts_packet, ts_payload_offset + 19 + 1, ts_service_provider_name_length);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex.Message);
                                    }

                                    //Log.Information(service_provider);

                                    Int32 ts_service_name_length = ts_packet[ts_payload_offset + 19 + ts_service_provider_name_length + 1];

                                    string service_provider_name = "";

                                    try
                                    {
                                        service_provider_name = System.Text.Encoding.ASCII.GetString(ts_packet, ts_payload_offset + 19 + ts_service_provider_name_length + 2, ts_service_name_length);
                                    }
                                    catch (Exception ex)
                                    {
                                        Log.Error(ex.Message);
                                    }

                                    //Log.Information(service_provider);
                                    //Log.Information(service_provider_name);

                                    // temp hack to reset null counters
                                    if (prevServiceName != service_provider_name || prevServiceProvider != service_provider)
                                    {
                                        ts_packet_total_count = 0;
                                        ts_packet_null_count = 0;

                                        prevServiceName = service_provider_name;
                                        prevServiceProvider = service_provider;

                                        Health.Reset();
                                    }

                                    if (ts_data_callback != null)
                                    {
                                        TSStatus new_status = new TSStatus();
                                        new_status.ServiceName = service_provider_name;
                                        new_status.ServiceProvider = service_provider;
                                        new_status.TotalTSPackets = ts_packet_total_count;

                                        if (ts_packet_total_count > 0)
                                        {
                                            new_status.NullPacketsPerc = Convert.ToUInt32((Convert.ToDouble(ts_packet_null_count) / Convert.ToDouble(ts_packet_total_count)) * 100);
                                        }

                                        ts_data_callback(new_status);
                                    }
                                }
                            }
                            else
                            {
                                // remove the byte and continue - counted once per stretch of lost sync, not per byte
                                if (!sync_lost)
                                {
                                    sync_lost = true;
                                    Health.AddSyncLoss();
                                }

                                check = parser_ts_data_queue.Dequeue();
                                continue;
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(100);
                    }
                }
            }
            catch (ThreadAbortException)
            {
                //Log.Information("TS Parser Thread Closed");
                Thread.ResetAbort();
            }
        }
    }
}