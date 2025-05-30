using SimpleNoteNG.Audio;
using SimpleNoteNG.Controls;
using SimpleNoteNG.Data;
using SimpleNoteNG.Pages;
using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

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

            // Инициализация интерфейса
            InitializeMixerChannels(8);
            LoadPianoRoll();
        }

        private void LoadPianoRoll()
        {
            _pianoRoll = new PianoRoll(_audioEngine, _metronome); // Сохраняем объект в поле
            _pianoRoll.TimeUpdated += time => TimerDisplay.Text = time;
            MainFrame.Navigate(_pianoRoll);
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

        }
    }
}
