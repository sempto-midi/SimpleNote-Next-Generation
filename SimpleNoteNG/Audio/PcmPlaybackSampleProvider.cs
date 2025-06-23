using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleNoteNG.Audio
{
	public class PcmPlaybackSampleProvider : ISampleProvider
	{
		private readonly ISampleProvider _source;
		private readonly float _frequency;
		private readonly int _sampleRate;

		public PcmPlaybackSampleProvider(ISampleProvider source, float frequency)
		{
			_source = source;
			_frequency = frequency;
			_sampleRate = source.WaveFormat.SampleRate;
		}

		public int Read(float[] buffer, int offset, int count)
		{
			return _source.Read(buffer, offset, count);
		}

		public WaveFormat WaveFormat => _source.WaveFormat;
	}
}
