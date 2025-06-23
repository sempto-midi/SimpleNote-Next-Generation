using NAudio.Wave;
using System;

public class GainControlSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    public float Gain { get; set; } = 1f;

    public WaveFormat WaveFormat => _source.WaveFormat;

    public GainControlSampleProvider(ISampleProvider source)
    {
        _source = source;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        for (int i = 0; i < samplesRead; i++)
        {
            buffer[offset + i] *= Gain;
        }
        return samplesRead;
    }
}