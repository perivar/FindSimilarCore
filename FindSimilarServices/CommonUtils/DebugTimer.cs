using System;
using System.IO;
using System.Diagnostics;

namespace CommonUtils
{
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