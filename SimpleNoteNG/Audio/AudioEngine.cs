using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SimpleNoteNG.Controls;
using System;
using System.Collections.Generic;
using System.IO;

public class AudioEngine : IDisposable
{
    private readonly WaveOutEvent _waveOut = new();
    private readonly MixingSampleProvider _mixer;
    private readonly List<ISampleProvider> _trackProviders = new();
    private Dictionary<int, SmbPitchShiftingSampleProvider> _activeNotes = new();

    private GainControlSampleProvider _gainProvider;
    private ISampleProvider _sampleProvider;

    public event Action<bool> OnMuteChanged;
    public event Action<double> OnVolumeChanged;

    public float Volume
    {
        get => _gainProvider?.Gain ?? 1f;
        set
        {
            if (_gainProvider != null)
            {
                _gainProvider.Gain = value;
                OnVolumeChanged?.Invoke(value * 100);
            }
        }
    }

    public bool IsMuted
    {
        get => _gainProvider?.Gain == 0f;
        set
        {
            if (_gainProvider != null)
            {
                _gainProvider.Gain = value ? 0f : Volume;
                OnMuteChanged?.Invoke(value);
            }
        }
    }

    public AudioEngine()
    {
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };
        _waveOut.Init(_mixer);
    }

    // Конструктор с загрузкой семпла
    public AudioEngine(string pianoSamplePath) : this()
    {
        LoadSample(pianoSamplePath);
    }

    public void LoadSample(string filePath)
    {
        using var reader = new AudioFileReader(filePath);
        var buffer = new float[reader.Length / 4];
        int samplesRead = reader.Read(buffer, 0, buffer.Length);

        var rawSource = new RawSourceWaveStream(
            new MemoryStream(buffer.Take(samplesRead).SelectMany(BitConverter.GetBytes).ToArray()),
            reader.WaveFormat);

        _sampleProvider = rawSource.ToSampleProvider();
        _gainProvider = new GainControlSampleProvider(_sampleProvider);
        _mixer.AddMixerInput(_gainProvider);
    }

    public void PlayNote(int midiNote)
    {
        if (_sampleProvider == null) return;

        float frequency = (float)MidiNoteToFrequency(midiNote);
        float speedRatio = frequency / 440f;

        var pitchProvider = new SmbPitchShiftingSampleProvider(_sampleProvider)
        {
            PitchFactor = speedRatio
        };

        _mixer.AddMixerInput(pitchProvider);
    }

    public void StopNote(int midiNote)
    {
        if (_activeNotes.TryGetValue(midiNote, out var provider))
        {
            _activeNotes.Remove(midiNote);
        }
    }

    public void Start() => _waveOut.Play();
    public void Stop() => _waveOut.Stop();

    // Громкость
    public void SetVolume(int trackIndex, double volume)
    {
        if (_trackProviders != null && trackIndex >= 0 && trackIndex < _trackProviders.Count)
        {
            if (_trackProviders[trackIndex] is GainControlSampleProvider gainProvider)
            {
                gainProvider.Gain = (float)(volume / 100.0);
            }
        }
    }

    // Mute
    public void SetMute(int trackIndex, bool mute)
    {
        if (_trackProviders != null && trackIndex >= 0 && trackIndex < _trackProviders.Count)
        {
            if (_trackProviders[trackIndex] is GainControlSampleProvider gainProvider)
            {
                gainProvider.Gain = mute ? 0 : 1;
            }
        }
    }

    public void SetSolo(int trackIndex)
    {
        for (int i = 0; i < _trackProviders.Count; i++)
        {
            if (_trackProviders[i] is GainControlSampleProvider gainProvider)
            {
                gainProvider.Gain = i == trackIndex ? 1 : 0;
            }
        }
    }

    private double MidiNoteToFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2, (midiNote - 69) / 12.0);
    }

    public void Dispose()
    {
        _waveOut.Stop();
        _waveOut.Dispose();

        foreach (var note in _activeNotes.Values)
        {
            _mixer.RemoveMixerInput(note); // Необязательно, но можно
        }

        _activeNotes.Clear();
    }
}