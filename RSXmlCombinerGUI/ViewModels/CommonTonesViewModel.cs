using ReactiveUI;

using RSXmlCombinerGUI.Models;

using System.Collections.Generic;
using System.Reactive;

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class CommonTonesViewModel : ViewModelBase
    {
        public List<ToneNamesViewModel> Arrangements { get; } = new List<ToneNamesViewModel>();

        public ReactiveCommand<Unit, List<ToneNamesViewModel>> Ok { get; }

        public CommonTonesViewModel()
        {
            Arrangements.Add(new ToneNamesViewModel());
            Arrangements.Add(new ToneNamesViewModel());
            Arrangements.Add(new ToneNamesViewModel());

            Ok = ReactiveCommand.Create(() => Arrangements);
        }

        public CommonTonesViewModel(TrackListViewModel trackList)
        {
            var leadCommon = trackList.CommonToneNames[ArrangementType.Lead];
            if (string.IsNullOrEmpty(leadCommon[0]) && trackList.Tracks.Count > 0)
                leadCommon[0] = trackList.Tracks[0].LeadArrangement?.BaseTone ?? string.Empty;

            if (string.IsNullOrEmpty(leadCommon[1]))
                leadCommon[1] = leadCommon[0];

            var rhythmCommon = trackList.CommonToneNames[ArrangementType.Rhythm];
            if (string.IsNullOrEmpty(rhythmCommon[0]) && trackList.Tracks.Count > 0)
                rhythmCommon[0] = trackList.Tracks[0].RhythmArrangement?.BaseTone ?? string.Empty;

            if (string.IsNullOrEmpty(rhythmCommon[1]))
                rhythmCommon[1] = rhythmCommon[0];

            var bassCommon = trackList.CommonToneNames[ArrangementType.Bass];
            if (string.IsNullOrEmpty(bassCommon[0]) && trackList.Tracks.Count > 0)
                bassCommon[0] = trackList.Tracks[0].BassArrangement?.BaseTone ?? string.Empty;

            if (string.IsNullOrEmpty(bassCommon[1]))
                bassCommon[1] = bassCommon[0];

            Arrangements.Add(new ToneNamesViewModel(ArrangementType.Lead, trackList.CommonToneNames[ArrangementType.Lead]));
            Arrangements.Add(new ToneNamesViewModel(ArrangementType.Rhythm, trackList.CommonToneNames[ArrangementType.Rhythm]));
            Arrangements.Add(new ToneNamesViewModel(ArrangementType.Bass, trackList.CommonToneNames[ArrangementType.Bass]));

            Ok = ReactiveCommand.Create(() => Arrangements);
        }
    }
}
