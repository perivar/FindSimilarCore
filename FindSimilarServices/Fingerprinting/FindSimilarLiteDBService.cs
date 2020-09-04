using System;
using System.Collections.Generic;
using System.Linq;
using CommonUtils;
using LiteDB;
using Serilog;
using SoundFingerprinting;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using FindSimilarServices.Fingerprinting;

namespace SoundFingerprinting
{
    public class FindSimilarLiteDBService : IModelService, IFindSimilarDatabase
    {
        public class TrackDataDTO
        {
            public string Id { get; set; }

            public string Artist { get; set; }

            public string Title { get; set; }

            public string ISRC { get; set; }

            public string Album { get; set; }

            public int ReleaseYear { get; set; }

            public double TrackLengthSec { get; set; }

            public static TrackDataDTO CopyToTrackDataDTO(TrackData trackData)
            {
                return new TrackDataDTO()
                {
                    Id = Guid.NewGuid().ToString(),
                    ISRC = trackData.ISRC,
                    Artist = trackData.Artist,
                    Title = trackData.Title,
                    Album = trackData.Album,
                    ReleaseYear = trackData.ReleaseYear,
                    TrackLengthSec = trackData.Length
                };
            }

            public static TrackData CopyToTrackData(TrackDataDTO dto)
            {
                return new TrackData(
                    dto.ISRC,
                    dto.Artist,
                    dto.Title,
                    dto.Album,
                    dto.ReleaseYear,
                    dto.TrackLengthSec,
                    new ModelReference<string>(dto.Id));
            }
        }

        public class SubFingerprintDTO
        {
            public string SubFingerprintId { get; set; }
            public string TrackId { get; set; }
            public int SequenceNumber { get; set; }
            public double SequenceAt { get; set; }
            public int[] HashBins { get; private set; }
            public IDictionary<int, int> Hashes { get; set; }
            public IEnumerable<string> Clusters { get; set; }

            public static Dictionary<int, int> FromHashesToDictionary(int[] hashBins)
            {
                var hashTables = hashBins.Select((hash, index) => new { index, hash })
                                         .ToDictionary(x => x.index, x => x.hash);
                return hashTables;
            }

            public static int[] FromDictionaryToHashes(IDictionary<int, int> hashTables)
            {
                int[] hashBins = new int[hashTables.Count];
                foreach (var hashTable in hashTables)
                {
                    hashBins[hashTable.Key] = hashTable.Value;
                }

                return hashBins;
            }

            public static SubFingerprintDTO CopyToSubFingerprintDTO(IModelReference trackReference, HashedFingerprint hash)
            {
                return new SubFingerprintDTO()
                {
                    SubFingerprintId = Guid.NewGuid().ToString(),
                    TrackId = trackReference.Id.ToString(),
                    SequenceNumber = (int)hash.SequenceNumber,
                    SequenceAt = hash.StartsAt,
                    HashBins = hash.HashBins,
                    Hashes = FromHashesToDictionary(hash.HashBins),
                    Clusters = hash.Clusters
                };
            }

            public static SubFingerprintData CopyToSubFingerprintData(SubFingerprintDTO dto)
            {
                return new SubFingerprintData(
                    dto.HashBins,
                    (uint)dto.SequenceNumber,
                    (float)dto.SequenceAt,
                    new ModelReference<string>(dto.SubFingerprintId),
                    new ModelReference<string>(dto.TrackId));
            }
        }

        public class Hash
        {
            public int Id { get; set; }

            public int HashTable { get; set; } // the index

            public long HashBin { get; set; } // the actual number

            public string SubFingerprintId { get; set; }

            public string TrackId { get; set; }
        }


        private readonly LiteDatabase db;
        public FindSimilarLiteDBService() : this(null)
        {

        }

        public FindSimilarLiteDBService(string connectionString)
        {
            if (!string.IsNullOrEmpty(connectionString))
            {
                var databasePath = connectionString;
                // check if this is a connection string or the actual databasePath
                if (connectionString.Contains("Data Source="))
                {
                    databasePath = connectionString.Replace("Data Source=", "");
                }
                try
                {
                    db = new LiteDatabase(databasePath);

                    var mapper = BsonMapper.Global;
                    mapper.Entity<SubFingerprintDTO>()
                    .Id(x => x.SubFingerprintId) // set your POCO document Id                 
                    // .Ignore(x => x.HashBins) // don't use the hashbin element,rather a separate table
                    // .Ignore(x => x.Hashes) // don't use the hashes dictionary
                    ;
                }
                catch (System.Exception e)
                {
                    Log.Fatal("Issues with LiteDatabase: '{0}' [{1}]", connectionString, e);
                }
            }
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
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            // return by isrc
            var result = col.FindOne(x => x.ISRC == isrc && x.Artist == artist && x.Title == title);
            return result != null ? true : false;
        }

        public int DeleteTrack(IModelReference trackReference)
        {
            throw new System.NotImplementedException();
        }

        public void InsertHashDataForTrack(IEnumerable<HashedFingerprint> hashes, IModelReference trackReference)
        {
            var dtos = hashes.Select(hash => SubFingerprintDTO.CopyToSubFingerprintDTO(trackReference, hash))
                .ToList();

            // Get fingerprint collection
            var col = db.GetCollection<SubFingerprintDTO>("fingerprints");

            // insert hashes objects 
            // Note! If we are using a separate table for 
            // the hashtables we should ignore the HashBin property on the SubFingerprintDTO 
            // to avoid duplicating the information and making the 
            // database larger than neccesary
            // e.g. var mapper = BsonMapper.Global.Entity<SubFingerprintDTO>().Ignore(x => x.HashBins); // don't use the hashbin element,rather a separate table
            var bson = col.InsertBulk(dtos);

            // then insert the HashBin property into a separate table 
            // note: this makes the database significantly larger
            foreach (var dto in dtos)
            {
                // insert each hash as a separate object
                InsertHashBinsToSeparateTable(dto.HashBins, dto.SubFingerprintId, (string)trackReference.Id);
            }
        }

        public IModelReference InsertTrack(TrackData track)
        {
            var dto = TrackDataDTO.CopyToTrackDataDTO(track);

            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            // insert new track object 
            var bson = col.Insert(dto);

            var trackReference = new ModelReference<string>(bson);
            return track.TrackReference = trackReference;
        }

        public IList<TrackData> ReadAllTracks()
        {
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");
            var results = col.FindAll();

            if (results.Count() == 0)
            {
                return new List<TrackData>();
            }

            return results.Select(TrackDataDTO.CopyToTrackData).ToList();
        }

        public IEnumerable<string> ReadAllTrackFilePaths()
        {
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");
            var results = col.Find(LiteDB.Query.All()).Select(x => x.Title);
            return results;
        }

        public IList<TrackData> ReadAllTracks(int skip, int limit)
        {
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            // get last added X objects of the collection
            var results = col.Find(LiteDB.Query.All(LiteDB.Query.Descending), skip, limit);

            if (results.Count() == 0)
            {
                return new List<TrackData>();
            }

            return results.Select(TrackDataDTO.CopyToTrackData).ToList();
        }

        public IList<TrackData> ReadTracksByQuery(string query)
        {
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            var results = col.Find(x => x.Title.Contains(query));

            if (results.Count() == 0)
            {
                return new List<TrackData>();
            }

            return results.Select(TrackDataDTO.CopyToTrackData).ToList();
        }


        public IList<HashedFingerprint> ReadHashedFingerprintsByTrack(IModelReference trackReference)
        {
            throw new System.NotImplementedException();
        }

        public IList<SubFingerprintData> ReadSubFingerprints(int[] hashBins, QueryConfiguration config)
        {
            // return unique matches
            // return ReadSubFingerprintDataByUniqueMatches(hashBins);

            // return matches using the threshold, ignoring the hashtable position
            // return ReadSubFingerprintDataByHashBucketsWithThreshold(hashBins, config.ThresholdVotes);

            // return matches using the threshold, ignoring or using the hashtable position
            bool ignoreHashTableIndex = true;
            return ReadSubFingerprintDataByHashBucketsWithThresholdSeparateTable(hashBins, config.ThresholdVotes, ignoreHashTableIndex);
        }

        public ISet<SubFingerprintData> ReadSubFingerprints(IEnumerable<int[]> hashes, QueryConfiguration config)
        {
            throw new System.NotImplementedException();
        }

        public IList<TrackData> ReadTrackByArtistAndTitleName(string artist, string title)
        {
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            var results = col.Find(x => x.Artist.Contains(artist) && x.Title.Contains(title));

            if (results.Count() == 0)
            {
                return new List<TrackData>();
            }

            return results.Select(TrackDataDTO.CopyToTrackData).ToList();
        }

        public TrackData ReadTrackByISRC(string isrc)
        {
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            // return by isrc
            var result = col.FindOne(x => x.ISRC == isrc);
            return TrackDataDTO.CopyToTrackData(result);
        }

        public TrackData ReadTrackByReference(IModelReference id)
        {
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            // return by id
            var result = col.FindById(new BsonValue(id.Id));
            return TrackDataDTO.CopyToTrackData(result);
        }

        public List<TrackData> ReadTracksByReferences(IEnumerable<IModelReference> ids)
        {
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            // two alternative methods
            // 1. use Query.In
            var bsonArray = new LiteDB.BsonArray(ids.Select(x => new BsonValue(x.Id)));
            var results = col.Find(LiteDB.Query.In("_id", bsonArray));

            // 2. Use Find and Linq Contains            
            // var results = col.Find(x => ids.Contains(new ModelReference<string>(x.Id)));

            if (results.Count() == 0)
            {
                return new List<TrackData>();
            }

            return results.Select(TrackDataDTO.CopyToTrackData).ToList();
        }

        public IList<SubFingerprintData> ReadSubFingerprintDataByUniqueMatches(int[] hashBins)
        {
            // Get fingerprint collection
            var col = db.GetCollection<SubFingerprintDTO>("fingerprints");

            // for some reason only dictionary lookup work, not the int hasbin array
            // this works:
            // var hashes = SubFingerprintDTO.FromHashesToDictionary(hashBins);
            // var results = col.Find(i => i.Hashes.Equals(hashes));

            // this doesn't work:
            // var results = col.Find(i => i.HashBins.Equals(hashBins));

            // this works:
            var results = col.Find(GetQueryForHashBinsUnique(hashBins));

            // return the converted results from dtos to a list of SubFingerprintData
            return results.Select(SubFingerprintDTO.CopyToSubFingerprintData).ToList();
        }

        public IList<SubFingerprintData> ReadSubFingerprintDataByHashBucketsWithThreshold(int[] hashBins, int thresholdVotes)
        {
            // Get fingerprint collection
            var col = db.GetCollection<SubFingerprintDTO>("fingerprints");

            // ensure that the hasbins can be searched for by value
            // had to disable: 04.09.2020 - check https://github.com/mbdavid/LiteDB/wiki/Indexes#expressions
            //col.EnsureIndex(x => x.HashBins, "$.HashBins[*]");
            col.EnsureIndex(x => x.HashBins);

            // don't care about the actual index of the hasbin, only if it exists
            var query = GetQueryForHashBinsIgnoreIndex(hashBins, false);
            var fingerprints = col.Find(query);

            // use linq to filter again since LiteDB doesn't support this directly
            // see https://stackoverflow.com/questions/31629937/match-to-most-match-words-with-linq
            var results = fingerprints.Select(fingerprint => new
            {
                Fingerprint = fingerprint,
                MatchedCount = hashBins.Count(hashBin => fingerprint.HashBins.Contains(hashBin))
            })
                .Where(e => e.MatchedCount >= thresholdVotes)
                .OrderByDescending(o => o.MatchedCount)
                .Select(s => s.Fingerprint);

            // return the converted results from dtos to a list of SubFingerprintData
            return results.Select(SubFingerprintDTO.CopyToSubFingerprintData).ToList();
        }

        private void InsertHashBinsToSeparateTable(int[] hashBins, string subFingerprintId, string trackId)
        {
            // https://github.com/AddictedCS/soundfingerprinting.mongodb/blob/7287083b3c6cc06ac59b8eb18bc6796411226246/src/SoundFingerprinting.MongoDb/HashBinDao.cs
            var hashes = new List<Hash>();
            for (int hashtable = 1; hashtable <= hashBins.Length; hashtable++)
            {
                var hash = new Hash
                {
                    // Id = Guid.NewGuid().ToString(), // auto-increment instead
                    HashTable = hashtable,
                    HashBin = hashBins[hashtable - 1],
                    SubFingerprintId = subFingerprintId,
                    TrackId = trackId
                };
                hashes.Add(hash);
            }

            // Get hash collection
            var col = db.GetCollection<Hash>("hashes");

            // Insert hashes objects 
            var bson = col.InsertBulk(hashes);
        }

        // build a query for the document hash array that ensures 
        // a unique match (all hashbins exist)
        private LiteDB.BsonExpression GetQueryForHashBinsUnique(int[] hashBins)
        {
            var queries = new List<LiteDB.BsonExpression>();
            for (int hashtable = 0; hashtable < hashBins.Length; hashtable++)
            {
                var hashBinAreEqual = LiteDB.Query.EQ("HashBins", hashBins[hashtable]);
                queries.Add(hashBinAreEqual);
            }

            return LiteDB.Query.And(queries.ToArray());
        }

        // build a query for the separate hashes table that cares about
        // the actual index of the hashbin
        private LiteDB.BsonExpression GetQueryForHashBins(int[] hashBins)
        {
            // ensure we care about the actual index of the hashbin
            // See https://github.com/AddictedCS/soundfingerprinting.mongodb/blob/release/2.3.x/src/SoundFingerprinting.MongoDb/HashBinDao.cs
            var queries = new List<LiteDB.BsonExpression>();
            for (int hashtable = 1; hashtable <= hashBins.Length; hashtable++)
            {
                var hashTableAndHashBinAreEqual = LiteDB.Query.And(
                    LiteDB.Query.EQ("HashTable", hashtable), LiteDB.Query.EQ("HashBin", hashBins[hashtable - 1]));
                queries.Add(hashTableAndHashBinAreEqual);
            }

            return LiteDB.Query.Or(queries.ToArray());
        }

        // build a query for the separate hashes-table, or the document hash array that ignores
        // the actual index of the hashbin
        private LiteDB.BsonExpression GetQueryForHashBinsIgnoreIndex(int[] hashBins, bool useSeparateTable)
        {
            // don't care about the actual index of the hasbin, only if it exists
            // See https://github.com/perivar/FindSimilar2/blob/master/Soundfingerprinting/DatabaseService.cs\
            var bsonArray = new LiteDB.BsonArray(hashBins.Select(x => new BsonValue(x)));

            if (useSeparateTable)
            {
                return LiteDB.Query.In("HashBin", bsonArray);
            }
            else
            {
                // use the hashbin array on the main document
                return LiteDB.Query.In("HashBins[*]", bsonArray);
            }
        }

        // read subfingerprints using the separate hashes table
        public IList<SubFingerprintData> ReadSubFingerprintDataByHashBucketsWithThresholdSeparateTable(int[] hashBins, int thresholdVotes, bool ignoreHashTableIndex)
        {
            // check IEnumerable<SubFingerprintData> ReadSubFingerprintDataByHashBucketsWithThreshold(long[] hashBins, int thresholdVotes)
            // https://github.com/AddictedCS/soundfingerprinting.mongodb/blob/release/2.3.x/src/SoundFingerprinting.MongoDb/HashBinDao.cs

            LiteDB.BsonExpression query = null;
            if (ignoreHashTableIndex)
            {
                query = GetQueryForHashBinsIgnoreIndex(hashBins, true);
            }
            else
            {
                query = GetQueryForHashBins(hashBins);
            }

            // Get hash collection
            var col = db.GetCollection<Hash>("hashes");

            // ensure indexes
            col.EnsureIndex(x => x.HashBin);
            col.EnsureIndex(x => x.HashTable);

            // find the subfingerprints that have more than the threshold number
            // of hashes that belong to that subfingerprint  
            var hashes = col.Find(query)
                .GroupBy(g => g.SubFingerprintId)
                .Select(s => new
                {
                    Key = s.Key,
                    MatchedCount = s.Count(),
                    Hashes = s.OrderBy(f => f.HashTable)
                })
                .Where(e => e.MatchedCount >= thresholdVotes)
                .OrderByDescending(o => o.MatchedCount)
                .Select(s => new ModelReference<string>(s.Key))
                .ToList();

            if (!hashes.Any())
            {
                return Enumerable.Empty<SubFingerprintData>().ToList();
            }

            // get the SubFingerprintData for each of the hits
            return ReadSubFingerprintDataByReference(hashes);
        }

        public SubFingerprintData ReadSubFingerprintDataByReference(IModelReference id)
        {
            // Get fingerprint collection
            var col = db.GetCollection<SubFingerprintDTO>("fingerprints");

            // ensure indexes
            col.EnsureIndex(x => x.SubFingerprintId);

            // return by id
            var result = col.FindById(new BsonValue(id.Id));
            return SubFingerprintDTO.CopyToSubFingerprintData(result);
        }

        public List<SubFingerprintData> ReadSubFingerprintDataByReference(IEnumerable<IModelReference> ids)
        {
            // Get fingerprint collection
            var col = db.GetCollection<SubFingerprintDTO>("fingerprints");

            // ensure indexes
            col.EnsureIndex(x => x.SubFingerprintId);

            // two alternative methods
            // 1. use Query.In
            var bsonArray = new LiteDB.BsonArray(ids.Select(x => new BsonValue(x.Id)));
            var results = col.Find(LiteDB.Query.In("_id", bsonArray));

            // 2. Use Find and Linq Contains            
            // var results = col.Find(x => ids.Contains(new ModelReference<string>(x.Id)));

            if (results.Count() == 0)
            {
                return new List<SubFingerprintData>();
            }

            return results.Select(SubFingerprintDTO.CopyToSubFingerprintData).ToList();
        }
    }
}