using ReactiveUI.Fody.Helpers;

using RSXmlCombinerGUI.Models;

using System.Collections.Generic;

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class ToneNamesViewModel : ViewModelBase
    {
        [Reactive]
        public ArrangementType ArrangementType { get; set; }

        public List<string> Tones { get; } = new List<string>();

        public ToneNamesViewModel()
        {
            ArrangementType = ArrangementType.Bass;

            Tones.Add("Base");
            Tones.Add("Test A");
            Tones.Add("Test B");
            Tones.Add("Test C");
            Tones.Add("Test D");
        }

        public ToneNamesViewModel(ArrangementType arrangementType, string[] toneNames)
        {
            ArrangementType = arrangementType;

            foreach (var name in toneNames)
            {
                Tones.Add(name);
            }
        }
    }
}
