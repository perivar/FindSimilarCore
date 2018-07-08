using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using CSCore.Codecs.AIFF;
using CSCore.Codecs.FLAC;
using CSCore.Codecs.WAV;
using CSCore.Codecs.MP3;
using CSCore.Codecs.OGG;
using CSCore.Codecs.ADPCM;

namespace CSCore.Codecs
{
    /// <summary>
    ///     Helps to choose the right decoder for different codecs.
    /// </summary>
    public class CodecFactory
    {
        // ReSharper disable once InconsistentNaming
        private static readonly CodecFactory _instance = new CodecFactory();

        private readonly Dictionary<object, CodecFactoryEntry> _codecs;

        private CodecFactory()
        {
            _codecs = new Dictionary<object, CodecFactoryEntry>();

            Register("wave", new CodecFactoryEntry(s =>
         {
             IWaveSource res = new WaveFileReader(s);
             if (res.WaveFormat.WaveFormatTag != AudioEncoding.Pcm &&
                 res.WaveFormat.WaveFormatTag != AudioEncoding.IeeeFloat &&
                 res.WaveFormat.WaveFormatTag != AudioEncoding.Extensible)
             {
                 switch ((short)res.WaveFormat.WaveFormatTag)
                 {
                     case 0x0002: // Microsoft ADPCM
                     case 0x0011: // IMA ADPCM   
                     case 0x0061: // Duck DK4 IMA ADPCM
                     case 0x0062: // Duck DK3 IMA ADPCM 
                         res.Dispose();
                         res = new AdpcmSource(s, res.WaveFormat, ((WaveFileReader)res).Chunks);
                         break;
                     case 0x674f: // OGG_VORBIS_MODE_1 "Og" Original stream compatible
                     case 0x676f: // OGG_VORBIS_MODE_1_PLUS "og" Original stream compatible
                     case 0x6750: // OGG_VORBIS_MODE_2 "Pg" Have independent header
                     case 0x6770: // OGG_VORBIS_MODE_2_PLUS "pg" Have independent headere
                     case 0x6751: // OGG_VORBIS_MODE_3 "Qg" Have no codebook header
                     case 0x6771: // OGG_VORBIS_MODE_3_PLUS "qg" Have no codebook header
                         res.Dispose();
                         res = new CSVorbisSource(s, res.WaveFormat, ((WaveFileReader)res).Chunks);
                         break;
                     default:
                         throw new ArgumentException(string.Format("Non PCM, IEEE or Extensible wave-files, or format not supported: ({0})", res.WaveFormat.WaveFormatTag));
                 }
             }
             return res;
         },
             "wav", "wave"));
            Register("flac", new CodecFactoryEntry(s => new FlacFile(s),
                "flac", "fla"));
            Register("aiff", new CodecFactoryEntry(s => new AiffReader(s),
                "aiff", "aif", "aifc"));

            // ADDED BY PER IVAR
            Register("ogg-vorbis", new CodecFactoryEntry(s => new CSVorbisSource(s), "ogg"));
            Register("mpeg", new CodecFactoryEntry(s => new NLayerSource(s).ToWaveSource(),
            "mp1", "m1a", "mp2", "m2a", "mp3", "mpg", "mpeg", "mpeg3"));
        }

        /// <summary>
        ///     Gets the default singleton instance of the <see cref="CodecFactory" /> class.
        /// </summary>
        public static CodecFactory Instance
        {
            get { return _instance; }
        }

        /// <summary>
        ///     Gets the file filter in English. This filter can be used e.g. in combination with an OpenFileDialog.
        /// </summary>
        public static string SupportedFilesFilterEn
        {
            get { return Instance.GenerateFilter(); }
        }

        /// <summary>
        ///     Registers a new codec.
        /// </summary>
        /// <param name="key">
        ///     The key which gets used internally to save the <paramref name="codec" /> in a
        ///     <see cref="Dictionary{TKey,TValue}" />. This is typically the associated file extension. For example: the mp3 codec
        ///     uses the string "mp3" as its key.
        /// </param>
        /// <param name="codec"><see cref="CodecFactoryEntry" /> which provides information about the codec.</param>
        public void Register(object key, CodecFactoryEntry codec)
        {
            var keyString = key as string;
            if (keyString != null)
                key = keyString.ToLower();

            if (_codecs.ContainsKey(key) != true)
                _codecs.Add(key, codec);
        }

        /// <summary>
        ///     Returns a fully initialized <see cref="IWaveSource" /> instance which is able to decode the specified file. If the
        ///     specified file can not be decoded, this method throws an <see cref="NotSupportedException" />.
        /// </summary>
        /// <param name="filename">Filename of the specified file.</param>
        /// <returns>Fully initialized <see cref="IWaveSource" /> instance which is able to decode the specified file.</returns>
        /// <exception cref="NotSupportedException">The codec of the specified file is not supported.</exception>
        public IWaveSource GetCodec(string filename)
        {
            if (String.IsNullOrEmpty(filename))
                throw new ArgumentNullException("filename");

            if (!File.Exists(filename))
                throw new FileNotFoundException("File not found.", filename);

            string extension = Path.GetExtension(filename).Remove(0, 1); //get the extension without the "dot".

            IWaveSource source = null;
            if (File.Exists(filename))
            {
                Stream stream = File.OpenRead(filename);
                try
                {
                    foreach (var codecEntry in _codecs)
                    {
                        try
                        {
                            if (
                                codecEntry.Value.FileExtensions.Any(
                                    x => x.Equals(extension, StringComparison.OrdinalIgnoreCase)))
                            {
                                source = codecEntry.Value.GetCodecAction(stream);
                                if (source != null)
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine(ex.Message);
                        }
                    }
                }
                finally
                {
                    if (source == null)
                    {
                        stream.Dispose();
                    }
                    else
                    {
                        source = new DisposeFileStreamSource(source, stream);
                    }
                }
            }

            if (source != null)
                return source;

            return Default(filename);
        }

        /// <summary>
        ///     Returns a fully initialized <see cref="IWaveSource" /> instance which is able to decode the audio source behind the
        ///     specified <paramref name="uri" />.
        ///     If the specified audio source can not be decoded, this method throws an <see cref="NotSupportedException" />.
        /// </summary>
        /// <param name="uri">Uri which points to an audio source.</param>
        /// <returns>Fully initialized <see cref="IWaveSource" /> instance which is able to decode the specified audio source.</returns>
        /// <exception cref="NotSupportedException">The codec of the specified audio source is not supported.</exception>
        public IWaveSource GetCodec(Uri uri)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            try
            {
                var filename = TryFindFilename(uri);
                if (!String.IsNullOrEmpty(filename))
                    return GetCodec(filename);

                return OpenWebStream(uri.ToString());
            }
            catch (IOException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new NotSupportedException("Codec not supported.", e);
            }
        }

        internal IWaveSource GetCodec(Stream stream, object key)
        {
            return _codecs[key].GetCodecAction(stream);
        }

        private IWaveSource OpenWebStream(string url)
        {
            try
            {
                return Default(url);
            }
            catch (Exception)
            {
                try
                {

                }
                catch (Exception)
                {
                    Debug.WriteLine("No mp3 webstream.");
                }
                throw; //better throw the exception of the MediaFoundationDecoder. We just try to use the Mp3WebStream class since a few mp3 streams are not supported by the mediafoundation.
            }
        }

        private static IWaveSource Default(string url)
        {
            /*             return new MediaFoundationDecoder(url);
             */
            return null;
        }

        /// <summary>
        ///     Returns all the common file extensions of all supported codecs. Note that some of these file extensions belong to
        ///     more than one codec.
        ///     That means that it can be possible that some files with the file extension abc can be decoded but other a few files
        ///     with the file extension abc can't be decoded.
        /// </summary>
        /// <returns>Supported file extensions.</returns>
        public string[] GetSupportedFileExtensions()
        {
            var extensions = new List<string>();
            foreach (CodecFactoryEntry item in _codecs.Select(x => x.Value))
            {
                foreach (string e in item.FileExtensions)
                {
                    if (!extensions.Contains(e))
                        extensions.Add(e);
                }
            }
            return extensions.ToArray();
        }

        private string GenerateFilter()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append("Supported Files|");
            stringBuilder.Append(String.Concat(GetSupportedFileExtensions().Select(x => "*." + x + ";").ToArray()));
            stringBuilder.Remove(stringBuilder.Length - 1, 1);
            return stringBuilder.ToString();
        }

        private string TryFindFilename(Uri uri)
        {
            if (File.Exists(uri.LocalPath))
                return uri.LocalPath;

            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(uri.LocalPath);
                if (fileInfo.Exists)
                    return fileInfo.FullName;
            }
            catch (Exception)
            {
                Debug.WriteLine(String.Format("{0} not found.", uri.LocalPath));
            }

            try
            {
                fileInfo = new FileInfo(uri.OriginalString);
                if (fileInfo.Exists)
                    return fileInfo.FullName;
            }
            catch (Exception)
            {
                Debug.WriteLine(String.Format("{0} not found.", uri.OriginalString));
            }

            /*             var path = Win32.NativeMethods.PathCreateFromUrl(uri.OriginalString);
                        if (path == null || !File.Exists(path))
                        {
                            path = Win32.NativeMethods.PathCreateFromUrl(uri.AbsoluteUri);
                        }
                        if (path != null && File.Exists(path))
                        {
                            return path;
                        }
             */
            return null;
        }

        private class DisposeFileStreamSource : WaveAggregatorBase
        {
            private Stream _stream;

            public DisposeFileStreamSource(IWaveSource source, Stream stream)
                : base(source)
            {
                _stream = stream;
            }

            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (_stream != null)
                {
                    try
                    {
                        _stream.Dispose();
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("Stream was already disposed.");
                    }
                    finally
                    {
                        _stream = null;
                    }
                }
            }
        }
    }
}