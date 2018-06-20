using System.Linq;
using SoundFingerprinting.Strides;
using SoundFingerprinting.Windows;

namespace SoundFingerprinting.Configuration
{
    public class ShortSamplesQueryConfiguration : QueryConfiguration
    {
        public ShortSamplesQueryConfiguration()
        {
            ThresholdVotes = 1; // default 4
            MaxTracksToReturn = 25; // default 25
            Clusters = Enumerable.Empty<string>();
            AllowMultipleMatchesOfTheSameTrackInQuery = false;
            FingerprintConfiguration = new ShortSamplesFingerprintConfiguration
            {
                // 0,046 sec is 2028 / 44100	or 	1472/32000
                // use a 128 ms random stride instead = 4096, since every 46 ms gives way too many fingerprints to query efficiently
                Stride = new IncrementalRandomStride(1, 4096)
            };

        }
    }
}