using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommonUtils;
using CommonUtils.Audio;
using CSCore;
using CSCore.Codecs.WAV;
using Serilog;

namespace CSCore.Codecs.ADPCM
{
    public class AdpcmSource : IWaveSource
    {
        private readonly object _lockObj = new object();

        private readonly WaveFormat _waveFormat;
        private readonly AudioFormat _audioFormat;
        private readonly ReadOnlyCollection<WaveFileChunk> _chunks;
        private readonly Adpcm.Decoder _decoder;
        private bool _disposed;
        private Stream _stream;
        private readonly long _length;

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

            if (waveFormat.WaveFormatTag != AudioEncoding.Adpcm && waveFormat.WaveFormatTag != AudioEncoding.ImaAdpcm)
            {
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
                audioFormat.AverageBytesPerSecond = reader.ReadInt32();
                audioFormat.BlockAlign = reader.ReadInt16();
                audioFormat.BitsPerSample = reader.ReadInt16();

                if (fmtChunk.ChunkDataSize > 16)
                {
                    audioFormat.ExtraSize = reader.ReadInt16();

                    if (audioFormat.BitsPerSample != 4)
                    {
                        throw new ArgumentException(string.Format("Can only handle 4-bit MS ADPCM in wav files: {0}", audioFormat.BitsPerSample));
                    }

                    if (audioFormat.ExtraSize >= 2)
                    {
                        audioFormat.SamplesPerBlock = reader.ReadInt16();
                    }

                    if (waveFormat.WaveFormatTag == AudioEncoding.Adpcm)
                    {
                        if (audioFormat.ExtraSize < 4)
                        {
                            throw new ArgumentException(string.Format("Format {0}: Expects extra size >= 4", audioFormat.Encoding));
                        }
                        if (audioFormat.BytesPerBlock > audioFormat.BlockAlign)
                        {
                            throw new ArgumentException(string.Format("Format {0}: samplesPerBlock {1} incompatible with blockAlign {2}", audioFormat.Encoding, audioFormat.SamplesPerBlock, audioFormat.BytesPerBlock));
                        }

                        audioFormat.Coefficients = reader.ReadInt16();
                        if (audioFormat.Coefficients < 7 || audioFormat.Coefficients > 0x100)
                        {
                            throw new ArgumentException(string.Format("ADPCM file number of coeffs {0} makes no sense", audioFormat.Coefficients));
                        }

                        if (waveFormat.ExtraSize < 4 + 4 * audioFormat.Coefficients)
                        {
                            throw new ArgumentException(string.Format("Wave header error: extrasize {0} too small for num coeffs {1}", audioFormat.ExtraSize, audioFormat.Coefficients));
                        }

                        // check the coefficients up against the stored legal table of predictor value pairs
                        int len = audioFormat.ExtraSize - 4;
                        int i, errorControl = 0;
                        var msAdpcmCoefficients = new int[audioFormat.Coefficients * 2];
                        for (i = 0; len >= 2 && i < 2 * audioFormat.Coefficients; i++)
                        {
                            msAdpcmCoefficients[i] = reader.ReadInt16();
                            len -= 2;
                            if (i < 14) errorControl += (msAdpcmCoefficients[i] != Adpcm.MSAdpcmICoeff[i / 2][i % 2] ? 1 : 0);
                        }
                        if (errorControl > 0) throw new ArgumentException(string.Format("base lsx_ms_adpcm_i_coefs differ in {0}/14 positions", errorControl));
                    }
                }

                // reset position
                stream.Position = oldPosition;
                reader = null;
            }

            var dataChunk = (DataChunk)_chunks.FirstOrDefault(x => x is DataChunk);
            if (dataChunk != null)
            {
                audioFormat.DataChunkSize = dataChunk.ChunkDataSize;
                audioFormat.DataStartPosition = dataChunk.DataStartPosition;
                audioFormat.DataEndPosition = dataChunk.DataEndPosition;

                switch (waveFormat.WaveFormatTag)
                {
                    case AudioEncoding.Adpcm:
                        audioFormat.BytesPerBlock = MSBytesPerBlock(audioFormat.Channels, audioFormat.SamplesPerBlock);
                        audioFormat.SamplesPerChannel = MSSamplesLength(dataChunk.ChunkDataSize, audioFormat.Channels, audioFormat.BlockAlign, audioFormat.SamplesPerBlock);
                        break;
                    case AudioEncoding.ImaAdpcm:
                        audioFormat.BytesPerBlock = ImaBytesPerBlock(audioFormat.Channels, audioFormat.SamplesPerBlock);
                        audioFormat.SamplesPerChannel = ImaSamplesLength(dataChunk.ChunkDataSize, audioFormat.Channels, audioFormat.BlockAlign, audioFormat.SamplesPerBlock);
                        break;
                }
            }
            else
            {
                throw new ArgumentException("The specified stream does not contain any data chunks.");
            }

            Log.Verbose(audioFormat.ToString());

            var decoder = new Adpcm.Decoder();
            decoder.AudioFormat = audioFormat;
            if (Adpcm.OpenDecoder(ref decoder))
            {
                _decoder = decoder;
            }
            else
            {
                throw new ArgumentException("Could not start Adpcm decoder!");
            }

            // set the format identifiers to what this class returns
            waveFormat.BitsPerSample = 16; // originally 4
            waveFormat.WaveFormatTag = AudioEncoding.Pcm; // originally adpcm
            _waveFormat = waveFormat;

            // calculate byte length when 16 bits per sample
            // double duration = ((double)audioFormat.SamplesPerChannel / (double)audioFormat.SampleRate);
            // long length = WaveFormat.SecondsToBytes(duration);
            // calculating byte length via duration will sometimes be 1 byte off
            _length = (long)(audioFormat.SamplesPerChannel * waveFormat.Channels * (waveFormat.BitsPerSample / 8));

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
                if (buffer == null)
                    throw new ArgumentNullException("buffer");
                if (offset < 0)
                    throw new ArgumentOutOfRangeException("offset");
                if (count < 0)
                    throw new ArgumentOutOfRangeException("count");

                CheckForDisposed();

                // ensure we are comparing apples with apples
                // Length is byte position in the _resulting_ stream which is almost 4 times 
                // as large as the actual data length (_audioFormat.DataEndPosition)
                long remainingBytes = _audioFormat.DataEndPosition - _stream.Position;
                count = (int)Math.Min(count, remainingBytes);

                if (count <= 0)
                {
                    return 0;
                }

                // however the returned buffer need to fit within the passed buffer length
                // since adpcm returns approx. 4 times the length of the original stream
                if (count * 4 > buffer.Length)
                {
                    count /= 4;
                }

                if (count < _audioFormat.BlockAlign)
                {
                    // we have a partial block
                    Log.Verbose("Partial block found: {0} < block-size {1}", count, _audioFormat.BlockAlign);
                    // return 0; // Only for testing seeking - must be removed
                }
                else
                {
                    // check that we are reading the maximum amount of bytes left 
                    // which is a multiple of the blockalign count 
                    count -= count % _audioFormat.BlockAlign;
                }

                var inBuffer = new byte[count];
                int readCount = _stream.Read(inBuffer, 0, count);
                if (readCount > 0)
                {
                    var outBuffer = Adpcm.DecodeAudio(_decoder, inBuffer, readCount);
                    Buffer.BlockCopy(outBuffer, 0, buffer, 0, outBuffer.Length);
                    return outBuffer.Length;
                }
                return readCount;
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
            get
            {
                if (_disposed)
                    return 0;
                return _stream.Position - _audioFormat.DataStartPosition;
            }
            set
            {
                CheckForDisposed();

                if (value > Length || value < 0)
                    throw new ArgumentOutOfRangeException("value", "The position must not be bigger than the length or less than zero.");

                double numBytes = (double)value / (double)_waveFormat.Channels / (double)(_waveFormat.BitsPerSample / 8);
                double blockNum = numBytes / (double)_audioFormat.SamplesPerBlock;
                double blockRelativePos = blockNum * (double)_audioFormat.BlockAlign;
                blockRelativePos -= blockRelativePos % _audioFormat.BlockAlign;
                double blockActualPos = blockRelativePos + _audioFormat.DataStartPosition;

                _stream.Position = (long)blockActualPos;
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
            get { return _length; }
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
        private static long MSSamplesLength(
                long dataLen,
                int chans,
                int blockAlign,
                int samplesPerBlock
        )
        {
            long m, n = 0;

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
            if (m >= (7 * chans))
            {
                m -= 7 * chans;             /* bytes beyond block-header */
                m = (2 * m) / chans + 2;    /* nibbles/chans + 2 in header */
                if (samplesPerBlock > 0 && m > samplesPerBlock) m = samplesPerBlock;
                n += m;
            }
            return n;
        }

        private static int MSBytesPerBlock(
            int channels,
            int samplesPerBlock)
        {
            int n = 7 * channels;  /* header */

            if (samplesPerBlock > 2)
                n += (((int)samplesPerBlock - 2) * channels + 1) / 2;

            return n;
        }

        /*
         * lsxImaSamplesIn(dataLen, chans, blockAlign, samplesPerBlock)
         *  returns the number of samples/channel which would go
         *  in the dataLen, given the other parameters ...
         *  if input samplesPerBlock is 0, then returns the max
         *  samplesPerBlock which would go into a block of size blockAlign
         *  Yes, it is confusing.
         */
        private static long ImaSamplesLength(
          long dataLen,
          int chans,
          int blockAlign,
          int samplesPerBlock
        )
        {
            long m, n;

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
            if (m >= (int)4 * chans)
            {
                m -= 4 * chans;    /* number of bytes beyond block-header */
                m /= 4 * chans;    /* number of 4-byte blocks/channel beyond header */
                m = 8 * m + 1;     /* samples/chan beyond header + 1 in header */
                if (samplesPerBlock > 0 && m > samplesPerBlock) m = samplesPerBlock;
                n += m;
            }

            return n;
        }

        /*
         * int lsxImaBytesPerBlock(chans, samplesPerBlock)
         * return minimum blocksize which would be required
         * to encode number of chans with given samplesPerBlock
         */
        private static int ImaBytesPerBlock(
          int chans,
          int samplesPerBlock
        )
        {
            int n;
            /* per channel, ima has blocks of len 4, the 1st has 1st sample, the others
             * up to 8 samples per block,
             * so number of later blocks is (nsamp-1 + 7)/8, total blocks/chan is
             * (nsamp-1+7)/8 + 1 = (nsamp+14)/8
             */
            n = ((int)samplesPerBlock + 14) / 8 * 4 * chans;
            return n;
        }

    }
}