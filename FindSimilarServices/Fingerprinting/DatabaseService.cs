using System;
using System.Data;
using Microsoft.Data.Sqlite;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Configuration; // ConfigurationManager
using FindSimilarServices.Fingerprinting.Dao.Entities;
using FindSimilarServices.Fingerprinting.DbStorage.Entities;

namespace FindSimilarServices.Fingerprinting.DbStorage
{
    // SQL Lite database class
    // Original idea/class from Soundfingerprinting Project
    // Heavily modified by perivar@nerseth.com
    public static class DatabaseService
    {
        // private variables
        private static string _connection = string.Empty;
        private static string _SqliteDbPath = string.Empty;

        // key name in app.config
        private const string SqliteDbPathKeyName = "Sqlitedbpath";

        // how to increase Sqlite performance
        // http://stackoverflow.com/questions/4356363/Sqlite-net-performance-how-to-speed-up-things
        // http://www.codeproject.com/Articles/853842/Csharp-Avoiding-Performance-Issues-with-Inserts-in

        #region SqliteConnection Methods
        /// <summary>
        /// Database path can be set from config otherwise application uses the defaultpath
        /// </summary>
        /// <returns>connection string</returns>
        /// <remarks>The idea for a static database class comes from
        ///  Sqlite.DataAccessLayer SqliteDal.cs (http://mycodefunda.blogspot.com/)</remarks>
        private static string GetConnectionString()
        {
            if (!string.IsNullOrEmpty(_connection)) return _connection;

            // ensure that the _SqliteDbPath is defined
            InitializeSqliteDBPath();

            bool doResetDatabase = false;
            if (!File.Exists(_SqliteDbPath))
            {
                CreateDB(_SqliteDbPath);
                doResetDatabase = true;
            }

            // store the connection string
            _connection = GetSqliteConnectionString(_SqliteDbPath);

            if (doResetDatabase)
            {
                AddDatabaseTables();
            }

            return _connection;
        }

        private static void InitializeSqliteDBPath()
        {
            if (!string.IsNullOrEmpty(_SqliteDbPath)) return;

            // http://www.nikola-breznjak.com/blog/c/how-to-use-app-config-in-visual-studio-c-net/
            //if (ConfigurationManager.AppSettings[SqliteDbPathKeyName] != null)
            //{
            //    _SqliteDbPath = ConfigurationManager.AppSettings[SqliteDbPathKeyName];
            // }
            //else
            {
                // use default directory
                // SqliteDbPath = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName,"DataBase","Sqlite.db");

                // store in the users home directory
                string homedir = Environment.GetFolderPath(Environment.SpecialFolder.Personal);
                string dbdir = Path.Combine(homedir, ".findsimilar");

                // set the db file path
                _SqliteDbPath = Path.Combine(dbdir, "findsimilar.db");
            }
        }

        private static string GetSqliteConnectionString(string dbFilePath)
        {

            var connBuilder = new SqliteConnectionStringBuilder();
            connBuilder.DataSource = dbFilePath;
            /*
			connBuilder.Version = 3;
			connBuilder.PageSize = 4096; 	// set page size to NTFS cluster size = 4096 bytes
			connBuilder.CacheSize = 10000; 	// cache size in bytes
			
			// Whether to use the inbuilt connection pooling of System.Data.Sqlite
			// It is possible to use a separate connection pool which could be faster like:
			// https://github.com/MediaPortal/MediaPortal-2/blob/master/MediaPortal/Incubator/SqliteDatabase/ConnectionPool.cs
			connBuilder.Pooling = true;
			
			// false = Use the newer 3.3x database format which compresses numbers more effectively
			connBuilder.LegacyFormat = false;
			
			// The default command timeout in seconds
			connBuilder.DefaultTimeout = 30;
			
			// Sqlite supports this, but it has to be enabled for each database connection by a PRAGMA command
			// For details see http://www.Sqlite.org/foreignkeys.html
			connBuilder.ForeignKeys = false;
			
			// Automatically create the database if it does not exist
			connBuilder.FailIfMissing = false;
			
			// Store GUIDs as binaries, not as string
			// Saves some space in the database and is said to make search queries on GUIDs faster
			connBuilder.BinaryGUID = true;
			
			// Sychronization Mode "Normal" enables parallel database access while at the same time preventing database
			// corruption and is therefore a good compromise between "Off" (more performance) and "On"
			// More information can be found here: http://www.Sqlite.org/pragma.html#pragma_synchronous
			connBuilder.SyncMode = SynchronizationModes.Normal;
			
			// Use the Write Ahead Log mode
			// In this journal mode write locks do not block reads
			// More information can be found here: http://www.Sqlite.org/wal.html
			connBuilder.JournalMode = SqliteJournalModeEnum.Wal;
			
			
			// Best performance settings
			// http://devlights.hatenablog.com/entry/2014/02/01/151642
			// According to that guy: Sync Mode: off and Journal: Wal are best

			// And according this this guy, this is best
			// http://stackoverflow.com/questions/784173/what-are-the-performance-characteristics-of-Sqlite-with-very-large-database-file
			//PRAGMA main.page_size=4096;
			//PRAGMA main.cache_size=10000;
			//PRAGMA main.locking_mode=EXCLUSIVE;
			//PRAGMA main.synchronous=NORMAL;
			//PRAGMA main.journal_mode=WAL;
			
			// And according to MusicBrowser.Engines.Cache SqliteHelper.cs this is best
			// PRAGMA main.page_size = 4096;
			// PRAGMA main.cache_size=-32;
			// PRAGMA main.temp_store = MEMORY;
			// PRAGMA main.synchronous=OFF;
			// PRAGMA main.journal_mode=MEMORY;
			
			// also check this
			// http://stackoverflow.com/questions/15383615/multiple-access-to-a-single-Sqlite-database-file-via-system-data-Sqlite-and-c-sh
			 */

            return connBuilder.ToString();
        }
        #endregion

        #region Static Database Methods
        public static void CreateDB(string dbFilePath)
        {
            if (!Directory.Exists(Path.GetDirectoryName(dbFilePath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(dbFilePath));
            }
            if (!File.Exists(dbFilePath))
            {
                //SqliteConnection.CreateFile(dbFilePath);
            }
        }

        private static void DeleteDB(string dbFilePath)
        {
            if (File.Exists(dbFilePath))
            {
                File.Delete(dbFilePath);
            }
        }

        public static void MoveDB(string dbOldFilePath, string dbNewFilePath)
        {
            if (File.Exists(dbOldFilePath))
            {
                File.Move(dbOldFilePath, dbNewFilePath);
            }
        }
        #endregion

        #region Reset Database Methods
        public static void ResetDatabase()
        {

            // ensure that the _SqliteDbPath is defined
            InitializeSqliteDBPath();

            // if using sql lite connection pooling
            GC.Collect();
            GC.WaitForPendingFinalizers();

            DeleteDB(_SqliteDbPath);
            CreateDB(_SqliteDbPath);

            AddDatabaseTables();
        }

        public static void AddDatabaseTables()
        {
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open(); // must open connection in order to begin a transaction
                using (var transaction = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        AddFingerprintTable(command);
                        AddHashBinTable(command);
                        AddTrackTable(command);
                        transaction.Commit();
                    }
                }
            }
        }
        #endregion

        #region Add and Remove the Fingerprint table
        public static bool AddFingerprintTable(SqliteCommand dbcmd)
        {
            dbcmd.CommandText = "CREATE TABLE IF NOT EXISTS fingerprints"
                + " (id INTEGER PRIMARY KEY AUTOINCREMENT, trackid INTEGER, songorder INTEGER, totalfingerprints INTEGER, signature BLOB)";

            try
            {
                dbcmd.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Add and Remove the HashBin table
        public static bool AddHashBinTable(SqliteCommand dbcmd)
        {
            dbcmd.CommandText = "CREATE TABLE IF NOT EXISTS hashbins"
                + " (id INTEGER PRIMARY KEY AUTOINCREMENT, hashbin INTEGER, hashtable INTEGER, trackid INTEGER, fingerprintid INTEGER)";

            try
            {
                dbcmd.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Add and Remove the Track table
        public static bool AddTrackTable(SqliteCommand dbcmd)
        {
            dbcmd.CommandText = "CREATE TABLE IF NOT EXISTS tracks"
                + " (id INTEGER PRIMARY KEY AUTOINCREMENT, albumid INTEGER, length INTEGER, artist TEXT, title TEXT, filepath TEXT, tags TEXT)";

            try
            {
                dbcmd.ExecuteNonQuery();
            }
            catch (SqliteException)
            {
                return false;
            }

            return true;
        }
        #endregion

        #region Inserts
        public static bool InsertFingerprint(IEnumerable<Fingerprint> collection)
        {
            try
            {
                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open(); // must open connection in order to begin a transaction
                    using (var transaction = connection.BeginTransaction())
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "INSERT INTO fingerprints (trackid, songorder, totalfingerprints, signature) " +
                                "VALUES (@trackid, @songorder, @totalfingerprints, @signature); SELECT last_insert_rowid();";

                            int totalfingerprintsCount = collection.Count();
                            command.Parameters.Add("@trackid", SqliteType.Integer);
                            command.Parameters.Add("@songorder", SqliteType.Integer);
                            command.Parameters.Add("@totalfingerprints", SqliteType.Integer);
                            command.Parameters.Add("@signature", SqliteType.Blob);

                            // Call Prepare after setting the Commandtext and Parameters.
                            // Executing the Prepare method should only be used in situations where the SQL will be executed multiple times.
                            // The process of preparing the SQL statement requires a bit of overhead and thus if the statements are only executed once,
                            // performance will degrade
                            command.Prepare();

                            foreach (var fingerprint in collection)
                            {
                                command.Parameters["@trackid"].Value = fingerprint.TrackId;
                                command.Parameters["@songorder"].Value = fingerprint.SongOrder;
                                command.Parameters["@totalfingerprints"].Value = fingerprint.TotalFingerprintsPerTrack = totalfingerprintsCount;
                                command.Parameters["@signature"].Value = BoolToByte(fingerprint.Signature);

                                fingerprint.Id = Convert.ToInt32(command.ExecuteScalar());
                            }
                        }

                        // We commit the whole transaction to the database. In case of an exception, the transaction is rolled back behind the scenes.
                        // In case of an exception, the transaction is rolled back behind the scenes.
                        transaction.Commit();
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool InsertTrack(Track track)
        {
            try
            {
                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open(); // must open connection in order to begin a transaction
                    using (var transaction = connection.BeginTransaction())
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "INSERT INTO tracks (albumid, length, artist, title, filepath, tags) " +
                                "VALUES (@albumid, @length, @artist, @title, @filepath, @tags); SELECT last_insert_rowid();";

                            // could possibly also use command.Parameters.AddWithValue
                            command.Parameters.Add("@albumid", SqliteType.Integer).Value = track.AlbumId;
                            command.Parameters.Add("@length", SqliteType.Integer).Value = track.TrackLengthMs;
                            command.Parameters.Add("@artist", SqliteType.Text).Value = track.Artist;
                            command.Parameters.Add("@title", SqliteType.Text).Value = track.Title;
                            command.Parameters.Add("@filepath", SqliteType.Text).Value = track.FilePath;
                            command.Parameters.Add("@tags", SqliteType.Text).Value = string.Join(";", track.Tags.Select(x => x.Key + "=" + x.Value));

                            track.Id = Convert.ToInt32(command.ExecuteScalar());
                        }

                        // We commit the whole transaction to the database. In case of an exception, the transaction is rolled back behind the scenes.
                        // In case of an exception, the transaction is rolled back behind the scenes.
                        transaction.Commit();
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool InsertHashBin(IEnumerable<HashBinMinHash> collection)
        {
            try
            {
                using (var connection = new SqliteConnection(GetConnectionString()))
                {
                    connection.Open(); // must open connection in order to begin a transaction
                    using (var transaction = connection.BeginTransaction())
                    {
                        using (var command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = "INSERT INTO hashbins (hashbin, hashtable, trackid, fingerprintid) " +
                                "VALUES (@hashbin, @hashtable, @trackid, @fingerprintid)";

                            command.Parameters.Add("@hashbin", SqliteType.Integer);
                            command.Parameters.Add("@hashtable", SqliteType.Integer);
                            command.Parameters.Add("@trackid", SqliteType.Integer);
                            command.Parameters.Add("@fingerprintid", SqliteType.Integer);

                            // Call Prepare after setting the Commandtext and Parameters.
                            // Executing the Prepare method should only be used in situations where the SQL will be executed multiple times.
                            // The process of preparing the SQL statement requires a bit of overhead and thus if the statements are only executed once,
                            // performance will degrade
                            command.Prepare();

                            foreach (var hashBin in collection)
                            {
                                command.Parameters["@hashbin"].Value = hashBin.Bin;
                                command.Parameters["@hashtable"].Value = hashBin.HashTable;
                                command.Parameters["@trackid"].Value = hashBin.TrackId;
                                command.Parameters["@fingerprintid"].Value = hashBin.FingerprintId;

                                int rowsAffected = command.ExecuteNonQuery();
                            }
                        }

                        // We commit the whole transaction to the database. In case of an exception, the transaction is rolled back behind the scenes.
                        // In case of an exception, the transaction is rolled back behind the scenes.
                        transaction.Commit();
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
        #endregion

        #region Reads
        public static int GetTrackCount()
        {
            int count = -1;
            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                using (var command = connection.CreateCommand())
                {
                    connection.Open();
                    command.CommandText = "SELECT Count(*) FROM [tracks]";
                    count = Convert.ToInt32(command.ExecuteScalar());
                }
            }
            return count;
        }

        public static IList<Fingerprint> ReadFingerprints()
        {
            var fingerprints = new List<Fingerprint>();

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT id, trackid, songorder, signature FROM [fingerprints]";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var fingerprint = new Fingerprint();
                            fingerprint.Id = reader.GetInt32(0);
                            fingerprint.TrackId = reader.GetInt32(1);
                            fingerprint.SongOrder = reader.GetInt32(2);
                            fingerprint.Signature = ByteToBool((byte[])reader.GetValue(3));
                            fingerprints.Add(fingerprint);
                        }
                    }
                }
            }

            return fingerprints;
        }

        public static IList<Fingerprint> ReadFingerprintsByTrackId(int trackId, int numberOfFingerprintsToRead)
        {
            var fingerprints = new List<Fingerprint>();

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT id, songorder, signature FROM [fingerprints] WHERE [trackid] = @trackid LIMIT @limit";
                    command.Parameters.AddWithValue("@trackid", trackId);
                    command.Parameters.AddWithValue("@limit", numberOfFingerprintsToRead);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var fingerprint = new Fingerprint();
                            fingerprint.Id = reader.GetInt32(0);
                            fingerprint.TrackId = trackId;
                            fingerprint.SongOrder = reader.GetInt32(1);
                            fingerprint.Signature = ByteToBool((byte[])reader.GetValue(2));
                            fingerprints.Add(fingerprint);
                        }
                    }
                }
            }

            return fingerprints;
        }

        public static IDictionary<int, IList<Fingerprint>> ReadFingerprintsByMultipleTrackId(
            IEnumerable<Track> tracks, int numberOfFingerprintsToRead)
        {
            var result = new Dictionary<int, IList<Fingerprint>>();
            var fingerprints = new List<Fingerprint>();

            String statementValueTags = String.Join(",", tracks.Select(x => x.Id));
            String query = String.Format("SELECT id, trackid, songorder, signature FROM [fingerprints] WHERE (trackid IN ({0})) LIMIT {0};", statementValueTags, numberOfFingerprintsToRead);

            int lastTrackId = -1;

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var fingerprint = new Fingerprint();
                            fingerprint.Id = reader.GetInt32(0);
                            fingerprint.TrackId = reader.GetInt32(1);
                            fingerprint.SongOrder = reader.GetInt32(2);
                            fingerprint.Signature = ByteToBool((byte[])reader.GetValue(3));

                            if (lastTrackId == -1 || lastTrackId == fingerprint.TrackId)
                            {
                                // still processing same track
                            }
                            else
                            {
                                // new track
                                // add fingerprints to dictionary and then reset fingerprints
                                result.Add(lastTrackId, fingerprints);
                                fingerprints.Clear();
                                fingerprints.Add(fingerprint);
                            }
                            lastTrackId = fingerprint.TrackId;
                        }
                        if (lastTrackId != -1)
                        {
                            // add last fingerprints
                            result.Add(lastTrackId, fingerprints);
                        }

                    }
                }
            }

            return result;
        }

        public static Fingerprint ReadFingerprintById(int id)
        {
            Fingerprint fingerprint = null;

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT trackid, songorder, totalfingerprints, signature FROM [fingerprints] WHERE [id] = @id";
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            fingerprint = new Fingerprint();
                            fingerprint.Id = id;
                            fingerprint.TrackId = reader.GetInt32(0);
                            fingerprint.SongOrder = reader.GetInt32(1);
                            fingerprint.TotalFingerprintsPerTrack = reader.GetInt32(2);
                            fingerprint.Signature = ByteToBool((byte[])reader.GetValue(3));
                        }
                    }
                }
            }

            return fingerprint;
        }

        public static IList<Fingerprint> ReadFingerprintById(IEnumerable<int> ids)
        {
            var fingerprints = new List<Fingerprint>();

            string statementValueTags = String.Join(",", ids);
            string query = String.Format("SELECT id, trackid, songorder, totalfingerprints, signature FROM [fingerprints] WHERE (id IN ({0}));", statementValueTags);

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var fingerprint = new Fingerprint();
                            fingerprint.Id = reader.GetInt32(0);
                            fingerprint.TrackId = reader.GetInt32(1);
                            fingerprint.SongOrder = reader.GetInt32(2);
                            fingerprint.TotalFingerprintsPerTrack = reader.GetInt32(3);
                            fingerprint.Signature = ByteToBool((byte[])reader.GetValue(4));
                            fingerprints.Add(fingerprint);
                        }
                    }
                }
            }

            return fingerprints;
        }

        public static IList<string> ReadTrackFilenames()
        {

            var filenames = new List<string>();

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT filepath FROM [tracks]";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string filename = reader.GetString(0);
                            filenames.Add(filename);
                        }
                    }
                }
            }

            return filenames;
        }

        public static IList<Track> ReadTracks()
        {
            var tracks = new List<Track>();

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT id, albumid, length, artist, title, filepath FROM [tracks]";

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var track = new Track();
                            track.Id = reader.GetInt32(0);
                            track.AlbumId = reader.GetInt32(1);
                            track.TrackLengthMs = reader.GetInt32(2);
                            if (!reader.IsDBNull(3))
                            {
                                track.Artist = reader.GetString(3);
                            }
                            track.Title = reader.GetString(4);
                            track.FilePath = reader.GetString(5);
                            tracks.Add(track);
                        }
                    }
                }
            }

            return tracks;
        }

        public static IList<Track> ReadTracks(string whereClause)
        {
            var tracks = new List<Track>();

            string query = "SELECT id, albumid, length, artist, title, filepath FROM [tracks]";
            if (!string.IsNullOrEmpty(whereClause))
            {
                query = string.Format("{0} {1}", query, whereClause);
            }

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var track = new Track();
                            track.Id = reader.GetInt32(0);
                            track.AlbumId = reader.GetInt32(1);
                            track.TrackLengthMs = reader.GetInt32(2);
                            if (!reader.IsDBNull(3))
                            {
                                track.Artist = reader.GetString(3);
                            }
                            track.Title = reader.GetString(4);
                            track.FilePath = reader.GetString(5);
                            tracks.Add(track);
                        }
                    }
                }
            }

            return tracks;
        }

        public static Track ReadTrackById(int id)
        {
            Track track = null;

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT albumid, length, artist, title, filepath FROM [tracks] WHERE [id] = @id";
                    command.Parameters.AddWithValue("@id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            track = new Track();
                            track.Id = id;
                            track.AlbumId = reader.GetInt32(0);
                            track.TrackLengthMs = reader.GetInt32(1);
                            if (!reader.IsDBNull(2))
                            {
                                track.Artist = reader.GetString(2);
                            }
                            track.Title = reader.GetString(3);
                            track.FilePath = reader.GetString(4);
                        }
                    }
                }
            }

            return track;
        }

        public static IList<Track> ReadTrackById(IEnumerable<int> ids)
        {
            var tracks = new List<Track>();

            string statementValueTags = String.Join(",", ids);
            string query = String.Format("SELECT id, albumid, length, artist, title, filepath FROM [tracks] WHERE (id IN ({0}));", statementValueTags);

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var track = new Track();
                            track.Id = reader.GetInt32(0);
                            track.AlbumId = reader.GetInt32(1);
                            track.TrackLengthMs = reader.GetInt32(2);
                            if (!reader.IsDBNull(3))
                            {
                                track.Artist = reader.GetString(3);
                            }
                            track.Title = reader.GetString(4);
                            track.FilePath = reader.GetString(5);
                            tracks.Add(track);
                        }
                    }
                }
            }

            return tracks;
        }

        /// <summary>
        /// Find fingerprints using hash-buckets (e.g. HashBins)
        /// </summary>
        /// <param name="hashBuckets"></param>
        /// <returns>Return dictionary with fingerprintids as keys and the corresponding hashbins as values</returns>
        public static IDictionary<int, IList<HashBinMinHash>> ReadFingerprintsByHashBucketLsh(long[] hashBuckets)
        {

            IDictionary<int, IList<HashBinMinHash>> result = new Dictionary<int, IList<HashBinMinHash>>();

            string statementValueTags = String.Join(",", hashBuckets);

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = String.Format("SELECT id, hashbin, hashtable, trackid, fingerprintid FROM hashbins WHERE (hashbin IN ({0}))", statementValueTags);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var hash = new HashBinMinHash();
                            hash.Id = reader.GetInt32(0);
                            hash.Bin = reader.GetInt64(1);
                            hash.HashTable = reader.GetInt32(2);
                            hash.TrackId = reader.GetInt32(3);
                            hash.FingerprintId = reader.GetInt32(4);

                            if (result.ContainsKey(hash.FingerprintId))
                            {
                                result[hash.FingerprintId].Add(hash);
                            }
                            else
                            {
                                result.Add(hash.FingerprintId, new List<HashBinMinHash>(new[] { hash }));
                            }
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Read all fingerprints ignoring the hash-buckets (e.g. HashBins)
        /// </summary>
        /// <returns>Return dictionary with fingerprintids as keys and the corresponding hashbins as values</returns>
        public static IDictionary<int, IList<HashBinMinHash>> ReadAllFingerprints()
        {

            IDictionary<int, IList<HashBinMinHash>> result = new Dictionary<int, IList<HashBinMinHash>>();

            string query = String.Format("SELECT id, hashbin, hashtable, trackid, fingerprintid FROM hashbins");

            using (var connection = new SqliteConnection(GetConnectionString()))
            {
                connection.Open();
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = query;

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var hash = new HashBinMinHash();
                            hash.Id = reader.GetInt32(0);
                            hash.Bin = reader.GetInt64(1);
                            hash.HashTable = reader.GetInt32(2);
                            hash.TrackId = reader.GetInt32(3);
                            hash.FingerprintId = reader.GetInt32(4);

                            if (result.ContainsKey(hash.FingerprintId))
                            {
                                result[hash.FingerprintId].Add(hash);
                            }
                            else
                            {
                                result.Add(hash.FingerprintId, new List<HashBinMinHash>(new[] { hash }));
                            }
                        }
                    }
                }
            }

            return result;
        }
        #endregion

        #region Private Static Utils
        private static bool[] ByteToBool(byte[] byteArray)
        {
            // basic - same count
            var boolArray = new bool[byteArray.Length];
            for (int i = 0; i < byteArray.Length; i++)
            {
                boolArray[i] = (byteArray[i] == 1 ? true : false);
            }
            return boolArray;
        }

        private static byte[] BoolToByte(bool[] boolArray)
        {
            // http://stackoverflow.com/questions/713057/convert-bool-to-byte-c-sharp
            // basic - same count
            byte[] byteArray = Array.ConvertAll(boolArray, b => b ? (byte)1 : (byte)0);
            return byteArray;
        }
        #endregion
    }
}