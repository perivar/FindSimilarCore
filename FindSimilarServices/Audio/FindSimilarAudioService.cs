using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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

        private RiffRead preLoadedRiffData = null;

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
            resampler.SetFeedMode(true); // input driven
        }

        private float[] ToTargetSampleRate(float[] monoSamples, int sourceSampleRate, int newSampleRate)
        {
            return Resample(monoSamples, 1, 1, sourceSampleRate, newSampleRate);
        }

        private float[] Resample(float[] audioSamples, int readerChannels, int writerChannels, int sourceSampleRate, int newSampleRate)
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
            float[] resampledBuffer = new float[outAvailable * writerChannels];
            Array.Copy(outBuffer, 0, resampledBuffer, 0, outAvailable * writerChannels);

            return resampledBuffer;
        }

        public override float GetLengthInSeconds(string pathToSourceFile)
        {
            var riff = new RiffRead(pathToSourceFile);
            riff.Process();
            preLoadedRiffData = riff;
            return riff.LengthInSeconds;
        }

        public override AudioSamples ReadMonoSamplesFromFile(string pathToSourceFile, int sampleRate, double seconds, double startAt)
        {
            var monoType = MonoSummingType.Mix;

            RiffRead riff = null;
            if (preLoadedRiffData != null)
            {
                riff = preLoadedRiffData;
            }
            else
            {
                riff = new RiffRead(pathToSourceFile);
                riff.Process();
            }

            int samplesPerChannel = riff.SampleCount;
            int channels = riff.Channels;

            float[] monoSamples;
            if (channels == 1)
            {
                monoSamples = riff.SoundData[0];
            }
            else if (channels == 2)
            {
                // we are getting a stereo channel file back
                float sampleValueLeft = 0;
                float sampleValueRight = 0;
                float sampleValueMono = 0;

                monoSamples = new float[samplesPerChannel];
                for (int i = 0; i < samplesPerChannel; i++)
                {
                    sampleValueLeft = riff.SoundData[0][i];
                    sampleValueRight = riff.SoundData[1][i];

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
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    monoSamples[i] = sampleValueMono;
                }
            }
            else
            {
                // we don't support more than 2 channels
                return null;
            }

            float[] downsampled = ToTargetSampleRate(monoSamples, riff.SampleRate, sampleRate);
            audioSamplesNormalizer.NormalizeInPlace(downsampled);

            // https://github.com/AddictedCS/soundfingerprinting.soundtools/blob/master/src/SoundFingerprinting.SoundTools/DrawningTool/WinDrawningTool.cs
            ImageService imageService = new ImageService(new StandardHaarWaveletDecomposition());
            using (Image image = imageService.GetSignalImage(downsampled, 2000, 500))
            {
                image.Save(pathToSourceFile + ".png", ImageFormat.Jpeg);
            }

            return new AudioSamples(downsampled, pathToSourceFile, sampleRate);
        }

        /// <summary>
        ///   Read mono from file
        /// </summary>
        /// <param name="filename">Name of the file</param>
        /// <param name="samplerate">Sample rate</param>
        /// <param name="milliseconds">milliseconds to read</param>
        /// <param name="startmillisecond">Start millisecond</param>
        /// <returns>Array of samples</returns>
        public static float[] ReadMonoFromFile(string filename, int samplerate, int milliseconds, int startmillisecond)
        {
            int totalmilliseconds = milliseconds <= 0 ? int.MaxValue : milliseconds + startmillisecond;
            float[] data = null;

            // read as mono file
            var floatList = new List<float>();

            /*          var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(samplerate, 1);
                        SampleChannel sampleChannel = ResampleToSampleChannel(filename, waveFormat);
                        if (sampleChannel == null) return data;
             */
            int sampleCount = 0;
            int readCount = 0;
            const int bufferSize = 16 * 1024;
            var buffer = new float[bufferSize];

            /*          // read until we have read the number of samples (measured in ms) we are supposed to do
                        while ((readCount = sampleChannel.Read(buffer, 0, bufferSize)) > 0 && (float)(sampleCount) / samplerate * 1000 < totalmilliseconds)
                        {
                            floatList.AddRange(buffer.Take(readCount));

                            // increment with size of data
                            sampleCount += readCount;
                        }
             */
            data = floatList.ToArray();

            if ((float)(sampleCount) / samplerate * 1000 < (milliseconds + startmillisecond))
            {
                // not enough samples to return the requested data
                return null;
            }

            // Select specific part of the song
            int start = (int)((float)startmillisecond * samplerate / 1000);
            int end = (milliseconds <= 0) ?
                sampleCount :
                (int)((float)(startmillisecond + milliseconds) * samplerate / 1000);
            if (start != 0 || end != sampleCount)
            {
                var temp = new float[end - start];
                Array.Copy(data, start, temp, 0, end - start);
                data = temp;
            }

            return data;
        }

        /// <summary>
        /// Read an audio file into a float array as a Mono file (32-bit floating-point sample data)
        /// </summary>
        /// <param name="fileName">Fully referenced path and file name of the Wave file to create.</param>
        /// <returns>Array with mono channel data</returns>
        /// <exception cref="Exception">Thrown when an error occurs in the BASS Audio system</exception>
        public static float[] ReadMonoFromFile(string fileName)
        {
            return ReadMonoFromFile(fileName, MonoSummingType.Mix);
        }

        /// <summary>
        /// Read an audio file into a float array as a Mono file (32-bit floating-point sample data)
        /// </summary>
        /// <param name="fileName">Fully referenced path and file name of the Wave file to create.</param>
        /// <param name="monoType">Define how converting to mono should happen (mix, left or right)</param>
        /// <returns>Array with mono channel data</returns>
        /// <exception cref="Exception">Thrown when an error occurs in the BASS Audio system</exception>
        public static float[] ReadMonoFromFile(string fileName, MonoSummingType monoType)
        {

            int sampleRate = -1;
            int bitsPerSample = -1;
            long byteLength = -1;

            return ReadMonoFromFile(fileName, out sampleRate, out bitsPerSample, out byteLength, monoType);
        }

        /// <summary>
        /// Read an audio file into a float array as a Mono file (32-bit floating-point sample data)
        /// </summary>
        /// <param name="fileName">Fully referenced path and file name of the Wave file to create.</param>
        /// <param name="sampleRate">Sample rate of the wave file (e.g. 8000, 11025, 22050, 44100, 48000, 96000) in Hz.</param>
        /// <param name="bitsPerSample">Bits per sample of the wave file (must be either 8, 16, 24 or 32).</param>
        /// <param name="byteLength">Length of file in bytes</param>
        /// <param name="monoType">Define how converting to mono should happen (mix, left or right)</param>
        /// <returns>Array with mono channel data</returns>
        /// <exception cref="Exception">Thrown when an error occurs in the BASS Audio system</exception>
        public static float[] ReadMonoFromFile(string fileName, out int sampleRate, out int bitsPerSample, out long byteLength, MonoSummingType monoType)
        {

            /*             int channels = -1;
                        int channelSampleLength = -1;

                        float[] audioSamples = ReadFromFile(fileName, out sampleRate, out bitsPerSample, out channels, out byteLength, out channelSampleLength);

                        return GetMonoSignal(audioSamples, channels, monoType);
             */
            sampleRate = 0;
            bitsPerSample = 0;
            byteLength = 0;
            return new float[] { };
        }

        /// <summary>
        /// Take an input signal and return the mono signal as requested using the MonoSummingType
        /// Note! Does not support more than 2 channels yet
        /// </summary>
        /// <param name="audioSamples">float array (mono or multi channel)</param>
        /// <param name="channels">number of channels</param>
        /// <param name="monoType">Define how converting to mono should happen (mix, left or right)</param>
        /// <returns></returns>
        public static float[] GetMonoSignal(float[] audioSamples, int channels, MonoSummingType monoType)
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
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    channelSamples[i] = sampleValueMono;
                }

                return channelSamples;
            }
            else
            {
                // don't support multi channel audio files (more than 2 channels)
                return null;
            }
        }
    }
}

