﻿using System;
using System.IO;
using CommonUtils;
using CommonUtils.Audio;
using Serilog;

namespace CSCore.Codecs.WAV
{
    /// <summary>
    ///     Represents a wave file chunk. For more information see
    ///     <see href="http://www.sonicspot.com/guide/wavefiles.html#wavefilechunks" />.
    /// </summary>
    public class WaveFileChunk
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="WaveFileChunk" /> class.
        /// </summary>
        /// <param name="stream"><see cref="Stream" /> which contains the wave file chunk.</param>
        public WaveFileChunk(Stream stream)
            : this(new BinaryReader(stream))
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="WaveFileChunk" /> class.
        /// </summary>
        /// <param name="reader"><see cref="BinaryReader" /> which should be used to read the wave file chunk.</param>
        public WaveFileChunk(BinaryReader reader)
        {
            if (reader == null)
                throw new ArgumentNullException("reader");
            ChunkID = reader.ReadInt32();
            ChunkDataSize = reader.ReadUInt32();

            StartPosition = reader.BaseStream.Position;
        }

        /// <summary>
        ///     Gets the unique ID of the Chunk. Each type of chunk has its own id.
        /// </summary>
        public int ChunkID { get; private set; }

        /// <summary>
        ///     Gets the data size of the chunk.
        /// </summary>
        public long ChunkDataSize { get; private set; }

        /// <summary>
        ///     Parses the <paramref name="stream" /> and returns a <see cref="WaveFileChunk" />. Note that the position of the
        ///     stream has to point to a wave file chunk.
        /// </summary>
        /// <param name="stream"><see cref="Stream" /> which points to a wave file chunk.</param>
        /// <returns>
        ///     Instance of the <see cref="WaveFileChunk" /> class or any derived classes. It the stream does not point to a
        ///     wave file chunk the instance of the <see cref="WaveFileChunk" /> which gets return will be invalid.
        /// </returns>
        public static WaveFileChunk FromStream(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (stream.CanRead == false)
                throw new ArgumentException("stream is not readable");

            var reader = new BinaryReader(stream);
            int id = reader.ReadInt32();
            stream.Position -= 4;

            Log.Verbose("Processing chunk: {0}", StringUtils.IsAsciiPrintable(FourCC.FromFourCC(id)) ? FourCC.FromFourCC(id) : string.Format("int {0} is not FourCC", id));

            if (id == FmtChunk.FmtChunkID)
                return new FmtChunk(reader);
            if (id == DataChunk.DataChunkID)
                return new DataChunk(reader);
            if (id == ListChunk.ListChunkID)
                return new ListChunk(reader);
            return new WaveFileChunk(reader);
        }

        /// <summary>
        /// Gets the zero-based position inside of the stream at which the audio data starts.
        /// </summary>
        public long StartPosition { get; private set; }

        public long EndPosition
        {
            get { return StartPosition + ChunkDataSize; }
        }
    }
}