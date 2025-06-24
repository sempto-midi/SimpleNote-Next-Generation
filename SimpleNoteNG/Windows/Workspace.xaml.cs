using NAudio.Midi;
using NAudio.Wave;
using SimpleNoteNG.Audio;
using SimpleNoteNG.Controls;
using SimpleNoteNG.Data;
using SimpleNoteNG.Models;
using SimpleNoteNG.Pages;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Brushes = System.Windows.Media.Brushes;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace SimpleNoteNG.Windows
{
    /// <summary>
    /// Логика взаимодействия для Workspace.xaml
    /// </summary>
    public partial class Workspace : Window, IDisposable
    {
        int projectId;
        int userId;

        private readonly AudioEngine _audioEngine;
        private Metronome _metronome;
        private PianoRoll _pianoRoll;

        private bool disposed = false;

        private List<PluginTrack> _pluginTracks = new List<PluginTrack>();
		private int _currentTrackIndex = 0;

        private readonly Dictionary<string, PianoRoll> _pianoRolls = new Dictionary<string, PianoRoll>();
        private PianoRoll _currentPianoRoll;

        private const int KeyHeight = 20;
        private const int TotalKeys = 60;
        private const int LowestNote = 36;

        public Workspace(int projectId, int userId)
		{
			InitializeComponent();
			this.projectId = projectId;
			this.userId = userId;
            string pianoSamplePath = Path.Combine("C:\\SNprojects", "piano.wav");
            _audioEngine = new AudioEngine(pianoSamplePath);
            _metronome = new Metronome(int.Parse(Tempo.Text));
            InitializeMixerChannels(8); // 7 + 1
			InitializeChannelRack();
			LoadPianoRoll();
            _metronome.BeatChanged += Metronome_BeatChanged;

            this.PreviewKeyDown += Workspace_PreviewKeyDown;
            this.Focusable = true;
            this.Focus(); // Чтобы окно получало фокус и ловило клавиши
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // Освободить управляемые ресурсы
                    _pianoRoll?.Dispose();
                    _audioEngine?.Dispose();
                    _metronome?.Dispose();

                    foreach (var track in _pluginTracks)
                    {
                        track.Dispose();
                    }
                    _pluginTracks.Clear();

                    // Отписываемся от событий
                    this.PreviewKeyDown -= Workspace_PreviewKeyDown;
                }

                // Очистка неуправляемых ресурсов

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Workspace()
        {
            Dispose(false);
        }

        private void Workspace_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                e.Handled = true; // Предотвращаем стандартное поведение ОС
            }
        }

        private void Metronome_BeatChanged(int beat)
        {
            Dispatcher.Invoke(() =>
            {
                Beat1.Fill = beat == 1 ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.Gray;
                Beat2.Fill = beat == 2 ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.Gray;
                Beat3.Fill = beat == 3 ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.Gray;
                Beat4.Fill = beat == 4 ? System.Windows.Media.Brushes.Orange : System.Windows.Media.Brushes.Gray;
            });
        }
        private void LoadPianoRoll()
        {
            _pianoRoll = new PianoRoll(_audioEngine, _metronome, projectId, userId);
            _pianoRoll.TimeUpdated += time => TimerDisplay.Text = time;
            MainFrame.Navigate(_pianoRoll);
        }
        public void LoadProject(string filePath)
        {
            _pianoRoll.LoadMidiFile(filePath);
        }

        public void InitializeEmptyProject()
        {
            _pianoRoll.InitializeEmptyProject();
        }
        private void InitializeMixerChannels(int count)
        {
            // Очищаем панель перед инициализацией
            MixerPanel.Children.Clear();

            // Создаём один единственный канал для загруженного семпла
            var sampleChannel = new MixerChannel(
                new SilenceSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)),
                "Sample", 0);

            sampleChannel.OnVolumeChanged += vol => _audioEngine.SetVolume(0, vol);
            sampleChannel.OnMuteChanged += mute => _audioEngine.SetMute(0, mute);
            sampleChannel.OnSoloChanged += solo =>
            {
                if (solo) _audioEngine.SetSolo(0);
            };

            MixerPanel.Children.Add(sampleChannel);

            // Мастер-канал всегда в конце
            var masterChannel = new MixerChannel(
                new SilenceSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)),
                "Master", 1, isMaster: true);

            masterChannel.IsSoloVisible = false;
            masterChannel.OnVolumeChanged += vol => _audioEngine.SetVolume(1, vol);
            masterChannel.OnMuteChanged += mute => _audioEngine.SetMute(1, mute);

            MixerPanel.Children.Add(masterChannel);
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            _pianoRoll.StopPlayback();
            _audioEngine.Stop();
            _metronome?.Stop();
            using (var context = new AppDbContext())
            {
                var user = context.Users.FirstOrDefault(u => u.UserId == userId);
                ProjectsWindow win = new ProjectsWindow(user.Username, userId);
                win.Show();
                this.Close();
            }
            
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            _audioEngine.Start();
            _pianoRoll.StartPlayback();
            _metronome?.Start();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _pianoRoll.StopPlayback();
            _metronome?.Stop();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (_pianoRoll != null)
            {
                _pianoRoll.PausePlayback();
            }
        }

        public void ExportToMIDI_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MIDI Files (*.mid)|*.mid",
                DefaultExt = ".mid",
                FileName = $"Project_{projectId}_{DateTime.Now:yyyyMMdd_HHmmss}.mid"
            };
            if (saveDialog.ShowDialog() == true)
            {
                _pianoRoll.ExportToMidi(saveDialog.FileName, projectId, userId);

                // После успешного экспорта обновляем список проектов
                UpdateProjectList();
            }
        }
        private void UpdateProjectList()
        {
            using (var context = new AppDbContext())
            {
                var project = context.Projects.FirstOrDefault(p => p.ProjectId == projectId && p.UserId == userId);
                if (project != null)
                {
                    project.Path = Path.Combine("C:\\SNprojects", $"{projectId}.mid");
                    project.ProjectName = $"Project {projectId}";
                    project.UpdatedAt = DateTime.Now;
                    context.SaveChanges();
                }
            }
        }
        public void ExportToMP3_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MP3 Files (*.mp3)|*.mp3",
                DefaultExt = ".mp3",
                FileName = $"Project_{projectId}_{DateTime.Now:yyyyMMdd_HHmmss}.mp3"
            };

            if (saveDialog.ShowDialog() == true)
            {
                _pianoRoll.ExportToMp3(saveDialog.FileName);
            }
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "MIDI Files (*.mid;*.midi)|*.mid;*.midi",
                DefaultExt = ".mid"
            };

            if (openDialog.ShowDialog() == true)
            {
                _pianoRoll.ImportFromMidi(openDialog.FileName);
            }
        }

        private void Metronome_Click(object sender, RoutedEventArgs e)
        {
            _metronome?.Toggle();
            // Визуальная индикация состояния
            if (_metronome != null)
            {
                Metronome.Background = _metronome.IsEnabled
                    ? new System.Windows.Media.SolidColorBrush(
                        System.Windows.Media.Color.FromArgb(255, 207, 110, 0))
                    : System.Windows.Media.Brushes.Transparent;
            }
        }
        private void Tempo_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(Tempo.Text, out int newTempo) && newTempo >= 20 && newTempo <= 300)
            {
                _metronome?.SetTempo(newTempo);
                _pianoRoll?.SetTempo(newTempo);

                // Обновляем метроном, если он активен
                if (_metronome?.IsEnabled == true && _pianoRoll?.isPlaying == true)
                {
                    _metronome.Stop();
                    _metronome.Start();
                }
            }
        }
		private void InitializeChannelRack()
		{
			ChannelRack.Children.Clear();

			var header = new TextBlock
			{
				Text = "Channel Rack",
				Foreground = Brushes.Orange,
				Margin = new Thickness(0, 0, 0, 10),
				FontSize = 16
			};
			ChannelRack.Children.Add(header);

			var addButton = new System.Windows.Controls.Button
			{
				Content = "Add Plugin",
				Width = 200,
				Height = 30,
				Margin = new Thickness(0, 20, 0, 0),
				Background = Brushes.Orange
			};
			addButton.Click += AddPluginButton_Click;
			ChannelRack.Children.Add(addButton);
		}

        private void AddPluginButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "WAV Files (*.wav)|*.wav",
                DefaultExt = ".wav"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    _audioEngine.LoadSample(dialog.FileName);
                    var box = new ModalBox(this, "Successfully added new WAV!");
                    box.ShowDialog();
                }
                catch (Exception ex)
                {
                    var box = new ModalBox(this, $"Error! {ex.Message}");
                    box.ShowDialog();
                }
            }
        }

        public void StartPlayback()
        {
            foreach (var pianoRoll in _pianoRolls.Values)
            {
                pianoRoll.StartPlayback();
            }
        }

        private void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_pianoRoll.isRecording)
            {
                _pianoRoll.StopRecording();
                RecordButton.Background = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B)); // Темно-серый
                RecordEllipse.Fill = Brushes.Red;
            }
            else
            {
                _pianoRoll.StartRecording();
                RecordButton.Background = Brushes.Red; // Индикация активной записи
                RecordEllipse.Fill = new SolidColorBrush(Color.FromRgb(0x2B, 0x2B, 0x2B));
            }
        }
    }
}
