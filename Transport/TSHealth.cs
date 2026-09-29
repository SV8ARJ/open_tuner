using System;

namespace opentuner
{
    // Snapshot of the TSHealth counters (issue #19).
    public struct TSHealthCounts
    {
        public uint Continuity;     // continuity counter jumped: TS packets lost
        public uint Transport;      // TEI set: packet flagged uncorrectable by the demodulator
        public uint Sync;           // TS sync (0x47) lost, bytes skipped
        public uint Scrambled;      // packets with the scrambling bits set (encrypted stream)
        public uint Overflow;       // a consumer queue was full and dropped bytes (player/recorder/UDP too slow)
    }

    // Error counters of the TS analysis (issue #19), summed over a sliding window: they show
    // problems that are happening *now* and fall back to zero on their own, instead of growing forever.
    // Written by the TS threads, read by the UI/status code, hence the lock.
    public class TSHealth
    {
        public const int WindowSeconds = 10;

        // "CC 12 TEI 1 SYNC 3 BUF 2 ENC", empty when nothing happened (same flags as the info line above the video).
        public static string Describe(TSHealthCounts h)
        {
            var parts = new System.Collections.Generic.List<string>();

            if (h.Continuity > 0) parts.Add("CC " + h.Continuity);
            if (h.Transport > 0) parts.Add("TEI " + h.Transport);
            if (h.Sync > 0) parts.Add("SYNC " + h.Sync);
            if (h.Overflow > 0) parts.Add("BUF " + h.Overflow);
            if (h.Scrambled > 0) parts.Add("ENC");

            return string.Join(" ", parts);
        }

        private const int Cc = 0, Tei = 1, Sync = 2, Scrambled = 3, Overflow = 4, Kinds = 5;

        private readonly object _lock = new object();
        private readonly long[] _bucketSecond = new long[WindowSeconds];
        private readonly uint[][] _counts = CreateCounts();

        private static uint[][] CreateCounts()
        {
            var counts = new uint[Kinds][];
            for (int k = 0; k < Kinds; k++)
                counts[k] = new uint[WindowSeconds];
            return counts;
        }

        public void AddContinuityError() { Add(Cc); }
        public void AddTransportError() { Add(Tei); }
        public void AddSyncLoss() { Add(Sync); }
        public void AddScrambled() { Add(Scrambled); }
        public void AddBufferOverflow() { Add(Overflow); }

        public TSHealthCounts Get()
        {
            long now = Environment.TickCount64 / 1000;
            var sum = new uint[Kinds];

            lock (_lock)
            {
                for (int i = 0; i < WindowSeconds; i++)
                {
                    if (now - _bucketSecond[i] < WindowSeconds)
                    {
                        for (int k = 0; k < Kinds; k++)
                            sum[k] += _counts[k][i];
                    }
                }
            }

            return new TSHealthCounts
            {
                Continuity = sum[Cc],
                Transport = sum[Tei],
                Sync = sum[Sync],
                Scrambled = sum[Scrambled],
                Overflow = sum[Overflow]
            };
        }

        public void Reset()
        {
            lock (_lock)
            {
                for (int i = 0; i < WindowSeconds; i++)
                    ClearBucket(i, 0);
            }
        }

        private void ClearBucket(int i, long second)
        {
            _bucketSecond[i] = second;
            for (int k = 0; k < Kinds; k++)
                _counts[k][i] = 0;
        }

        private void Add(int kind)
        {
            long now = Environment.TickCount64 / 1000;
            int i = (int)(now % WindowSeconds);

            lock (_lock)
            {
                if (_bucketSecond[i] != now)
                    ClearBucket(i, now);   // bucket still holds an older second: reuse it

                _counts[kind][i]++;
            }
        }
    }
}
