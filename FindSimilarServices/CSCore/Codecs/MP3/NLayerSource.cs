using System;
using System.IO;
using NLayer;

namespace CSCore.Codecs.MP3
{
    public class NLayerSource : ISampleSource
    {
        private readonly MpegFile _mpegFile;
        private readonly WaveFormat _waveFormat;
        private readonly Stream _stream;
        private bool _disposed;

        public NLayerSource(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (!stream.CanRead)
                throw new ArgumentException("Stream is not readable.", "stream");

            _stream = stream;
            _mpegFile = new MpegFile(stream);
            _waveFormat = new WaveFormat(_mpegFile.SampleRate, 32, _mpegFile.Channels, AudioEncoding.IeeeFloat);
        }

        public bool CanSeek
        {
            get { return _stream.CanSeek; }
        }

        public WaveFormat WaveFormat
        {
            get { return _waveFormat; }
        }

        public long Length
        {
            get { return _mpegFile.Length; }
        }

        public long Position
        {
            get
            {
                return _mpegFile.Position;
            }
            set
            {
                _mpegFile.Position = value;
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return _mpegFile.ReadSamples(buffer, offset, count);
        }

        public void Dispose()
        {
            if (!_disposed)
                _mpegFile.Dispose();
            else
                throw new ObjectDisposedException("NLayerSource");
            _disposed = true;
        }
    }
}