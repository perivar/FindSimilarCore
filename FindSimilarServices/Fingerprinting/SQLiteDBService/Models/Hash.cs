namespace FindSimilarServices.Fingerprinting.SQLiteDb.Models
{
    public class Hash
    {
        public int Id { get; set; }
        public int HashTable { get; set; } // the index
        public long HashBin { get; set; } // the actual number
        public int TrackId { get; set; }
        public Track Track { get; set; } // navigation property 
        public int SubFingerprintId { get; set; }
        public SubFingerprint SubFingerprint { get; set; } // navigation property 
    }
}