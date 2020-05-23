using ReactiveUI.Fody.Helpers;

using RSXmlCombinerGUI.Models;

using System.Collections.Generic;
using System.Linq;

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class ToneNamesViewModel : ViewModelBase
    {
        [Reactive]
        public ArrangementType ArrangementType { get; set; }

        public List<string> Tones { get; }

        // Constructor for the designer
        public ToneNamesViewModel()
        {
            ArrangementType = ArrangementType.Bass;

            Tones = new List<string>
            {
                "Base",
                "Test A",
                "Test B",
                "Test C",
                "Test D"
            };
        }

        public ToneNamesViewModel(ArrangementType arrangementType, string[] toneNames)
        {
            ArrangementType = arrangementType;

            Tones = toneNames.ToList();
        }
    }
}
