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

        // the PIDs seen in the window, most packets first (issue #63)
        public TSPidInfo[] Pids;
    }

    // One PID of the TS Info table: packets in the window and what the PAT / PMT say about it.
    public struct TSPidInfo
    {
        public int Pid;
        public long Packets;
        public bool IsPmt;       // a PID the PAT names as the PMT of a program
        public int StreamType;   // stream_type from the PMT, -1 = not in a PMT (yet)
        public bool IsPcr;       // the PID a PMT names as the carrier of the PCR
        public string Info;      // decoded content of the table or stream ("" = nothing known), for the hover text
        public byte Category;    // 0 = overhead, 1 = video, 2 = audio, 3 = null packets (as in the Payload Diagram)
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

        // packets per PID of the last seconds, a ring with one array per second (only the window is needed)
        private const int PidRing = StatWindowSeconds + 2;
        private readonly uint[][] _pid_history = CreatePidHistory();
        private readonly long[] _pid_history_second = new long[PidRing];
        private readonly uint[] _cur_pid_count = new uint[TSParserThread.MAX_PID];   // running second (parser thread only)
        private readonly System.Collections.Generic.List<int> _cur_pids = new System.Collections.Generic.List<int>();
        // what the PAT / PMT said, published (written by the parser thread under the lock)
        private readonly bool[] _pub_pmt = new bool[TSParserThread.MAX_PID];
        private readonly short[] _pub_stream_type = CreateStreamTypes();
        private readonly bool[] _pub_pcr = new bool[TSParserThread.MAX_PID];
        private readonly byte[] _pub_class = new byte[TSParserThread.MAX_PID];
        private readonly string[] _pub_info = new string[TSParserThread.MAX_PID];
        private string _eit_now = "", _eit_next = "";   // parser thread only

        private static uint[][] CreatePidHistory()
        {
            var ring = new uint[PidRing][];
            for (int i = 0; i < PidRing; i++)
                ring[i] = new uint[TSParserThread.MAX_PID];
            return ring;
        }

        private static short[] CreateStreamTypes()
        {
            var types = new short[TSParserThread.MAX_PID];
            Array.Fill(types, (short)-1);
            return types;
        }

        public TSAnalyzer()
        {
            Array.Fill(_history_second, -1L);
            Array.Fill(_pid_history_second, -1L);
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

                // packets per PID in the same window
                var sums = new long[TSParserThread.MAX_PID];
                for (long second = now - StatWindowSeconds; second < now; second++)
                {
                    int slot = (int)(second % PidRing);
                    if (_pid_history_second[slot] != second || second < _stats_since)
                        continue;

                    uint[] counts = _pid_history[slot];
                    for (int pid = 0; pid < sums.Length; pid++)
                        sums[pid] += counts[pid];
                }

                var pids = new System.Collections.Generic.List<TSPidInfo>();
                for (int pid = 0; pid < sums.Length; pid++)
                {
                    if (sums[pid] > 0)
                        pids.Add(new TSPidInfo { Pid = pid, Packets = sums[pid], IsPmt = _pub_pmt[pid], StreamType = _pub_stream_type[pid],
                                             IsPcr = _pub_pcr[pid], Info = _pub_info[pid] ?? "",
                                             Category = pid == TSParserThread.TS_PID_NULL ? (byte)3 : _pub_class[pid] });
                }

                pids.Sort((a, b) => b.Packets.CompareTo(a.Packets));
                result.Pids = pids.ToArray();
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

                    lock (_lock)
                    {
                        Array.Clear(_pub_pmt, 0, _pub_pmt.Length);
                        Array.Clear(_pub_pcr, 0, _pub_pcr.Length);
                        Array.Clear(_pub_class, 0, _pub_class.Length);
                        Array.Clear(_pub_info, 0, _pub_info.Length);
                        Array.Fill(_pub_stream_type, (short)-1);
                    }
                    _eit_now = _eit_next = "";
                }
            }

            if (_cur_pid_count[pid]++ == 0)
                _cur_pids.Add((int)pid);

            if (pid == TSParserThread.TS_PID_NULL)
            {
                _cur_null++;
                return;
            }

            bool payload_start = (packet[1] & 0x40) != 0;
            bool has_payload = (packet[3] & 0x10) != 0;

            if (has_payload && payload_start && (pid == TSParserThread.TS_PID_PAT || _pmt_pid[pid] || IsSiPid(pid)))
            {
                int offset = 4;
                if ((packet[3] & 0x20) != 0)
                    offset += 1 + packet[4];

                if (pid == TSParserThread.TS_PID_PAT)
                    ParsePat(packet, offset);
                else if (_pmt_pid[pid])
                    ParsePmt(packet, offset, (int)pid);
                else
                    ParseSi(packet, offset, (int)pid);
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

                int pid_slot = (int)(_current_second % PidRing);
                uint[] counts = _pid_history[pid_slot];
                Array.Clear(counts, 0, counts.Length);
                foreach (int pid in _cur_pids)
                    counts[pid] = _cur_pid_count[pid];
                _pid_history_second[pid_slot] = _current_second;
            }

            foreach (int pid in _cur_pids)
                _cur_pid_count[pid] = 0;
            _cur_pids.Clear();

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
                {
                    _pmt_pid[pid] = true;
                    lock (_lock)
                        _pub_pmt[pid] = true;
                }
            }

            SetInfo(TSParserThread.TS_PID_PAT, TSTableDecoder.Pat(packet, start, end));
        }

        private void ParsePmt(byte[] packet, int offset, int pmt_pid)
        {
            if (!SectionStart(packet, offset, TSParserThread.TS_TABLE_PMT, out int start, out int end) || start + 12 > end)
                return;

            SetInfo(pmt_pid, TSTableDecoder.Pmt(packet, start, end, out int program, out int pcr_pid, out _));

            int program_info_length = ((packet[start + 10] & 0x0F) << 8) | packet[start + 11];
            int p = start + 12 + program_info_length;
            bool pcr_in_stream = false;

            while (p + 5 <= end)
            {
                byte stream_type = packet[p];
                int pid = ((packet[p + 1] & 0x1F) << 8) | packet[p + 2];
                int es_info_length = ((packet[p + 3] & 0x0F) << 8) | packet[p + 4];
                int descriptors_end = Math.Min(p + 5 + es_info_length, end);

                byte stream_class = ClassOf(stream_type, packet, p + 5, descriptors_end);
                _pid_class[pid] = stream_class;

                string language = TSTableDecoder.EsLanguage(packet, p + 5, descriptors_end);
                pcr_in_stream |= pid == pcr_pid;
                lock (_lock)
                {
                    _pub_stream_type[pid] = stream_type;
                    _pub_class[pid] = stream_class;
                    _pub_info[pid] = "program " + program + (language == "" ? "" : ", " + language) + (pid == pcr_pid ? ", carries the PCR" : "");
                }

                p += 5 + es_info_length;
            }

            if (pcr_pid != TSParserThread.TS_PID_NULL)
            {
                lock (_lock)
                {
                    _pub_pcr[pcr_pid] = true;
                    if (!pcr_in_stream)
                        _pub_info[pcr_pid] = "PCR of program " + program;
                }
            }
        }

        private static bool IsSiPid(uint pid)
        {
            return pid == 0x10 || pid == 0x11 || pid == 0x12 || pid == 0x14;   // NIT, SDT/BAT, EIT, TDT/TOT
        }

        private void SetInfo(int pid, string text)
        {
            if (text == "")
                return;

            lock (_lock)
                _pub_info[pid] = text;
        }

        // The DVB service information tables that are worth a line in the hover text: SDT, NIT, EIT now/next, TDT/TOT.
        private void ParseSi(byte[] packet, int offset, int pid)
        {
            if (offset >= TSParserThread.TS_PACKET_SIZE)
                return;

            int start = offset + 1 + packet[offset];   // pointer_field
            if (start + 3 > TSParserThread.TS_PACKET_SIZE)
                return;

            byte table_id = packet[start];
            int section_length = ((packet[start + 1] & 0x0F) << 8) | packet[start + 2];
            int end = start + 3 + section_length - 4;   // without the CRC; a TDT has none, see TSTableDecoder.Time
            if (end > TSParserThread.TS_PACKET_SIZE)
                return;

            switch (pid)
            {
                case 0x11 when table_id == 0x42:   // SDT of this transport stream
                    SetInfo(pid, TSTableDecoder.Sdt(packet, start, end));
                    break;

                case 0x10 when table_id == 0x40:   // NIT of this network
                    SetInfo(pid, TSTableDecoder.Nit(packet, start, end));
                    break;

                case 0x12 when table_id == 0x4E:   // EIT present / following of this transport stream
                    string name = TSTableDecoder.EitEvent(packet, start, end, out int section_number);
                    if (section_number == 0) _eit_now = name;
                    if (section_number == 1) _eit_next = name;
                    SetInfo(pid, (_eit_now == "" ? "" : "now: " + _eit_now) + (_eit_now != "" && _eit_next != "" ? " | " : "") + (_eit_next == "" ? "" : "next: " + _eit_next));
                    break;

                case 0x14 when table_id == 0x70 || table_id == 0x73:   // TDT / TOT
                    SetInfo(pid, TSTableDecoder.Time(packet, start, end));
                    break;
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
