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
using Serilog;

namespace FindSimilarClient.Controllers
{
    [Route("api/[controller]")]
    public class StreamingController : Controller
    {
        private IFindSimilarDatabase _database;
        public StreamingController(IFindSimilarDatabase database)
        {
            _database = database;

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console()
                .CreateLogger();
        }

        [HttpGet("{id}")]
        public async Task<FileStreamResult> Get(string id)
        {
            // ids have this form:
            // "b6b9ba73-293c-46ae-bd7d-2ea23ecb5e1c"
            // "e12ec602-9ab7-4999-9506-1e996f9d6eb2"
            var track = _database.ReadTrackByReference(new ModelReference<string>(id));

            if (!string.IsNullOrEmpty(track.Title))
            {
                string pathToSourceFile = track.Title;

                // var pathToSourceFile = @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\Van Halen Jump\FPC_Crash_G16InLite_01.wav";
                // var pathToSourceFile = @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\House Baerum\ATE Reverb Kick - 003.wav";
                // var pathToSourceFile = @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Jason Derulo In My Head Remix\La Manga.ogg";
                // return StreamAudioV3(pathToSourceFile);
                // return StreamAudioSampleSource(pathToSourceFile);
                // return StreamAudioBuiltIn(pathToSourceFile);

                return MultipartFileSender.FromFile(pathToSourceFile, "audio/wav");
            }
            else
            {
                return null;
            }

            /* 
            // works but not seeking
            var httpStream = await _streamingService.GetByName(name);
            return new FileStreamResult(httpStream, "video/mp4");

            // works but no seeking
            var pathToVideoFile = @"C:\Users\pnerseth\Documents\Presentations\BlockChain\Cryptocurrencies_John_Oliver_Trim.mp4";
            var stream = new FileStream(pathToVideoFile, FileMode.Open);
            return new FileStreamResult(stream, new MediaTypeHeaderValue("video/mp4"));

            // ASP NET CORE 2.1 supports enableRangeProcessing: Set to true to enable range requests processing.
            // So no need to enable this in Program.cs
            // ref. https://github.com/aspnet/Mvc/pull/6895#issuecomment-356477675
            return File(System.IO.File.OpenRead(pathToVideoFile), "video/mp4", true);

            // works with seeking
            var pathToSourceFile = @"C:\Users\pnerseth\Amazon Drive\Documents\Audio\FL Projects\!PERIVAR\Jason Derulo In My Head Remix\La Manga.ogg";
            Stream memStream;
            using (var fileStream = new FileStream(pathToSourceFile, FileMode.Open))
            {
                memStream = GetMemoryStream(fileStream);
            }
            // using StreamResult we support seeking
            return new StreamResult(memStream, new MediaTypeHeaderValue("audio/wav"));
            
            // using FileStreamResult directly does not support seeking
            // return new FileStreamResult(memStream, new MediaTypeHeaderValue("audio/wav"));
             */
        }

        private FileStreamResult StreamAudioV5(string pathToSourceFile)
        {
            IWaveSource waveSource = CodecFactory.Instance.GetCodec(pathToSourceFile);
            ISampleSource sampleSource = waveSource.ToSampleSource();
            IWaveSource ieeeFloatSource = sampleSource.ToWaveSource();
            return new WaveSourceStreamResult(ieeeFloatSource, new MediaTypeHeaderValue("audio/wav"));
        }

        private FileStreamResult StreamAudioSampleSource(string pathToSourceFile)
        {
            IWaveSource waveSource = CodecFactory.Instance.GetCodec(pathToSourceFile);
            ISampleSource sampleSource = waveSource.ToSampleSource();
            return new SampleSourceStreamResult(sampleSource, new MediaTypeHeaderValue("audio/wav"));
        }

        private FileStreamResult StreamAudioV3(string pathToSourceFile)
        {
            IWaveSource waveSource = CodecFactory.Instance.GetCodec(pathToSourceFile);
            return new WaveSourceStreamResult(waveSource, new MediaTypeHeaderValue("audio/wav"));
        }

        private FileStreamResult StreamAudioV2(string pathToSourceFile)
        {
            MemoryStream outputStream = new MemoryStream();
            using (IWaveSource soundSource = CodecFactory.Instance.GetCodec(pathToSourceFile))
            {
                using (WaveWriter waveWriter = new WaveWriter(outputStream, soundSource.WaveFormat))
                {
                    byte[] bytes = new byte[soundSource.Length];
                    soundSource.Position = 0;
                    soundSource.Read(bytes, 0, (int)soundSource.Length);
                    waveWriter.Write(bytes, 0, bytes.Length);
                }
            }
            outputStream.Seek(0, SeekOrigin.Begin);

            string contentType = "audio/wav";
            // return new StreamResult(stream, new MediaTypeHeaderValue(contentType));
            return new FileStreamResult(outputStream, new MediaTypeHeaderValue(contentType))
            {
                EnableRangeProcessing = true
            };

            //return File(outputStream, contentType, true);
        }

        private FileStreamResult StreamAudio(string pathToSourceFile)
        {
            MemoryStream outputStream = new MemoryStream();
            using (var soundSource = CodecFactory.Instance.GetCodec(pathToSourceFile))
            {
                using (var inputStream = GetMemoryStream(soundSource))
                {
                    using (WaveWriter waveWriter = new WaveWriter(outputStream, soundSource.WaveFormat))
                    {
                        byte[] bytes = new byte[inputStream.Length];
                        inputStream.Position = 0;
                        inputStream.Read(bytes, 0, (int)inputStream.Length);
                        waveWriter.Write(bytes, 0, bytes.Length);
                    }
                }
            }
            outputStream.Seek(0, SeekOrigin.Begin);

            string contentType = "audio/wav";
            // return new StreamResult(stream, new MediaTypeHeaderValue(contentType));
            // return new FileStreamResult(outputStream, new MediaTypeHeaderValue(contentType));
            return File(outputStream, contentType, true);
        }

        private FileStreamResult StreamAudioBuiltIn(string pathToSourceFile)
        {
            string contentType = "audio/wav";

            return new FileStreamResult(System.IO.File.OpenRead(pathToSourceFile), new MediaTypeHeaderValue(contentType))
            {
                EnableRangeProcessing = true
            };

            // ASP NET CORE 2.1 supports enableRangeProcessing: Set to true to enable range requests processing.
            // So no need to enable this in Program.cs
            // ref. https://github.com/aspnet/Mvc/pull/6895#issuecomment-356477675
            // return File(System.IO.File.OpenRead(pathToSourceFile), contentType, true);

            // return new StreamResult(stream, new MediaTypeHeaderValue(contentType));
        }

        public byte[] GetByteArray(Stream stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }

        public byte[] GetByteArray(IWaveSource stream)
        {
            byte[] buffer = new byte[32768];
            using (MemoryStream ms = new MemoryStream())
            {
                while (true)
                {
                    int read = stream.Read(buffer, 0, buffer.Length);
                    if (read <= 0)
                        return ms.ToArray();
                    ms.Write(buffer, 0, read);
                }
            }
        }

        public Stream GetMemoryStream(Stream stream)
        {
            var ms = new MemoryStream();
            byte[] buffer = new byte[32768];
            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;
                ms.Write(buffer, 0, read);
            }
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }

        public Stream GetMemoryStream(IWaveSource stream)
        {
            var ms = new MemoryStream();
            byte[] buffer = new byte[32768];
            while (true)
            {
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read <= 0)
                    break;
                ms.Write(buffer, 0, read);
            }
            ms.Seek(0, SeekOrigin.Begin);
            return ms;
        }
    }
}