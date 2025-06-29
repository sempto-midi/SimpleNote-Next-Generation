﻿using NAudio.Wave.SampleProviders;
using NAudio.Wave;

public class Metronome : IDisposable
{
    private int _bpm;
    private bool _isRunning;
    private bool _isEnabled;
    private Timer _timer;
    private WaveOutEvent _waveOut;
    private SignalGenerator _signalGenerator;
    private int _beatCount;
    private bool disposed = false;

    public event Action<int> BeatChanged;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                if (!_isEnabled && _isRunning)
                {
                    Stop();
                }
            }
        }
    }

    public Metronome(int bpm)
    {
        _bpm = bpm;
        _waveOut = new WaveOutEvent();
        _signalGenerator = new SignalGenerator()
        {
            Type = SignalGeneratorType.Sin,
            Gain = 0.2
        };
        _waveOut.Init(_signalGenerator);
    }

    public void Toggle() => IsEnabled = !IsEnabled;

    public void Start()
    {
        if (!IsEnabled || _isRunning) return;
        _isRunning = true;
        _beatCount = -1;
        int intervalMs = (int)(60000.0 / _bpm);
        _timer = new Timer(Tick, null, 0, intervalMs);
        Tick(null);
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _timer?.Change(Timeout.Infinite, 0);
        _timer?.Dispose();
        _timer = null;
        _beatCount = 0;
        BeatChanged?.Invoke(0);
    }

    public void Pause()
    {
        if (_isRunning)
        {
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            _isRunning = false;
        }
    }

    public void Resume()
    {
        if (IsEnabled && !_isRunning)
        {
            _beatCount = -1;
            _isRunning = true;
            int intervalMs = (int)(60000.0 / _bpm);
            _timer?.Change(0, intervalMs);
            BeatChanged?.Invoke(1);
        }
    }

    private void Tick(object state)
    {
        _beatCount = (_beatCount % 4) + 1;
        _signalGenerator.Frequency = _beatCount == 1 ? 1000 : 800;
        _signalGenerator.Gain = _beatCount == 1 ? 0.3 : 0.2;
        _waveOut.Play();
        Thread.Sleep(50);
        _waveOut.Stop();
        BeatChanged?.Invoke(_beatCount);
    }

    public void SetTempo(int bpm)
    {
        if (bpm < 20 || bpm > 300)
            throw new ArgumentOutOfRangeException(nameof(bpm));
        _bpm = bpm;
        if (_isRunning)
        {
            Stop();
            Start();
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposed)
        {
            if (disposing)
            {
                Stop();
                _timer?.Dispose();
                _waveOut?.Dispose();
                _signalGenerator = null;
            }

            disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~Metronome()
    {
        Dispose(false);
    }
}