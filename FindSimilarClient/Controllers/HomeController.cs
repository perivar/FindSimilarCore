using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using FindSimilarClient.Models;
using FindSimilarServices.Fingerprinting;
using SoundFingerprinting.DAO.Data;
using SoundFingerprinting.DAO;
using System.IO;
using FindSimilarServices;
using SoundFingerprinting;

namespace FindSimilarClient.Controllers
{
    public class HomeController : Controller
    {
        private IModelService _database;
        private ISoundFingerprinter _fingerprinter;

        public HomeController(IModelService database, ISoundFingerprinter fingerprinter)
        {
            _database = database;
            _fingerprinter = fingerprinter;
        }

        public IActionResult Index(string query)
        {
            IList<TrackData> tracks = new List<TrackData>();
            if (!string.IsNullOrEmpty(query))
            {
                // tracks = _database.ReadTracksByQuery(query);
            }
            else
            {
                // tracks = _database.ReadAllTracks(0, 50);
                tracks = _database.ReadAllTracks().Take(50).ToList();
            }
            ViewBag.Tracks = tracks;

            return View();
        }

        public IActionResult FindSimilar(string id)
        {
            IList<TrackData> tracks = new List<TrackData>();
            var track = _database.ReadTrackByReference(new ModelReference<string>(id));
            if (!string.IsNullOrEmpty(track.Title))
            {
                var filePath = track.Title;
                var results = _fingerprinter.GetBestMatchesForSong(Path.GetFullPath(filePath), -1, -1, Verbosity.Normal);

                foreach (var result in results)
                {
                    // the track title holds the full filename                     
                    // FileInfo fileInfo = new FileInfo(result.Track.Title);
                    // Console.WriteLine("{0}, confidence {1}, coverage {2}, est. coverage {3}", fileInfo.FullName, result.Confidence, result.Coverage, result.EstimatedCoverage);
                    tracks.Add(result.Track);
                }
            }

            ViewBag.Tracks = tracks;

            return View("Index");
        }

        public IActionResult OpenDirectory(string id)
        {
            string query = null;
            var track = _database.ReadTrackByReference(new ModelReference<string>(id));
            if (!string.IsNullOrEmpty(track.Title))
            {
                var filePath = Path.GetDirectoryName(track.Title);
                query = filePath;
            }
            return RedirectToAction("Index", new { query = query });
        }

        [HttpGet("api/download/{id}")]
        public async Task<IActionResult> DownloadFile(string id)
        {
            var track = _database.ReadTrackByReference(new ModelReference<string>(id));
            if (!string.IsNullOrEmpty(track.Title))
            {
                var filePath = track.Title;

                var memory = new MemoryStream();
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    await stream.CopyToAsync(memory);
                }
                memory.Position = 0;

                string contentType = MimeMapping.MimeUtility.GetMimeMapping(filePath);
                return File(memory, contentType, Path.GetFileName(filePath));
            }

            return this.NotFound();
        }

        public IActionResult About()
        {
            ViewData["Message"] = "Your application description page.";

            return View();
        }

        public IActionResult Contact()
        {
            ViewData["Message"] = "Your contact page.";

            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
