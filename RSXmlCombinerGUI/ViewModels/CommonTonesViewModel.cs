using ReactiveUI;

using RSXmlCombinerGUI.Extensions;
using RSXmlCombinerGUI.Models;

using System.Collections.Generic;
using System.Linq;
using System.Reactive;

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class CommonTonesViewModel : ViewModelBase
    {
        public List<ToneNamesViewModel> ToneNames { get; } = new List<ToneNamesViewModel>();

        public ReactiveCommand<Unit, List<ToneNamesViewModel>> Ok { get; }

        public CommonTonesViewModel()
        {
            ToneNames.Add(new ToneNamesViewModel());
            ToneNames.Add(new ToneNamesViewModel());
            ToneNames.Add(new ToneNamesViewModel());
            ToneNames.Add(new ToneNamesViewModel(ArrangementType.Combo, new string[] { "test", "test" }));

            Ok = ReactiveCommand.Create(() => ToneNames);
        }

        public CommonTonesViewModel(TrackListViewModel trackList)
        {
            var arrangementTypes = trackList
                .Tracks
                .SelectMany(t => t.Arrangements)
                .Where(a => a.ArrangementType.IsInstrumental())
                .Select(a => a.ArrangementType)
                .OrderBy(t => t)
                .Distinct();

            foreach (var arrType in arrangementTypes)
            {
                var commonTones = CommonTonesRepository.GetCommonTones(arrType);

                // If the base tone names are not set, get the names from the first track
                var arr = trackList.Tracks[0]
                    .Arrangements
                    .FirstOrDefault(a => a.ArrangementType == arrType && a.Model != null);

                if(arr != null
                    && string.IsNullOrEmpty(commonTones[0])
                    && !string.IsNullOrEmpty(((InstrumentalArrangement)arr.Model!).BaseTone))
                {
                    commonTones[0] = ((InstrumentalArrangement)arr.Model!).BaseTone;
                }

                ToneNames.Add(new ToneNamesViewModel(arrType, commonTones));
            }

            Ok = ReactiveCommand.Create(() => ToneNames);
        }
    }
}
