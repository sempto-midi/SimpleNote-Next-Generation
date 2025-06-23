using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;

public class AudioEngine : IDisposable
{
    private readonly WasapiOut _waveOut;
    private readonly MixingSampleProvider _mixer;
    private AudioFileReader _audioFile;
    private GainControlSampleProvider _gainProvider;
    private readonly Dictionary<int, SmbPitchShiftingSampleProvider> _activeNotes = new();

    // Конструкторы
    public AudioEngine() : this(null) { }

    public AudioEngine(string samplePath)
    {
        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };

        _waveOut = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 30);
        _waveOut.Init(_mixer);
        _waveOut.Play();

        if (!string.IsNullOrEmpty(samplePath))
        {
            LoadSample(samplePath);
        }
    }

    // Метод для конвертации MIDI ноты в частоту
    private double MidiNoteToFrequency(int midiNote)
    {
        return 440.0 * Math.Pow(2, (midiNote - 69) / 12.0);
    }

    public void LoadSample(string filePath)
    {
        _audioFile?.Dispose();
        _audioFile = new AudioFileReader(filePath);
        _gainProvider = new GainControlSampleProvider(_audioFile.ToSampleProvider());
        _mixer.AddMixerInput(_gainProvider);
    }

    public void PlayNote(int midiNote)
    {
        if (_audioFile == null) return;

        _audioFile.Position = 0;
        float pitchFactor = (float)(MidiNoteToFrequency(midiNote) / 440.0);

        var pitchShifter = new SmbPitchShiftingSampleProvider(_audioFile.ToSampleProvider())
        {
            PitchFactor = pitchFactor
        };

        if (_activeNotes.TryGetValue(midiNote, out var oldNote))
        {
            _mixer.RemoveMixerInput(oldNote);
        }

        _activeNotes[midiNote] = pitchShifter;
        _mixer.AddMixerInput(pitchShifter);
    }

    public void StopNote(int midiNote)
    {
        if (_activeNotes.TryGetValue(midiNote, out var provider))
        {
            _mixer.RemoveMixerInput(provider);
            _activeNotes.Remove(midiNote);
        }
    }

    // Управление громкостью
    public float Volume
    {
        get => _gainProvider?.Gain ?? 1f;
        set
        {
            if (_gainProvider != null)
                _gainProvider.Gain = value;
        }
    }

    public bool IsMuted
    {
        get => _gainProvider?.Gain == 0f;
        set
        {
            if (_gainProvider != null)
                _gainProvider.Gain = value ? 0f : Volume;
        }
    }

    // Методы для микшера
    public void SetVolume(int trackIndex, double volume)
    {
        Volume = (float)(volume / 100.0);
    }

    public void SetMute(int trackIndex, bool mute)
    {
        IsMuted = mute;
    }

    public void SetSolo(int trackIndex)
    {
        Volume = 1f;
        IsMuted = false;
    }

    // Управление воспроизведением
    public void Start()
    {
        _waveOut.Play();
    }

    public void Stop()
    {
        _waveOut.Stop();
        foreach (var note in _activeNotes.Values)
        {
            _mixer.RemoveMixerInput(note);
        }
        _activeNotes.Clear();
    }

    public void Dispose()
    {
        _waveOut?.Dispose();
        _audioFile?.Dispose();
        _gainProvider = null;
    }
}