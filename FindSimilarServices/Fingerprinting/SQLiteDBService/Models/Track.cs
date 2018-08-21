namespace FindSimilarServices.Fingerprinting.SQLiteDb.Models
{
    public class Track
    {
        public int Id { get; set; }
        public string Artist { get; set; }
        public string Title { get; set; }
        public string ISRC { get; set; }
        public string Album { get; set; }
        public int ReleaseYear { get; set; }
        public double TrackLengthSec { get; set; }
    }
}