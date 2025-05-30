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

namespace SimpleNoteNG.Pages
{
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
        private Point dragStartPoint;
        private List<Rectangle> selectedNotes = new List<Rectangle>();
        private bool isResizing = false;
        private Rectangle noteToResize = null;
        private Point resizeStartPoint;
        private List<Rectangle> createdRectangles = new List<Rectangle>();
        private Dictionary<Rectangle, Point> originalNotePositions = new Dictionary<Rectangle, Point>();

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

        private string currentScale = "C";
        private string currentGridDivision = "1/4";
        private StackPanel toolPanel;
        private bool isDraggingPlayhead = false;

        public PianoRoll(AudioEngine audioEngine, Metronome metronome)
        {
            _audioEngine = audioEngine;
            _metronome = metronome;

            InitializeComponent();
            Loaded += PianoRollPage_Loaded;
            this.PreviewKeyDown += PianoRoll_KeyDown;

            InitializePlayback();
            PianoRollCanvas.MouseRightButtonDown += PianoRollCanvas_MouseRightButtonDown;
            PianoRollCanvas.MouseLeftButtonDown += PianoRollCanvas_MouseLeftButtonDown;
            PianoRollCanvas.MouseMove += PianoRollCanvas_MouseMove;
            PianoRollCanvas.MouseLeftButtonUp += PianoRollCanvas_MouseLeftButtonUp;
        }

        private void PianoRollPage_Loaded(object sender, RoutedEventArgs e)
        {
            PianoRollCanvas.Width = TotalTakts * 100;
            PianoRollCanvas.Height = TotalKeys * KeyHeight;

            PianoKeysPanel.Height = PianoRollCanvas.Height;

            MainScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
            MainScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;

            toolPanel = (StackPanel)this.FindName("ToolPanel");

            InitializeScaleSelector();
            InitializeGridDivisionSelector();

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

        private void InitializeScaleSelector()
        {
            var scaleComboBox = new ComboBox
            {
                Width = 100,
                Margin = new Thickness(10),
                ItemsSource = new List<string> { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" },
                SelectedItem = currentScale
            };
            scaleComboBox.SelectionChanged += ScaleComboBox_SelectionChanged;
            ToolPanel.Children.Add(scaleComboBox);
        }

        private void InitializeGridDivisionSelector()
        {
            var divisionComboBox = new ComboBox
            {
                Width = 80,
                Margin = new Thickness(10),
                ItemsSource = new List<string> { "1", "1/2", "1/4", "1/8" },
                SelectedItem = currentGridDivision
            };
            divisionComboBox.SelectionChanged += DivisionComboBox_SelectionChanged;
            ToolPanel.Children.Add(divisionComboBox);
        }

        private void ScaleComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            currentScale = (string)((ComboBox)sender).SelectedItem;
            UpdateNoteVisibility();
        }

        private void DivisionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoaded)
            {
                currentGridDivision = (string)((ComboBox)sender).SelectedItem;
                UpdateGridLines();
            }
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
            }
            else if (e.Key == Key.A && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                SelectAllNotes();
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
                note.Fill = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0));
            }
            else
            {
                selectedNotes.Add(note);
                note.Fill = new SolidColorBrush(Color.FromArgb(128, 207, 110, 0));
            }
        }

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
        #endregion

        #region Note Editing
        private void PianoRollCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
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

        private void PianoRollCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(PianoRollCanvas);

            if (isResizing && noteToResize != null)
            {
                double newWidth = Math.Max(20, noteToResize.Width + (position.X - resizeStartPoint.X));
                int division = currentGridDivision switch
                {
                    "1" => 1,
                    "1/2" => 2,
                    "1/4" => 4,
                    _ => 4
                };
                double gridSize = 100.0 / division;
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

                    // Привязка к сетке
                    int division = currentGridDivision switch
                    {
                        "1" => 1,
                        "1/2" => 2,
                        "1/4" => 4,
                        _ => 4
                    };
                    double gridSize = 100.0 / division;
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
            int division = currentGridDivision switch
            {
                "1" => 1,
                "1/2" => 2,
                "1/4" => 4,
                _ => 4
            };
            double gridSize = 100.0 / division;
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
                selectedNotes.Remove(rectToRemove);
            }
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
            if (!isPlaying)
            {
                playbackStartTime = DateTime.Now - elapsedPlaybackTime;
                if (playbackTimer == null)
                {
                    InitializePlayback();
                }

                _metronome?.Start();
                isPlaying = true;
                playheadMarker.Visibility = Visibility.Visible;
                playbackTimer.Start();
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
            playheadMarker.Visibility = Visibility.Hidden;
            TimeUpdated?.Invoke("00:00:000");
            StopAllNotes();
        }

        public void PausePlayback()
        {
            if (isPlaying)
            {
                isPlaying = false;
                playbackTimer.Stop();
                playheadMarker.Visibility = Visibility.Visible;
                elapsedPlaybackTime = DateTime.Now - playbackStartTime;
                StopAllNotes();
            }
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
                    Style = (Style)FindResource("PianoKeyStyle"),
                    HorizontalContentAlignment = HorizontalAlignment.Center,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = 60 + i
                };

                if (noteName.Contains("#"))
                {
                    keyButton.Background = Brushes.Black;
                    keyButton.Foreground = Brushes.White;
                }
                else
                {
                    keyButton.Background = Brushes.White;
                    keyButton.Foreground = Brushes.Black;
                }

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

        private void UpdateGridLines()
        {
            var linesToRemove = PianoRollCanvas.Children.OfType<Line>()
                .Where(l => l.Tag?.ToString() == "grid")
                .ToList();

            foreach (var line in linesToRemove)
            {
                PianoRollCanvas.Children.Remove(line);
            }

            int division = currentGridDivision switch
            {
                "1" => 1,
                "1/2" => 2,
                "1/4" => 4,
                _ => 4
            };

            double step = 100.0 / division;

            for (double x = 0; x <= TotalTakts * 100; x += step)
            {
                PianoRollCanvas.Children.Add(new Line
                {
                    X1 = x,
                    X2 = x,
                    Y1 = 0,
                    Y2 = TotalKeys * KeyHeight,
                    Stroke = Brushes.Gray,
                    StrokeThickness = (x % 100 == 0) ? 0.5 : 0.25,
                    Tag = "grid"
                });
            }

            for (int y = 0; y <= TotalKeys * KeyHeight; y += KeyHeight)
            {
                PianoRollCanvas.Children.Add(new Line
                {
                    X1 = 0,
                    X2 = TotalTakts * 100,
                    Y1 = y,
                    Y2 = y,
                    Stroke = Brushes.Gray,
                    StrokeThickness = 0.5,
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
                    keyButton.Background = highlight ? Brushes.Yellow :
                        (keyButton.Content.ToString().Contains("#") ? Brushes.Black : Brushes.White);
                }
            }));
        }
        #endregion

        #region Event Handlers
        private void PlaybackTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                if (!isPlaying || playbackStartTime == DateTime.MinValue) return;

                elapsedPlaybackTime = DateTime.Now - playbackStartTime;
                currentPosition = elapsedPlaybackTime.TotalSeconds * pixelsPerSecond;

                if (TimeUpdated != null && playheadMarker != null && PianoRollCanvas != null)
                {
                    TimeUpdated.Invoke($"{elapsedPlaybackTime:mm\\:ss\\.fff}");
                    UpdatePlayheadPosition();
                    UpdateActiveNotes();

                    if (currentPosition >= PianoRollCanvas.ActualWidth)
                    {
                        StopPlayback();
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Playback error: {ex.Message}");
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

            foreach (var note in notesToPlay.Where(n => !activeNotesState.ContainsKey(n) || !activeNotesState[n]))
            {
                PlayNote(note);
                activeNotesState[note] = true;
            }

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

        #region Export And Import Methods
        public void ExportToMidi(string filePath, int projectId, int userId)
        {
            try
            {
                const int ticksPerQuarterNote = 480;
                var midiEvents = new MidiEventCollection(1, ticksPerQuarterNote);

                // Добавляем событие темпа
                midiEvents.AddEvent(new TempoEvent(500000, 0), 0);

                // Сортируем и добавляем ноты
                var sortedNotes = createdRectangles
                    .OrderBy(rect => Canvas.GetLeft(rect))
                    .ToList();

                foreach (var rect in sortedNotes)
                {
                    int midiNote = 60 + TotalKeys - 1 - (int)(Canvas.GetTop(rect) / KeyHeight);
                    int startTime = (int)(Canvas.GetLeft(rect) / 100 * ticksPerQuarterNote * 4);
                    int duration = (int)(rect.Width / 100 * ticksPerQuarterNote * 4);

                    midiEvents.AddEvent(new NoteOnEvent(startTime, 1, midiNote, 90, 0), 0);
                    midiEvents.AddEvent(new NoteEvent(startTime + duration, 1, MidiCommandCode.NoteOff, midiNote, 0), 0);
                }

                // Добавляем конец трека
                long endTime = sortedNotes.Any() ?
                    (long)(sortedNotes.Last().Width + Canvas.GetLeft(sortedNotes.Last())) / 100 * ticksPerQuarterNote * 4 + 10 :
                    100;
                midiEvents.AddEvent(new MetaEvent(MetaEventType.EndTrack, 0, endTime), 0);

                // Экспортируем MIDI
                MidiFile.Export(filePath, midiEvents);

                // Обновляем информацию о проекте в БД
                UpdateProjectInDatabase(filePath, projectId, userId);

                MessageBox.Show("MIDI файл успешно экспортирован и проект обновлён!",
                               "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
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
                        MessageBox.Show("Проект не найден в базе данных", "Ошибка",
                                       MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при обновлении проекта: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void ExportToMp3(string filePath)
        {
            try
            {
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
                using var memoryStream = new MemoryStream();

                // Создаем временный WAV файл в памяти
                using (var waveWriter = new WaveFileWriter(memoryStream, waveFormat))
                {
                    // Сортируем ноты по времени начала
                    var sortedNotes = createdRectangles
                        .OrderBy(rect => Canvas.GetLeft(rect))
                        .ToList();

                    double totalDuration = sortedNotes.Max(rect => Canvas.GetLeft(rect) + rect.Width) / 100 * (60.0 / tempo);
                    int totalSamples = (int)(waveFormat.SampleRate * totalDuration);
                    var buffer = new float[totalSamples * waveFormat.Channels];

                    foreach (var rect in sortedNotes)
                    {
                        int midiNote = 60 + TotalKeys - 1 - (int)(Canvas.GetTop(rect) / KeyHeight);
                        double startTime = Canvas.GetLeft(rect) / 100 * (60.0 / tempo);
                        double duration = rect.Width / 100 * (60.0 / tempo);

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

                        // Добавляем ноту в общий буфер
                        for (int i = 0; i < noteBuffer.Length; i++)
                        {
                            if (startSample * waveFormat.Channels + i < buffer.Length)
                            {
                                buffer[startSample * waveFormat.Channels + i] += noteBuffer[i];
                            }
                        }
                    }

                    // Нормализация и запись
                    float max = buffer.Max(Math.Abs);
                    if (max > 0)
                    {
                        for (int i = 0; i < buffer.Length; i++)
                        {
                            buffer[i] = buffer[i] / max * 0.8f; // Нормализация и снижение громкости
                        }
                    }

                    waveWriter.WriteSamples(buffer, 0, buffer.Length);
                }

                // Конвертация WAV в MP3
                memoryStream.Position = 0;
                using (var reader = new WaveFileReader(memoryStream))
                using (var writer = new LameMP3FileWriter(filePath, reader.WaveFormat, LAMEPreset.STANDARD))
                {
                    reader.CopyTo(writer);
                }

                MessageBox.Show("MP3 файл успешно экспортирован!", "Экспорт",
                               MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте MP3: {ex.Message}", "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public void ImportFromMidi(string filePath)
        {
            try
            {
                // Очищаем текущие ноты
                foreach (var rect in createdRectangles.ToList())
                {
                    PianoRollCanvas.Children.Remove(rect);
                    createdRectangles.Remove(rect);
                }
                selectedNotes.Clear();

                var midiFile = new MidiFile(filePath);
                int ppq = midiFile.DeltaTicksPerQuarterNote;

                // Словарь для хранения активных нот (NoteNumber -> NoteOnEvent)
                var activeNotes = new Dictionary<int, NoteOnEvent>();

                // Список для хранения пар NoteOn-NoteOff
                var notePairs = new List<(NoteOnEvent NoteOn, long NoteOffTime)>();

                // Обрабатываем все события во всех треках
                for (int track = 0; track < midiFile.Tracks; track++)
                {
                    foreach (var midiEvent in midiFile.Events[track])
                    {
                        if (midiEvent is NoteOnEvent noteOnEvent)
                        {
                            if (noteOnEvent.Velocity > 0)
                            {
                                // Это начало ноты
                                activeNotes[noteOnEvent.NoteNumber] = noteOnEvent;
                            }
                            else
                            {
                                // Velocity = 0 означает конец ноты
                                if (activeNotes.TryGetValue(noteOnEvent.NoteNumber, out var startedNote))
                                {
                                    notePairs.Add((startedNote, noteOnEvent.AbsoluteTime));
                                    activeNotes.Remove(noteOnEvent.NoteNumber);
                                }
                            }
                        }
                        else if (midiEvent is NoteEvent noteEvent &&
                                 midiEvent.CommandCode == MidiCommandCode.NoteOff)
                        {
                            // Обработка NoteOff событий
                            if (activeNotes.TryGetValue(noteEvent.NoteNumber, out var startedNote))
                            {
                                notePairs.Add((startedNote as NoteOnEvent, noteEvent.AbsoluteTime));
                                activeNotes.Remove(noteEvent.NoteNumber);
                            }
                        }
                    }
                }

                // Конвертируем в прямоугольники
                foreach (var (noteOn, noteOffTime) in notePairs)
                {
                    double startTime = noteOn.AbsoluteTime / (double)ppq; // в четвертных нотах
                    double duration = (noteOffTime - noteOn.AbsoluteTime) / (double)ppq;

                    double left = startTime * 100; // 100 пикселей на такт (четвертная нота)
                    double width = duration * 100;

                    // Преобразуем MIDI ноту в позицию на пианоролле
                    int keyIndex = noteOn.NoteNumber - 60;
                    if (keyIndex >= 0 && keyIndex < TotalKeys)
                    {
                        double top = (TotalKeys - 1 - keyIndex) * KeyHeight;

                        var rect = new Rectangle
                        {
                            Stroke = Brushes.Green,
                            Fill = new SolidColorBrush(Color.FromArgb(128, 0, 255, 0)),
                            StrokeThickness = 1,
                            Width = width,
                            Height = KeyHeight
                        };

                        Canvas.SetLeft(rect, left);
                        Canvas.SetTop(rect, top);
                        PianoRollCanvas.Children.Add(rect);
                        createdRectangles.Add(rect);
                    }
                }

                MessageBox.Show($"MIDI файл успешно импортирован! Загружено {notePairs.Count} нот.",
                    "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте MIDI: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion
    }
}