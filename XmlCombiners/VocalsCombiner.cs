using Rocksmith2014.XML;

using System.Collections.Generic;

namespace XmlCombiners
{
    public sealed class VocalsCombiner
    {
        private List<Vocal>? CombinedVocals { get; set; }
        private int SongLength { get; set; }

        public void Save(string fileName)
        {
            if (CombinedVocals is not null)
                Vocals.Save(fileName, CombinedVocals);
        }

        public void AddNext(List<Vocal>? next, int songLength, int trimAmount)
        {
            // Adding first arrangement
            if (CombinedVocals is null)
            {
                AddFirst(next, songLength);
                return;
            }

            if (next is null)
            {
                SongLength += songLength - trimAmount;
                return;
            }

            int startTime = SongLength - trimAmount;

            UpdateVocals(next, startTime);
            CombinedVocals.AddRange(next);

            SongLength += songLength - trimAmount;
        }

        private void AddFirst(List<Vocal>? next, int songLength)
        {
            if (next is List<Vocal>)
                CombinedVocals = next;
            else
                CombinedVocals = new List<Vocal>();

            SongLength = songLength;
        }

        private static void UpdateVocals(List<Vocal> vocals, int startTime)
        {
            foreach (var vocal in vocals)
            {
                vocal.Time += startTime;
            }
        }
    }
}
