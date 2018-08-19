using System.Collections.Generic;

namespace FindSimilarServices.Fingerprinting.SQLiteDb.Models
{
    public class SubFingerprint
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public virtual Track Track { get; set; } // navigation property 
        public int SequenceNumber { get; set; }
        public int SequencesCount { get; set; }
        public double SequenceAt { get; set; }
        public ICollection<Hash> Hashes { get; set; } // navigation property 
    }
}