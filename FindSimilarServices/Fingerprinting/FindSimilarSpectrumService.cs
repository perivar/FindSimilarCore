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
    using SoundFingerprinting.SoundTools.DrawningTool;

    internal class FindSimilarSpectrumService : ISpectrumService
    {
        private readonly IFFTService fftService;
        private readonly ILogUtility logUtility;
        private readonly Lomont.LomontFFT lomonFFT;

        internal FindSimilarSpectrumService(IFFTService fftService, ILogUtility logUtility)
        {
            this.fftService = fftService;
            this.logUtility = logUtility;

            this.lomonFFT = new Lomont.LomontFFT();
        }

        public List<SpectralImage> CreateLogSpectrogram(AudioSamples audioSamples, SpectrogramConfig configuration)
        {
            int wdftSize = configuration.WdftSize;
            int width = (audioSamples.Samples.Length - wdftSize) / configuration.Overlap;
            if (width < 1)
            {
                return new List<SpectralImage>();
            }

            double[][] frames = new double[width][];
            ushort[] logFrequenciesIndexes = logUtility.GenerateLogFrequenciesRanges(audioSamples.SampleRate, configuration);
            float[] window = configuration.Window.GetWindow(wdftSize);
            float[] samples = audioSamples.Samples;

            for (int i = 0; i < width; i++)
            {
                double[] complexSignal = new double[2 * configuration.WdftSize]; /*even - Re, odd - Img, thats how Exocortex works*/

                // take 371 ms each 11.6 ms (2048 samples each 64 samples, samplerate 5512)
                // or 256 ms each 16 ms (8192 samples each 512 samples, samplerate 32000)
                for (int j = 0; j < configuration.WdftSize; j++)
                {
                    // Weight by Window
                    complexSignal[2 * j] = window[j] * samples[(i * configuration.Overlap) + j];

                    // need to clear out as fft modifies buffer (phase)
                    complexSignal[(2 * j) + 1] = 0;
                }

                lomonFFT.TableFFT(complexSignal, true);

                frames[i] = ExtractLogBins(complexSignal, logFrequenciesIndexes, configuration.LogBins);
            }

#if DEBUG
            var imageService = new FindSimilarImageService();
            using (Image image = imageService.GetSpectrogramImage(frames, width * 20, configuration.LogBins * 30))
            {
                var fileName = Path.Combine(@"C:\Users\pnerseth\My Projects", (Path.GetFileNameWithoutExtension(audioSamples.Origin) + "_spectrogram.png"));
                if (fileName != null)
                {
                    image.Save(fileName, ImageFormat.Png);
                }
            }
#endif

            var images = CutLogarithmizedSpectrum(frames, audioSamples.SampleRate, configuration);

            WriteOutputUtils.WriteCSV(images.FirstOrDefault().Image, @"images1-new.csv");

            ScaleFullSpectrum(images, configuration);
            return images;
        }

        private void ScaleFullSpectrum(IEnumerable<SpectralImage> spectralImages, SpectrogramConfig configuration)
        {
            foreach (var image in spectralImages)
            {
                ScaleSpectrum(image, configuration.ScalingFunction);
            }
        }

        private void ScaleSpectrum(SpectralImage spectralImage, Func<float, float, float> scalingFunction)
        {
            float max = spectralImage.Image.Max(f => Math.Abs(f));

            for (int i = 0; i < spectralImage.Image.Length; ++i)
            {
                spectralImage.Image[i] = scalingFunction(spectralImage.Image[i], max);
            }
        }

        public List<SpectralImage> CutLogarithmizedSpectrum(double[][] spectrum, int sampleRate, SpectrogramConfig configuration)
        {
            var strideBetweenConsecutiveImages = configuration.Stride;
            int overlap = configuration.Overlap;
            int numberOfLogBins = configuration.LogBins;
            ushort fingerprintImageLength = configuration.ImageLength;
            var spectralImages = new List<SpectralImage>();

            int rowCount = spectrum[0].Length;
            int columnCount = spectrum.Length;
            for (int column = 0; column < columnCount; column++)
            {
                float[] spectralImage = new float[rowCount];
                for (int row = 0; row < rowCount; row++)
                {
                    spectralImage[row] = (float)spectrum[column][row];
                }

                float startsAt = column * ((float)overlap / sampleRate);
                spectralImages.Add(new SpectralImage(spectralImage, fingerprintImageLength, (ushort)numberOfLogBins, startsAt, (uint)column));
            }

            return spectralImages;
        }

        private double[] ExtractLogBins(double[] spectrum, ushort[] logFrequenciesIndex, int logBins)
        {
            int width = spectrum.Length / 2;
            double[] sumFreq = new double[logBins];
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
    }
}
