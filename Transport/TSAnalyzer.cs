using System;

namespace opentuner
{
    // What the "TS Info" tab shows (issue #61): the TS packets of the last seconds sorted into Video, Audio, Null and
    // Overhead (everything else: PAT, SDT, PMT, PCR, other PIDs), as a per-second history for the bit rate chart and as
    // sums over the last StatWindowSeconds seconds for the pie chart and the statistics (a sliding window, so they move).
    public struct TSAnalysis
    {
        // packets per second of the last HistorySeconds seconds, oldest first, the newest entry is the last COMPLETE second
        public uint[] PacketsPerSecond;

        // packets in the window, by category
        public long Video, Audio, Null, Overhead;

        public long Total => Video + Audio + Null + Overhead;

        // complete seconds the window covers (less than StatWindowSeconds right after a start or a reset)
        public int Seconds;
    }

    // Counts TS packets by category. AddPacket() is called by one thread only (TSParserThread), Get() and the resets by
    // the UI thread, hence the lock around the published data. The packets of the running second are collected without
    // a lock and published when the second is over.
    public class TSAnalyzer
    {
        public const int HistorySeconds = 120;
        public const int StatWindowSeconds = 10;

        private const byte Other = 0, Video = 1, Audio = 2;

        // classification of the PIDs, written and read by the parser thread only
        private readonly byte[] _pid_class = new byte[TSParserThread.MAX_PID];
        private readonly bool[] _pmt_pid = new bool[TSParserThread.MAX_PID];

        // running second (parser thread only)
        private long _current_second = -1;
        private uint _cur_video, _cur_audio, _cur_null, _cur_overhead;

        // published data
        private readonly object _lock = new object();
        private readonly uint[] _history = new uint[HistorySeconds];       // packets of that second
        private readonly long[] _history_second = new long[HistorySeconds];
        private readonly uint[] _video_history = new uint[HistorySeconds];
        private readonly uint[] _audio_history = new uint[HistorySeconds];
        private readonly uint[] _null_history = new uint[HistorySeconds];
        private readonly uint[] _overhead_history = new uint[HistorySeconds];
        private long _stats_since = 0;   // seconds before this one don't count for the statistics (reset)
        private volatile bool _forget_requested = false;

        public TSAnalyzer()
        {
            Array.Fill(_history_second, -1L);
        }

        // Forget which PID is video / audio (new service); the PAT and PMT repeat within a few hundred ms.
        public void ForgetServices()
        {
            _forget_requested = true;
        }

        // Starts the pie chart and the statistics from zero (the running second is left out, it is partly from before).
        public void ResetStats()
        {
            lock (_lock)
                _stats_since = Environment.TickCount64 / 1000 + 1;
        }

        // Starts the bit rate history from zero.
        public void ResetHistory()
        {
            lock (_lock)
            {
                Array.Clear(_history, 0, _history.Length);
                Array.Fill(_history_second, -1L);
            }
        }

        public TSAnalysis Get()
        {
            long now = Environment.TickCount64 / 1000;
            var packets = new uint[HistorySeconds];
            var result = new TSAnalysis { PacketsPerSecond = packets };

            lock (_lock)
            {
                // entry i = second (now - HistorySeconds + i); the running second (now) is not complete yet and left out
                for (int i = 0; i < HistorySeconds; i++)
                {
                    long second = now - HistorySeconds + i;
                    int slot = (int)(second % HistorySeconds);
                    if (_history_second[slot] == second)
                        packets[i] = _history[slot];
                }

                for (long second = now - StatWindowSeconds; second < now; second++)
                {
                    int slot = (int)(second % HistorySeconds);
                    if (_history_second[slot] != second || second < _stats_since)
                        continue;

                    result.Video += _video_history[slot];
                    result.Audio += _audio_history[slot];
                    result.Null += _null_history[slot];
                    result.Overhead += _overhead_history[slot];
                    result.Seconds++;
                }
            }

            return result;
        }

        // One TS packet (188 bytes, sync byte checked, PID already extracted). Called by the parser thread for every packet.
        public void AddPacket(byte[] packet, uint pid)
        {
            long second = Environment.TickCount64 / 1000;
            if (second != _current_second)
            {
                Publish();
                _current_second = second;

                if (_forget_requested)
                {
                    _forget_requested = false;
                    Array.Clear(_pid_class, 0, _pid_class.Length);
                    Array.Clear(_pmt_pid, 0, _pmt_pid.Length);
                }
            }

            if (pid == TSParserThread.TS_PID_NULL)
            {
                _cur_null++;
                return;
            }

            bool payload_start = (packet[1] & 0x40) != 0;
            bool has_payload = (packet[3] & 0x10) != 0;

            if (has_payload && payload_start && (pid == TSParserThread.TS_PID_PAT || _pmt_pid[pid]))
            {
                int offset = 4;
                if ((packet[3] & 0x20) != 0)
                    offset += 1 + packet[4];

                if (pid == TSParserThread.TS_PID_PAT)
                    ParsePat(packet, offset);
                else
                    ParsePmt(packet, offset);
            }

            switch (_pid_class[pid])
            {
                case Video: _cur_video++; break;
                case Audio: _cur_audio++; break;
                default: _cur_overhead++; break;
            }
        }

        // Moves the packets of the second that just ended into the history and the totals.
        private void Publish()
        {
            if (_current_second < 0)
                return;

            uint total = _cur_video + _cur_audio + _cur_null + _cur_overhead;

            lock (_lock)
            {
                int slot = (int)(_current_second % HistorySeconds);
                _history[slot] = total;
                _history_second[slot] = _current_second;

                _video_history[slot] = _cur_video;
                _audio_history[slot] = _cur_audio;
                _null_history[slot] = _cur_null;
                _overhead_history[slot] = _cur_overhead;
            }

            _cur_video = _cur_audio = _cur_null = _cur_overhead = 0;
        }

        // A section that fits into one packet (PAT and PMT of a normal service do): pointer_field, table_id,
        // section_length, ... Sections spread over several packets are ignored.
        private static bool SectionStart(byte[] packet, int offset, byte table_id, out int start, out int end)
        {
            start = end = 0;

            if (offset >= TSParserThread.TS_PACKET_SIZE)
                return false;

            start = offset + 1 + packet[offset];   // pointer_field
            if (start + 3 > TSParserThread.TS_PACKET_SIZE || packet[start] != table_id)
                return false;

            int section_length = ((packet[start + 1] & 0x0F) << 8) | packet[start + 2];
            end = start + 3 + section_length - 4;   // without the CRC
            return end <= TSParserThread.TS_PACKET_SIZE;
        }

        private void ParsePat(byte[] packet, int offset)
        {
            if (!SectionStart(packet, offset, TSParserThread.TS_TABLE_PAT, out int start, out int end))
                return;

            for (int p = start + 8; p + 4 <= end; p += 4)
            {
                int program = (packet[p] << 8) | packet[p + 1];
                int pid = ((packet[p + 2] & 0x1F) << 8) | packet[p + 3];

                if (program != 0)   // 0 = network PID
                    _pmt_pid[pid] = true;
            }
        }

        private void ParsePmt(byte[] packet, int offset)
        {
            if (!SectionStart(packet, offset, TSParserThread.TS_TABLE_PMT, out int start, out int end))
                return;

            int program_info_length = ((packet[start + 10] & 0x0F) << 8) | packet[start + 11];
            int p = start + 12 + program_info_length;

            while (p + 5 <= end)
            {
                byte stream_type = packet[p];
                int pid = ((packet[p + 1] & 0x1F) << 8) | packet[p + 2];
                int es_info_length = ((packet[p + 3] & 0x0F) << 8) | packet[p + 4];

                _pid_class[pid] = ClassOf(stream_type, packet, p + 5, Math.Min(p + 5 + es_info_length, end));
                p += 5 + es_info_length;
            }
        }

        private static byte ClassOf(byte stream_type, byte[] packet, int descriptors, int descriptors_end)
        {
            switch (stream_type)
            {
                case 0x01: case 0x02: case 0x10: case 0x1B: case 0x24:   // MPEG-1/2, MPEG-4 visual, H.264, H.265
                    return Video;
                case 0x03: case 0x04: case 0x0F: case 0x11: case 0x81: case 0x87:   // MPEG audio, AAC, LATM, AC-3, E-AC-3
                    return Audio;
                case 0x06:   // PES private data: audio if it carries an AC-3 / E-AC-3 / DTS / AAC descriptor
                    for (int d = descriptors; d + 2 <= descriptors_end; d += 2 + packet[d + 1])
                    {
                        byte tag = packet[d];
                        if (tag == 0x6A || tag == 0x7A || tag == 0x7B || tag == 0x7C)
                            return Audio;
                    }
                    return Other;
                default:
                    return Other;
            }
        }
    }
}
