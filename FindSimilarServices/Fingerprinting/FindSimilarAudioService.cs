using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using CommonUtils;
using CommonUtils.Audio;
using CSCore;
using CSCore.Codecs;
using SoundFingerprinting.Audio;
using SoundFingerprinting.SoundTools.DrawingTool;
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
        private readonly object _lockObj = new object();

        public override IReadOnlyCollection<string> SupportedFormats
        {
            get
            {
                return new[] { ".wav", ".aif", ".aiff", ".fla", ".flac", ".ogg" };
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
            float[] resampledBuffer;

            // make thread safe
            lock (_lockObj)
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
            float duration = 0;
            
            lock (_lockObj)
            {
                try
                {
                    using (IWaveSource soundSource = CodecFactory.Instance.GetCodec(pathToSourceFile))
                    {
                        var time = soundSource.GetLength();
                        duration = (float)time.TotalSeconds;
                    }
                }
                catch (System.Exception e)
                {
                    throw new ArgumentException(string.Format("GetLengthInSeconds failed for {0}: {1}", pathToSourceFile, e.Message));
                }
            }

            return duration;
        }

        public void ConvertToPCM16Bit(string pathToSourceFile, string pathToDestinationFile)
        {
            using (IWaveSource source = CodecFactory.Instance.GetCodec(pathToSourceFile))
            {
                using (IWaveSource destination = source.ToSampleSource().ToWaveSource(16))
                {
                    destination.WriteToFile(pathToDestinationFile);
                }
            }
        }

        public AudioSamples ReadSamplesFromFile(string pathToSourceFile)
        {
            float[] samples = new float[0];
            int srcSampleRate = 0;
            try
            {
                var soundSource = CodecFactory.Instance.GetCodec(pathToSourceFile);
                var sampleSource = soundSource.ToSampleSource();

                srcSampleRate = sampleSource.WaveFormat.SampleRate;
                int srcChannelCount = sampleSource.WaveFormat.Channels;
                float[] sampleBuffer = new float[srcSampleRate * srcChannelCount]; // 1 sec
                int read;
                var floatChannelSamples = new List<float>();
                while ((read = sampleSource.Read(sampleBuffer, 0, sampleBuffer.Length)) > 0)
                {
                    // add the number of samples we read
                    floatChannelSamples.AddRange(sampleBuffer.Take(read));
                }

                samples = floatChannelSamples.ToArray();
                sampleSource.Dispose();
                soundSource.Dispose();
            }
            catch (System.Exception e)
            {
                throw new ArgumentException(string.Format("ReadSamplesFromFile failed for {0}: {1}", pathToSourceFile, e.Message), e);
            }

            return new AudioSamples(samples, pathToSourceFile, srcSampleRate);
        }

        public override AudioSamples ReadMonoSamplesFromFile(string pathToSourceFile, int sampleRate, double seconds, double startAt)
        {
            float[] downsampled = new float[0];
            try
            {
                var soundSource = CodecFactory.Instance.GetCodec(pathToSourceFile);
                var sampleSource = soundSource.ToSampleSource();

                int srcSampleRate = sampleSource.WaveFormat.SampleRate;
                int srcChannelCount = sampleSource.WaveFormat.Channels;
                float[] sampleBuffer = new float[srcSampleRate * srcChannelCount]; // 1 sec
                int read;
                var floatChannelSamples = new List<float>();
                while ((read = sampleSource.Read(sampleBuffer, 0, sampleBuffer.Length)) > 0)
                {
                    // add the number of samples we read
                    floatChannelSamples.AddRange(sampleBuffer.Take(read));
                }

                float[] monoSamples = ToMonoSignal(floatChannelSamples.ToArray(), srcChannelCount);
                downsampled = ToTargetSampleRate(monoSamples, srcSampleRate, sampleRate);
                audioSamplesNormalizer.NormalizeInPlace(downsampled);
                CutRegion(downsampled, sampleRate, seconds, startAt);
                sampleSource.Dispose();
                soundSource.Dispose();
            }
            catch (System.Exception e)
            {
                throw new ArgumentException(string.Format("ReadSamplesFromFile failed for {0}: {1}", pathToSourceFile, e.Message), e);
            }

            return new AudioSamples(downsampled, pathToSourceFile, sampleRate);
        }

        /// <summary>
        /// Take an input signal and return the mono signal as requested using the MonoSummingType
        /// Note! Does not support more than 2 channels yet
        /// </summary>
        /// <param name="audioSamples">float array (mono or multi channel)</param>
        /// <param name="channels">number of channels</param>
        /// <param name="monoType">Define how converting to mono should happen (mix, left or right)</param>
        /// <returns></returns>
        public static float[] ToMonoSignal(float[] audioSamples, int channels, MonoSummingType monoType = MonoSummingType.Mix)
        {
            if (channels == 1)
            {
                return audioSamples;
            }
            else if (channels == 2)
            {
                // we are getting a stereo channel file back
                float sampleValueLeft = 0;
                float sampleValueRight = 0;
                float sampleValueMono = 0;

                int samplesPerChannel = (int)((double)audioSamples.Length / (double)channels);
                var channelSamples = new float[samplesPerChannel];

                for (int i = 0; i < samplesPerChannel; i++)
                {
                    sampleValueLeft = audioSamples[channels * i];
                    sampleValueRight = audioSamples[channels * i + 1];

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

                    channelSamples[i] = sampleValueMono;
                }

                return channelSamples;
            }
            else
            {
                // don't support multi channel audio files (more than 2 channels)
                throw new ArgumentException("No support for multi channel audio files (more than 2 channels)!");
            }
        }

        private void CutRegion(float[] monoAudio, int sampleRate, double seconds, double startAt)
        {
            // Select specific part of the song
            if ((float)(monoAudio.Length) / sampleRate < (seconds + startAt))
            {
                // not enough samples to return the requested data
                throw new ArgumentOutOfRangeException("Not enough samples to return the requested part of the audio-file");
            }

            int start = (int)((float)startAt * sampleRate);
            int end = (seconds <= 0) ? sampleRate : (int)((float)(startAt + seconds) * sampleRate);
            if (start != 0 || end != sampleRate)
            {
                var temp = new float[end - start];
                Array.Copy(monoAudio, start, temp, 0, end - start);
                monoAudio = temp;
            }
        }
    }
}

