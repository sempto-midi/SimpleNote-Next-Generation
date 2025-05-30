using SimpleNoteNG.Audio;
using SimpleNoteNG.Controls;
using SimpleNoteNG.Data;
using SimpleNoteNG.Pages;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SimpleNoteNG.Windows
{
    /// <summary>
    /// Логика взаимодействия для Workspace.xaml
    /// </summary>
    public partial class Workspace : Window
    {
        int projectId;
        int userId;
        private readonly AudioEngine _audioEngine;
        private Metronome _metronome;
        private PianoRoll _pianoRoll;


        public Workspace(int projectId, int userId)
        {
            InitializeComponent();
            this.projectId = projectId;
            this.userId = userId;
            _audioEngine = new AudioEngine();
            _metronome = new Metronome(int.Parse(Tempo.Text));
            _metronome.BeatChanged += Metronome_BeatChanged;

            InitializeMixerChannels(8);
            LoadPianoRoll();
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
            _pianoRoll = new PianoRoll(_audioEngine, _metronome); // Сохраняем объект в поле
            _pianoRoll.TimeUpdated += time => TimerDisplay.Text = time;
            MainFrame.Navigate(_pianoRoll);
        }
        public void LoadProject(string filePath)
        {
            // Теперь просто делегируем работу PianoRoll
            _pianoRoll.LoadMidiFile(filePath);
        }

        public void InitializeEmptyProject()
        {
            _pianoRoll.InitializeEmptyProject();
        }
        private void InitializeMixerChannels(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var channelControl = new MixerChannel(i + 1);
                MixerPanel.Children.Add(channelControl);
            }
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
            if (_pianoRoll == null)
            {
                LoadPianoRoll();
            }

            _audioEngine.Start();
            _pianoRoll.StartPlayback();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            if (_pianoRoll != null)
            {
                _pianoRoll.PausePlayback();
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            if (_pianoRoll != null)
            {
                _pianoRoll.StopPlayback();
            }
            _audioEngine.Stop();
            TimerDisplay.Text = "00:00:000";
        }

        private void ExportToMIDI_Click(object sender, RoutedEventArgs e)
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
            }
        }

        private void ExportToMP3_Click(object sender, RoutedEventArgs e)
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
    }
}
