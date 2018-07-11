using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using CommonUtils.Audio;
using CSCore;
using CSCore.Codecs.WAV;
using Serilog;

namespace CSCore.Codecs.ADPCM
{
    public class WavExtensibleSource : IWaveSource
    {
        [Flags]
        public enum ChannelPositions : uint
        {
            FrontLeft = 0x1,
            FrontRight = 0x2,
            FrontCenter = 0x4,
            Lfe = 0x8,
            BackLeft = 0x10,
            BackRight = 0x20,
            FrontLeftOfCenter = 0x40,
            FrontRightOfCenter = 0x80,
            BackCenter = 0x100,
            SideLeft = 0x200,
            SideRight = 0x400,
            TopCenter = 0x800,
            TopFrontLeft = 0x1000,
            TopFrontCenter = 0x2000,
            TopFrontRight = 0x4000,
            TopBackLeft = 0x8000,
            TopBackCenter = 0x10000,
            TopBackRight = 0x20000
        }

        private readonly object _lockObj = new object();

        private readonly WaveFormat _waveFormat;
        private readonly AudioFormat _audioFormat;
        private readonly ReadOnlyCollection<WaveFileChunk> _chunks;

        private bool _disposed;
        private Stream _stream;

        /// <summary>
        ///     Initializes a new instance of the <see cref="WavExtensibleSource" /> class.
        /// </summary>
        /// <param name="stream"><see cref="Stream" /> which contains raw waveform-audio data.</param>
        /// <param name="waveFormat">The format of the waveform-audio data within the <paramref name="stream" />.</param>
        public WavExtensibleSource(Stream stream, WaveFormat waveFormat, ReadOnlyCollection<WaveFileChunk> chunks)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (waveFormat == null)
                throw new ArgumentNullException("waveFormat");
            if (!stream.CanRead)
                throw new ArgumentException("stream is not readable", "stream");

            if (waveFormat.WaveFormatTag != AudioEncoding.Extensible)
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
                audioFormat.BytesPerSecond = reader.ReadInt32();
                audioFormat.BlockAlign = reader.ReadInt16();
                audioFormat.BitsPerSample = reader.ReadInt16();

                if (fmtChunk.ChunkDataSize > 16)
                {
                    audioFormat.ExtraSize = reader.ReadInt16();

                    if (audioFormat.ExtraSize == 22)
                    {
                        audioFormat.NumberOfValidBits = reader.ReadInt16();
                        if (audioFormat.NumberOfValidBits == 0)
                        {
                            audioFormat.NumberOfValidBits = audioFormat.BitsPerSample;
                        }

                        audioFormat.SpeakerPositionMask = reader.ReadUInt32();

                        // read GUID, including the data format code 
                        // The first two bytes of the GUID form the sub-code specifying the data format code, e.g. WAVE_FORMAT_PCM. 
                        // The remaining 14 bytes contain a fixed string, 
                        // «\x00\x00\x00\x00\x10\x00\x80\x00\x00\xAA\x00\x38\x9B\x71».
                        audioFormat.SubEncoding = (AudioEncoding)reader.ReadInt16();
                        var guidAfData = reader.ReadBytes(14); // GUID + Audio Format data.   
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

                Log.Verbose(audioFormat.ToString());

                // set the format identifiers to what this class returns
                waveFormat.WaveFormatTag = audioFormat.SubEncoding;
                _waveFormat = waveFormat;

                _stream = stream;
            }
        }

        /// <summary>
        ///     Reads a sequence of bytes from the <see cref="WavExtensibleSource" /> and advances the position within the stream by the
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
                var inBuffer = new byte[count];
                int readCount = _stream.Read(inBuffer, 0, count);
                if (readCount > 0)
                {
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
            get { return _stream.Position; }
            set
            {
                lock (_lockObj)
                {
                    if (value > Length || value < 0)
                        throw new ArgumentOutOfRangeException("value", "The position must not be bigger than the length or less than zero.");
                    _stream.Position = value;
                }
            }
        }

        /// <summary>
        ///     Gets the length of the <see cref="WavExtensibleSource" /> in bytes.
        /// </summary>
        public long Length
        {
            get { return 0; }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_stream != null) _stream.Dispose();
            }
            else
            {
                // TODO: Why does this happen?
                //throw new ObjectDisposedException("WavExtensibleSource");
            }
            _disposed = true;
        }

    }
}