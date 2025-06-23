using System;
using NAudio.Wave;

namespace SimpleNoteNG.Audio
{
    public class SampleAggregator : ISampleProvider
    {
        private readonly IWaveProvider _source;
        private readonly WaveFormat _waveFormat;

        public SampleAggregator(IWaveProvider source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _waveFormat = source.WaveFormat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            byte[] sourceBuffer = new byte[count * 2]; // 16-bit PCM
            int bytesRead = _source.Read(sourceBuffer, 0, sourceBuffer.Length);

            // Convert to float and deinterleave if stereo
            int samplesRead = bytesRead / 2;
            for (int i = 0; i < samplesRead; i++)
            {
                short sample = BitConverter.ToInt16(sourceBuffer, i * 2);
                float sample32 = sample / 32768f;
                buffer[offset + i] = sample32;
            }

            return samplesRead;
        }

        public WaveFormat WaveFormat => _waveFormat;
    }
}