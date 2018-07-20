using System;
using System.Collections.Generic;
using System.Linq;
using CommonUtils;
using LiteDB;
using SoundFingerprinting;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;

namespace SoundFingerprinting
{
    public class FindSimilarLiteDBService : IModelService
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
                    FromDictionaryToHashes(dto.Hashes),
                    (uint)dto.SequenceNumber,
                    (float)dto.SequenceAt,
                    new ModelReference<string>(dto.SubFingerprintId),
                    new ModelReference<string>(dto.TrackId));
            }
        }


        private readonly LiteDatabase db;
        public FindSimilarLiteDBService() : this(null)
        {

        }

        public FindSimilarLiteDBService(string databasePath)
        {
            if (!string.IsNullOrEmpty(databasePath))
            {
                db = new LiteDatabase(databasePath);

                try
                {

                    var mapper = BsonMapper.Global;
                    // mapper.Entity<TrackData>()
                    // .Id(x => x.ISRC); // set your POCO document Id                 

                    mapper.Entity<SubFingerprintDTO>()
                    .Id(x => x.SubFingerprintId) // set your POCO document Id                 
                                                 // .Ignore(x => x.HashBins)
                    ;
                }
                catch (System.Exception e)
                {

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
            throw new System.NotImplementedException();
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

            // Insert hashes objects (Id will be auto-incremented)
            var bson = col.InsertBulk(dtos);
        }

        public IModelReference InsertTrack(TrackData track)
        {
            var dto = TrackDataDTO.CopyToTrackDataDTO(track);

            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            // insert new track object (id will be auto-incremented)
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

        public IList<HashedFingerprint> ReadHashedFingerprintsByTrack(IModelReference trackReference)
        {
            throw new System.NotImplementedException();
        }

        public IList<SubFingerprintData> ReadSubFingerprints(int[] hashBins, QueryConfiguration config)
        {
            // [SolrField("hashTable_")]
            // public IDictionary<int, int> Hashes { get; set; }
            // var terms = hashBins.Select((hash, index) => $"hashTable_{index}:{hash}").ToList();
            // var query = string.Join(" ", terms);

            // Get fingerprint collection
            var col = db.GetCollection<SubFingerprintDTO>("fingerprints");

            // for some reason only dictionary lookup work, not the int hasbin array
            var hashes = SubFingerprintDTO.FromHashesToDictionary(hashBins);
            var results = col.Find(i => i.Hashes.Equals(hashes));

            // return the converted results from dtos to a list of SubFingerprintData
            return results.Select(SubFingerprintDTO.CopyToSubFingerprintData).ToList();
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
            // get track collection
            var col = db.GetCollection<TrackDataDTO>("tracks");

            // return by id
            var result = col.FindById(new BsonValue(trackReference.Id));
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
    }
}