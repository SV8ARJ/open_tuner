using System;
using System.Diagnostics;
using System.Threading;
using Serilog;

namespace opentuner.Utilities
{
    // Logs a warning when the UI thread does not answer for a while, and again when it recovers.
    // Every 500 ms a small message is posted to the UI thread; how long it waits in the message queue
    // is the response time. Also logs CPU load and garbage collection time, to tell a UI thread that is
    // blocked from one that is just very busy. Create it on the UI thread.
    public class UiWatchdog : IDisposable
    {
        private const int CheckIntervalMs = 500;
        private const int StallThresholdMs = 2000;

        private readonly SynchronizationContext _ui;
        private readonly System.Threading.Timer _check;
        private readonly Process _process = Process.GetCurrentProcess();

        private long _probeSent;          // tick of the probe that is waiting for the UI thread, 0 = none
        private long _lastAnswer;         // tick of the last probe the UI thread answered
        private bool _stalled;
        private long _stallFrom;
        private double _stallGcPauseMs;

        private TimeSpan _lastCpu;
        private long _lastCpuTick;
        private double _cpuPercent;       // whole process, of all cores, since the last check

        public UiWatchdog()
        {
            _ui = SynchronizationContext.Current;
            if (_ui == null)
            {
                Log.Warning("UI watchdog: no synchronization context, watchdog not started");
                return;
            }

            _lastCpu = _process.TotalProcessorTime;
            _lastCpuTick = Environment.TickCount64;

            _check = new System.Threading.Timer(Check, null, CheckIntervalMs, CheckIntervalMs);
        }

        // runs on the UI thread
        private void Answer(object state)
        {
            Volatile.Write(ref _lastAnswer, Environment.TickCount64);
            Volatile.Write(ref _probeSent, 0);
        }

        // runs on a thread pool thread
        private void Check(object state)
        {
            long now = Environment.TickCount64;

            try
            {
                _process.Refresh();
                TimeSpan cpu = _process.TotalProcessorTime;
                long elapsed = now - _lastCpuTick;
                if (elapsed > 0)
                {
                    _cpuPercent = (cpu - _lastCpu).TotalMilliseconds / (elapsed * Environment.ProcessorCount) * 100.0;
                }
                _lastCpu = cpu;
                _lastCpuTick = now;
            }
            catch (Exception ex)
            {
                Log.Debug(ex, "UI watchdog: reading the process CPU time failed");
            }

            long sent = Volatile.Read(ref _probeSent);

            if (sent == 0)
            {
                if (_stalled)
                {
                    _stalled = false;
                    Log.Warning("UI watchdog: UI thread responding again, it was blocked for about {Ms} ms (GC pause during that time: {Gc} ms, CPU now {Cpu:F0} %)",
                        Volatile.Read(ref _lastAnswer) - _stallFrom, (int)(GC.GetTotalPauseDuration().TotalMilliseconds - _stallGcPauseMs), _cpuPercent);
                }

                Volatile.Write(ref _probeSent, now);
                _ui.Post(Answer, null);
                return;
            }

            long waiting = now - sent;
            if (waiting > StallThresholdMs && !_stalled)
            {
                _stalled = true;
                _stallFrom = sent;
                _stallGcPauseMs = GC.GetTotalPauseDuration().TotalMilliseconds;

                Log.Warning("UI watchdog: UI thread not responding for {Ms} ms (process CPU {Cpu:F0} % of all cores, {Threads} threads, {Mem} MB memory, GC {G0}/{G1}/{G2}, total GC pause {Gc} ms)",
                    waiting, _cpuPercent, _process.Threads.Count, _process.WorkingSet64 / (1024 * 1024),
                    GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), (int)_stallGcPauseMs);

                // with --breakonstall and a debugger attached, stop here to look at the UI thread
                if (opentuner.Program.BreakOnStall && Debugger.IsAttached)
                {
                    Debugger.Break();
                }
            }
        }

        public void Dispose()
        {
            _check?.Dispose();
        }
    }
}
