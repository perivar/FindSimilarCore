using System;
using CSCore;

namespace CommonUtils.Audio
{
    public static class WaveFormatExtensions
    {
        /// <summary>
        /// Convert position in seconds to byte position according to the new wave format
        /// </summary>
        /// <param name="positionInSeconds">position</param>
        /// <returns>the raw byte position</returns>
        public static long SecondsToBytes(this WaveFormat waveFormat, double positionInSeconds)
        {
            return (long)(positionInSeconds * (double)waveFormat.SampleRate * (double)waveFormat.Channels * (double)waveFormat.BytesPerSample);
        }

        /// <summary>
        /// Convert byte position to position in seconds according to the new wave format
        /// </summary>
        /// <param name="positionInBytes">the raw byte position</param>
        /// <returns>position in seconds</returns>
        public static double BytesToSeconds(this WaveFormat waveFormat, long positionInBytes)
        {
            return (double)TimeSpan.FromSeconds((double)positionInBytes / (double)waveFormat.SampleRate / (double)waveFormat.Channels / (double)waveFormat.BytesPerSample).TotalSeconds;
        }
    }
}