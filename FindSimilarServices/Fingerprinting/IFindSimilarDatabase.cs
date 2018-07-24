using System.Collections.Generic;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;

namespace FindSimilarServices.Fingerprinting
{
    public interface IFindSimilarDatabase
    {
        IList<TrackData> ReadAllTracks();
        IList<TrackData> ReadAllTracks(int skip, int limit);
        IList<TrackData> ReadTracksByQuery(string query);
        TrackData ReadTrackByReference(IModelReference id);
    }
}