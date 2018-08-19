using System.Collections.Generic;

namespace FindSimilarServices.Fingerprinting.SQLiteDb.Models
{
    public class SubFingerprint
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public Track Track { get; set; } // navigation property 
        public int SequenceNumber { get; set; }
        public double SequenceAt { get; set; }
        public List<Hash> Hashes { get; set; } // navigation property 
    }
}