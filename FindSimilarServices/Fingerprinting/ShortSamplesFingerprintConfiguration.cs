using System.Linq;
using SoundFingerprinting.Strides;
using SoundFingerprinting.Windows;

namespace SoundFingerprinting.Configuration
{
    public class ShortSamplesFingerprintConfiguration : FingerprintConfiguration
    {
        public ShortSamplesFingerprintConfiguration()
        {
            SpectrogramConfig = new ShortSamplesSpectrogramConfig();
            HashingConfig = new DefaultHashingConfig();
            TopWavelets = 200;

            // Using 32000 (instead of 44100) gives us a max of 16 khz resolution, which is OK for normal adult human hearing
            SampleRate = 32000; 	// Original: 5512

            HaarWaveletNorm = System.Math.Sqrt(2);
            Clusters = Enumerable.Empty<string>();
        }
    }

    internal class ShortSamplesSpectrogramConfig : SpectrogramConfig
    {
        public ShortSamplesSpectrogramConfig()
        {
            // 11,6 ms	is 	64/5512		or	512/44100	or 372/32000
            // The closest power of 2 in 2's complement format:
            // 1024 / 32000 = 32 ms
            // 512 	/ 32000 = 16 ms
            // 256 	/ 32000 = 8 ms
            Overlap = 1024; // Original 64

            // 371 ms 	is	2048/5512	or 	16384/44100	or 11889/32000
            // The closest power of 2 in 2's complement format:
            // 8192 / 32000 = 256 ms
            // 4096 / 32000 = 128 ms
            // 2048 / 32000 = 64 ms
            // Due to using this on many small samples, we need to reduce the window and overlap sizes
            // A transient is around 50 ms
            WdftSize = 2048;// 4096; // Original 2048

            FrequencyRange = new FrequencyRange(40, 16000); // Original FrequencyRange(318, 2000);

            LogBase = System.Math.E; // Original 2
            LogBins = 32;
            ImageLength = 128; // Original 128

            // calculate samples per fingerprint
            //int samplesPerFingerprint = ImageLength * Overlap;

            UseDynamicLogBase = false; // Original true            

            // 0,928 sec is	5115 / 5512 or 40924 / 44100	or	29695/32000
            //Stride = new IncrementalStaticStride(29695, 0, samplesPerFingerprint); // Original IncrementalStaticStride(512);
            // a static stride of 0 means no gaps between consecutive images (fingerprints)
            Stride = new StaticStride(0); // Original IncrementalStaticStride(512);            

            Window = new HanningWindow();
            ScalingFunction = (value, max) =>
            {
                float scaled = System.Math.Min(value / max, 1);
                int domain = 255;
                float c = (float)(1 / System.Math.Log(1 + domain));
                return (float)(c * System.Math.Log(1 + scaled * domain));
            };
        }
    }
}