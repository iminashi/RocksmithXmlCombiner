using ReactiveUI.Fody.Helpers;

using RSXmlCombinerGUI.Models;

using System;
using System.Reactive.Linq;

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class MainWindowViewModel : ViewModelBase
    {
        [Reactive]
        public ViewModelBase Content { get; set; }

        public TrackListViewModel TrackList { get; }

        public MainWindowViewModel()
        {
            Content = TrackList = new TrackListViewModel();
        }

        public void EditGlobalTones()
        {
            var vm = new CommonTonesViewModel(TrackList);

            vm.Ok
                .Take(1)
                .Subscribe(tones =>
                {
                    TrackList.UpdateTones(tones);
                    Content = TrackList;
                });

            Content = vm;
        }

        public void EditReplacementTones(InstrumentalArrangement arrangement)
        {
            var commonTones = CommonTonesRepository.GetCommonTones(arrangement.ArrangementType).AsSpan(1).ToArray();

            var vm = new ToneRenameViewModel(arrangement, commonTones);

            vm.Ok
                .Take(1)
                .Subscribe(replacements =>
                {
                    arrangement.ToneReplacements = replacements;
                    Content = TrackList;
                });

            Content = vm;
        }
    }
}
