using System;
using System.IO;
using System.Diagnostics;

namespace FindSimilarServices
{
    public class Debug
    {
        [Conditional("DEBUG")]
        public static void WriteLine(String l, params object[] args)
        {
            Console.WriteLine(l, args);
        }

        [Conditional("DEBUG")]
        public static void Write(String l)
        {
            Console.Write(l);
        }
    }

    public class DebugTimer
    {
        Stopwatch stopWatch;

        public void Start()
        {
            stopWatch = Stopwatch.StartNew();
        }

        public TimeSpan Stop()
        {
            stopWatch.Stop();

            // Get the elapsed time as a TimeSpan value.
            return stopWatch.Elapsed;
        }
    }
}