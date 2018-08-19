using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc;
using FindSimilarClient.Models;
using SoundFingerprinting.Audio;
using CSCore.Codecs;
using CSCore;
using CSCore.Codecs.WAV;
using SoundFingerprinting;
using SoundFingerprinting.DAO;
using FindSimilarServices.Fingerprinting;
using FindSimilarServices.Fingerprinting.SQLiteDb;

namespace FindSimilarClient.Controllers
{
    [Route("api/[controller]")]
    public class StreamingController : Controller
    {
        private readonly SQLiteDbContext _database;
        public StreamingController(SQLiteDbContext database)
        {
            _database = database;
        }

        // GET /api/streaming/10ad2403-34f2-47b6-bd51-819e486a1723
        [HttpGet("{id}")]
        public async Task<FileStreamResult> Get(string id)
        {
            // ids have this form:
            // "b6b9ba73-293c-46ae-bd7d-2ea23ecb5e1c"
            // "e12ec602-9ab7-4999-9506-1e996f9d6eb2"
            var track = _database.ReadTrackByReference(new ModelReference<string>(id));

            if (!string.IsNullOrEmpty(track.Title))
            {
                string filePath = track.Title;

                // return StreamAudioBuiltIn(filePath);
                // return File(System.IO.File.OpenRead(filePath), "audio/wav", true);
                // return MultipartFileSender.FromFile(filePath);
                return WaveSourceSender.FromFile(filePath);
            }
            else
            {
                return null;
            }
        }

        private FileStreamResult StreamAudioBuiltIn(string filePath)
        {
            string contentType = MimeMapping.MimeUtility.GetMimeMapping(filePath);

            return new FileStreamResult(System.IO.File.OpenRead(filePath), new MediaTypeHeaderValue(contentType))
            {
                EnableRangeProcessing = true,
            };

            // ASP NET CORE 2.1 supports enableRangeProcessing: Set to true to enable range requests processing.
            // So no need to enable this in Program.cs
            // ref. https://github.com/aspnet/Mvc/pull/6895#issuecomment-356477675
            // return File(System.IO.File.OpenRead(filePath), contentType, true);
        }
    }
}