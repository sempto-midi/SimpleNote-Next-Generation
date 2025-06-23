using NAudio.Wave;
using System;

namespace SimpleNoteNG.Controls
{
    public class SilenceSampleProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; }

        public SilenceSampleProvider(WaveFormat waveFormat)
        {
            WaveFormat = waveFormat;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }
    }
}