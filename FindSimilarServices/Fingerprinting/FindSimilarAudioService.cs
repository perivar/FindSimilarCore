using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using CommonUtils;
using CommonUtils.Audio;
using SoundFingerprinting.Audio;
using SoundFingerprinting.SoundTools.DrawningTool;
using SoundFingerprinting.Wavelets;

namespace FindSimilarServices.Audio
{
    public class FindSimilarAudioService : AudioService
    {
        /// <summary>
        /// Define how converting to mono should happen,
        /// A mix of left and right or only one of the channels?
        /// </summary>
        public enum MonoSummingType
        {
            Mix,
            Left,
            Right
        }

        private readonly IAudioSamplesNormalizer audioSamplesNormalizer;
        private readonly WdlResampler resampler;

        public override IReadOnlyCollection<string> SupportedFormats
        {
            get
            {
                return new[] { ".wav" };
            }
        }

        public FindSimilarAudioService()
        {
            audioSamplesNormalizer = new AudioSamplesNormalizer();

            // http://markheath.net/post/fully-managed-input-driven-resampling-wdl
            resampler = new WdlResampler();
            resampler.SetMode(true, 2, false);
            resampler.SetFilterParms();

            // feed mode
            // if true, that means the first parameter to ResamplePrepare 
            // will specify however much input you have, not how much you want
            resampler.SetFeedMode(true); // input driven            
        }

        private float[] ToTargetSampleRate(float[] monoSamples, int sourceSampleRate, int newSampleRate)
        {
            return Resample(monoSamples, 1, 1, sourceSampleRate, newSampleRate);
        }

        private float[] Resample(float[] audioSamples, int readerChannels, int writerChannels, int sourceSampleRate, int newSampleRate)
        {
            // make thread safe
            float[] resampledBuffer;

            lock (resampler)
            {
                // Use WDL Resampler
                // http://markheath.net/post/fully-managed-input-driven-resampling-wdl
                resampler.SetRates(sourceSampleRate, newSampleRate);

                float[] buffer = audioSamples;
                int read = audioSamples.Length;

                // resample
                int framesAvailable = read / readerChannels;
                float[] inBuffer;
                int inBufferOffset;
                int inNeeded = resampler.ResamplePrepare(framesAvailable, writerChannels, out inBuffer, out inBufferOffset);

                // prepare input buffer
                Array.Copy(buffer, 0, inBuffer, inBufferOffset, inNeeded * readerChannels);

                int inAvailable = inNeeded;
                float[] outBuffer = new float[inAvailable * writerChannels]; // originally 2000 plenty big enough
                int framesRequested = outBuffer.Length / writerChannels;
                int outAvailable = resampler.ResampleOut(outBuffer, 0, inAvailable, framesRequested, writerChannels);

                // copy to output buffer
                resampledBuffer = new float[outAvailable * writerChannels];
                Array.Copy(outBuffer, 0, resampledBuffer, 0, outAvailable * writerChannels);
            }

            return resampledBuffer;
        }

        public override float GetLengthInSeconds(string pathToSourceFile)
        {
            try
            {
                return SoundIO.ReadWaveDurationInSeconds(new BinaryFile(pathToSourceFile));
            }
            catch (System.Exception e)
            {
                throw new ArgumentException(e.Message);
            }
        }

        public override AudioSamples ReadMonoSamplesFromFile(string pathToSourceFile, int sampleRate, double seconds, double startAt)
        {
            int srcChannels = -1;
            int srcSampleCount = -1;
            int srcSampleRate = -1;
            float srcLengthInSeconds = -1;
            float[][] audioData;
            try
            {
                audioData = SoundIO.ReadWaveFile(new BinaryFile(pathToSourceFile), ref srcChannels, ref srcSampleCount, ref srcSampleRate, ref srcLengthInSeconds);
            }
            catch (System.Exception e)
            {
                throw new ArgumentException(e.Message);
            }

            // convert to mono
            var monoType = MonoSummingType.Mix;
            int samplesPerChannel = srcSampleCount;
            int channels = srcChannels;

            float[] monoSamples;
            if (srcChannels == 1)
            {
                monoSamples = audioData[0];
            }
            else if (srcChannels == 2)
            {
                // we are getting a stereo channel file back
                float sampleValueLeft = 0;
                float sampleValueRight = 0;
                float sampleValueMono = 0;

                monoSamples = new float[samplesPerChannel];
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    sampleValueLeft = audioData[0][i];
                    sampleValueRight = audioData[1][i];

                    switch (monoType)
                    {
                        case MonoSummingType.Mix:
                            // convert to mono by taking an average of the first two channels:
                            // f_mono = function(l, r) {
                            //   return (l + r) / 2;
                            //}
                            sampleValueMono = (sampleValueLeft + sampleValueRight) / 2;
                            break;
                        case MonoSummingType.Left:
                            sampleValueMono = sampleValueLeft;
                            break;
                        case MonoSummingType.Right:
                            sampleValueMono = sampleValueRight;
                            break;
                    }
                    monoSamples[i] = sampleValueMono;
                }
            }
            else
            {
                // we don't support more than 2 channels
                throw new NotSupportedException("More than 2 channels not supported");
            }

            float[] downsampled = ToTargetSampleRate(monoSamples, srcSampleRate, sampleRate);
            audioSamplesNormalizer.NormalizeInPlace(downsampled);

            // Select specific part of the song
            if ((float)(downsampled.Length) / srcSampleRate < (seconds + startAt))
            {
                // not enough samples to return the requested data
                return null;
            }

            int start = (int)((float)startAt * srcSampleRate);
            int end = (seconds <= 0) ? srcSampleRate : (int)((float)(startAt + seconds) * srcSampleRate);
            if (start != 0 || end != srcSampleRate)
            {
                var temp = new float[end - start];
                Array.Copy(downsampled, start, temp, 0, end - start);
                downsampled = temp;
            }

            return new AudioSamples(downsampled, pathToSourceFile, srcSampleRate);
        }
    }
}

