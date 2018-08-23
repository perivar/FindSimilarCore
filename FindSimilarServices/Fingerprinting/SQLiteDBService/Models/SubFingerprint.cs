using System.Collections.Generic;

namespace FindSimilarServices.Fingerprinting.SQLiteDb.Models
{
    public class SubFingerprint
    {
        public int Id { get; set; }
        public int TrackId { get; set; }
        public Track Track { get; set; } // navigation property 
        public int SequenceNumber { get; set; }
        public float SequenceAt { get; set; }

        // the 25 hash tables
        public int HashTable0 { get; set; }
        public int HashTable1 { get; set; }
        public int HashTable2 { get; set; }
        public int HashTable3 { get; set; }
        public int HashTable4 { get; set; }
        public int HashTable5 { get; set; }
        public int HashTable6 { get; set; }
        public int HashTable7 { get; set; }
        public int HashTable8 { get; set; }
        public int HashTable9 { get; set; }
        public int HashTable10 { get; set; }
        public int HashTable11 { get; set; }
        public int HashTable12 { get; set; }
        public int HashTable13 { get; set; }
        public int HashTable14 { get; set; }
        public int HashTable15 { get; set; }
        public int HashTable16 { get; set; }
        public int HashTable17 { get; set; }
        public int HashTable18 { get; set; }
        public int HashTable19 { get; set; }
        public int HashTable20 { get; set; }
        public int HashTable21 { get; set; }
        public int HashTable22 { get; set; }
        public int HashTable23 { get; set; }
        public int HashTable24 { get; set; }

        public string Clusters { get; set; }
    }
}