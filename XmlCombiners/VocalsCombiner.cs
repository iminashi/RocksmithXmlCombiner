using Rocksmith2014Xml;

namespace XmlCombiners
{
    public sealed class VocalsCombiner
    {
        private Vocals? CombinedVocals { get; set; }
        private float SongLength { get; set; }

        public void Save(string fileName)
        {
            CombinedVocals?.Save(fileName);
        }

        public void AddNext(Vocals? next, float songLength, float trimAmount)
        {
            // Adding first arrangement
            if(CombinedVocals is null)
            {
                AddFirst(next, songLength);
                return;
            }

            if (next is null)
            {
                SongLength += songLength - trimAmount;
                return;
            }

            float startTime = SongLength - trimAmount;

            UpdateVocals(next, startTime);
            CombinedVocals.AddRange(next);

            SongLength += songLength - trimAmount;
        }

        private void AddFirst(Vocals? next, float songLength)
        {
            if (next is Vocals)
                CombinedVocals = next;
            else
                CombinedVocals = new Vocals();

            SongLength = songLength;
        }

        private void UpdateVocals(Vocals vocals, float startTime)
        {
            foreach (var vocal in vocals)
            {
                vocal.Time += startTime;
            }
        }
    }
}
