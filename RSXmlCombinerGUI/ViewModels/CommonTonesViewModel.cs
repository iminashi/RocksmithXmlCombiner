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
            string leadBase = trackList.CommonToneNames[ArrangementType.Lead][0];
            if (string.IsNullOrEmpty(leadBase) && trackList.Tracks.Count > 0)
                trackList.CommonToneNames[ArrangementType.Lead][0] = trackList.Tracks[0].LeadArrangement?.BaseTone ?? string.Empty;

            string rhythmBase = trackList.CommonToneNames[ArrangementType.Rhythm][0];
            if (string.IsNullOrEmpty(rhythmBase) && trackList.Tracks.Count > 0)
                trackList.CommonToneNames[ArrangementType.Rhythm][0] = trackList.Tracks[0].RhythmArrangement?.BaseTone ?? string.Empty;

            string bassBase = trackList.CommonToneNames[ArrangementType.Bass][0];
            if (string.IsNullOrEmpty(bassBase) && trackList.Tracks.Count > 0)
                trackList.CommonToneNames[ArrangementType.Bass][0] = trackList.Tracks[0].BassArrangement?.BaseTone ?? string.Empty;

            Arrangements.Add(new ToneNamesViewModel(ArrangementType.Lead, trackList.CommonToneNames[ArrangementType.Lead]));
            Arrangements.Add(new ToneNamesViewModel(ArrangementType.Rhythm, trackList.CommonToneNames[ArrangementType.Rhythm]));
            Arrangements.Add(new ToneNamesViewModel(ArrangementType.Bass, trackList.CommonToneNames[ArrangementType.Bass]));

            Ok = ReactiveCommand.Create(() => Arrangements);
        }
    }
}
