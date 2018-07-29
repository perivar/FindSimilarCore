using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommonUtils;
using CommonUtils.Audio;
using CSCore.Codecs.WAV;
using Microsoft.Extensions.Logging;
using OggSharp;

namespace CSCore.Codecs.OGG
{
    public class OggSharpSource : IWaveSource
    {
        private readonly object _lockObj = new object();
        private readonly WaveFormat _waveFormat;
        private readonly OggDecoder _oggDecoder;
        private IEnumerator<PCMChunk> _oggPCMChunkEnumerator;
        private byte[] _prevPCMChunkBuffer;
        private readonly Stream _stream;
        private readonly long _length;
        private bool _disposed;
        private readonly ILogger _logger;
        private Stream _oggDecodedMemoryStream; // not used by the PCM chunk reader

        public OggSharpSource(Stream stream) : this(stream, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="OggSharpSource" /> class.
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

            _logger = ApplicationLogging.CreateLogger<OggSharpSource>();

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

                _logger.LogDebug(audioFormat.ToString());
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
                _oggPCMChunkEnumerator = _oggDecoder.GetEnumerator();
            }
            else
            {
                _oggDecoder.Initialize(stream);
                _oggPCMChunkEnumerator = _oggDecoder.GetEnumerator();
            }

            int channels = _oggDecoder.Channels;
            int sampleRate = _oggDecoder.SampleRate;

            _logger.LogDebug(string.Format("Ogg Vorbis bitstream is {0} channel, {1} Hz", channels, sampleRate));
            _logger.LogDebug(string.Format("Comment: {0}", _oggDecoder.Comment));
            _logger.LogDebug(string.Format("Encoded by: {0}", _oggDecoder.Vendor));

            _waveFormat = new WaveFormat(sampleRate, 16, channels, AudioEncoding.Pcm);

            // store length in bytes according to the new waveformat
            _length = SecondsToBytes(_oggDecoder.Length);
        }

        /// <summary>
        /// Convert position in seconds to byte position according to the new wave format
        /// </summary>
        /// <param name="positionInSeconds">position</param>
        /// <returns>the raw byte position</returns>
        private long SecondsToBytes(float positionInSeconds)
        {
            // 2 bytes per sample
            return (long)(positionInSeconds * _waveFormat.SampleRate * _waveFormat.Channels * _waveFormat.BytesPerSample);
        }

        /// <summary>
        /// Convert byte position to position in seconds according to the new wave format
        /// </summary>
        /// <param name="positionInBytes">the raw byte position</param>
        /// <returns>position in seconds</returns>
        private float BytesToSeconds(long positionInBytes)
        {
            return (float)TimeSpan.FromSeconds((double)positionInBytes / _waveFormat.SampleRate / _waveFormat.Channels / _waveFormat.BytesPerSample).TotalSeconds;
        }

        #region Memory Stream methods (not used)
        private Stream DecodeToMemoryStream(OggDecoder decoder)
        {
            Stream output = new MemoryStream(4096);
            foreach (PCMChunk chunk in decoder)
            {
                output.Write(chunk.Bytes, 0, chunk.Length);
            }
            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        public int ReadFromMemoryStream(byte[] buffer, int offset, int count)
        {
            lock (_lockObj)
            {
                if (_oggDecodedMemoryStream == null)
                {
                    _oggDecodedMemoryStream = DecodeToMemoryStream(_oggDecoder);
                }
                return _oggDecodedMemoryStream.Read(buffer, offset, count);
            }
        }
        #endregion

        /// <summary>
        /// Reads a sequence of bytes from the <see cref="OggSharpSource" /> and advances the position within the stream by the
        /// number of bytes read.
        /// </summary>
        /// <param name="buffer">
        /// An array of bytes. When this method returns, the <paramref name="buffer" /> contains the specified
        /// byte array with the values between <paramref name="offset" /> and (<paramref name="offset" /> +
        /// <paramref name="count" /> - 1) replaced by the bytes read from the current source.
        /// </param>
        /// <param name="offset">
        /// The zero-based byte offset in the <paramref name="buffer" /> at which to begin storing the data
        /// read from the current stream.
        /// </param>
        /// <param name="count">The maximum number of bytes to read from the current source.</param>
        /// <returns>The total number of bytes read into the buffer.</returns>
        public int Read(byte[] buffer, int offset, int remainingByteCount)
        {
            // check https://github.com/renaudbedard/nvorbis/blob/master/NVorbis/VorbisStreamDecoder.cs

            int samplesRead = 0;

            lock (_lockObj)
            {
                if (_prevPCMChunkBuffer != null)
                {
                    // get samples from the previous buffer's data
                    var cnt = Math.Min(remainingByteCount, _prevPCMChunkBuffer.Length);
                    Buffer.BlockCopy(_prevPCMChunkBuffer, 0, buffer, offset, cnt);

                    // if we have samples left over, rebuild the previous buffer array...
                    if (cnt < _prevPCMChunkBuffer.Length)
                    {
                        int remainingBytesInPrevBuffer = _prevPCMChunkBuffer.Length - cnt;
                        var buf = new byte[remainingBytesInPrevBuffer];
                        Buffer.BlockCopy(_prevPCMChunkBuffer, cnt, buf, 0, remainingBytesInPrevBuffer);
                        _prevPCMChunkBuffer = buf;
                    }
                    else
                    {
                        // if no samples left over, clear the previous buffer
                        _prevPCMChunkBuffer = null;
                    }

                    // reduce the desired sample count & increase the desired sample offset
                    remainingByteCount -= cnt;
                    offset += cnt;
                    samplesRead = cnt;
                }

                try
                {
                    while (remainingByteCount > 0 && _oggPCMChunkEnumerator.MoveNext())
                    {
                        var curPCMChunk = _oggPCMChunkEnumerator.Current;

                        // get samples from the current pcm chunk data
                        var cnt = Math.Min(remainingByteCount, curPCMChunk.Length);
                        Buffer.BlockCopy(curPCMChunk.Bytes, 0, buffer, offset, cnt);

                        // if we have samples left over, rebuild the previous buffer array...
                        if (cnt < curPCMChunk.Length)
                        {
                            int remainingBytesInChunk = curPCMChunk.Length - cnt;
                            var buf = new byte[remainingBytesInChunk];
                            Buffer.BlockCopy(curPCMChunk.Bytes, cnt, buf, 0, remainingBytesInChunk);
                            _prevPCMChunkBuffer = buf;
                        }

                        // reduce the desired sample count & increase the desired sample offset
                        remainingByteCount -= cnt;
                        offset += cnt;
                        samplesRead += cnt;
                    }
                }
                catch (System.Exception)
                {
                }
            }

            return samplesRead + remainingByteCount;
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="OggSharpSource"/> supports seeking.
        /// </summary>
        public bool CanSeek
        {
            get { return _stream.CanSeek; }
        }

        /// <summary>
        /// Gets the format of the raw data.
        /// </summary>
        public WaveFormat WaveFormat
        {
            get { return _waveFormat; }
        }

        /// <summary>
        /// Gets the length of the <see cref="OggSharpSource" /> in bytes.
        /// </summary>
        public long Length
        {
            get { return _length; }
        }

        /// <summary>
        /// Gets or sets the position of the <see cref="OggSharpSource" /> in bytes.
        /// </summary>
        public long Position
        {
            get
            {
                return CanSeek ? SecondsToBytes(_oggDecoder.Position) : 0;
            }
            set
            {
                if (!CanSeek)
                    throw new InvalidOperationException("OggSharpSource is not seekable.");
                if (value < 0 || value > Length)
                    throw new ArgumentOutOfRangeException("value");

                // _oggDecoder doesn't support seeking to 0
                if (value > 0)
                {
                    float seconds = BytesToSeconds(value);
                    seconds = Math.Min(seconds, _oggDecoder.Length);
                    _oggDecoder.Position = seconds;
                }
            }
        }

        /// <summary>
        /// Disposes the <see cref="OggSharpSource" /> instance and disposes the underlying stream.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_oggDecodedMemoryStream != null)
                {
                    _oggDecodedMemoryStream.Dispose();
                    _oggDecodedMemoryStream = null;
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