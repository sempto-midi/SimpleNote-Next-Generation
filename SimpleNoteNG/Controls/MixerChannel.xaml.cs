using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNoteNG.Controls
{
    public partial class MixerChannel : UserControl
    {
        public Action<bool> OnMuteChanged { get; set; }
        public Action<bool> OnSoloChanged { get; set; }
        public Action<double> OnVolumeChanged { get; set; }

        private GainControlSampleProvider _gainProvider;

        public MixerChannel()
            : this(new SilenceSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)), "Unnamed", 0, false)
        {
        }

        public MixerChannel(ISampleProvider source, string name, int channelNumber, bool isMaster = false)
        {
            InitializeComponent();
            DataContext = this;

            ChannelLabel = name;
            ChannelNumber = channelNumber;
            IsMaster = isMaster;

            _gainProvider = new GainControlSampleProvider(source);
            VolumeSlider.Value = _gainProvider.Gain * 100;

            MuteCheckBox.Checked += (s, e) => OnMuteChanged?.Invoke(true);
            MuteCheckBox.Unchecked += (s, e) => OnMuteChanged?.Invoke(false);

            SoloCheckBox.Checked += (s, e) => OnSoloChanged?.Invoke(true);
            SoloCheckBox.Unchecked += (s, e) => OnSoloChanged?.Invoke(false);

            VolumeSlider.ValueChanged += (s, e) =>
            {
                if (!double.IsNaN(VolumeSlider.Value))
                    OnVolumeChanged?.Invoke(VolumeSlider.Value);
            };
        }

        // Dependency Properties
        public string ChannelLabel
        {
            get => (string)GetValue(ChannelLabelProperty);
            set => SetValue(ChannelLabelProperty, value);
        }

        public static readonly DependencyProperty ChannelLabelProperty =
            DependencyProperty.Register("ChannelLabel", typeof(string), typeof(MixerChannel));

        public string PluginName
        {
            get => (string)GetValue(PluginNameProperty);
            set => SetValue(PluginNameProperty, value);
        }

        public static readonly DependencyProperty PluginNameProperty =
            DependencyProperty.Register("PluginName", typeof(string), typeof(MixerChannel));

        public int ChannelNumber
        {
            get => (int)GetValue(ChannelNumberProperty);
            set => SetValue(ChannelNumberProperty, value);
        }

        public static readonly DependencyProperty ChannelNumberProperty =
            DependencyProperty.Register("ChannelNumber", typeof(int), typeof(MixerChannel));

        public bool IsMaster
        {
            get => (bool)GetValue(IsMasterProperty);
            set => SetValue(IsMasterProperty, value);
        }

        public static readonly DependencyProperty IsMasterProperty =
            DependencyProperty.Register("IsMaster", typeof(bool), typeof(MixerChannel));

        public bool IsSoloVisible
        {
            get => (bool)GetValue(IsSoloVisibleProperty);
            set => SetValue(IsSoloVisibleProperty, value);
        }

        public static readonly DependencyProperty IsSoloVisibleProperty =
            DependencyProperty.Register("IsSoloVisible", typeof(bool), typeof(MixerChannel), new PropertyMetadata(true));

        // Public API
        public void SetInput(ISampleProvider source)
        {
            _gainProvider = new GainControlSampleProvider(source);
        }

        public ISampleProvider GetOutput()
        {
            return _gainProvider;
        }

        public void SetVolume(double volume)
        {
            _gainProvider.Gain = (float)(volume / 100.0);
            VolumeSlider.Value = volume;
        }

        public void SetMute(bool mute)
        {
            _gainProvider.Gain = mute ? 0 : 1;
        }

        public void Dispose()
        {
            _gainProvider.Gain = 0;
        }
    }
}