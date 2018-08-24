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
        private readonly IFindSimilarDatabase _database;
        public StreamingController(IFindSimilarDatabase database)
        {
            _database = database;
        }

        // GET /api/streaming/23
        [HttpGet("{id}")]
        public FileStreamResult Get(string id)
        {

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