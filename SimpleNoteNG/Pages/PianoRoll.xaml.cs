using NAudio.Midi;
using NAudio.Wave.SampleProviders;
using NAudio.Wave;
using NAudio.Lame;
using SimpleNoteNG.Audio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace SimpleNoteNG.Pages
{
    /// <summary>
    /// Логика взаимодействия для PianoRoll.xaml
    /// </summary>
    public partial class PianoRoll : Page
    {
        private const int KeyHeight = 20;
        private const int OctaveCount = 5;
        private const int KeysPerOctave = 12;
        private const int TotalKeys = OctaveCount * KeysPerOctave + 1;
        private const int TotalTakts = 200;

        // UI элементы и таймеры
        private Line playheadMarker;
        private DispatcherTimer playbackTimer;

        // Состояние воспроизведения
        private double currentPosition = 0;
        public bool isPlaying = false;
        private int tempo = 120;
        private double secondsPerBeat;
        private double pixelsPerSecond;
        private DateTime playbackStartTime;
        private TimeSpan elapsedPlaybackTime;

        // Редактирование нот
        private bool isDragging = false;
        private Rectangle draggedRectangle = null;
        private Point offset;
        private bool isResizing = false;
        private Rectangle currentRectangle = null;
        private int currentTaktIndex = 0;
        private List<Rectangle> createdRectangles = new List<Rectangle>();

        // Музыкальные элементы
        private List<string> noteNames = new List<string> { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private Dictionary<int, Button> keyButtons = new Dictionary<int, Button>();
        private Dictionary<int, SignalGenerator> activeNotes = new Dictionary<int, SignalGenerator>();
        private Dictionary<int, bool> activeNotesState = new Dictionary<int, bool>();

        // Аудио компоненты
        private MidiIn midiIn;
        private WaveOutEvent waveOut;
        private MixingSampleProvider mixer;
        private Dictionary<int, ISampleProvider> noteBuffers = new Dictionary<int, ISampleProvider>();
        private Dictionary<int, SignalGenerator> noteGenerators = new Dictionary<int, SignalGenerator>();

        // Внешние зависимости
        private readonly AudioEngine _audioEngine;
        private readonly Metronome _metronome;

        // Событие для обновления времени
        public event Action<string> TimeUpdated;

        public PianoRoll(AudioEngine audioEngine, Metronome metronome)
        {
            _audioEngine = audioEngine;
            _metronome = metronome;

            InitializeComponent();
            Loaded += PianoRollPage_Loaded;

            playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            playbackTimer.Tick += PlaybackTimer_Tick;
            PianoRollCanvas.MouseRightButtonDown += PianoRollCanvas_MouseRightButtonDown;
        }

        private void PianoRollPage_Loaded(object sender, RoutedEventArgs e)
        {
            InitializePianoKeys();
            DrawGridLines();
            DrawTaktNumbers();
            InitializeAudio();
            InitializeNoteBuffers();
            InitializeMidi();
            InitializeNoteGenerators();

            secondsPerBeat = 60.0 / tempo;
            pixelsPerSecond = 100 / (secondsPerBeat * 4);

            // Инициализация маркера воспроизведения
            playheadMarker = new Line
            {
                X1 = 0,
                X2 = 0,
                Y1 = 0,
                Y2 = TotalKeys * KeyHeight,
                Stroke = Brushes.DarkOrange,
                StrokeThickness = 2
            };
            PianoRollCanvas.Children.Add(playheadMarker);
        }

        private void TaktNumbersScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0)
            {
                MainScrollViewer.ScrollToHorizontalOffset(TaktNumbersScrollViewer.HorizontalOffset);
            }
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.HorizontalChange != 0)
            {
                TaktNumbersScrollViewer.ScrollToHorizontalOffset(MainScrollViewer.HorizontalOffset);
            }

            if (e.VerticalChange != 0)
            {
                LeftScrollViewer.ScrollToVerticalOffset(MainScrollViewer.VerticalOffset);
            }
        }

        private void LeftScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0)
            {
                MainScrollViewer.ScrollToVerticalOffset(LeftScrollViewer.VerticalOffset);
            }
        }

        #region Playback Control
        public void SetTempo(int newTempo)
        {
            tempo = newTempo;
            secondsPerBeat = 60.0 / tempo;
            pixelsPerSecond = 100 / (secondsPerBeat * 4);

            if (isPlaying)
            {
                playbackTimer.Interval = TimeSpan.FromSeconds(secondsPerBeat / 4);
            }
        }

        public void StartPlayback()
        {
            if (!isPlaying)
            {
                _metronome?.Start();
                isPlaying = true;
                playbackStartTime = DateTime.Now - elapsedPlaybackTime;
                playbackTimer.Start();
            }
        }

        public void PausePlayback()
        {
            if (isPlaying)
            {
                isPlaying = false;
                playbackTimer.Stop();
                elapsedPlaybackTime = DateTime.Now - playbackStartTime;
                StopAllNotes();
            }
        }

        public void StopPlayback()
        {
            _metronome?.Stop();
            isPlaying = false;
            playbackTimer.Stop();
            currentPosition = 0;
            elapsedPlaybackTime = TimeSpan.Zero;
            UpdatePlayheadPosition();
            TimeUpdated?.Invoke("00:00:000");
            StopAllNotes();
        }

        private void StopAllNotes()
        {
            foreach (var note in activeNotes.Keys.ToList())
            {
                StopNote(note);
            }
        }
        #endregion

        #region Audio Methods
        private void InitializeAudio()
        {
            waveOut = new WaveOutEvent();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2))
            {
                ReadFully = true
            };
            waveOut.Init(mixer);
            waveOut.Play();
        }

        private void InitializeNoteBuffers()
        {
            for (int midiNote = 0; midiNote < 128; midiNote++)
            {
                var generator = new SignalGenerator
                {
                    Type = SignalGeneratorType.Sin,
                    Gain = 0.2,
                    Frequency = MidiNoteToFrequency(midiNote)
                };
                noteBuffers[midiNote] = generator.Take(TimeSpan.FromSeconds(1));
            }
        }

        private void InitializeNoteGenerators()
        {
            for (int midiNote = 0; midiNote < 128; midiNote++)
            {
                noteGenerators[midiNote] = new SignalGenerator
                {
                    Type = SignalGeneratorType.Sin,
                    Gain = 0.2,
                    Frequency = MidiNoteToFrequency(midiNote)
                };
            }
        }

        private void PlayNote(int midiNote)
        {
            if (!activeNotes.ContainsKey(midiNote))
            {
                var signalGenerator = new SignalGenerator
                {
                    Type = SignalGeneratorType.Sin,
                    Gain = 0.2,
                    Frequency = MidiNoteToFrequency(midiNote)
                };

                activeNotes[midiNote] = signalGenerator;
                UpdateAudioOutput();
                HighlightKey(midiNote, true);
            }
        }

        private void StopNote(int midiNote)
        {
            if (activeNotes.ContainsKey(midiNote))
            {
                activeNotes.Remove(midiNote);
                UpdateAudioOutput();
                HighlightKey(midiNote, false);
            }
        }

        private void UpdateAudioOutput()
        {
            waveOut?.Stop();
            var mixedSignal = MixMultipleSignals();
            if (mixedSignal != null)
            {
                waveOut?.Init(mixedSignal);
                waveOut?.Play();
            }
        }

        private ISampleProvider MixMultipleSignals()
        {
            if (activeNotes.Count == 0) return null;

            var mixingProvider = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2));
            mixingProvider.ReadFully = true;

            foreach (var generator in activeNotes.Values)
            {
                mixingProvider.AddMixerInput(generator);
            }

            return mixingProvider;
        }
        #endregion

        #region UI Methods
        private void InitializePianoKeys()
        {
            for (int i = TotalKeys - 1; i >= 0; i--)
            {
                int octave = i / KeysPerOctave;
                int noteIndex = i % KeysPerOctave;
                string noteName = noteNames[noteIndex] + (octave + 3);

                var keyButton = new Button
                {
                    Height = KeyHeight,
                    Content = noteName,
                    Padding = new Thickness(0),
                    Margin = new Thickness(0),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Brushes.Gray,
                    Background = noteName.Contains("#") ? Brushes.Black : Brushes.White,
                    Foreground = noteName.Contains("#") ? Brushes.White : Brushes.Black,
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = 60 + i
                };

                keyButton.PreviewMouseDown += Button_MouseDown;
                keyButton.PreviewMouseUp += Button_MouseUp;
                PianoKeysPanel.Children.Add(keyButton);
                keyButtons[60 + i] = keyButton;
            }
        }

        private void DrawGridLines()
        {
            for (int i = 0; i < TotalKeys; i++)
            {
                PianoRollCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = TotalTakts * 100,
                    Y1 = i * KeyHeight,
                    Y2 = i * KeyHeight,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 0.5
                });
            }

            for (int i = 0; i < TotalTakts * 400; i += 100)
            {
                PianoRollCanvas.Children.Add(new Line
                {
                    X1 = i,
                    X2 = i,
                    Y1 = 0,
                    Y2 = TotalKeys * KeyHeight,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 0.5
                });
            }
        }

        private void DrawTaktNumbers()
        {
            for (int i = 1; i <= TotalTakts; i++)
            {
                TaktNumbersPanel.Children.Add(new TextBlock
                {
                    Text = i.ToString(),
                    Width = 100,
                    TextAlignment = TextAlignment.Center,
                    Foreground = (i % 4 == 0) ? Brushes.Orange : Brushes.LightGray,
                    FontSize = (i % 4 == 0) ? 14 : 12
                });
            }
        }

        private void UpdatePlayheadPosition()
        {
            Canvas.SetLeft(playheadMarker, currentPosition);
        }

        private void HighlightKey(int midiNote, bool highlight)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (keyButtons.TryGetValue(midiNote, out var keyButton))
                {
                    keyButton.Background = highlight ? Brushes.Yellow :
                        (keyButton.Content.ToString().Contains("#") ? Brushes.Black : Brushes.White);
                }
            }));
        }
        #endregion

        #region Event Handlers
        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            if (!isPlaying) return;

            elapsedPlaybackTime = DateTime.Now - playbackStartTime;
            currentPosition = elapsedPlaybackTime.TotalSeconds * pixelsPerSecond;

            TimeUpdated?.Invoke($"{elapsedPlaybackTime:mm\\:ss\\.fff}");
            UpdatePlayheadPosition();
            UpdateActiveNotes();

            if (currentPosition >= PianoRollCanvas.Width)
            {
                StopPlayback();
            }
        }

        private void UpdateActiveNotes()
        {
            var notesToPlay = createdRectangles
                .Where(rect => currentPosition >= Canvas.GetLeft(rect) &&
                               currentPosition < Canvas.GetLeft(rect) + rect.Width)
                .Select(rect => 60 + TotalKeys - 1 - (int)(Canvas.GetTop(rect) / KeyHeight))
                .Distinct()
                .ToList();

            // Активируем ноты, которые должны звучать
            foreach (var note in notesToPlay.Where(n => !activeNotesState.ContainsKey(n) || !activeNotesState[n]))
            {
                PlayNote(note);
                activeNotesState[note] = true;
            }

            // Деактивируем ноты, которые не должны звучать
            foreach (var note in activeNotesState.Keys.Except(notesToPlay).ToList())
            {
                StopNote(note);
                activeNotesState[note] = false;
            }
        }

        private void Button_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.Tag is int midiNote)
            {
                PlayNote(midiNote);
            }
        }

        private void Button_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is Button button && button.Tag is int midiNote)
            {
                StopNote(midiNote);
            }
        }
        #endregion

        #region Note Editing
        private void PianoRollCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(PianoRollCanvas);

            // Проверка перемещения существующей ноты
            foreach (var rect in createdRectangles)
            {
                Rect bounds = new Rect(Canvas.GetLeft(rect), Canvas.GetTop(rect), rect.Width, rect.Height);
                if (bounds.Contains(position))
                {
                    isDragging = true;
                    draggedRectangle = rect;
                    offset = new Point(position.X - bounds.Left, position.Y - bounds.Top);
                    Mouse.OverrideCursor = Cursors.Hand;
                    return;
                }
            }

            // Создание новой ноты
            int keyIndex = (int)(position.Y / KeyHeight);
            int taktIndex = (int)(position.X / 100);

            if (keyIndex >= 0 && keyIndex < TotalKeys &&
                taktIndex >= 0 && taktIndex < TotalTakts &&
                !createdRectangles.Any(rect =>
                    (int)(Canvas.GetLeft(rect) / 100) == taktIndex &&
                    (int)(Canvas.GetTop(rect) / KeyHeight) == keyIndex))
            {
                var newRectangle = new Rectangle
                {
                    Stroke = Brushes.Green,
                    Fill = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0)),
                    StrokeThickness = 1,
                    Width = 100,
                    Height = KeyHeight
                };

                Canvas.SetLeft(newRectangle, taktIndex * 100);
                Canvas.SetTop(newRectangle, keyIndex * KeyHeight);
                PianoRollCanvas.Children.Add(newRectangle);
                createdRectangles.Add(newRectangle);
            }
        }

        private void PianoRollCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var position = e.GetPosition(PianoRollCanvas);
            var rectToRemove = createdRectangles.FirstOrDefault(rect =>
            {
                Rect bounds = new Rect(Canvas.GetLeft(rect), Canvas.GetTop(rect), rect.Width, rect.Height);
                return bounds.Contains(position);
            });

            if (rectToRemove != null)
            {
                PianoRollCanvas.Children.Remove(rectToRemove);
                createdRectangles.Remove(rectToRemove);
            }
        }

        private void PianoRollCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(PianoRollCanvas);

            if (isDragging && draggedRectangle != null)
            {
                int newTaktIndex = (int)((position.X - offset.X) / 100);
                int newKeyIndex = (int)((position.Y - offset.Y) / KeyHeight);

                newTaktIndex = Math.Clamp(newTaktIndex, 0, TotalTakts - 1);
                newKeyIndex = Math.Clamp(newKeyIndex, 0, TotalKeys - 1);

                if (!createdRectangles.Any(rect =>
                    (int)(Canvas.GetLeft(rect) / 100) == newTaktIndex &&
                    (int)(Canvas.GetTop(rect) / KeyHeight) == newKeyIndex &&
                    rect != draggedRectangle))
                {
                    Canvas.SetLeft(draggedRectangle, newTaktIndex * 100);
                    Canvas.SetTop(draggedRectangle, newKeyIndex * KeyHeight);
                }
            }
        }

        private void PianoRollCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            draggedRectangle = null;
            Mouse.OverrideCursor = null;
        }
        #endregion

        #region Utility Methods
        private double MidiNoteToFrequency(int midiNote)
        {
            return 440.0 * Math.Pow(2, (midiNote - 69) / 12.0);
        }

        private void InitializeMidi()
        {
            if (MidiIn.NumberOfDevices > 0)
            {
                midiIn = new MidiIn(0);
                midiIn.MessageReceived += MidiIn_MessageReceived;
                midiIn.Start();
            }
        }

        private void MidiIn_MessageReceived(object sender, MidiInMessageEventArgs e)
        {
            if (e.MidiEvent is NoteEvent noteEvent)
            {
                Dispatcher.Invoke(() =>
                {
                    if (noteEvent.CommandCode == MidiCommandCode.NoteOn)
                        PlayNote(noteEvent.NoteNumber);
                    else if (noteEvent.CommandCode == MidiCommandCode.NoteOff)
                        StopNote(noteEvent.NoteNumber);
                });
            }
        }
        #endregion

        #region Export Methods
        public void ExportToMidi(string filePath)
        {
            var midiEvents = new MidiEventCollection(1, 480);
            int currentTime = 0;

            foreach (var rect in createdRectangles)
            {
                int midiNote = 60 + TotalKeys - 1 - (int)(Canvas.GetTop(rect) / KeyHeight);
                int startTime = (int)(Canvas.GetLeft(rect) / 100 * 480);
                int duration = (int)(rect.Width / 100 * 480);

                midiEvents.AddEvent(new NoteOnEvent(currentTime + startTime, 1, midiNote, 127, 0), 0);
                midiEvents.AddEvent(new NoteEvent(currentTime + startTime + duration, 1, MidiCommandCode.NoteOff, midiNote, 0), 0);
            }

            MidiFile.Export(filePath, midiEvents);
        }

        public void ExportToMp3(string filePath)
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
            using var memoryStream = new MemoryStream();

            using (var waveWriter = new WaveFileWriter(memoryStream, waveFormat))
            {
                foreach (var rect in createdRectangles)
                {
                    int midiNote = 60 + TotalKeys - 1 - (int)(Canvas.GetTop(rect) / KeyHeight);
                    double durationSeconds = rect.Width / 100 * (60.0 / tempo);

                    var generator = new SignalGenerator
                    {
                        Type = SignalGeneratorType.Sin,
                        Gain = 0.2,
                        Frequency = MidiNoteToFrequency(midiNote)
                    };

                    var samples = new float[(int)(waveFormat.SampleRate * durationSeconds * waveFormat.Channels)];
                    generator.Read(samples, 0, samples.Length);
                    waveWriter.WriteSamples(samples, 0, samples.Length);
                }
            }

            memoryStream.Position = 0;
            using var mp3Writer = new LameMP3FileWriter(filePath, waveFormat, LAMEPreset.STANDARD);
            memoryStream.CopyTo(mp3Writer);
        }
        #endregion
    }
}
