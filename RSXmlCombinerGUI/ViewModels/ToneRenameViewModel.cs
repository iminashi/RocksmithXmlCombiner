using ReactiveUI;

using RSXmlCombinerGUI.Models;

using System.Collections.Generic;
using System.Linq;
using System.Reactive;

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class ToneRenameViewModel : ViewModelBase
    {
        public string[] OldTones { get; } = new string[4];
        public string[] ReplacementTones { get; } = new string[4];

        public List<string> CommonTones { get; }

        public ReactiveCommand<Unit, Dictionary<string, string>> Ok { get; }

        // Constructor for the designer
        public ToneRenameViewModel()
        {
            OldTones[0] = "old_dist";
            ReplacementTones[0] = "new_dist";
            CommonTones = new List<string>();

            Ok = ReactiveCommand.Create(() => new Dictionary<string, string>());
        }

        public ToneRenameViewModel(InstrumentalArrangement arrangement, string[] commonTones)
        {
            CommonTones = commonTones.ToList();

            // Make sure the tones are up to date in case the user has edited the file
            arrangement.UpdateTones();

            // Read the old tone names
            if (arrangement.ToneNames != null)
            {
                for (int i = 0; i < arrangement.ToneNames.Count; i++)
                {
                    OldTones[i] = arrangement.ToneNames[i];
                }
            }

            // Read the replacement tone names
            for (int i = 0; i < OldTones.Length; i++)
            {
                if (string.IsNullOrEmpty(OldTones[i]))
                    break;

                if (arrangement.ToneReplacements.ContainsKey(OldTones[i]))
                {
                    ReplacementTones[i] = arrangement.ToneReplacements[OldTones[i]];
                }
            }

            Ok = ReactiveCommand.Create(() =>
            {
                var replacements = new Dictionary<string, string>();

                for (int i = 0; i < OldTones.Length; i++)
                {
                    if (string.IsNullOrEmpty(OldTones[i]))
                        break;

                    replacements.Add(OldTones[i], ReplacementTones[i]);
                }

                return replacements;
            });
        }
    }
}
