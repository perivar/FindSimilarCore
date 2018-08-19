using Microsoft.EntityFrameworkCore;
using FindSimilarServices.Fingerprinting.SQLiteDb.Models;
using SoundFingerprinting;
using SoundFingerprinting.Configuration;
using SoundFingerprinting.DAO;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.Data;
using System.Collections.Generic;
using System.Linq;
using System;
using Microsoft.Extensions.Logging;

namespace FindSimilarServices.Fingerprinting.SQLiteDb
{
    public class SQLiteDbContext : DbContext, IModelService
    {
        private readonly ILoggerFactory _loggerFactory;

        public SQLiteDbContext(DbContextOptions<SQLiteDbContext> options, ILoggerFactory loggerFactory)
            : base(options)
        {
            _loggerFactory = loggerFactory;
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder
                .UseLoggerFactory(_loggerFactory);
        }

        public DbSet<Track> Track { get; set; }
        public DbSet<SubFingerprint> SubFingerprint { get; set; }
        public DbSet<Hash> Hash { get; set; }

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

            this.SubFingerprint.AddRange(subFingerprints);
            this.SaveChanges();
        }

        public IModelReference InsertTrack(TrackData trackData)
        {
            var track = CopyToTrack(trackData);

            // insert new track object 
            this.Track.Add(track);
            this.SaveChanges();

            return trackData.TrackReference = new ModelReference<int>(track.Id);
        }

        public IList<TrackData> ReadAllTracks()
        {
            var results = this.Track.AsNoTracking().ToListAsync().GetAwaiter().GetResult();
            return results.Select(CopyToTrackData).ToList();
        }

        public IList<HashedFingerprint> ReadHashedFingerprintsByTrack(IModelReference trackReference)
        {
            throw new System.NotImplementedException();
        }

        public IList<SubFingerprintData> ReadSubFingerprints(int[] hashBins, QueryConfiguration config)
        {
            // var query = GetQueryForHashBins(hashBins);
            String statementValueTags = String.Join(",", hashBins);
            String query = $"SELECT Id, HashTable, HashBin, TrackId, SubFingerprintId FROM Hash WHERE (HashBin IN ({statementValueTags}))";

            var hashes = this.Hash.Where(i => hashBins.Contains(i.HashBin))
                .GroupBy(g => g.SubFingerprintId)
                .Select(s => new
                {
                    Key = s.Key,
                    MatchedCount = s.Count(),
                    Hashes = s.OrderBy(f => f.HashTable)
                })
                .Where(e => e.MatchedCount >= config.ThresholdVotes)
                .OrderByDescending(o => o.MatchedCount)
                .Select(s => new ModelReference<int>(s.Key))
                .ToList();

            if (!hashes.Any())
            {
                return Enumerable.Empty<SubFingerprintData>().ToList();
            }

            // get the SubFingerprintData for each of the hits
            // return ReadSubFingerprintDataByReference(hashes);
            return null;
        }

        public List<SubFingerprintData> ReadSubFingerprintDataByReference(IEnumerable<IModelReference> ids)
        {
            var listOfIds = ids.Select(i => i.Id);
            var results = this.SubFingerprint.Where(i => listOfIds.Contains(i.Id));

            if (results.Count() == 0)
            {
                return new List<SubFingerprintData>();
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
            var track = this.Track
                .Single(t => t.Id == (int)trackReference.Id);

            return CopyToTrackData(track);
        }

        public List<TrackData> ReadTracksByReferences(IEnumerable<IModelReference> ids)
        {
            throw new System.NotImplementedException();
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