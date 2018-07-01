using System;
using System.Text;
using CSCore;

namespace CommonUtils.Audio
{
    public class AudioFormat : ICloneable, IEquatable<AudioFormat>
    {
        private AudioEncoding _encoding;
        private short _channels;
        private int _sampleRate;
        private int _bytesPerSecond;

        private short _blockAlign;
        private short _bitsPerSample;
        private short _extraSize;
        private short _samplesPerBlock;
        private short _bytesPerBlock;
        private short _coefficients;
        private short _samplesPerChannel;
        private long _bytesDataSize;

        /// <summary>
        /// Gets the number of channels in the waveform-audio data. Mono data uses one channel and stereo data uses two
        /// channels.
        /// </summary>
        public virtual int Channels
        {
            get { return _channels; }
            protected internal set
            {
                _channels = (short)value;
            }
        }

        /// <summary>
        /// Gets the sample rate, in samples per second (hertz).
        /// </summary>
        public virtual int SampleRate
        {
            get { return _sampleRate; }
            protected internal set
            {
                _sampleRate = value;
            }
        }

        /// <summary>
        /// Gets the required average data transfer rate, in bytes per second. For example, 16-bit stereo at 44.1 kHz has an
        /// average data rate of 176,400 bytes per second (2 channels — 2 bytes per sample per channel — 44,100 samples per
        /// second).
        /// </summary>
        public virtual int BytesPerSecond
        {
            get { return _bytesPerSecond; }
            protected internal set
            {
                _bytesPerSecond = value;
            }
        }

        /// <summary>
        /// Gets the block alignment, in bytes. The block alignment is the minimum atomic unit of data. For PCM data, the block
        /// alignment is the number of bytes used by a single sample, including data for both channels if the data is stereo.
        /// For example, the block alignment for 16-bit stereo PCM is 4 bytes (2 channels x 2 bytes per sample).
        /// </summary>
        public virtual int BlockAlign
        {
            get { return _blockAlign; }
            protected internal set
            {
                _blockAlign = (short)value;
            }
        }

        /// <summary>
        /// Gets the number of bits, used to store one sample.
        /// </summary>
        public virtual int BitsPerSample
        {
            get { return _bitsPerSample; }
            protected internal set
            {
                _bitsPerSample = (short)value;
            }
        }

        /// <summary>
        /// Gets the size (in bytes) of extra information.
        /// </summary>
        public virtual int ExtraSize
        {
            get { return _extraSize; }
            protected internal set
            {
                _extraSize = (short)value;
            }
        }

        /// <summary>
        /// Get number of samples per block
        /// </summary>
        public virtual int SamplesPerBlock
        {
            get { return _samplesPerBlock; }
            protected internal set
            {
                _samplesPerBlock = (short)value;
            }
        }

        /// <summary>
        /// Get number of bytes per block
        /// </summary>
        public virtual int BytesPerBlock
        {
            get { return _bytesPerBlock; }
            protected internal set
            {
                _bytesPerBlock = (short)value;
            }
        }

        /// <summary>
        /// Get number of coefficients
        /// </summary>
        public virtual int Coefficients
        {
            get { return _coefficients; }
            protected internal set
            {
                _coefficients = (short)value;
            }
        }

        /// <summary>
        /// Get number of samples per channel
        /// </summary>
        public virtual int SamplesPerChannel
        {
            get { return _samplesPerChannel; }
            protected internal set
            {
                _samplesPerChannel = (short)value;
            }
        }

        /// <summary>
        /// Gets the number of data bytes
        /// </summary>
        public virtual long BytesDataSize
        {
            get { return _bytesDataSize; }
            protected internal set
            {
                _bytesDataSize = value;
            }
        }

        /// <summary>
        /// Gets the waveform-audio format type.
        /// </summary>
        public virtual AudioEncoding Encoding
        {
            get { return _encoding; }
            protected internal set { _encoding = value; }
        }

        /// <summary>
        /// Indicates whether the current object is equal to another object of the same type.
        /// </summary>
        /// <param name="other">The <see cref="AudioFormat"/> to compare with this <see cref="AudioFormat"/>.</param>
        /// <returns>true if the current object is equal to the other parameter; otherwise, false.</returns>
        public virtual bool Equals(AudioFormat other)
        {
            return Channels == other.Channels &&
                   SampleRate == other.SampleRate &&
                   BytesPerSecond == other.BytesPerSecond &&
                   BlockAlign == other.BlockAlign &&
                   BitsPerSample == other.BitsPerSample &&
                   ExtraSize == other.ExtraSize &&
                   Encoding == other.Encoding;
        }

        /// <summary>
        /// Creates a new <see cref="AudioFormat" /> object that is a copy of the current instance.
        /// </summary>
        /// <returns>A copy of the current instance.</returns>
        public virtual object Clone()
        {
            return MemberwiseClone(); //since there are value types MemberWiseClone is enough.
        }

        /// <summary>
        ///     Returns a string which describes the <see cref="AudioFormat" />.
        /// </summary>
        /// <returns>A string which describes the <see cref="AudioFormat" />.</returns>
        public override string ToString()
        {
            return GetInformation().ToString();
        }

        private StringBuilder GetInformation()
        {
            var builder = new StringBuilder();
            builder.AppendFormat("Reading Wave file: {0} format, {1} channels, {2} samp/sec\n", _encoding, _channels, _sampleRate);
            builder.AppendFormat("{0} byte/sec, {1} block align, {2} bits/samp, {3} data bytes\n", _bytesPerSecond, _blockAlign, _bitsPerSample, _bytesDataSize);
            builder.AppendFormat("{0} Extsize, {1} Samps/block, {2} bytes/block {3} Num Coefs, {4} Samps/chan\n", _extraSize, _samplesPerBlock, _bytesPerBlock, _coefficients, _samplesPerChannel);
            return builder;
        }
    }
}