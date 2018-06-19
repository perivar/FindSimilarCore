namespace SoundFingerprinting
{
    using System.Collections.Generic;

    using SoundFingerprinting.Audio;
    using SoundFingerprinting.Configuration;
    using SoundFingerprinting.Data;

    internal interface IFingerprintService
    {
        List<HashedFingerprint> CreateFingerprints(string pathToSourceFile, AudioSamples samples, FingerprintConfiguration configuration);
    }
}