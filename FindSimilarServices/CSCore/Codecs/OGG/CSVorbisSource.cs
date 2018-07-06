using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommonUtils.Audio;
using CSCore;
using CSCore.Codecs.WAV;
using OggDecoder;

namespace FindSimilarServices.CSCore.Codecs.OGG
{
    public class CSVorbisSource : IWaveSource
    {
        private readonly object _lockObj = new object();

        private readonly WaveFormat _waveFormat;
        private readonly AudioFormat _audioFormat;
        private readonly ReadOnlyCollection<WaveFileChunk> _chunks;
        private readonly OggDecodeStream _oggDecodeStream;

        private bool _disposed;
        private Stream _stream;
        private readonly bool _closeStream;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CSVorbisSource" /> class.
        /// </summary>
        /// <param name="stream"><see cref="Stream" /> which contains raw waveform-audio data.</param>
        /// <param name="waveFormat">The format of the waveform-audio data within the <paramref name="stream" />.</param>
        public CSVorbisSource(Stream stream, WaveFormat waveFormat, ReadOnlyCollection<WaveFileChunk> chunks)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (waveFormat == null)
                throw new ArgumentNullException("waveFormat");
            if (!stream.CanRead)
                throw new ArgumentException("stream is not readable", "stream");

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

            this._chunks = chunks;

            // check format
            var audioFormat = new AudioFormat();
            this._audioFormat = audioFormat;

            var fmtChunk = (FmtChunk)_chunks.FirstOrDefault(x => x is FmtChunk);
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
                audioFormat.BytesPerSecond = reader.ReadInt32();
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

            var dataChunk = (DataChunk)_chunks.FirstOrDefault(x => x is DataChunk);
            if (dataChunk != null)
            {
                audioFormat.BytesDataSize = dataChunk.ChunkDataSize;
            }
            else
            {
                throw new ArgumentException("The specified stream does not contain any data chunks.");
            }

            Debug.WriteLine(audioFormat.ToString());

            // set the format identifiers to what this class returns
            waveFormat.BitsPerSample = 16;
            waveFormat.WaveFormatTag = AudioEncoding.Pcm;
            _waveFormat = waveFormat;

            // TODO: check with reference implementation
            // https://github.com/xiph/vorbis
            _oggDecodeStream = new OggDecodeStream(stream, true);
            _stream = stream;
        }

        /// <summary>
        ///     Reads a sequence of bytes from the <see cref="CSVorbisSource" /> and advances the position within the stream by the
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
                CheckForDisposed();

                return _oggDecodeStream.Read(buffer, offset, count);
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

        /// <summary>
        ///     Gets or sets the position of the <see cref="RawDataReader" /> in bytes.
        /// </summary>
        public long Position
        {
            get { return _stream.Position; }
            set
            {
                lock (_lockObj)
                {
                    CheckForDisposed();

                    if (value > Length || value < 0)
                        throw new ArgumentOutOfRangeException("value", "The position must not be bigger than the length or less than zero.");
                    _stream.Position = value;
                }
            }
        }

        private void CheckForDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        ///     Gets the length of the <see cref="CSVorbisSource" /> in bytes.
        /// </summary>
        public long Length
        {
            get { return 0; }
        }

        /// <summary>
        ///     Disposes the <see cref="CSVorbisSource" /> and the underlying <see cref="Stream" />.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        ///     Disposes the <see cref="CSVorbisSource" /> and the underlying <see cref="Stream" />.
        /// </summary>
        /// <param name="disposing">
        ///     True to release both managed and unmanaged resources; false to release only unmanaged
        ///     resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (_stream != null)
            {
                _stream.Dispose();
                _stream = null;
            }
        }

        /// <summary>
        /// Destructor which calls the <see cref="Dispose(bool)"/> method.
        /// </summary>
        ~CSVorbisSource()
        {
            Dispose(false);
        }
    }
}