using NAudio.Wave;
using SimpleNoteNG.Controls;
using SimpleNoteNG.Pages;

namespace SimpleNoteNG.Models
{
    public class PluginTrack : IDisposable
    {
        public string FilePath { get; }
        private GainControlSampleProvider _gainProvider;

        public PluginTrack(string filePath)
        {
            FilePath = filePath;
            var reader = new AudioFileReader(filePath);
            _gainProvider = new GainControlSampleProvider(reader.ToSampleProvider());
        }

        public ISampleProvider GetOutput() => _gainProvider;

        public void SetVolume(double volume)
        {
            _gainProvider.Gain = (float)(volume / 100.0);
        }

        public void SetMute(bool mute)
        {
            _gainProvider.Gain = mute ? 0 : 1;
        }

        public void Dispose()
        {
            // AudioFileReader не всегда можно Dispose(), но можно оставить заглушку
            _gainProvider = null;
        }
    }
}