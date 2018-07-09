using System;
using System.IO;
using System.Diagnostics;
using System.Text;

namespace OggSharp
{
    public class OggDecodeStream : Stream
    {
        private readonly Stream decodedStream;
        public int Channels { set; get; }

        public int SampleRate { set; get; }

        public OggDecodeStream(Stream input)
        {
            if (input == null)
                throw new ArgumentNullException("input");

            // copy whole stream into memory            
            MemoryStream memoryStream = new MemoryStream();
            using (input)
            {
                input.CopyTo(memoryStream);
            }
            memoryStream.Position = 0;

            OggDecoder decoder = new OggDecoder();
            decoder.Initialize(memoryStream);

            decodedStream = DecodeStream(decoder);

            Channels = (decoder.Stereo ? 2 : 1);
            SampleRate = decoder.SampleRate;
        }

        Stream DecodeStream(OggDecoder decoder)
        {
            Stream output = new MemoryStream(4096);
            foreach (PCMChunk chunk in decoder)
            {
                output.Write(chunk.Bytes, 0, chunk.Length);
            }
            output.Seek(0, SeekOrigin.Begin);
            return output;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return true; }
        }

        public override bool CanWrite
        {
            get { return false; }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length
        {
            get { return decodedStream.Length; }
        }

        public override long Position
        {
            get
            {
                return decodedStream.Position;
            }
            set
            {
                decodedStream.Position = value;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return decodedStream.Read(buffer, offset, count);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
