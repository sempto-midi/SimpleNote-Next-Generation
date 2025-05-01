using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleNoteNG.Audio
{
    public class Metronome : IDisposable
    {
        private int _bpm;
        private bool _isRunning;
        private Timer _timer;

        public Metronome(int bpm)
        {
            _bpm = bpm;
        }

        // Переключение состояния метронома (вкл/выкл)
        public void Toggle()
        {
            if (_isRunning)
            {
                Stop();
            }
            else
            {
                Start();
            }
        }

        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            int intervalMs = (int)(60000.0 / _bpm); // Интервал в миллисекундах
            _timer = new Timer(Tick, null, 0, intervalMs);
        }

        public void Stop()
        {
            if (!_isRunning) return;

            _isRunning = false;
            _timer?.Dispose();
            _timer = null;
        }

        private void Tick(object state)
        {
            Console.Beep(800, 50);
        }

        // Установка темпа (ударов в минуту)
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

        // Освобождение ресурсов
        public void Dispose()
        {
            Stop();
            _timer?.Dispose();
        }
    }
}
