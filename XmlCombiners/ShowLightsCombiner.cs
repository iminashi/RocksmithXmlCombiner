using Rocksmith2014.XML;

using System.Collections.Generic;

namespace XmlCombiners
{
    public sealed class ShowLightsCombiner
    {
        private List<ShowLight>? CombinedShowlights { get; set; }
        private int SongLength { get; set; }

        public void Save(string fileName)
        {
            if (CombinedShowlights is not null)
                ShowLights.Save(fileName, CombinedShowlights);
        }

        public void AddNext(List<ShowLight> next, int songLength, int trimAmount)
        {
            // Adding first arrangement
            if (CombinedShowlights is null)
            {
                CombinedShowlights = next;

                SongLength = songLength;
                return;
            }

            int startTime = SongLength - trimAmount;

            UpdateShowLights(next, startTime);
            CombinedShowlights.AddRange(next);

            SongLength += songLength - trimAmount;
        }

        private static void UpdateShowLights(List<ShowLight> showLights, int startTime)
        {
            foreach (var sl in showLights)
            {
                sl.Time += startTime;
            }
        }
    }
}
