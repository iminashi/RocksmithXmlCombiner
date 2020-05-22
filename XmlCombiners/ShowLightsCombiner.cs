using Rocksmith2014Xml;

namespace XmlCombiners
{
    public sealed class ShowLightsCombiner
    {
        private ShowLights? CombinedShowlights { get; set; }
        private float SongLength { get; set; }

        public void Save(string fileName)
        {
            CombinedShowlights?.Save(fileName);
        }

        public void AddNext(ShowLights next, float songLength, float trimAmount)
        {
            // Adding first arrangement
            if(CombinedShowlights is null)
            {
                CombinedShowlights = next;

                SongLength = songLength;
                return;
            }

            float startTime = SongLength - trimAmount;

            UpdateShowLights(next, startTime);
            CombinedShowlights.AddRange(next);

            SongLength += songLength - trimAmount;
        }

        private void UpdateShowLights(ShowLights showLights, float startTime)
        {
            foreach (var sl in showLights)
            {
                sl.Time += startTime;
            }
        }
    }
}
