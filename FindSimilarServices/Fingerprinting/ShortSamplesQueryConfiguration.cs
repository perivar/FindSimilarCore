using System.Linq;
using SoundFingerprinting.Strides;
using SoundFingerprinting.Windows;

namespace SoundFingerprinting.Configuration
{
    public class ShortSamplesQueryConfiguration : QueryConfiguration
    {
        public ShortSamplesQueryConfiguration()
        {
            ThresholdVotes = 4; // default 4
            MaxTracksToReturn = 25; // default 25
            Clusters = Enumerable.Empty<string>();
            AllowMultipleMatchesOfTheSameTrackInQuery = false;
            FingerprintConfiguration = new ShortSamplesFingerprintConfiguration
            {
                // default = new IncrementalRandomStride(256, 512);
                // 256 / 5512 = 46 ms
                // 1486 / 32000 = 46 ms
                // 1024 / 32000 = 32 ms
                // use a 128 ms random stride instead = 4096, since every 46 ms gives way too many fingerprints to query efficiently
                Stride = new IncrementalRandomStride(8192, 16384) // Original IncrementalRandomStride(256, 512);
            };

        }
    }
}