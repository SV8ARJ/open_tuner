using System;
using System.Diagnostics;
using System.Threading;

namespace opentuner.Utilities
{
    // Counts the FTDI I2C transfers and the time they take, to see where a slow NIM status cycle spends its time.
    public static class I2cStats
    {
        private static long _count;
        private static long _ticks;

        public static void Add(long startTimestamp)
        {
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _ticks, Stopwatch.GetTimestamp() - startTimestamp);
        }

        public static (long count, long ms) Snapshot()
        {
            return (Interlocked.Read(ref _count), Interlocked.Read(ref _ticks) * 1000 / Stopwatch.Frequency);
        }
    }
}
