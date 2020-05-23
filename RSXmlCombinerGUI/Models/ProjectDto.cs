using RSXmlCombinerGUI.ViewModels;

using System.Collections.Generic;
using System.Linq;

namespace RSXmlCombinerGUI.Models
{
    public sealed class ProjectDto
    {
        public string CombinedTitle { get; set; } = string.Empty;
        public bool CoercePhrases { get; set; } = true;
        public bool AddTrackNamesToLyrics { get; set; } = true;
        public Dictionary<string, string[]> CommonToneNames { get; set; } = new Dictionary<string, string[]>();
        public List<TrackDto> Tracks { get; set; } = new List<TrackDto>();

        public ProjectDto() { }

        public ProjectDto(TrackListViewModel vm)
        {
            CombinedTitle = vm.CombinedTitle;
            CoercePhrases = vm.CoercePhrases;
            AddTrackNamesToLyrics = vm.AddTrackNamesToLyrics;
            foreach (var kv in CommonTonesRepository.GetCopy())
            {
                CommonToneNames.Add(kv.Key.ToString(), kv.Value);
            }
            Tracks = vm.Tracks.Select(x => new TrackDto(x)).ToList();
        }
    }
}
