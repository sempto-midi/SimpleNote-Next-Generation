using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleNoteNG.Audio
{
    public interface INotePlayer
    {
        void Stop();
    }

    public class PluginNotePlayer : INotePlayer
    {
        private readonly AudioEngine _engine;
        private readonly int _midiNote;

        public PluginNotePlayer(AudioEngine engine, int midiNote)
        {
            _engine = engine;
            _midiNote = midiNote;
        }

        public void Stop() => _engine.StopNote(_midiNote);
    }

    public class GeneratedNotePlayer : INotePlayer
    {
        private readonly ISampleProvider _sampleProvider;
        private readonly MixingSampleProvider _mixer;

        public GeneratedNotePlayer(ISampleProvider sampleProvider, MixingSampleProvider mixer)
        {
            _sampleProvider = sampleProvider;
            _mixer = mixer;
        }

        public void Stop()
        {
            _mixer.RemoveMixerInput(_sampleProvider);
        }
    }
}
