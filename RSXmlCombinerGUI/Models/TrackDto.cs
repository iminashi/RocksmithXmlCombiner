using RSXmlCombinerGUI.ViewModels;

using System;

namespace RSXmlCombinerGUI.Models
{
    public class TrackDto
    {
        public string Title { get; set; } = string.Empty;
        public float TrimAmount { get; set; }
        public string? AudioFile { get; set; }
        public float SongLength { get; set; }
        public InstrumentalArrangement? LeadArrangement { get; set; }
        public InstrumentalArrangement? RhythmArrangement { get; set; }
        public InstrumentalArrangement? BassArrangement { get; set; }
        public string? VocalsArrangement { get; set; }
        public string? ShowLightsArrangement { get; set; }

        public TrackDto() { }

        public TrackDto(TrackViewModel vm)
        {
            var vmProperties = vm.GetType().GetProperties();
            var dtoProperties = GetType().GetProperties();

            foreach (var vmProp in vmProperties)
            {
                var dtoProp = Array.Find(dtoProperties, p => p.Name == vmProp.Name);
                dtoProp?.SetValue(this, vmProp.GetValue(vm));
            }
        }
    }
}
