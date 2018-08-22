using System.Collections.Generic;
using SoundFingerprinting;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using FindSimilarServices.Fingerprinting.SQLiteDb;
using System.Linq;
using FindSimilarServices.Fingerprinting.SQLiteDb.Models;
using Microsoft.EntityFrameworkCore;

namespace FindSimilarServices.Fingerprinting.SQLiteDBService
{
    public class FindSimilarSQLiteService : IModelService, IFindSimilarDatabase
    {
        private readonly SQLiteDbContext _context;

        public FindSimilarSQLiteService(SQLiteDbContext context)
        {
            _context = context;
        }

        public bool SupportsBatchedSubFingerprintQuery
        {
            get
            {
                return false;
            }
        }

        public bool ContainsTrack(string isrc, string artist, string title)
        {
            throw new System.NotImplementedException();
        }

        public int DeleteTrack(IModelReference trackReference)
        {
            throw new System.NotImplementedException();
        }

        public void InsertHashDataForTrack(IEnumerable<HashedFingerprint> hashes, IModelReference trackReference)
        {
            var subFingerprints = hashes.Select(hash => CopyToSubFingerprint(trackReference, hash))
                .ToList();

            _context.SubFingerprint.AddRange(subFingerprints);
            _context.SaveChanges();
        }

        public IModelReference InsertTrack(TrackData trackData)
        {
            var track = CopyToTrack(trackData);

            // insert new track object 
            _context.Track.Add(track);
            _context.SaveChanges();

            return trackData.TrackReference = new ModelReference<int>(track.Id);
        }

        public IList<TrackData> ReadAllTracks()
        {
            return _context.Track
                    .Select(CopyToTrackData)
                    .ToList();
        }

        public IList<TrackData> ReadAllTracks(int skip, int limit)
        {
            return _context.Track
                    .Skip(skip)
                    .Take(limit)
                    .Select(CopyToTrackData)
                    .ToList();
        }

        public IList<TrackData> ReadTracksByQuery(string query)
        {
            return _context.Track
                    .Where(q => q.Title.Contains(query))
                    .Select(CopyToTrackData)
                    .ToList();
        }

        public IList<HashedFingerprint> ReadHashedFingerprintsByTrack(IModelReference trackReference)
        {
            throw new System.NotImplementedException();
        }

        public IList<SubFingerprintData> ReadSubFingerprints(int[] hashBins, QueryConfiguration config)
        {
            using (new DebugTimer("ReadSubFingerprints()"))
            {
                /*
                                // this whole query gets compiled to SQL and 
                                // returns the ids as ModelReferences
                                var hashes = _context.Hash
                                    .Where(i => hashBins.Contains(i.HashBin))
                                    .GroupBy(g => g.SubFingerprintId)
                                    .Select(s => new
                                    {
                                        SubFingerprintId = s.Key,
                                        MatchedCount = s.Count(),
                                        Hashes = s.OrderBy(f => f.HashTable)
                                    })
                                    .Where(e => e.MatchedCount >= config.ThresholdVotes)
                                    .OrderByDescending(o => o.MatchedCount)
                                    .Select(s => s.SubFingerprintId)
                                    .ToList();

                                if (!hashes.Any())
                                {
                                    return Enumerable.Empty<SubFingerprintData>().ToList();
                                }

                                // get the SubFingerprintData for each of the hits
                                return ReadSubFingerprintDataByReference(hashes);
                 */

                var subFingerprintQuery = from h in _context.Hash
                                          join s in _context.SubFingerprint on h.SubFingerprintId equals s.Id
                                          join m in _context.Hash on s.Id equals m.SubFingerprintId
                                          where hashBins.Contains(h.HashBin)
                                          group h by new { h.SubFingerprintId, s.TrackId, s.SequenceAt, s.SequenceNumber, m.HashTable, m.HashBin } into g
                                          where g.Count() >= config.ThresholdVotes
                                          orderby g.Count() descending
                                          select new
                                          {
                                              SubFingerprintId = g.Key.SubFingerprintId,
                                              TrackId = g.Key.TrackId,
                                              SequenceAt = g.Key.SequenceAt,
                                              SequenceNumber = g.Key.SequenceNumber,
                                              HashTable = g.Key.HashTable,
                                              HashBin = g.Key.HashBin,
                                              Count = g.Count()
                                          };

                // the above query returns a flattened list where 
                // all parameters except HashTable and HashBin is repeated 
                // a number of times (25)
                var lastId = 0;
                var lastIndex = -1;
                var hashTableSize = config.FingerprintConfiguration.HashingConfig.NumberOfLSHTables;
                var subFingerprintDataList = new List<SubFingerprintData>();
                foreach (var element in subFingerprintQuery)
                {
                    if (element.SubFingerprintId == lastId)
                    {
                        var hashes = subFingerprintDataList[lastIndex].Hashes[element.HashTable] = element.HashBin;
                    }
                    else
                    {
                        // new element
                        var subFingerprintData = new SubFingerprintData();
                        subFingerprintData.SubFingerprintReference = new ModelReference<int>(element.SubFingerprintId);
                        subFingerprintData.TrackReference = new ModelReference<int>(element.TrackId);
                        subFingerprintData.SequenceAt = (float)element.SequenceAt;
                        subFingerprintData.SequenceNumber = (uint)element.SequenceNumber;
                        subFingerprintData.Hashes = new int[hashTableSize]; 
                        subFingerprintData.Hashes[element.HashTable] = element.HashBin;
                        subFingerprintDataList.Add(subFingerprintData);
                        lastId = element.SubFingerprintId;
                        lastIndex++;
                    }
                }

                return subFingerprintDataList;
            }
        }

        public List<SubFingerprintData> ReadSubFingerprintDataByReference(IEnumerable<int> ids)
        {
            var results = _context.SubFingerprint
                            .Where(i => ids.Contains(i.Id))
                            .Include(h => h.Hashes)
                            .Select(CopyToSubFingerprintData)
                            .ToList();

            return results;
        }

        public List<SubFingerprintData> ReadSubFingerprintDataByReference(IEnumerable<IModelReference> ids)
        {
            var listOfIds = ids.Select(i => i.Id);
            var results = _context.SubFingerprint
                            .Where(i => listOfIds.Contains(i.Id))
                            .Include(h => h.Hashes)
                            // .Include(t => t.Track) // don't need to include the track since it will be stripped away in SubFingerprintData anyway
                            ;

            if (!results.Any())
            {
                return Enumerable.Empty<SubFingerprintData>().ToList();
            }

            return results.Select(CopyToSubFingerprintData).ToList();
        }

        public ISet<SubFingerprintData> ReadSubFingerprints(IEnumerable<int[]> hashes, QueryConfiguration config)
        {
            throw new System.NotImplementedException();
        }

        public IList<TrackData> ReadTrackByArtistAndTitleName(string artist, string title)
        {
            throw new System.NotImplementedException();
        }

        public TrackData ReadTrackByISRC(string isrc)
        {
            throw new System.NotImplementedException();
        }

        public TrackData ReadTrackByReference(IModelReference trackReference)
        {
            var track = _context.Track
                .Single(t => t.Id == (int)trackReference.Id);

            return CopyToTrackData(track);
        }

        public List<TrackData> ReadTracksByReferences(IEnumerable<IModelReference> ids)
        {
            var listOfIds = ids.Select(i => i.Id);
            var results = _context.Track
                            .Where(i => listOfIds.Contains(i.Id));

            if (!results.Any())
            {
                return Enumerable.Empty<TrackData>().ToList();
            }

            return results.Select(CopyToTrackData).ToList();
        }

        public static Track CopyToTrack(TrackData trackData)
        {
            return new Track()
            {
                ISRC = trackData.ISRC,
                Artist = trackData.Artist,
                Title = trackData.Title,
                Album = trackData.Album,
                ReleaseYear = trackData.ReleaseYear,
                TrackLengthSec = trackData.Length
            };
        }

        public static TrackData CopyToTrackData(Track track)
        {
            return new TrackData(
                track.ISRC,
                track.Artist,
                track.Title,
                track.Album,
                track.ReleaseYear,
                track.TrackLengthSec,
                new ModelReference<int>(track.Id));
        }

        public static SubFingerprintData CopyToSubFingerprintData(SubFingerprint subFingerprint)
        {
            return new SubFingerprintData(
                FromHashListToHashes(subFingerprint.Hashes),
                (uint)subFingerprint.SequenceNumber,
                (float)subFingerprint.SequenceAt,
                new ModelReference<int>(subFingerprint.Id),
                new ModelReference<int>(subFingerprint.TrackId));
        }

        public static int[] FromHashListToHashes(List<Hash> hashes)
        {
            return hashes.Select(hash => hash.HashBin).ToArray();
        }

        public static SubFingerprint CopyToSubFingerprint(IModelReference trackReference, HashedFingerprint hash)
        {
            return new SubFingerprint()
            {
                TrackId = (int)trackReference.Id,
                SequenceNumber = (int)hash.SequenceNumber,
                SequenceAt = hash.StartsAt,
                Hashes = FromHashesToHashList(trackReference, hash.HashBins),
            };
        }

        public static List<Hash> FromHashesToHashList(IModelReference trackReference, int[] hashBins)
        {
            return hashBins.Select((hash, index) => new Hash { HashBin = hash, HashTable = index, TrackId = (int)trackReference.Id }).ToList();
        }
    }
}