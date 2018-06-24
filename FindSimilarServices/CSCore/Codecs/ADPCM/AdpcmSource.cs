using System;
using System.IO;
using CSCore;
using CSCore.Codecs.WAV;

namespace FindSimilarServices.CSCore.Codecs.ADPCM
{
    public class AdpcmSource : IWaveSource
    {
        private readonly object _lockObj = new object();

        private readonly Adpcm _adpcm;
        private bool _disposed;
        private Stream _stream;
        private WaveFormat _waveFormat;
        private readonly DataChunk _dataChunk;
        private readonly bool _closeStream;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AdpcmSource" /> class.
        /// </summary>
        /// <param name="stream"><see cref="Stream" /> which contains raw waveform-audio data.</param>
        /// <param name="waveFormat">The format of the waveform-audio data within the <paramref name="stream" />.</param>
        public AdpcmSource(Stream stream, WaveFormat waveFormat, DataChunk dataChunk)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (waveFormat == null)
                throw new ArgumentNullException("waveFormat");
            if (!stream.CanRead)
                throw new ArgumentException("stream is not readable", "stream");

            _adpcm = new Adpcm();
            _dataChunk = dataChunk;

            if (waveFormat.WaveFormatTag != AudioEncoding.Adpcm)
                throw new ArgumentException("Not supported encoding: {" + waveFormat.WaveFormatTag + "}");

            // fix new format identifiers
            waveFormat.BitsPerSample = 16; // originally 4
            waveFormat.WaveFormatTag = AudioEncoding.Pcm; // originally adpcm

            _stream = stream;
            _waveFormat = waveFormat;
        }

        /// <summary>
        ///     Reads a sequence of bytes from the <see cref="AdpcmSource" /> and advances the position within the stream by the
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

                count = (int)Math.Min(count, _dataChunk.DataEndPosition - _stream.Position);
                count -= count % WaveFormat.BlockAlign;
                if (count <= 0)
                    return 0;

                var inBuffer = new byte[count];
                int read = _stream.Read(inBuffer, 0, count);
                if (read > 0)
                {
                    //var outBuffer = new byte[read * 2];
                    var state = new AdpcmState();
                    //var returnCount = _adpcm.AdpcmDecoder(inBuffer, outBuffer, 0, read, state);
                    var outBuffer = _adpcm.DecodeIma(inBuffer, 0, read);

                    Buffer.BlockCopy(outBuffer, 0, buffer, 0, outBuffer.Length);
                    return outBuffer.Length;
                }
                return read;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the <see cref="IAudioSource"/> supports seeking.
        /// </summary>
        public bool CanSeek
        {
            get { return true; }
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
            get { return _stream != null ? _stream.Position - _dataChunk.DataStartPosition : 0; }
            set
            {
                lock (_lockObj)
                {
                    CheckForDisposed();

                    if (value > Length || value < 0)
                        throw new ArgumentOutOfRangeException("value", "The position must not be bigger than the length or less than zero.");
                    value -= (value % WaveFormat.BlockAlign);
                    _stream.Position = value + _dataChunk.DataStartPosition;
                }
            }
        }

        private void CheckForDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        ///     Gets the length of the <see cref="AdpcmSource" /> in bytes.
        /// </summary>
        public long Length
        {
            get { return _dataChunk != null ? _dataChunk.ChunkDataSize : 0; }
        }

        /// <summary>
        ///     Disposes the <see cref="AdpcmSource" /> and the underlying <see cref="Stream" />.
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
        ///     Disposes the <see cref="AdpcmSource" /> and the underlying <see cref="Stream" />.
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
        ~AdpcmSource()
        {
            Dispose(false);
        }
    }
}