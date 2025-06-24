using NAudio.Lame;
using NAudio.Midi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SimpleNoteNG.Audio;
using SimpleNoteNG.Data;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using SimpleNoteNG.Models;
using SignalGenerator = NAudio.Wave.SampleProviders.SignalGenerator;
using Rectangle = System.Windows.Shapes.Rectangle;
using SimpleNoteNG.Windows;

namespace SimpleNoteNG.Pages
{
    public class RecordedNote
    {
        public int MidiNote { get; set; }
        public double StartTime { get; set; }
        public double EndTime { get; set; }
    }

    public partial class PianoRoll : Page, IDisposable
    {
        private const int KeyHeight = 20;
        private const int OctaveCount = 5;
        private const int KeysPerOctave = 12;
        private const int TotalKeys = OctaveCount * KeysPerOctave + 1;
        private const int TotalTakts = 200;
        private const int LowestNote = 60; // C4 - самая низкая нота в вашем piano roll

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

        private bool isDragging = false;
        private Point dragStartPoint;
        private List<Rectangle> selectedNotes = new List<Rectangle>();
        private bool isResizing = false;
        private Rectangle noteToResize = null;
        private Point resizeStartPoint;
        public List<Rectangle> createdRectangles = new List<Rectangle>();
        private Dictionary<Rectangle, Point> originalNotePositions = new Dictionary<Rectangle, Point>();

        // Музыкальные элементы
        private Dictionary<int, Button> keyButtons = new Dictionary<int, Button>();
        private readonly Dictionary<int, SignalGenerator> activeNotes = new Dictionary<int, SignalGenerator>();
        private Dictionary<int, bool> activeNotesState = new Dictionary<int, bool>();

        // Аудио компоненты
        private MidiIn midiIn;
        private WaveOutEvent waveOut;
        private MixingSampleProvider mixer;
        private Dictionary<int, ISampleProvider> noteBuffers = new Dictionary<int, ISampleProvider>();
        private Dictionary<int, SignalGenerator> noteGenerators = new Dictionary<int, SignalGenerator>();
        private string _currentSampleName = null;
        private bool disposed = false;

        // Поля для записи
        public bool isRecording
        {
            get { return _isRecording; }
            private set { _isRecording = value; }
        }

        public List<RecordedNote> RecordedNotes
        {
            get { return _recordedNotes; }
        }
        private Dictionary<int, RecordedNote> _activeRecordedNotes = new Dictionary<int, RecordedNote>();
        private List<RecordedNote> _recordedNotes = new List<RecordedNote>();
        private bool _isRecording = false;
        private DateTime _recordingStartTime;
        private const string RECORDED_NOTE_TAG = "recorded_note";

        // Внешние зависимости
        private readonly AudioEngine _audioEngine;
        private readonly Metronome _metronome;
        private int _projectId;
        private int _userId;

        // Событие для обновления времени
        public event Action<string> TimeUpdated;

        private string currentScale = "C";
        private bool isDraggingPlayhead = false;

        private string _pluginName;
        private bool _isPluginTrack;

        private bool _shouldPlay = true;

        private Dictionary<int, List<Note>> _notesByMidiNote = new();

        private CancellationTokenSource _playbackCts = new CancellationTokenSource();
        private Task _playbackTask;

        private readonly Dictionary<int, INotePlayer> _activeNotes = new Dictionary<int, INotePlayer>();

        public PianoRoll(AudioEngine audioEngine, Metronome metronome, int projectId, int userId)
        {
            _audioEngine = audioEngine;
            _metronome = metronome;
            _projectId = projectId;
            _userId = userId;

			InitializeComponent();
            Loaded += PianoRollPage_Loaded;

            InitializePlayback();
            PianoRollCanvas.MouseRightButtonDown += PianoRollCanvas_MouseRightButtonDown;
            PianoRollCanvas.MouseLeftButtonDown += PianoRollCanvas_MouseLeftButtonDown;
            PianoRollCanvas.MouseMove += PianoRollCanvas_MouseMove;
            PianoRollCanvas.MouseLeftButtonUp += PianoRollCanvas_MouseLeftButtonUp;

            MainScrollViewer.PreviewMouseWheel += MainScrollViewer_PreviewMouseWheel;
        }

        private void PianoRollPage_Loaded(object sender, RoutedEventArgs e)
        {
            PianoRollCanvas.Width = TotalTakts * 100;
            PianoRollCanvas.Height = TotalKeys * KeyHeight;

            PianoKeysPanel.Height = PianoRollCanvas.Height;

            MainScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            MainScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

            this.Focus();

            InitializePianoKeys();
            UpdateGridLines();
            DrawTaktNumbers();
            InitializeAudio();
            InitializeNoteBuffers();
            InitializeMidi();
            InitializeNoteGenerators();

            playheadMarker = new Line
            {
                X1 = 0,
                X2 = 0,
                Y1 = 0,
                Y2 = TotalKeys * KeyHeight,
                Stroke = new SolidColorBrush(Color.FromArgb(255, 110, 158, 255)),
                Visibility = Visibility.Hidden,
                StrokeThickness = 2
            };
            PianoRollCanvas.Children.Add(playheadMarker);

            playheadMarker.MouseLeftButtonDown += PlayheadMarker_MouseDown;
            playheadMarker.MouseMove += PlayheadMarker_MouseMove;
            playheadMarker.MouseLeftButtonUp += PlayheadMarker_MouseUp;
            playheadMarker.Cursor = Cursors.SizeWE;
        }
        public void InitializeEmptyProject()
        {
            // Очищаем canvas
            PianoRollCanvas.Children.Clear();
            createdRectangles.Clear();
            selectedNotes.Clear();
            currentPosition = 0;

            // Переинициализируем ключи и разметку
            InitializePianoKeys();
            UpdateGridLines();
            DrawTaktNumbers();

            // Добавляем playhead marker обратно, если он был удален
            if (!PianoRollCanvas.Children.Contains(playheadMarker))
            {
                PianoRollCanvas.Children.Add(playheadMarker);
            }

            UpdatePlayheadPosition();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Освободить UI-ресурсы
                    if (playbackTimer != null)
                    {
                        playbackTimer.Stop();
                        playbackTimer.Tick -= PlaybackTimer_Tick;
                        playbackTimer = null;
                    }

                    // Освободить MIDI
                    if (midiIn != null)
                    {
                        midiIn.MessageReceived -= MidiIn_MessageReceived;
                        midiIn.Stop();
                        midiIn.Dispose();
                        midiIn = null;
                    }

                    // Очистить список активных нот
                    _activeNotes.Clear();
                    _recordedNotes.Clear();
                    _activeRecordedNotes.Clear();
                    createdRectangles.Clear();
                    selectedNotes.Clear();
                    originalNotePositions.Clear();
                    keyButtons.Clear();
                    noteGenerators.Clear();
                    noteBuffers.Clear();
                    _notesByMidiNote.Clear();
                    _playbackCts?.Cancel();
                    _playbackCts?.Dispose();
                    if (_playbackTask != null && !_playbackTask.IsCompleted)
                    {
                        try
                        {
                            _playbackTask.Wait();
                        }
                        catch { /* Ignore exceptions on task cancellation */ }
                    }
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~PianoRoll()
        {
            Dispose(false);
        }

        public void LoadMidiFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    InitializeEmptyProject();
                    return;
                }
                if (!File.Exists(filePath))
                {
                    var box = new ModalBox(null, "MIDI file not found");
                    box.ShowDialog();
                    InitializeEmptyProject();
                    return;
                }

                ImportFromMidi(filePath);
            }
            catch (Exception ex)
            {
                var box = new ModalBox(null, $"Error loading MIDI: {ex.Message}");
                box.ShowDialog();
                InitializeEmptyProject();
            }
        }
        private void AddRectangleToCanvas(Rectangle rect, double left, double top)
        {
            if (rect == null)
            {
                throw new ArgumentNullException(nameof(rect), "Cannot add a null rectangle to the canvas.");
            }

            if (left < 0 || top < 0)
            {
                throw new ArgumentException("Left and Top values must be non-negative.", nameof(left));
            }

            Canvas.SetLeft(rect, left);
            Canvas.SetTop(rect, top);
            PianoRollCanvas.Children.Add(rect);
            createdRectangles.Add(rect);
        }
        private void UpdateNoteVisibility()
        {
            foreach (var keyButton in keyButtons.Values)
            {
                string noteName = keyButton.Content.ToString().Split(new[] { '#' })[0];
                bool isRootNote = noteName.Equals(currentScale, StringComparison.OrdinalIgnoreCase);

                keyButton.Foreground = isRootNote ? Brushes.White : Brushes.Gray;
                keyButton.Opacity = isRootNote ? 1.0 : 0.7;
            }
        }

        private void InitializePlayback()
        {
            playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            playbackTimer.Tick += PlaybackTimer_Tick;
            secondsPerBeat = 60.0 / tempo;
            pixelsPerSecond = 100 / (secondsPerBeat * 4);
            PianoRollCanvas.Width = TotalTakts * 100;
        }

        private void PianoRoll_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                if (isPlaying)
                {
                    StopPlayback();
                }
                else
                {
                    StartPlayback();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && selectedNotes.Count > 0)
            {
                DeleteSelectedNotes();
                e.Handled = true;
            }
            else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SelectAllNotes();
                e.Handled = true;
            }
        }

        #region Record Methods
        public void StartRecording()
        {
            _recordingStartTime = DateTime.Now;
            _recordedNotes.Clear();
            _activeRecordedNotes.Clear();

            // Переинициализация MIDI для надежности
            try
            {
                if (midiIn != null)
                {
                    midiIn.Stop();
                    midiIn.Dispose();
                }
                if (MidiIn.NumberOfDevices > 0)
                {
                    midiIn = new MidiIn(0);
                    midiIn.MessageReceived += MidiIn_MessageReceived;
                    midiIn.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MIDI initialization error: {ex.Message}");
            }

            _isRecording = true;
        }

        public void StopRecording()
        {
            _isRecording = false;

            // Добавляем все активные ноты в записанные
            foreach (var note in _activeRecordedNotes.Values)
            {
                note.EndTime = (DateTime.Now - _recordingStartTime).TotalSeconds;
                _recordedNotes.Add(note);
            }
            _activeRecordedNotes.Clear();

            // Отрисовываем записанные ноты
            DrawRecordedNotes();
        }

        private void DrawRecordedNotes()
        {
            // Удаляем только записанные ноты
            var toRemove = PianoRollCanvas.Children
                .OfType<Rectangle>()
                .Where(r => r.Tag?.ToString() == RECORDED_NOTE_TAG)
                .ToList();

            foreach (var rect in toRemove)
            {
                PianoRollCanvas.Children.Remove(rect);
                createdRectangles.Remove(rect);
                selectedNotes.Remove(rect);
            }

            // Добавляем ноты как обычные прямоугольники
            foreach (var note in _recordedNotes)
            {
                int keyIndex = note.MidiNote - LowestNote;
                if (keyIndex < 0 || keyIndex >= TotalKeys) continue;

                double top = (TotalKeys - 1 - keyIndex) * KeyHeight;
                double startPos = note.StartTime * pixelsPerSecond;
                double duration = Math.Max(5, (note.EndTime - note.StartTime) * pixelsPerSecond);

                var rect = new Rectangle
                {
                    Tag = RECORDED_NOTE_TAG,
                    Stroke = Brushes.Blue,
                    Fill = new SolidColorBrush(Color.FromArgb(180, 70, 130, 180)),
                    StrokeThickness = 1,
                    Width = duration,
                    Height = KeyHeight,
                    Cursor = Cursors.Hand
                };

                // Добавляем обработчики событий
                rect.MouseLeftButtonDown += Note_MouseLeftButtonDown;
                rect.MouseRightButtonDown += Note_MouseRightButtonDown;

                Canvas.SetLeft(rect, startPos);
                Canvas.SetTop(rect, top);
                PianoRollCanvas.Children.Add(rect);
                createdRectangles.Add(rect);
            }
        }
        private void Note_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle rect)
            {
                // Стандартная логика выделения
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    ToggleNoteSelection(rect);
                }
                else if (!selectedNotes.Contains(rect))
                {
                    ClearSelection();
                    selectedNotes.Add(rect);
                    rect.Fill = new SolidColorBrush(Color.FromArgb(180, 255, 165, 0));
                }

                // Начинаем перетаскивание
                isDragging = true;
                dragStartPoint = e.GetPosition(PianoRollCanvas);
                originalNotePositions.Clear();
                foreach (var note in selectedNotes)
                {
                    originalNotePositions[note] = new Point(Canvas.GetLeft(note), Canvas.GetTop(note));
                }
                e.Handled = true;
            }
        }

        private void Note_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is Rectangle rect)
            {
                PianoRollCanvas.Children.Remove(rect);
                createdRectangles.Remove(rect);
                selectedNotes.Remove(rect);

                // Удаляем соответствующую ноту из _recordedNotes
                var notePosition = Canvas.GetLeft(rect);
                var noteToRemove = _recordedNotes.FirstOrDefault(n =>
                    Math.Abs(n.StartTime * pixelsPerSecond - notePosition) < 1);
                if (noteToRemove != null)
                {
                    _recordedNotes.Remove(noteToRemove);
                }
                e.Handled = true;
            }
        }
        #endregion

        #region ScrollViewer Handlers
        private void TaktNumbersScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Синхронизация горизонтального скролла с основным ScrollViewer
            MainScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }

        private void MainScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Синхронизация горизонтального скролла с номерами тактов
            TaktNumbersScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);

            // Синхронизация вертикального скролла с клавиатурой пианино
            LeftScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
        }

        private void LeftScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // Синхронизация вертикального скролла с основным ScrollViewer
            MainScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
        }

        private void MainScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                double offset = MainScrollViewer.HorizontalOffset - (e.Delta / 3);
                MainScrollViewer.ScrollToHorizontalOffset(offset);
                TaktNumbersScrollViewer.ScrollToHorizontalOffset(offset);
                e.Handled = true;
            }
        }
        #endregion

        #region Note Editing
        private void PianoRollCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Focus();
            var position = e.GetPosition(PianoRollCanvas);

            // Проверяем попадание на область изменения размера
            foreach (var rect in createdRectangles)
            {
                Rect bounds = new Rect(Canvas.GetLeft(rect), Canvas.GetTop(rect), rect.Width, rect.Height);
                Rect resizeArea = new Rect(bounds.Right - 5, bounds.Top, 5, bounds.Height);

                if (resizeArea.Contains(position))
                {
                    isResizing = true;
                    noteToResize = rect;
                    resizeStartPoint = position;
                    Mouse.Capture(PianoRollCanvas);
                    e.Handled = true;
                    return;
                }
            }

            // Проверяем попадание на ноту
            Rectangle clickedNote = null;
            foreach (var rect in createdRectangles)
            {
                Rect bounds = new Rect(Canvas.GetLeft(rect), Canvas.GetTop(rect), rect.Width, rect.Height);
                if (bounds.Contains(position))
                {
                    clickedNote = rect;
                    break;
                }
            }

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (clickedNote != null)
                {
                    ToggleNoteSelection(clickedNote);
                }
            }
            else if (clickedNote != null)
            {
                if (!selectedNotes.Contains(clickedNote))
                {
                    ClearSelection();
                    selectedNotes.Add(clickedNote);
                    clickedNote.Fill = new SolidColorBrush(Color.FromArgb(128, 207, 110, 0));
                }

                // Начинаем перемещение
                isDragging = true;
                dragStartPoint = position;
                originalNotePositions.Clear();
                foreach (var note in selectedNotes)
                {
                    originalNotePositions[note] = new Point(Canvas.GetLeft(note), Canvas.GetTop(note));
                }
                Mouse.Capture(PianoRollCanvas);
            }
            else
            {
                ClearSelection();
                CreateNewNoteAtPosition(position);
            }

            e.Handled = true;
        }
        private void PianoRollCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.Focus();
            ClearSelection();
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
                selectedNotes.Remove(rectToRemove);
            }
        }

        private void PianoRollCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(PianoRollCanvas);

            if (isResizing && noteToResize != null)
            {
                double newWidth = Math.Max(20, noteToResize.Width + (position.X - resizeStartPoint.X));
                
                double gridSize = 100.0 / 4;
                double snappedWidth = Math.Round(newWidth / gridSize) * gridSize;
                noteToResize.Width = snappedWidth;
                resizeStartPoint = position;
            }
            else if (isDragging && selectedNotes.Count > 0)
            {
                double deltaX = position.X - dragStartPoint.X;
                double deltaY = position.Y - dragStartPoint.Y;

                foreach (var note in selectedNotes)
                {
                    double originalLeft = originalNotePositions[note].X;
                    double originalTop = originalNotePositions[note].Y;

                    double newLeft = originalLeft + deltaX;
                    double newTop = originalTop + deltaY;

                    double gridSize = 100.0 / 4;
                    newLeft = Math.Round(newLeft / gridSize) * gridSize;
                    newTop = Math.Round(newTop / KeyHeight) * KeyHeight;

                    // Проверка на выход за границы
                    newLeft = Math.Max(0, Math.Min(newLeft, PianoRollCanvas.Width - note.Width));
                    newTop = Math.Max(0, Math.Min(newTop, PianoRollCanvas.Height - note.Height));

                    Canvas.SetLeft(note, newLeft);
                    Canvas.SetTop(note, newTop);
                }
            }
        }
        private void PianoRollCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            isResizing = false;
            noteToResize = null;
            Mouse.Capture(null);
        }
        private void CreateNewNoteAtPosition(Point position)
        {
            double gridSize = 100.0 / 4;
            int keyIndex = (int)(position.Y / KeyHeight);
            double snappedX = Math.Floor(position.X / gridSize) * gridSize;
            int taktIndex = (int)(snappedX / 100);
            double noteX = taktIndex * 100 + (snappedX % 100);

            if (keyIndex >= 0 && keyIndex < TotalKeys &&
                taktIndex >= 0 && taktIndex < TotalTakts &&
                !createdRectangles.Any(rect =>
                    Math.Abs(Canvas.GetLeft(rect) - noteX) < 1 &&
                    (int)(Canvas.GetTop(rect) / KeyHeight) == keyIndex))
            {
                var newRectangle = new Rectangle
                {
                    Stroke = Brushes.Green,
                    Fill = new SolidColorBrush(Color.FromArgb(128, 207, 110, 0)),
                    StrokeThickness = 1,
                    Width = gridSize,
                    Height = KeyHeight
                };

                Canvas.SetLeft(newRectangle, noteX);
                Canvas.SetTop(newRectangle, keyIndex * KeyHeight);
                PianoRollCanvas.Children.Add(newRectangle);
                createdRectangles.Add(newRectangle);
                selectedNotes.Clear();
                selectedNotes.Add(newRectangle);
            }
        }
        private void DeleteSelectedNotes()
        {
            foreach (var note in selectedNotes.ToList())
            {
                PianoRollCanvas.Children.Remove(note);
                createdRectangles.Remove(note);
            }
            selectedNotes.Clear();
        }

        private void SelectAllNotes()
        {
            ClearSelection();
            foreach (var note in createdRectangles)
            {
                selectedNotes.Add(note);
                note.Fill = new SolidColorBrush(Color.FromArgb(128, 207, 110, 0));
            }
        }

        private void ClearSelection()
        {
            foreach (var note in selectedNotes)
            {
                note.Fill = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));
            }
            selectedNotes.Clear();
        }

        private void ToggleNoteSelection(Rectangle note)
        {
            if (selectedNotes.Contains(note))
            {
                selectedNotes.Remove(note);
                note.Fill = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0)); // стандартный цвет
            }
            else
            {
                selectedNotes.Add(note);
                note.Fill = new SolidColorBrush(Color.FromArgb(128, 207, 110, 0)); // выделенный цвет
            }
        }
        private string GetNoteName(int midiNote)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (midiNote / 12) - 1;
            return noteNames[midiNote % 12] + octave;
        }
        #endregion

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
            if (_isRecording)
                StartRecording();

            if (!isPlaying)
            {
                playbackStartTime = DateTime.Now - elapsedPlaybackTime;
                if (playbackTimer == null)
                {
                    InitializePlayback();
                }
                if (_metronome?.IsEnabled == true)
                {
                    _metronome.Start();
                }
                isPlaying = true;
                playheadMarker.Visibility = Visibility.Visible;
                playbackTimer.Start();
            }
        }
        private void UpdatePlaybackPosition()
        {
            elapsedPlaybackTime = DateTime.Now - playbackStartTime;
            currentPosition = elapsedPlaybackTime.TotalSeconds * pixelsPerSecond;

            UpdateActiveNotes();
            UpdatePlayheadPosition();
            TimeUpdated?.Invoke($"{elapsedPlaybackTime:mm\\:ss\\.fff}");

            if (currentPosition >= PianoRollCanvas.ActualWidth)
            {
                StopPlayback();
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
                _metronome?.Pause();
            }
        }

        public void StopPlayback()
        {
            isPlaying = false;
            playbackTimer.Stop();
            currentPosition = 0;
            elapsedPlaybackTime = TimeSpan.Zero;
            UpdatePlayheadPosition();
            playheadMarker.Visibility = Visibility.Hidden;
            TimeUpdated?.Invoke("00:00:000");
            StopAllNotes();
        }

        private void StopAllNotes()
        {
            foreach (var note in _activeNotes.Keys.ToList())
            {
                StopNote(note);
            }
            _activeNotes.Clear();
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
        public void SetCurrentSample(string sampleName)
        {
            _currentSampleName = sampleName;
        }
        private void PlayNote(int midiNote)
        {
            _audioEngine.PlayNote(midiNote);
            HighlightKey(midiNote, true);

        }

        private void StopNote(int midiNote)
        {
            _audioEngine.StopNote(midiNote);
            HighlightKey(midiNote, false);
        }

        public void SetPlaybackEnabled(bool enabled)
        {
            if (enabled && !isPlaying && _shouldPlay)
            {
                StartPlayback();
            }
            else if (!enabled && isPlaying)
            {
                PausePlayback();
            }
            _shouldPlay = enabled;
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

        public void SetVolume(double volume)
        {
        }
        #endregion

        #region UI Methods
        private void InitializePianoKeys()
        {
            PianoKeysPanel.Children.Clear();
            keyButtons.Clear();

            for (int i = TotalKeys - 1; i >= 0; i--)
            {
                int midiNote = LowestNote + i;
                bool isBlackKey = IsBlackKey(midiNote);

                var keyButton = new Button
                {
                    Height = KeyHeight,
                    Content = "", // Убрано название клавиши
                    Tag = midiNote,
                    Background = isBlackKey ? Brushes.Black : Brushes.White,
                    BorderBrush = Brushes.Gray,
                    BorderThickness = new Thickness(0.5),
                    Margin = new Thickness(0),
                    Padding = new Thickness(0),
                    Style = (Style)FindResource("PianoKeyStyle"),
                    Template = (ControlTemplate)FindResource("PianoKeyControlTemplate")
                };

                // Обработчики событий для предпрослушивания нот
                keyButton.PreviewMouseLeftButtonDown += (s, e) =>
                {
                    int midiNote = (int)keyButton.Tag;
                    _audioEngine.PlayNote(midiNote);
                };
                keyButton.PreviewMouseLeftButtonUp += (s, e) =>
                {
                    int midiNote = (int)keyButton.Tag;
                    _audioEngine.StopNote(midiNote);
                };

                keyButton.MouseLeave += (s, e) =>
                {
                    if (Mouse.LeftButton == MouseButtonState.Pressed)
                        StopNote(midiNote);
                };

                // Добавляем эффект нажатия
                keyButton.Template = (ControlTemplate)FindResource("PianoKeyControlTemplate");

                PianoKeysPanel.Children.Add(keyButton);
                keyButtons[midiNote] = keyButton;
            }
        }

        private bool IsBlackKey(int midiNote)
        {
            int noteIndex = midiNote % 12;
            return noteIndex == 1 || noteIndex == 3 || noteIndex == 6 || noteIndex == 8 || noteIndex == 10;
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

        private void UpdateGridLines()
        {
            // Удаляем старые линии
            var linesToRemove = PianoRollCanvas.Children.OfType<Line>()
                .Where(l => l.Tag?.ToString() == "grid")
                .ToList();

            foreach (var line in linesToRemove)
            {
                PianoRollCanvas.Children.Remove(line);
            }

            // Горизонтальные линии (клавиши)
            for (int y = 0; y <= TotalKeys * KeyHeight; y += KeyHeight)
            {
                PianoRollCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = PianoRollCanvas.Width,
                    Y1 = y,
                    Y2 = y,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 0.5,
                    Tag = "grid"
                });
            }

            // Вертикальные линии (фиксированный шаг 25px)
            for (double x = 0; x <= PianoRollCanvas.Width; x += 25)
            {
                PianoRollCanvas.Children.Add(new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 0,
                    Y2 = PianoRollCanvas.Height,
                    Stroke = Brushes.Gray,
                    StrokeThickness = (x % 100 == 0) ? 0.5 : 0.25, // Жирнее каждые 4 линии
                    Tag = "grid"
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
                    keyButton.Tag = highlight ? "Active" : null;
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

            UpdateActiveNotes(); // Воспроизводит ноты под маркером
            UpdatePlayheadPosition();

            if (currentPosition >= PianoRollCanvas.ActualWidth)
            {
                StopPlayback();
            }

            TimeUpdated?.Invoke($"{elapsedPlaybackTime:mm\\:ss\\.fff}");
        }
        private void UpdateActiveNotes()
        {
            var notesToPlay = new HashSet<int>();
            foreach (var rect in createdRectangles)
            {
                double start = Canvas.GetLeft(rect);
                double end = start + rect.Width;
                if (currentPosition >= start && currentPosition < end)
                {
                    int midiNote = LowestNote + TotalKeys - 1 - (int)(Canvas.GetTop(rect) / KeyHeight);
                    notesToPlay.Add(midiNote);
                }
            }

            // Стоп ноты вне диапазона
            foreach (var note in _activeNotes.Keys.Except(notesToPlay).ToList())
            {
                StopNote(note);
                _activeNotes.Remove(note);
            }

            // Начать новые ноты
            foreach (var note in notesToPlay.Where(n => !_activeNotes.ContainsKey(n)))
            {
                PlayNote(note);
                _activeNotes[note] = null;
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

		private void PlayheadMarker_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                isDraggingPlayhead = true;
                Mouse.Capture(playheadMarker);
                e.Handled = true;
            }
        }

        private void PlayheadMarker_MouseMove(object sender, MouseEventArgs e)
        {
            if (isDraggingPlayhead)
            {
                Point position = e.GetPosition(PianoRollCanvas);
                double newX = Math.Max(0, Math.Min(position.X, PianoRollCanvas.ActualWidth));
                Canvas.SetLeft(playheadMarker, newX);
                currentPosition = newX;

                elapsedPlaybackTime = TimeSpan.FromSeconds(currentPosition / pixelsPerSecond);
                TimeUpdated?.Invoke($"{elapsedPlaybackTime:mm\\:ss\\.fff}");
            }
        }

        private void PlayheadMarker_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (isDraggingPlayhead)
            {
                isDraggingPlayhead = false;
                Mouse.Capture(null);
                e.Handled = true;
            }
        }
        #endregion

        #region Utility Methods
        private double MidiNoteToFrequency(int midiNote)
        {
            return 440.0 * Math.Pow(2, (midiNote - 69) / 12.0);
        }

        private void InitializeMidi()
        {
            try
            {
                if (MidiIn.NumberOfDevices > 0)
                {
                    midiIn = new MidiIn(0);
                    midiIn.MessageReceived += MidiIn_MessageReceived;
                    midiIn.Start();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MIDI initialization error: {ex.Message}");
            }
        }

        private void MidiIn_MessageReceived(object sender, MidiInMessageEventArgs e)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (e.MidiEvent is NoteEvent noteEvent)
                    {
                        if (noteEvent.CommandCode == MidiCommandCode.NoteOn && noteEvent.Velocity > 0)
                        {
                            _audioEngine.PlayNote(noteEvent.NoteNumber);
                            HighlightKey(noteEvent.NoteNumber, true);

                            // Запись в режиме записи
                            if (_isRecording)
                            {
                                _activeRecordedNotes[noteEvent.NoteNumber] = new RecordedNote
                                {
                                    MidiNote = noteEvent.NoteNumber,
                                    StartTime = (DateTime.Now - _recordingStartTime).TotalSeconds
                                };
                            }
                        }
                        else if (noteEvent.CommandCode == MidiCommandCode.NoteOff ||
                                (noteEvent is NoteOnEvent noteOn && noteOn.Velocity == 0))
                        {
                            _audioEngine.StopNote(noteEvent.NoteNumber);
                            HighlightKey(noteEvent.NoteNumber, false);

                            // Завершение записи ноты
                            if (_isRecording && _activeRecordedNotes.TryGetValue(noteEvent.NoteNumber, out var note))
                            {
                                note.EndTime = (DateTime.Now - _recordingStartTime).TotalSeconds;
                                _recordedNotes.Add(note);
                                _activeRecordedNotes.Remove(noteEvent.NoteNumber);
                            }
                        }
                    }
                }));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MIDI processing error: {ex.Message}");
            }
        }
        #endregion

        #region Export And Import Methods
        public void ExportToMidi(string filePath, int projectId, int userId)
        {
            try
            {
                const int ticksPerQuarterNote = 480;
                var midiEvents = new MidiEventCollection(1, ticksPerQuarterNote);

                // Добавляем событие темпа
                midiEvents.AddEvent(new TempoEvent(500000, 0), 0);

                // Правильное преобразование координат в ноты
                var sortedNotes = createdRectangles
                    .OrderBy(rect => Canvas.GetLeft(rect))
                    .ToList();

                foreach (var rect in sortedNotes)
                {
                    // Исправленное преобразование координаты Y в MIDI ноту
                    int keyIndex = (int)(Canvas.GetTop(rect) / KeyHeight);
                    int midiNote = LowestNote + (TotalKeys - 1 - keyIndex);

                    // Проверяем диапазон нот
                    if (midiNote < 0 || midiNote > 127) continue;

                    int startTime = (int)(Canvas.GetLeft(rect) / 100 * ticksPerQuarterNote * 4);
                    int duration = (int)(rect.Width / 100 * ticksPerQuarterNote * 4);

                    midiEvents.AddEvent(new NoteOnEvent(startTime, 1, midiNote, 90, 0), 0);
                    midiEvents.AddEvent(new NoteEvent(startTime + duration, 1, MidiCommandCode.NoteOff, midiNote, 0), 0);
                }

                // Конец трека
                long endTime = sortedNotes.Any() ?
                    (long)(sortedNotes.Last().Width + Canvas.GetLeft(sortedNotes.Last())) / 100 * ticksPerQuarterNote * 4 + 10 :
                    100;
                midiEvents.AddEvent(new MetaEvent(MetaEventType.EndTrack, 0, endTime), 0);

                MidiFile.Export(filePath, midiEvents);
                UpdateProjectInDatabase(filePath, projectId, userId);

                var box = new ModalBox(null, "Success!");
                box.ShowDialog();
            }
            catch (Exception ex)
            {
                var box = new ModalBox(null, $"Error! {ex.Message}");
                box.ShowDialog();
            }
        }
        
        private void UpdateProjectInDatabase(string filePath, int projectId, int userId)
        {
            try
            {
                using (var context = new AppDbContext())
                {
                    // Получаем проект из БД
                    var project = context.Projects
                        .FirstOrDefault(p => p.ProjectId == projectId && p.UserId == userId);

                    if (project != null)
                    {
                        // Обновляем данные проекта
                        project.ProjectName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                        project.Path = filePath;
                        project.UpdatedAt = DateTime.Now;

                        // Сохраняем изменения
                        context.SaveChanges();
                    }
                    else
                    {
                        var box = new ModalBox(null, "Project not found in database");
                        box.ShowDialog();
                    }
                }
            }
            catch (Exception ex)
            {
                var box = new ModalBox(null, $"Error updating the project: {ex.Message}");
                box.ShowDialog();
            }
        }

        public void ExportToMp3(string filePath)
        {
            try
            {
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                var tempWavPath = System.IO.Path.GetTempFileName();

                using (var waveWriter = new WaveFileWriter(tempWavPath, waveFormat))
                {
                    var sortedNotes = createdRectangles
                        .OrderBy(rect => Canvas.GetLeft(rect))
                        .ToList();

                    // Учитываем что 1 такт = 4 четверти = 100px
                    // Поэтому общая длительность в секундах = (макс. позиция / 100) * (60 / tempo) * 4
                    double maxPosition = sortedNotes.Max(rect => Canvas.GetLeft(rect) + rect.Width);
                    double totalDuration = (maxPosition / 100) * (60.0 / tempo) * 4;
                    int totalSamples = (int)(waveFormat.SampleRate * totalDuration);
                    var buffer = new float[totalSamples * waveFormat.Channels];

                    foreach (var rect in sortedNotes)
                    {
                        int midiNote = 60 + TotalKeys - 1 - (int)(Canvas.GetTop(rect) / KeyHeight);

                        // Позиция в тактах (1 такт = 100px)
                        double positionInMeasures = Canvas.GetLeft(rect) / 100;
                        // Длительность в тактах
                        double durationInMeasures = rect.Width / 100;

                        // Конвертируем в секунды с учетом что 1 такт = 4 четверти
                        double startTime = positionInMeasures * (60.0 / tempo) * 4;
                        double duration = durationInMeasures * (60.0 / tempo) * 4;

                        var generator = new SignalGenerator(waveFormat.SampleRate, waveFormat.Channels)
                        {
                            Type = SignalGeneratorType.Sin,
                            Gain = 0.2,
                            Frequency = MidiNoteToFrequency(midiNote)
                        };

                        int startSample = (int)(startTime * waveFormat.SampleRate);
                        int sampleCount = (int)(duration * waveFormat.SampleRate);

                        var noteBuffer = new float[sampleCount * waveFormat.Channels];
                        generator.Read(noteBuffer, 0, noteBuffer.Length);

                        for (int i = 0; i < noteBuffer.Length; i++)
                        {
                            if (startSample * waveFormat.Channels + i < buffer.Length)
                            {
                                buffer[startSample * waveFormat.Channels + i] += noteBuffer[i];
                            }
                        }
                    }

                    // Нормализация
                    float max = buffer.Max(Math.Abs);
                    if (max > 0)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            buffer[i] = buffer[i] / max * 0.8f;
                        }
                    }

                    waveWriter.WriteSamples(buffer, 0, buffer.Length);
                }

                // Конвертация в MP3
                using (var reader = new WaveFileReader(tempWavPath))
                using (var writer = new LameMP3FileWriter(filePath, reader.WaveFormat, LAMEPreset.STANDARD))
                {
                    reader.CopyTo(writer);
                }

                File.Delete(tempWavPath);

                var box = new ModalBox(null, "The MP3 file has been successfully exported!");
                box.ShowDialog();
            }
            catch (Exception ex)
            {
                var box = new ModalBox(null, $"Error exporting MP3: {ex.Message}");
                box.ShowDialog();
            }
        }
        public void ImportFromMidi(string filePath, bool resetPosition = true)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    InitializeEmptyProject();
                    return;
                }
                if (!File.Exists(filePath))
                {
                    var box = new ModalBox(null, "TMIDI file not found");
                    box.ShowDialog();
                    InitializeEmptyProject();
                    return;
                }

                // Очищаем canvas безопасным способом
                Dispatcher.Invoke(() =>
                {
                    PianoRollCanvas.Children.Clear();
                    createdRectangles.Clear();
                    selectedNotes.Clear();
                });

                var midiFile = new MidiFile(filePath);
                int ticksPerQuarterNote = midiFile.DeltaTicksPerQuarterNote;

                // Проверяем, что ticksPerQuarterNote не равен 0
                if (ticksPerQuarterNote == 0)
                {
                    var box = new ModalBox(null, "Invalid MIDI file: DeltaTicksPerQuarterNote is zero");
                    box.ShowDialog();
                    InitializeEmptyProject();
                    return;
                }

                var noteEvents = new List<MidiNoteEvent>();

                // Собираем все ноты из всех треков
                for (int track = 0; track < midiFile.Tracks; track++)
                {
                    var events = midiFile.Events[track];
                    if (events == null) continue;

                    var activeNotes = new Dictionary<int, NoteOnEvent>();
                    foreach (var midiEvent in events)
                    {
                        if (midiEvent == null) continue;

                        if (midiEvent is NoteOnEvent noteOn && noteOn.Velocity > 0)
                        {
                            activeNotes[noteOn.NoteNumber] = noteOn;
                        }
                        else if (midiEvent is NoteEvent noteOff &&
                                 (noteOff.CommandCode == MidiCommandCode.NoteOff ||
                                  (noteOff is NoteOnEvent zeroVelNoteOn && zeroVelNoteOn.Velocity == 0)))
                        {
                            if (activeNotes.TryGetValue(noteOff.NoteNumber, out var startedNote))
                            {
                                noteEvents.Add(new MidiNoteEvent
                                {
                                    Note = noteOff.NoteNumber,
                                    Start = startedNote.AbsoluteTime,
                                    End = noteOff.AbsoluteTime
                                });
                                activeNotes.Remove(noteOff.NoteNumber);
                            }
                        }
                    }
                }

                if (!noteEvents.Any())
                {
                    var box = new ModalBox(null, "No notes found in MIDI file");
                    box.ShowDialog();
                    InitializeEmptyProject();
                    return;
                }

                // Находим минимальное время для нормализации
                long minTime = noteEvents.Min(n => n.Start);

                // Создаем все ноты в UI потоке
                Dispatcher.Invoke(() =>
                {
                    foreach (var noteEvent in noteEvents)
                    {
                        double measureStart = (noteEvent.Start - minTime) / (double)(ticksPerQuarterNote * 4);
                        double durationInMeasures = (noteEvent.End - noteEvent.Start) / (double)(ticksPerQuarterNote * 4);

                        // Конвертируем в пиксели: 1 такт = 100px
                        double startPos = measureStart * 100;
                        double rectWidth = durationInMeasures * 100;

                        // Вычисляем высоту на основе MIDI-номера
                        int keyIndex = noteEvent.Note - LowestNote;
                        if (keyIndex < 0 || keyIndex >= TotalKeys) continue;

                        double top = (TotalKeys - 1 - keyIndex) * KeyHeight;

                        // Создаем прямоугольник — ноту
                        var rect = new Rectangle
                        {
                            Stroke = Brushes.Green,
                            Fill = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0)),
                            StrokeThickness = 1,
                            Width = Math.Max(5, rectWidth), // Минимальная ширина 5px
                            Height = KeyHeight
                        };

                        Canvas.SetLeft(rect, Math.Max(0, startPos));
                        Canvas.SetTop(rect, Math.Max(0, top));

                        // Проверяем, что rect не null перед добавлением
                        if (rect != null)
                        {
                            PianoRollCanvas.Children.Add(rect);
                            createdRectangles.Add(rect);
                        }
                    }

                    if (resetPosition)
                    {
                        currentPosition = 0;
                        UpdatePlayheadPosition();
                    }

                    UpdateGridLines();
                    DrawTaktNumbers();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    var box = new ModalBox(null, $"Error importing MIDI: {ex.Message}");
                    box.ShowDialog();
                    InitializeEmptyProject();
                });
            }
        }

        #endregion
        public List<Rectangle> GetNotes()
        {
            return createdRectangles;
        }
    }
}
public class PlaybackContext
{
    public SignalGenerator Generator { get; set; }
    public bool IsPluginNote { get; set; }
}

public class MidiNoteEvent
{
    public int Note { get; set; }
    public long Start { get; set; }
    public long End { get; set; }
}

public class RecordedNote
{
    public int MidiNote { get; set; }
    public double StartTime { get; set; } // в секундах
    public double EndTime { get; set; }   // в секундах
}