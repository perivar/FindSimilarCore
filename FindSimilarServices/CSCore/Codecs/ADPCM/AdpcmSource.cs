using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CSCore;
using CSCore.Codecs.WAV;

namespace FindSimilarServices.CSCore.Codecs.ADPCM
{
    public class AdpcmSource : IWaveSource
    {
        private readonly object _lockObj = new object();

        private readonly AdpcmMS _adpcm;
        private readonly int _channels;
        private readonly int _blockAlign;
        private readonly int _samplesPerBlock;
        private readonly int _bytesPerBlock;
        private readonly int _nCoefs;

        private WaveFormat _waveFormat;
        private readonly ReadOnlyCollection<WaveFileChunk> _chunks;

        private bool _disposed;
        private Stream _stream;
        private readonly bool _closeStream;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AdpcmSource" /> class.
        /// </summary>
        /// <param name="stream"><see cref="Stream" /> which contains raw waveform-audio data.</param>
        /// <param name="waveFormat">The format of the waveform-audio data within the <paramref name="stream" />.</param>
        public AdpcmSource(Stream stream, WaveFormat waveFormat, ReadOnlyCollection<WaveFileChunk> chunks)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (waveFormat == null)
                throw new ArgumentNullException("waveFormat");
            if (!stream.CanRead)
                throw new ArgumentException("stream is not readable", "stream");

            if (waveFormat.WaveFormatTag != AudioEncoding.Adpcm)
                throw new ArgumentException(string.Format("Not supported encoding: {0}", waveFormat.WaveFormatTag));

            this._chunks = chunks;

            // check format
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
                var encoding = (AudioEncoding)reader.ReadInt16();
                _channels = reader.ReadInt16();
                int sampleRate = reader.ReadInt32();
                int avgBps = reader.ReadInt32();
                _blockAlign = reader.ReadInt16();
                int bitsPerSample = reader.ReadInt16();

                int extraSize = 0;
                if (fmtChunk.ChunkDataSize > 16)
                {
                    extraSize = reader.ReadInt16();

                    if (extraSize < 4)
                    {
                        throw new ArgumentException(string.Format("Format {0}: Expects extra size >= 4", encoding));
                    }

                    if (bitsPerSample != 4)
                    {
                        throw new ArgumentException(string.Format("Can only handle 4-bit MS ADPCM in wav files: {0}", bitsPerSample));
                    }

                    _samplesPerBlock = reader.ReadInt16();
                    _bytesPerBlock = MSAdpcmBytesPerBlock(_channels, _samplesPerBlock);
                    if (_bytesPerBlock > _blockAlign)
                    {
                        throw new ArgumentException(string.Format("Format {0}: samplesPerBlock {1} incompatible with blockAlign {2}", encoding, _samplesPerBlock, _bytesPerBlock));
                    }

                    _nCoefs = reader.ReadInt16();
                    if (_nCoefs < 7 || _nCoefs > 0x100)
                    {
                        throw new ArgumentException(string.Format("ADPCM file nCoefs {0} makes no sense", _nCoefs));
                    }

                    if (waveFormat.ExtraSize < 4 + 4 * _nCoefs)
                    {
                        throw new ArgumentException(string.Format("Wave header error: wExtSize {0} too small for nCoefs {1}", extraSize, _nCoefs));
                    }

                    // check the coefficients up against the stored legal table of predictor value pairs
                    int len = extraSize - 4;
                    int i, errorControl = 0;
                    var msAdpcmICoefs = new int[_nCoefs * 2];
                    for (i = 0; len >= 2 && i < 2 * _nCoefs; i++)
                    {
                        msAdpcmICoefs[i] = reader.ReadInt16();
                        len -= 2;
                        if (i < 14) errorControl += (msAdpcmICoefs[i] != AdpcmMS.MSAdpcmICoef[i / 2][i % 2] ? 1 : 0);
                    }
                    if (errorControl > 0) throw new ArgumentException(string.Format("base lsx_ms_adpcm_i_coefs differ in {0}/14 positions", errorControl));
                }

                // reset position
                stream.Position = oldPosition;
            }

            var dataChunk = (DataChunk)_chunks.FirstOrDefault(x => x is DataChunk);
            if (dataChunk != null)
            {
                // read num samples in the data chunk
                int samples = MSApcmSamples((int)dataChunk.ChunkDataSize, _channels, _blockAlign, _samplesPerBlock);
            }
            else
            {
                throw new ArgumentException("The specified stream does not contain any data chunks.", "stream");
            }

            _adpcm = new AdpcmMS();

            // set the format identifiers to what this class returns
            waveFormat.BitsPerSample = 16; // originally 4
            waveFormat.WaveFormatTag = AudioEncoding.Pcm; // originally adpcm
            _waveFormat = waveFormat;

            _stream = stream;
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

                var inBuffer = new byte[count];
                int read = _stream.Read(inBuffer, 0, count);
                if (read > 0)
                {
                    var memStream = new MemoryStream(inBuffer, 0, read);
                    var binaryReader = new BinaryReader(memStream);
                    var outBuffer = AdpcmMS.ConvertToPCM(binaryReader, _channels, _blockAlign);

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
        ///     Gets the length of the <see cref="AdpcmSource" /> in bytes.
        /// </summary>
        public long Length
        {
            get { return 0; }
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

        /*
         * lsx_ms_adpcm_samples_in(dataLen, chans, blockAlign, samplesPerBlock)
         *  returns the number of samples/channel which would be
         *  in the dataLen, given the other parameters ...
         *  if input samplesPerBlock is 0, then returns the max
         *  samplesPerBlock which would go into a block of size blockAlign
         *  Yes, it is confusing usage.
         */
        private int MSApcmSamples(
                int dataLen,
                int chans,
                int blockAlign,
                int samplesPerBlock
        )
        {
            int m, n = 0;

            if (samplesPerBlock > 0)
            {
                n = (dataLen / blockAlign) * samplesPerBlock;
                m = (dataLen % blockAlign);
            }
            else
            {
                n = 0;
                m = blockAlign;
            }
            if (m >= (int)(7 * chans))
            {
                m -= 7 * chans;          /* bytes beyond block-header */
                m = (2 * m) / chans + 2;   /* nibbles/chans + 2 in header */
                if (samplesPerBlock > 0 && m > samplesPerBlock) m = samplesPerBlock;
                n += m;
            }
            return n;
        }

        private int MSAdpcmBytesPerBlock(
            int channels,
            int samplesPerBlock)
        {
            int n = 7 * channels;  /* header */

            if (samplesPerBlock > 2)
                n += ((samplesPerBlock - 2) * channels + 1) / 2;

            return n;
        }
    }
}