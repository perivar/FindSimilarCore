namespace SoundFingerprinting.FFT
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using FindSimilarServices;
    using SoundFingerprinting.Audio;
    using SoundFingerprinting.Configuration;
    using SoundFingerprinting.Data;
    using SoundFingerprinting.SoundTools.DrawingTool;
    using CommonUtils;

    internal class FindSimilarSpectrumService : ISpectrumService
    {
        private readonly ILogUtility logUtility;
        private readonly Lomont.LomontFFT lomontFFT;

        internal FindSimilarSpectrumService(SpectrogramConfig configuration, ILogUtility logUtility)
        {
            this.logUtility = logUtility;

            this.lomontFFT = new Lomont.LomontFFT();
            lomontFFT.A = 1;
            lomontFFT.B = 1;
            lomontFFT.Initialize(configuration.WdftSize);
        }

        public List<SpectralImage> CreateLogSpectrogram(AudioSamples audioSamples, SpectrogramConfig configuration)
        {
            var stopWatch = new DebugTimer();
            stopWatch.Start();

            int wdftSize = configuration.WdftSize;
            int width = (audioSamples.Samples.Length - wdftSize) / configuration.Overlap;
            if (width < 1)
            {
                return new List<SpectralImage>();
            }

            float[] frames = new float[width * configuration.LogBins];
            ushort[] logFrequenciesIndexes = logUtility.GenerateLogFrequenciesRanges(audioSamples.SampleRate, configuration);
            float[] window = configuration.Window.GetWindow(wdftSize);
            float[] samples = audioSamples.Samples;

            // PIN: reverted the following FFT to use lomontFFT with managed code (not the unsafe changed made by the original author due to the issues on my computers)

            //for (int index = 0; index < width; index++)
            Parallel.For(0, width, index =>
            {
                var fftArray = CopyAndWindow(samples, index * configuration.Overlap, window);

                lomontFFT.RealFFT(fftArray, true);

                // after the lomont realfft the fft input array will contain the FFT values
                // r0, r(n/2), r1, i1, r2, i2 ...
                // since the extract log bins method only uses lowBound index above 2 we can ignore the fact
                // that the first and second values are "special":  r0, r(n/2)
                // see https://github.com/perivar/FindSimilar/blob/6b658b1c54d1504136e25e933f39b7c303da5d9e/Mirage/Fft.cs
                ExtractLogBins(fftArray, logFrequenciesIndexes, configuration.LogBins, wdftSize, frames, index);
            }
            );

            if (configuration.Verbosity == Verbosity.Verbose)
            {
                var imageService = new FindSimilarImageService();
                using (Image image = imageService.GetSpectrogramImage(frames, width, configuration.LogBins, width, configuration.LogBins))
                {
                    var fileName = Path.Combine(SoundFingerprinter.DEBUG_PATH, (Path.GetFileNameWithoutExtension(audioSamples.Origin) + "_spectrogram.png"));
                    if (fileName != null)
                    {
                        image.Save(fileName, ImageFormat.Png);
                    }
                }

                WriteOutputUtils.WriteCSV(frames, Path.Combine(SoundFingerprinter.DEBUG_PATH, (Path.GetFileNameWithoutExtension(audioSamples.Origin) + "_frames.csv")));
            }

            var spectralImages = CutLogarithmizedSpectrum(frames, audioSamples.SampleRate, configuration);

            if (configuration.Verbosity == Verbosity.Verbose)
            {
                if (spectralImages.Count > 0)
                {
                    var spectralImageList = new List<float[]>();
                    foreach (var spectralImage in spectralImages)
                    {
                        spectralImageList.Add(spectralImage.Image);
                    }
                    var spectralImageArray = spectralImageList.ToArray();
                    WriteOutputUtils.WriteCSV(spectralImageArray, Path.Combine(SoundFingerprinter.DEBUG_PATH, (Path.GetFileNameWithoutExtension(audioSamples.Origin) + "_spectral_images.csv")), ";");
                }
            }

            ScaleFullSpectrum(spectralImages, configuration);

            if (configuration.Verbosity == Verbosity.Verbose)
            {
                Console.WriteLine("CreateLogSpectrogram - Time used: {0}", stopWatch.Stop());
            }

            return spectralImages;
        }

        private void ScaleFullSpectrum(IEnumerable<SpectralImage> spectralImages, SpectrogramConfig configuration)
        {
            // foreach (var image in spectralImages)
            Parallel.ForEach(spectralImages, image =>
            {
                ScaleSpectrum(image, configuration.ScalingFunction);
            }
            );
        }

        private void ScaleSpectrum(SpectralImage spectralImage, Func<float, float, float> scalingFunction)
        {
            float max = spectralImage.Image.Max(f => Math.Abs(f));

            for (int i = 0; i < spectralImage.Image.Length; ++i)
            {
                spectralImage.Image[i] = scalingFunction(spectralImage.Image[i], max);
            }
        }

        public List<SpectralImage> CutLogarithmizedSpectrum(float[] logarithmizedSpectrum, int sampleRate, SpectrogramConfig configuration)
        {
            var strideBetweenConsecutiveImages = configuration.Stride;
            int overlap = configuration.Overlap;
            int index = GetFrequencyIndexLocationOfAudioSamples(strideBetweenConsecutiveImages.FirstStride, overlap);
            int numberOfLogBins = configuration.LogBins;
            var spectralImages = new List<SpectralImage>();

            int width = logarithmizedSpectrum.Length / numberOfLogBins;
            ushort fingerprintImageLength = configuration.ImageLength;
            int fullLength = configuration.ImageLength * numberOfLogBins;
            uint sequenceNumber = 0;
            while (index + fingerprintImageLength <= width)
            {
                float[] spectralImage = new float[fingerprintImageLength * numberOfLogBins];
                Buffer.BlockCopy(logarithmizedSpectrum, sizeof(float) * index * numberOfLogBins, spectralImage, 0, fullLength * sizeof(float));
                float startsAt = index * ((float)overlap / sampleRate);
                spectralImages.Add(new SpectralImage(spectralImage, fingerprintImageLength, (ushort)numberOfLogBins, startsAt, sequenceNumber));
                index += fingerprintImageLength + GetFrequencyIndexLocationOfAudioSamples(strideBetweenConsecutiveImages.NextStride, overlap);
                sequenceNumber++;
            }

            // PIN: Make sure the full input spectrum is a part of the output list
            int remainingWidth = width - index;
            int remainingLength = remainingWidth * numberOfLogBins;
            if (remainingWidth > 0)
            {
                float[] spectralImage = new float[fingerprintImageLength * numberOfLogBins];
                Buffer.BlockCopy(logarithmizedSpectrum, sizeof(float) * index * numberOfLogBins, spectralImage, 0, remainingLength * sizeof(float));
                float startsAt = index * ((float)overlap / sampleRate);
                spectralImages.Add(new SpectralImage(spectralImage, fingerprintImageLength, (ushort)numberOfLogBins, startsAt, sequenceNumber));
            }

            return spectralImages;
        }

        private void ExtractLogBins(double[] spectrum, ushort[] logFrequenciesIndex, int logBins, int wdftSize, float[] targetArray, int targetIndex)
        {
            int width = wdftSize / 2; /* 1024 */
            for (int i = 0; i < logBins; i++)
            {
                int lowBound = logFrequenciesIndex[i];
                int higherBound = logFrequenciesIndex[i + 1];

                // PIN: clamp higherbound to half of the wdftsize 
                if (higherBound > width)
                {
                    higherBound = width;
                }

                for (int k = lowBound; k < higherBound; k++)
                {
                    double re = spectrum[2 * k] / width;
                    double img = spectrum[(2 * k) + 1] / width;
                    targetArray[(targetIndex * logBins) + i] += (float)((re * re) + (img * img));
                }

                targetArray[(targetIndex * logBins) + i] /= (higherBound - lowBound);
            }
        }

        private double[] ExtractLogBinsOriginal(double[] spectrum, ushort[] logFrequenciesIndex, int logBins, int wdftSize, float[] targetArray, int targetIndex)
        {
            int width = spectrum.Length / 2; /* 1024 */
            double[] sumFreq = new double[logBins]; /*32*/
            for (int i = 0; i < logBins; i++)
            {
                int lowBound = logFrequenciesIndex[i];
                int higherBound = logFrequenciesIndex[i + 1];

                for (int k = lowBound; k < higherBound; k++)
                {
                    double re = spectrum[2 * k];
                    double img = spectrum[(2 * k) + 1];

                    sumFreq[i] += Math.Sqrt(((re * re) + (img * img)) * width);
                }

                sumFreq[i] /= higherBound - lowBound;
            }

            return sumFreq;
        }

        private int GetFrequencyIndexLocationOfAudioSamples(int audioSamples, int overlap)
        {
            // There are 64 audio samples in 1 unit of spectrum due to FFT window overlap (which is 64)
            return (int)((float)audioSamples / overlap);
        }

        // copy, window and return as double array
        private double[] CopyAndWindow(float[] samples, int prefix, float[] window)
        {
            var fftArray = new double[window.Length];

            for (int j = 0; j < window.Length; ++j)
            {
                fftArray[j] = samples[prefix + j] * window[j];
            }

            return fftArray;
        }
    }
}
