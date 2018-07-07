namespace SoundFingerprinting
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;

    using SoundFingerprinting.Audio;
    using SoundFingerprinting.Configuration;
    using SoundFingerprinting.Data;
    using SoundFingerprinting.FFT;
    using SoundFingerprinting.Utils;
    using SoundFingerprinting.Wavelets;
    using SoundFingerprinting.LSH;
    using SoundFingerprinting.Math;
    using SoundFingerprinting.MinHash;
    using SoundFingerprinting.SoundTools.DrawningTool;
    using System.Drawing;
    using System.Drawing.Imaging;
    using CommonUtils;
    using System;

    internal class FindSimilarFingerprintService : IFingerprintService
    {
        private readonly ISpectrumService spectrumService;
        private readonly IWaveletDecomposition waveletDecomposition;
        private readonly IFingerprintDescriptor fingerprintDescriptor;
        private readonly ILocalitySensitiveHashingAlgorithm lshAlgorithm;

        internal FindSimilarFingerprintService(
            ISpectrumService spectrumService,
            ILocalitySensitiveHashingAlgorithm lshAlgorithm,
            IWaveletDecomposition waveletDecomposition,
            IFingerprintDescriptor fingerprintDescriptor)
        {
            this.lshAlgorithm = lshAlgorithm;
            this.spectrumService = spectrumService;
            this.waveletDecomposition = waveletDecomposition;
            this.fingerprintDescriptor = fingerprintDescriptor;
        }

        public List<HashedFingerprint> CreateFingerprints(AudioSamples samples, FingerprintConfiguration configuration)
        {
            // Explode samples to the range of 16 bit shorts (–32,768 to 32,767)
            // Matlab multiplies with 2^15 (32768)
            const int AUDIO_MULTIPLIER = 65536; // 32768 still makes alot of mfcc feature computations fail!

            // Explode samples to the range of 16 bit shorts (–32,768 to 32,767)
            // Matlab multiplies with 2^15 (32768)
            // e.g. if( max(abs(speech))<=1 ), speech = speech * 2^15; end;
            float[] audiodata = samples.Samples;
            MathUtils.Multiply(ref audiodata, AUDIO_MULTIPLIER);

            // zero pad if the audio file is too short to perform a fft
            if (audiodata.Length < (configuration.SpectrogramConfig.WdftSize + configuration.SpectrogramConfig.Overlap))
            {
                int lenNew = configuration.SpectrogramConfig.WdftSize + configuration.SpectrogramConfig.Overlap;
                Array.Resize<float>(ref audiodata, lenNew);
            }
            samples.Samples = audiodata;

            // create log spectrogram
            var spectrum = spectrumService.CreateLogSpectrogram(samples, configuration.SpectrogramConfig);

            /*             var imageService = new ImageService();
                        using (Image image = imageService.GetLogSpectralImages(spectrum, 5))
                        {
                            image.Save(pathToSourceFile + "_spectrums.png", ImageFormat.Png);
                        }
             */
            var fingerprints = CreateFingerprintsFromLogSpectrum(spectrum, configuration);

            /*             using (Image image = imageService.GetImageForFingerprints(fingerprints, 128, 32, 5))
                        {
                            image.Save(pathToSourceFile + "_fingerprints.png", ImageFormat.Png);
                        }
             */
            return HashFingerprints(fingerprints, configuration);
        }

        public List<Fingerprint> CreateFingerprintsFromLogSpectrum(IEnumerable<SpectralImage> spectralImages, FingerprintConfiguration configuration)
        {
            var fingerprints = new ConcurrentBag<Fingerprint>();
            var spectrumLength = configuration.SpectrogramConfig.ImageLength * configuration.SpectrogramConfig.LogBins;

            Parallel.ForEach(spectralImages, () => new ushort[spectrumLength], (spectralImage, loop, cachedIndexes) =>
            {
                waveletDecomposition.DecomposeImageInPlace(spectralImage.Image, spectralImage.Rows, spectralImage.Cols, configuration.HaarWaveletNorm);
                RangeUtils.PopulateIndexes(spectrumLength, cachedIndexes);
                var image = fingerprintDescriptor.ExtractTopWavelets(spectralImage.Image, configuration.TopWavelets, cachedIndexes);
                if (!image.IsSilence())
                {
                    fingerprints.Add(new Fingerprint(image, spectralImage.StartsAt, spectralImage.SequenceNumber));
                }

                return cachedIndexes;
            },
            cachedIndexes => { });

            return fingerprints.ToList();
        }

        private List<HashedFingerprint> HashFingerprints(IEnumerable<Fingerprint> fingerprints, FingerprintConfiguration configuration)
        {
            var hashedFingerprints = new ConcurrentBag<HashedFingerprint>();
            Parallel.ForEach(fingerprints, (fingerprint, state, index) =>
            {
                var hashedFingerprint = lshAlgorithm.Hash(fingerprint, configuration.HashingConfig, configuration.Clusters);
                hashedFingerprints.Add(hashedFingerprint);
            });

            return hashedFingerprints.ToList();
        }
    }
}
