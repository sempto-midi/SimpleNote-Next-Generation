using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimpleNoteNG.Models
{
    public class Note
    {
        public int MidiNote { get; set; }
        public double StartX { get; set; } // Позиция X на PianoRoll
        public double Width { get; set; }  // Длительность ноты в пикселях
        public double Top { get; set; }    // Позиция Y (высота) на PianoRoll
    }
}
