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
using Microsoft.Data.Sqlite;

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
                var commandText = @"
                SELECT * FROM SubFingerprint, 
                    ( SELECT Id FROM 
                        (
                            SELECT Id FROM SubFingerprint WHERE HashTable0 = @HashBin0
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable1 = @HashBin1
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable2 = @HashBin2
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable3 = @HashBin3
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable4 = @HashBin4
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable5 = @HashBin5
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable6 = @HashBin6
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable7 = @HashBin7
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable8 = @HashBin8
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable9 = @HashBin9
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable10 = @HashBin10
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable11 = @HashBin11
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable12 = @HashBin12
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable13 = @HashBin13
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable14 = @HashBin14
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable15 = @HashBin15
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable16 = @HashBin16
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable17 = @HashBin17
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable18 = @HashBin18
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable19 = @HashBin19
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable20 = @HashBin20
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable21 = @HashBin21
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable22 = @HashBin22
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable23 = @HashBin23
                            UNION ALL
                            SELECT Id FROM SubFingerprint WHERE HashTable24 = @HashBin24
                        ) AS Hashes
                        GROUP BY Hashes.Id
                        HAVING COUNT(Hashes.Id) >= @Threshold
                    ) AS Thresholded
                WHERE SubFingerprint.Id = Thresholded.Id";

                var threshold = new SqliteParameter("Threshold", config.ThresholdVotes);
                var hashBins0 = new SqliteParameter("HashBin0", hashBins[0]);
                var hashBins1 = new SqliteParameter("HashBin1", hashBins[1]);
                var hashBins2 = new SqliteParameter("HashBin2", hashBins[2]);
                var hashBins3 = new SqliteParameter("HashBin3", hashBins[3]);
                var hashBins4 = new SqliteParameter("HashBin4", hashBins[4]);
                var hashBins5 = new SqliteParameter("HashBin5", hashBins[5]);
                var hashBins6 = new SqliteParameter("HashBin6", hashBins[6]);
                var hashBins7 = new SqliteParameter("HashBin7", hashBins[7]);
                var hashBins8 = new SqliteParameter("HashBin8", hashBins[8]);
                var hashBins9 = new SqliteParameter("HashBin9", hashBins[9]);
                var hashBins10 = new SqliteParameter("HashBin10", hashBins[10]);
                var hashBins11 = new SqliteParameter("HashBin11", hashBins[11]);
                var hashBins12 = new SqliteParameter("HashBin12", hashBins[12]);
                var hashBins13 = new SqliteParameter("HashBin13", hashBins[13]);
                var hashBins14 = new SqliteParameter("HashBin14", hashBins[14]);
                var hashBins15 = new SqliteParameter("HashBin15", hashBins[15]);
                var hashBins16 = new SqliteParameter("HashBin16", hashBins[16]);
                var hashBins17 = new SqliteParameter("HashBin17", hashBins[17]);
                var hashBins18 = new SqliteParameter("HashBin18", hashBins[18]);
                var hashBins19 = new SqliteParameter("HashBin19", hashBins[19]);
                var hashBins20 = new SqliteParameter("HashBin20", hashBins[20]);
                var hashBins21 = new SqliteParameter("HashBin21", hashBins[21]);
                var hashBins22 = new SqliteParameter("HashBin22", hashBins[22]);
                var hashBins23 = new SqliteParameter("HashBin23", hashBins[23]);
                var hashBins24 = new SqliteParameter("HashBin24", hashBins[24]);

                var results = _context.SubFingerprint.FromSql(commandText,
                    threshold,
                    hashBins0,
                    hashBins1,
                    hashBins2,
                    hashBins3,
                    hashBins4,
                    hashBins5,
                    hashBins6,
                    hashBins7,
                    hashBins8,
                    hashBins9,
                    hashBins10,
                    hashBins11,
                    hashBins12,
                    hashBins13,
                    hashBins14,
                    hashBins15,
                    hashBins16,
                    hashBins17,
                    hashBins18,
                    hashBins19,
                    hashBins20,
                    hashBins21,
                    hashBins22,
                    hashBins23,
                    hashBins24
                    )
                    .ToList();
 */

                var commandText = $@" 
                        SELECT * FROM 
                        (          
                            SELECT * FROM SubFingerprint WHERE HashTable0 = {hashBins[0]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable1 = {hashBins[1]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable2 = {hashBins[2]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable3 = {hashBins[3]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable4 = {hashBins[4]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable5 = {hashBins[5]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable6 = {hashBins[6]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable7 = {hashBins[7]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable8 = {hashBins[8]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable9 = {hashBins[9]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable10 = {hashBins[10]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable11 = {hashBins[11]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable12 = {hashBins[12]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable13 = {hashBins[13]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable14 = {hashBins[14]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable15 = {hashBins[15]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable16 = {hashBins[16]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable17 = {hashBins[17]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable18 = {hashBins[18]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable19 = {hashBins[19]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable20 = {hashBins[20]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable21 = {hashBins[21]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable22 = {hashBins[22]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable23 = {hashBins[23]}
                            UNION ALL
                            SELECT * FROM SubFingerprint WHERE HashTable24 = {hashBins[24]}
                        ) AS Hashes
                	GROUP BY Hashes.Id
	                HAVING COUNT(Hashes.Id) >= {config.ThresholdVotes}
                ";

                var results = _context.SubFingerprint.FromSql(commandText)
                    .Select(CopyToSubFingerprintData)
                    .ToList();

                return results;
            }
        }

        public List<SubFingerprintData> ReadSubFingerprintDataByReference(IEnumerable<int> ids)
        {
            var results = _context.SubFingerprint
                            .Where(i => ids.Contains(i.Id))
                            .Select(CopyToSubFingerprintData)
                            .ToList();

            return results;
        }

        public List<SubFingerprintData> ReadSubFingerprintDataByReference(IEnumerable<IModelReference> ids)
        {
            var listOfIds = ids.Select(i => i.Id);
            var results = _context.SubFingerprint
                            .Where(i => listOfIds.Contains(i.Id))
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
                FromHashTableElementsToHashes(subFingerprint),
                (uint)subFingerprint.SequenceNumber,
                subFingerprint.SequenceAt,
                new ModelReference<int>(subFingerprint.Id),
                new ModelReference<int>(subFingerprint.TrackId));
        }

        public static int[] FromHashTableElementsToHashes(SubFingerprint subFingerprint)
        {
            var hashBins = new int[25];
            hashBins[0] = subFingerprint.HashTable0;
            hashBins[1] = subFingerprint.HashTable1;
            hashBins[2] = subFingerprint.HashTable2;
            hashBins[3] = subFingerprint.HashTable3;
            hashBins[4] = subFingerprint.HashTable4;
            hashBins[5] = subFingerprint.HashTable5;
            hashBins[6] = subFingerprint.HashTable6;
            hashBins[7] = subFingerprint.HashTable7;
            hashBins[8] = subFingerprint.HashTable8;
            hashBins[9] = subFingerprint.HashTable9;
            hashBins[10] = subFingerprint.HashTable10;
            hashBins[11] = subFingerprint.HashTable11;
            hashBins[12] = subFingerprint.HashTable12;
            hashBins[13] = subFingerprint.HashTable13;
            hashBins[14] = subFingerprint.HashTable14;
            hashBins[15] = subFingerprint.HashTable15;
            hashBins[16] = subFingerprint.HashTable16;
            hashBins[17] = subFingerprint.HashTable17;
            hashBins[18] = subFingerprint.HashTable18;
            hashBins[19] = subFingerprint.HashTable19;
            hashBins[20] = subFingerprint.HashTable20;
            hashBins[21] = subFingerprint.HashTable21;
            hashBins[22] = subFingerprint.HashTable22;
            hashBins[23] = subFingerprint.HashTable23;
            hashBins[24] = subFingerprint.HashTable24;
            return hashBins;
        }

        public static SubFingerprint CopyToSubFingerprint(IModelReference trackReference, HashedFingerprint hash)
        {
            var subFingerprint = new SubFingerprint()
            {
                TrackId = (int)trackReference.Id,
                SequenceNumber = (int)hash.SequenceNumber,
                SequenceAt = hash.StartsAt,
            };
            SetHashTableElements(subFingerprint, hash.HashBins);

            return subFingerprint;
        }

        public static void SetHashTableElements(SubFingerprint subFingerprint, int[] hashBins)
        {
            subFingerprint.HashTable0 = hashBins[0];
            subFingerprint.HashTable1 = hashBins[1];
            subFingerprint.HashTable2 = hashBins[2];
            subFingerprint.HashTable3 = hashBins[3];
            subFingerprint.HashTable4 = hashBins[4];
            subFingerprint.HashTable5 = hashBins[5];
            subFingerprint.HashTable6 = hashBins[6];
            subFingerprint.HashTable7 = hashBins[7];
            subFingerprint.HashTable8 = hashBins[8];
            subFingerprint.HashTable9 = hashBins[9];
            subFingerprint.HashTable10 = hashBins[10];
            subFingerprint.HashTable11 = hashBins[11];
            subFingerprint.HashTable12 = hashBins[12];
            subFingerprint.HashTable13 = hashBins[13];
            subFingerprint.HashTable14 = hashBins[14];
            subFingerprint.HashTable15 = hashBins[15];
            subFingerprint.HashTable16 = hashBins[16];
            subFingerprint.HashTable17 = hashBins[17];
            subFingerprint.HashTable18 = hashBins[18];
            subFingerprint.HashTable19 = hashBins[19];
            subFingerprint.HashTable20 = hashBins[20];
            subFingerprint.HashTable21 = hashBins[21];
            subFingerprint.HashTable22 = hashBins[22];
            subFingerprint.HashTable23 = hashBins[23];
            subFingerprint.HashTable24 = hashBins[24];
        }
    }
}