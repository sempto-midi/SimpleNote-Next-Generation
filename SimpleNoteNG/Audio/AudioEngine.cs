using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SimpleNoteNG.Audio
{
    public class AudioEngine : IDisposable
    {
        private readonly WaveOutEvent _output;
        private readonly MixingSampleProvider _mixer;

        public AudioEngine()
        {
            _output = new WaveOutEvent();
            _mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            _output.Init(_mixer);
        }

        public void Start() => _output.Play();
        public void Stop() => _output.Stop();

        public void AddAudioSource(ISampleProvider source)
        {
            _mixer.AddMixerInput(source);
        }

        public void Dispose()
        {
            _output?.Dispose();
        }
    }
}
