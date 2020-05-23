
using RSXmlCombinerGUI.Models;

using System;
using System.Collections.ObjectModel;

namespace RSXmlCombinerGUI.ViewModels
{
    public sealed class ArrangementToneControlsViewModel : ViewModelBase
    {
        public InstrumentalArrangement Model { get; }
        public Collection<TrackViewModel> Tracks { get; }
        public TrackViewModel Parent { get; }
        public string[] ToneNames { get; private set; }

        public ArrangementToneControlsViewModel(
            InstrumentalArrangement model,
            Collection<TrackViewModel> tracks,
            TrackViewModel parent)
        {
            Model = model;
            Tracks = tracks;
            Parent = parent;
            UpdateTones();
        }

        internal void UpdateTones()
        {
            ToneNames = CommonTonesRepository.GetCommonTones(Model.ArrangementType).AsSpan(1).ToArray();
        }
    }
}
