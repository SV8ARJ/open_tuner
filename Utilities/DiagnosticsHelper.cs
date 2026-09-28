using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Serilog;

namespace opentuner.Utilities
{
    public static class DiagnosticsHelper
    {
        [ThreadStatic]
        private static bool _inHandler;

        // Slow calls (in ms) are logged as warnings, so they show up with the default log level.
        private const int SlowCallMs = 1000;

        // Logs exceptions that would otherwise disappear (first-chance) and the ones that end the program.
        public static void Install()
        {
            AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
            {
                if (_inHandler)
                    return;

                _inHandler = true;
                try
                {
                    if (e.Exception is TimeoutException)
                    {
                        Log.Warning("First-chance TimeoutException on thread {Thread}: {Message}{NewLine}{Stack}",
                            Environment.CurrentManagedThreadId, e.Exception.Message, Environment.NewLine, Environment.StackTrace);
                    }
                    else if (e.Exception is TaskCanceledException)
                    {
                        Log.Debug("First-chance TaskCanceledException on thread {Thread}{NewLine}{Stack}",
                            Environment.CurrentManagedThreadId, Environment.NewLine, Environment.StackTrace);
                    }
                }
                finally
                {
                    _inHandler = false;
                }
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception (terminating: {Terminating})", e.IsTerminating);
                Log.CloseAndFlush();
            };
        }

        // Runs the action and only logs (as warning) when it took at least thresholdMs. For calls that run often.
        public static void MeasureSlow(string what, int thresholdMs, Action action)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                sw.Stop();
                if (sw.ElapsedMilliseconds >= thresholdMs)
                {
                    Log.Warning("{What} took {Ms} ms (thread {Thread})", what, sw.ElapsedMilliseconds, Environment.CurrentManagedThreadId);
                }
            }
        }

        // Runs the action and logs how long it took, and the exception if it fails (the exception is thrown again).
        public static void Measure(string what, Action action)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "{What} failed after {Ms} ms (thread {Thread})", what, sw.ElapsedMilliseconds, Environment.CurrentManagedThreadId);
                throw;
            }
            finally
            {
                sw.Stop();
                if (sw.ElapsedMilliseconds >= SlowCallMs)
                {
                    Log.Warning("{What} took {Ms} ms (thread {Thread})", what, sw.ElapsedMilliseconds, Environment.CurrentManagedThreadId);
                }
                else
                {
                    Log.Information("{What} took {Ms} ms (thread {Thread})", what, sw.ElapsedMilliseconds, Environment.CurrentManagedThreadId);
                }
            }
        }
    }
}
