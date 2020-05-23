using Rocksmith2014Xml;

using System;
using System.Collections.Generic;
using System.IO;

namespace RSXmlCombinerGUI.Models
{
    public sealed class InstrumentalArrangement : Arrangement
    {
        private string baseTone = string.Empty;

        public string BaseTone
        {
            get { return baseTone; }
            // Avalonia binding may give null values for some reason
            set { if(value is string) baseTone = value; }
        }
        public List<string>? ToneNames { get; set; }
        public Dictionary<string, string> ToneReplacements { get; set; } = new Dictionary<string, string>();

        public InstrumentalArrangement() { }

        public InstrumentalArrangement(string fileName, ArrangementType arrangementType)
        {
            FileName = fileName;
            ArrangementType = arrangementType;

            UpdateTones();
        }

        public InstrumentalArrangement(ArrangementType arrangementType)
        {
            ArrangementType = arrangementType;
        }

        public void UpdateTones()
        {
            if (!File.Exists(FileName))
                return;

            var song = RS2014Song.Load(FileName);

            if(string.IsNullOrEmpty(BaseTone) && !string.IsNullOrEmpty(song.ToneBase))
                BaseTone = song.ToneBase;

            if (song.Tones?.Count > 0)
            {
                ToneNames = new List<string>();

                if (!string.IsNullOrEmpty(song.ToneA))
                    ToneNames.Add(song.ToneA);
                if (!string.IsNullOrEmpty(song.ToneB))
                    ToneNames.Add(song.ToneB);
                if (!string.IsNullOrEmpty(song.ToneC))
                    ToneNames.Add(song.ToneC);
                if (!string.IsNullOrEmpty(song.ToneD))
                    ToneNames.Add(song.ToneD);
            }
        }
    }
}
