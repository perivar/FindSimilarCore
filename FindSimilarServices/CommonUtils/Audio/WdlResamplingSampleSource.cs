using System;
using System.Linq;
using CSCore;

namespace CommonUtils.Audio
{
    /// <summary>
    /// Fully managed resampling sample provider, based on the WDL Resampler
    /// Taken from the NAudio codebase
    /// </summary>
    public class WdlResamplingSampleSource : ISampleSource
    {
        private readonly WdlResampler resampler;
        private readonly AudioFormat outFormat;
        private readonly WaveFormat outWaveFormat;
        private readonly ISampleSource source;
        private readonly int channels;
        private bool disposed;

        /// <summary>
        /// Constructs a new resampler
        /// </summary>
        /// <param name="source">Source to resample</param>
        /// <param name="newSampleRate">Desired output sample rate</param>
        public WdlResamplingSampleSource(ISampleSource source, int newSampleRate)
        {
            channels = source.WaveFormat.Channels;
            outFormat = AudioFormat.CreateIeeeFloatAudioFormat(newSampleRate, channels);
            outWaveFormat = new WaveFormat(outFormat.SampleRate, outFormat.BitsPerSample, outFormat.Channels, AudioEncoding.IeeeFloat, 0);

            this.source = source;

            // http://markheath.net/post/fully-managed-input-driven-resampling-wdl
            // https://github.com/naudio/NAudio/blob/master/NAudio/Wave/SampleProviders/WdlResamplingSampleProvider.cs
            resampler = new WdlResampler();
            resampler.SetMode(true, 2, false);
            resampler.SetFilterParms();

            // feed mode
            // there are two approaches to resampling:
            //  - input driven and output driven. 
            // With input driven, every time you get new audio you give it to the resampler, 
            // and then read out what it got converted to. 
            // With output driven, you assume that the input is fully available (e.g. a file) 
            // and keep reading from the output until you get to the end.

            // if true, that means the first parameter to ResamplePrepare 
            // will specify however much input you have, not how much you want
            resampler.SetFeedMode(false); // output driven

            resampler.SetRates(source.WaveFormat.SampleRate, newSampleRate);
        }

        /// <summary>
        /// Reads from this sample provider
        /// </summary>
        public int Read(float[] buffer, int offset, int count)
        {
            float[] inBuffer;
            int inBufferOffset;
            int framesRequested = count / channels;
            int inNeeded = resampler.ResamplePrepare(framesRequested, outFormat.Channels, out inBuffer, out inBufferOffset);
            int inAvailable = source.Read(inBuffer, inBufferOffset, inNeeded * channels) / channels;
            int outAvailable = resampler.ResampleOut(buffer, offset, inAvailable, framesRequested, channels);
            return outAvailable * channels;
        }

        /// <summary>
        /// Output AudioFormat
        /// </summary>
        public AudioFormat AudioFormat
        {
            get { return outFormat; }
        }
        public WaveFormat WaveFormat
        {
            get { return outWaveFormat; }
        }

        public bool CanSeek
        {
            get { return source.CanSeek; }
        }

        public long Position
        {
            get { return source.Position; }
            set { source.Position = value; }
        }

        public long Length
        {
            get { return source.Length; }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (source != null) source.Dispose();
            }
            else
            {
                // TODO: Why does this happen?
                //throw new ObjectDisposedException("WdlResamplingSampleProvider");
            }
            disposed = true;
        }
    }
}