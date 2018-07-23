using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommonUtils.Audio;
using CSCore.Codecs.WAV;
using OggSharp;
using Serilog;

namespace CSCore.Codecs.OGG
{
    public class OggSharpSource : IWaveSource
    {
        private readonly object _lockObj = new object();
        private readonly WaveFormat _waveFormat;
        private readonly OggDecoder _oggDecoder;
        private Stream _oggDecodedStream;
        private readonly Stream _stream;
        private readonly long _length;
        private bool _disposed;

        public OggSharpSource(Stream stream) : this(stream, null, null)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="OggSharpSource" /> class.
        /// </summary>
        /// <param name="stream"><see cref="Stream" /> which contains raw waveform-audio data.</param>
        /// <param name="waveFormat">The format of the waveform-audio data within the <paramref name="stream" />.</param>
        /// <param name="chunks">the wave chunks read using the wavefile reader</param>
        public OggSharpSource(Stream stream, WaveFormat waveFormat, ReadOnlyCollection<WaveFileChunk> chunks)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanRead)
                throw new ArgumentException("stream is not readable", "stream");

            // format checking if the ogg is within a wav container
            if (waveFormat != null)
            {
                switch ((short)waveFormat.WaveFormatTag)
                {
                    case 0x674f: // OGG_VORBIS_MODE_1 "Og" Original stream compatible
                    case 0x676f: // OGG_VORBIS_MODE_1_PLUS "og" Original stream compatible
                    case 0x6750: // OGG_VORBIS_MODE_2 "Pg" Have independent header
                    case 0x6770: // OGG_VORBIS_MODE_2_PLUS "pg" Have independent headere
                    case 0x6751: // OGG_VORBIS_MODE_3 "Qg" Have no codebook header
                    case 0x6771: // OGG_VORBIS_MODE_3_PLUS "qg" Have no codebook header
                        break;
                    default:
                        throw new ArgumentException(string.Format("Not supported encoding: {0}", waveFormat.WaveFormatTag));
                }
            }

            // format checking if the ogg is within a wav container
            if (chunks != null)
            {
                // check format
                var audioFormat = new AudioFormat();

                var fmtChunk = (FmtChunk)chunks.FirstOrDefault(x => x is FmtChunk);
                if (fmtChunk != null)
                {
                    // https://github.com/chirlu/sox/blob/4927023d0978615c74e8a511ce981cf4c29031f1/src/wav.c
                    long oldPosition = stream.Position;
                    long startPosition = fmtChunk.StartPosition;
                    long endPosition = fmtChunk.EndPosition;
                    long chunkDataSize = fmtChunk.ChunkDataSize;

                    stream.Position = startPosition;
                    var reader = new BinaryReader(stream);

                    audioFormat.Encoding = (AudioEncoding)reader.ReadInt16();
                    audioFormat.Channels = reader.ReadInt16();
                    audioFormat.SampleRate = reader.ReadInt32();
                    audioFormat.AverageBytesPerSecond = reader.ReadInt32();
                    audioFormat.BlockAlign = reader.ReadInt16();
                    audioFormat.BitsPerSample = reader.ReadInt16();

                    if (fmtChunk.ChunkDataSize > 16)
                    {
                        audioFormat.ExtraSize = reader.ReadInt16();

                        if (audioFormat.ExtraSize >= 2)
                        {
                            audioFormat.SamplesPerBlock = reader.ReadInt16();
                        }
                    }

                    // reset position
                    stream.Position = oldPosition;
                    reader = null;
                }

                var dataChunk = (DataChunk)chunks.FirstOrDefault(x => x is DataChunk);
                if (dataChunk != null)
                {
                    audioFormat.DataChunkSize = dataChunk.ChunkDataSize;
                }
                else
                {
                    throw new ArgumentException("The specified stream does not contain any data chunks.");
                }

                Log.Verbose(audioFormat.ToString());
            }

            // set stream
            _stream = stream;

            // TODO: check with reference implementation
            // https://github.com/xiph/vorbis
            _oggDecoder = new OggDecoder();

            // if the ogg content is embedded in a wave file, copy whole stream into memory                        
            if (waveFormat != null)
            {
                MemoryStream memoryStream = new MemoryStream();
                stream.CopyTo(memoryStream);
                memoryStream.Position = 0;
                _oggDecoder.Initialize(memoryStream);
            }
            else
            {
                _oggDecoder.Initialize(stream);
            }

            int channels = (_oggDecoder.Stereo ? 2 : 1);
            int sampleRate = _oggDecoder.SampleRate;
            _length = (long)(_oggDecoder.Length * sampleRate * channels * 2); // 2 bytes per sample

            Log.Verbose(string.Format("Ogg Vorbis bitstream is {0} channel, {1} Hz", channels, sampleRate));
            Log.Verbose(string.Format("Comment: {0}", _oggDecoder.Comment));
            Log.Verbose(string.Format("Encoded by: {0}", _oggDecoder.Vendor));

            _waveFormat = new WaveFormat(sampleRate, 16, channels, AudioEncoding.Pcm);
        }
        private Stream DecodeStream(OggDecoder decoder)
        {
            Stream output = new MemoryStream(4096);
            foreach (PCMChunk chunk in decoder)
            {
                output.Write(chunk.Bytes, 0, chunk.Length);
            }
            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        /// <summary>
        ///     Reads a sequence of bytes from the <see cref="OggSharpSource" /> and advances the position within the stream by the
        ///     number of bytes read.
        /// </summary>
        /// <param name="buffer">
        ///     An array of bytes. When this method returns, the <paramref name="buffer" /> contains the specified
        ///     byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> +
        ///     <paramref name="count" /> - 1) replaced by the bytes read from the current source.
        /// </param>
        /// <param name="offset">
        ///     The zero-based byte offset in the <paramref name="buffer" /> at which to begin storing the data
        ///     read from the current stream.
        /// </param>
        /// <param name="count">The maximum number of bytes to read from the current source.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        public int Read(byte[] buffer, int offset, int count)
        {
            lock (_lockObj)
            {
                if (_oggDecodedStream == null)
                {
                    _oggDecodedStream = DecodeStream(_oggDecoder);
                }
                return _oggDecodedStream.Read(buffer, offset, count);
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="IAudioSource"/> supports seeking.
        /// </summary>
        public bool CanSeek
        {
            get { return _stream.CanSeek; }
        }

        /// <summary>
        ///     Gets the format of the raw data.
        /// </summary>
        public WaveFormat WaveFormat
        {
            get { return _waveFormat; }
        }

        public long Length
        {
            get { return _length; }
        }

        public long Position
        {
            get
            {
                if (_oggDecodedStream != null)
                {
                    return _oggDecodedStream.Position;
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                if (_oggDecodedStream != null)
                {
                    _oggDecodedStream.Position = value;
                }
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_oggDecodedStream != null)
                {
                    _oggDecodedStream.Dispose();
                    _oggDecodedStream = null;
                }
                if (_oggDecoder != null) _oggDecoder.Dispose();
                if (_stream != null) _stream.Dispose();
            }
            else
            {
                // TODO: Why does this happen?
                //throw new ObjectDisposedException("OggSharpSource");
            }
            _disposed = true;
        }
    }
}