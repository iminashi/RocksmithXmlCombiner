using RSXmlCombinerGUI.ViewModels;

using System;
using System.Linq;

namespace RSXmlCombinerGUI.Models
{
    public class TrackDto
    {
        public string Title { get; set; } = string.Empty;
        public float TrimAmount { get; set; }
        public string? AudioFile { get; set; }
        public float SongLength { get; set; }
        public Arrangement[] Arrangements { get; set; }

        public TrackDto() { }

        public TrackDto(TrackViewModel vm)
        {
            Arrangements = vm.Arrangements.Select(a => a.Model).ToArray();

            var vmProperties = vm.GetType().GetProperties();
            var dtoProperties = GetType().GetProperties();

            foreach (var vmProp in vmProperties.Where(p => p.Name != "Arrangements"))
            {
                var dtoProp = Array.Find(dtoProperties, p => p.Name == vmProp.Name);
                dtoProp?.SetValue(this, vmProp.GetValue(vm));
            }
        }
    }
}
