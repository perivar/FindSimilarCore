using System.Collections.Generic;
using SoundFingerprinting.Query;

namespace FindSimilarServices
{
    public interface ISoundFingerprinter
    {
          void FingerprintDirectory(string directoryPath, double skipDurationAboveSeconds, Verbosity verbosity);

          IEnumerable<ResultEntry> GetBestMatchesForSong(string queryAudioFile, int thresholdVotes, int maxTracksToReturn, Verbosity verbosity);
    }
}