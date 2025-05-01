using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SimpleNoteNG.Controls
{
    /// <summary>
    /// Логика взаимодействия для MixerChannel.xaml
    /// </summary>
    public partial class MixerChannel : UserControl
    {
        public int ChannelNumber { get; set; }

        public MixerChannel(int channelNumber)
        {
            InitializeComponent();
            ChannelNumber = channelNumber;
            DataContext = this; // Для привязки ChannelNumber в XAML
        }
    }
}
