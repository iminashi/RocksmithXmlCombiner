using Rocksmith2014Xml;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace XmlCombiners
{
    public sealed class InstrumentalCombiner
    {
        private RS2014Song CombinedArrangement { get; set; }

        public string ArrangementType => CombinedArrangement.Arrangement;

        private int ArrangementNumber { get; set; }

        private float TempoSum { get; set; }

        public void Save(string fileName, bool coercePhrases = false)
        {
            if (CombinedArrangement is null)
                throw new InvalidOperationException("Cannot save an empty arrangement.");

            CombinedArrangement.AverageTempo = TempoSum / ArrangementNumber;

            CleanupToneChanges(CombinedArrangement);

            CombinePhrases(CombinedArrangement);

            CombineChords(CombinedArrangement);

            // TODO: Make this work with DD levels
            if (coercePhrases && CombinedArrangement.Levels.Count == 1)
                CoercePhrasesAndSections(CombinedArrangement);

            CombinedArrangement.Save(fileName);
            Console.WriteLine($"Saved combined file as {fileName}");
        }

        public void AddNext(RS2014Song next, float trimAmount, bool isLast = false)
        {
            ArrangementNumber++;

            RemoveExtraBeats(next);
            if (!isLast)
            {
                RemoveEndPhrase(next);
            }

            // Adding the first arrangement
            if (CombinedArrangement is null)
            {
                CombinedArrangement = next;
                // Remove the transcription track in case one is present
                CombinedArrangement.TranscriptionTrack = new Level();
                return;
            }

            RemoveCountPhrase(next);

            float startTime = CombinedArrangement.SongLength - trimAmount;
            int lastMeasure = FindLastMeasure(CombinedArrangement);
            int lastChordId = CombinedArrangement.ChordTemplates.Count;
            int lastPhraseId = CombinedArrangement.Phrases.Count;

            if (CombinedArrangement.Levels.Count > 1 || next.Levels.Count > 1)
            {
                // Make sure phrase names are unique for DD files
                UpdatePhraseNames(next);
                UpdateLinkedDiffs(next, lastPhraseId);
            }

            UpdatePhraseIterations(next, startTime, lastPhraseId);
            UpdateBeats(next, startTime, lastMeasure);
            UpdateSections(next, startTime);
            UpdateEvents(next, startTime);
            UpdateNotes(next, startTime);
            UpdateChords(next, startTime, lastChordId);
            UpdateAnchors(next, startTime);
            UpdateHandShapes(next, startTime, lastChordId);

            CombinedArrangement.PhraseIterations.AddRange(next.PhraseIterations);
            CombinedArrangement.Phrases.AddRange(next.Phrases);
            CombinedArrangement.ChordTemplates.AddRange(next.ChordTemplates);
            CombinedArrangement.Ebeats.AddRange(next.Ebeats);
            CombinedArrangement.Events.AddRange(next.Events);
            CombinedArrangement.Sections.AddRange(next.Sections);
            UpdateSectionNumbers(CombinedArrangement);

            for (int i = 0; i < next.Levels.Count; i++)
            {
                if (CombinedArrangement.Levels.Count - 1 < i)
                    CombinedArrangement.Levels.Add(new Level((sbyte)i));

                CombinedArrangement.Levels[i].Notes.AddRange(next.Levels[i].Notes);
                CombinedArrangement.Levels[i].Chords.AddRange(next.Levels[i].Chords);
                CombinedArrangement.Levels[i].Anchors.AddRange(next.Levels[i].Anchors);
                CombinedArrangement.Levels[i].HandShapes.AddRange(next.Levels[i].HandShapes);
            }

            CombineTones(CombinedArrangement, next, startTime);

            CombinedArrangement.SongLength += next.SongLength - trimAmount;
            TempoSum += next.AverageTempo;
            CombineArrangementProperties(CombinedArrangement, next);
        }

        private void UpdatePhraseNames(RS2014Song song)
        {
            foreach (var phrase in song.Phrases)
            {
                if(!phrase.Name.Equals("END", StringComparison.OrdinalIgnoreCase) && !phrase.Name.Equals("noguitar", StringComparison.OrdinalIgnoreCase))
                    phrase.Name = $"arr{ArrangementNumber}{phrase.Name}";
            }
        }

        private void CleanupToneChanges(RS2014Song song)
        {
            if (song.ToneChanges is null)
                return;

            // Remove tones not included in the four tones
            for (int i = song.ToneChanges.Count - 1; i >= 0; i--)
            {
                var t = song.ToneChanges[i];
                if (song.ToneA != t.Name &&
                    song.ToneB != t.Name &&
                    song.ToneC != t.Name &&
                    song.ToneD != t.Name)
                {
                    song.ToneChanges.RemoveAt(i);
                }
            }

            // Remove duplicate tone changes
            for (int i = song.ToneChanges.Count - 2; i >= 0; i--)
            {
                var t = song.ToneChanges[i];
                if (t.Name == song.ToneChanges[i + 1].Name)
                {
                    song.ToneChanges.RemoveAt(i + 1);
                }
            }
        }

        private void CombinePhrases(RS2014Song song)
        {
            for (int i = song.Phrases.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if (song.Phrases[j].Name == song.Phrases[i].Name)
                    {
                        // If there are DD levels, all the phrases should have unique names
                        // There may be multiple noguitar phrases that can be combined
                        if (song.Levels.Count > 1)
                            Debug.Assert(song.Phrases[j].Name == "noguitar");

                        // Remove the phrase at the higher position
                        song.Phrases.RemoveAt(i);
                        foreach (var pi in song.PhraseIterations)
                        {
                            if (pi.PhraseId == i)
                            {
                                pi.PhraseId = j;
                            }
                            else if (pi.PhraseId > i)
                            {
                                pi.PhraseId--;
                            }
                        }

                        foreach (var nld in song.NewLinkedDiffs)
                        {
                            for (int p = 0; p < nld.PhraseIds.Count; p++)
                            {
                                if(nld.PhraseIds[p] == i)
                                {
                                    nld.PhraseIds[p] = j;
                                }
                                else if(nld.PhraseIds[p] > i)
                                {
                                    nld.PhraseIds[p]--;
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void CombineChords(RS2014Song combined)
        {
            for (int i = combined.ChordTemplates.Count - 1; i >= 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if (IsSameChordTemplate(combined.ChordTemplates[j], combined.ChordTemplates[i]))
                    {
                        combined.ChordTemplates.RemoveAt(i);
                        foreach (var level in combined.Levels)
                        {
                            foreach (var chord in level.Chords)
                            {
                                if (chord.ChordId == i)
                                {
                                    chord.ChordId = j;
                                }
                                else if (chord.ChordId > i)
                                {
                                    chord.ChordId--;
                                }
                            }
                            foreach (var hs in level.HandShapes)
                            {
                                if (hs.ChordId == i)
                                {
                                    hs.ChordId = j;
                                }
                                else if (hs.ChordId > i)
                                {
                                    hs.ChordId--;
                                }
                            }
                        }
                        break;
                    }
                }
            }
        }

        private bool IsSameChordTemplate(ChordTemplate first, ChordTemplate second)
        {
            if (first.ChordName != second.ChordName || first.DisplayName != second.DisplayName)
                return false;

            for (int i = 0; i < first.Frets.Length; i++)
            {
                if (first.Fingers[i] != second.Fingers[i] || first.Frets[i] != second.Frets[i])
                    return false;
            }

            return true;
        }

        public void SetTitle(string combinedTitle)
        {
            CombinedArrangement.Title = combinedTitle;
            CombinedArrangement.TitleSort = combinedTitle;
        }

        private void CoercePhrasesAndSections(RS2014Song song)
        {
            var phraseIterations = song.PhraseIterations;

            // Combine possible multiple phrases inside a single section
            for (int i = 1; i < song.Sections.Count - 2; i++)
            {
                int piIndex = phraseIterations.FindIndex(pi => Utils.TimeEqualToMilliseconds(pi.Time, song.Sections[i].Time));
                if (piIndex != -1)
                {
                    while (piIndex < phraseIterations.Count - 1 &&
                        !Utils.TimeEqualToMilliseconds(phraseIterations[piIndex + 1].Time, song.Sections[i + 1].Time))
                    {
                        phraseIterations.RemoveAt(piIndex + 1);
                    }
                }
            }

            while (phraseIterations.Count > 100)
            {
                // Search for the smallest section
                int smallestIndex = 1;
                float smallestLength = float.MaxValue;
                for (int i = 1; i < song.Sections.Count - 2; i++)
                {
                    // Skip noguitar sections and sections surrounded by noguitar sections
                    if (song.Sections[i].Name == "noguitar" ||
                        (song.Sections[i - 1].Name == "noguitar" &&
                         song.Sections[i + 1].Name == "noguitar"))
                    {
                        continue;
                    }

                    float length = song.Sections[i + 1].Time - song.Sections[i].Time;
                    if (length < smallestLength)
                    {
                        smallestIndex = i;
                        smallestLength = length;
                    }
                }

                int piIndex = phraseIterations.FindIndex(pi => Utils.TimeEqualToMilliseconds(pi.Time, song.Sections[smallestIndex].Time));

                // Combine with smallest neighbor
                if (smallestIndex == 1)
                {
                    song.Sections.RemoveAt(smallestIndex + 1);
                    if (piIndex != -1)
                        song.PhraseIterations.RemoveAt(piIndex + 1);
                }
                else if (smallestIndex == song.Sections.Count - 2)
                {
                    song.Sections.RemoveAt(smallestIndex);
                    if (piIndex != -1)
                        phraseIterations.RemoveAt(piIndex);
                }
                else
                {
                    float prevLength = song.Sections[smallestIndex].Time - song.Sections[smallestIndex - 1].Time;
                    float nextLength = song.Sections[smallestIndex + 2].Time - song.Sections[smallestIndex + 1].Time;

                    if ((prevLength < nextLength && song.Sections[smallestIndex - 1].Name != "noguitar") || song.Sections[smallestIndex + 1].Name == "noguitar")
                    {
                        song.Sections.RemoveAt(smallestIndex);
                        if (piIndex != -1)
                            phraseIterations.RemoveAt(piIndex);
                    }
                    else
                    {
                        song.Sections.RemoveAt(smallestIndex + 1);
                        if (piIndex != -1)
                            phraseIterations.RemoveAt(piIndex + 1);
                    }
                }
            }

            UpdateSectionNumbers(song);
        }

        private void RemoveExtraBeats(RS2014Song song)
        {
            float songLength = song.SongLength;

            for (int i = song.Ebeats.Count - 1; i >= 0; i--)
            {
                if (song.Ebeats[i].Time > songLength)
                {
                    song.Ebeats.RemoveAt(i);
                }
                else
                {
                    break;
                }
            }
        }

        private void RemoveEndPhrase(RS2014Song song)
        {
            int endPhraseId = song.Phrases.FindIndex(p => p.Name.Equals("END", StringComparison.OrdinalIgnoreCase));
            if (endPhraseId == -1)
                return;

            // Replace the end phrase with a no guitar phrase
            int ngPhraseId = song.Phrases.FindIndex(p => p.Name.Equals("noguitar", StringComparison.OrdinalIgnoreCase));
            if (ngPhraseId == -1)
            {
                // No "noguitar" phrase present, reuse the end phrase 
                song.Phrases[endPhraseId].Name = "noguitar";
            }
            else
            {
                // If the end phrase is not the last phrase for some reason, adjust the phrase IDs
                if (endPhraseId != song.Phrases.Count - 1)
                {
                    foreach (var phraseIter in song.PhraseIterations)
                    {
                        if (phraseIter.PhraseId > endPhraseId)
                            phraseIter.PhraseId--;
                    }
                    foreach (var nld in song.NewLinkedDiffs)
                    {
                        for (int i = 0; i < nld.PhraseIds.Count; i++)
                        {
                            if (nld.PhraseIds[i] > endPhraseId)
                                nld.PhraseIds[i]--;
                        }
                    }
                }

                song.Phrases.RemoveAt(endPhraseId);
                song.PhraseIterations[^1].PhraseId = ngPhraseId;
            }
        }

        private void RemoveCountPhrase(RS2014Song song)
        {
            int countPhraseId = song.PhraseIterations[0].PhraseId;
            song.Phrases.RemoveAt(countPhraseId);
            song.PhraseIterations.RemoveAt(0);
            foreach (var pi in song.PhraseIterations)
            {
                pi.PhraseId--;
            }
            foreach (var nld in song.NewLinkedDiffs)
            {
                for (int i = 0; i < nld.PhraseIds.Count; i++)
                {
                    nld.PhraseIds[i]--;
                }
            }
        }

        private int FindLastMeasure(RS2014Song song)
        {
            for (int i = song.Ebeats.Count - 1; i >= 0; i--)
            {
                if (song.Ebeats[i].Measure > 0)
                    return song.Ebeats[i].Measure;
            }

            return 0;
        }

        private void UpdatePhraseIterations(RS2014Song song, float startTime, int lastPhraseId)
        {
            foreach (var pi in song.PhraseIterations)
            {
                pi.Time += startTime;
                pi.PhraseId += lastPhraseId;
            }
        }

        private void UpdateLinkedDiffs(RS2014Song song, int lastPhraseId)
        {
            foreach (var nld in song.NewLinkedDiffs)
            {
                for (int i = 0; i < nld.PhraseIds.Count; i++)
                {
                    nld.PhraseIds[i] += lastPhraseId;
                }
            }
        }

        private void UpdateBeats(RS2014Song song, float startTime, int lastMeasure)
        {
            int measureCounter = 1;
            foreach (var beat in song.Ebeats)
            {
                if (beat.Measure >= 0)
                {
                    beat.Measure = lastMeasure + measureCounter++;
                }

                beat.Time += startTime;
            }
        }

        private void UpdateSections(RS2014Song song, float startTime)
        {
            foreach (var section in song.Sections)
            {
                section.Time += startTime;
            }
        }

        private void UpdateEvents(RS2014Song song, float startTime)
        {
            foreach (var @event in song.Events)
            {
                @event.Time += startTime;
            }
        }

        private void UpdateNotes(RS2014Song song, float startTime)
        {
            foreach (var level in song.Levels)
            {
                foreach (var note in level.Notes)
                {
                    note.Time += startTime;

                    if (note.BendValues?.Count > 0)
                    {
                        for (int i = 0; i < note.BendValues.Count; i++)
                        {
                            note.BendValues[i] = new BendValue(note.BendValues[i].Time + startTime, note.BendValues[i].Step);
                        }
                    }
                }
            }
        }

        private void UpdateChords(RS2014Song song, float startTime, int lastChordId)
        {
            foreach (var level in song.Levels)
            {
                foreach (var chord in level.Chords)
                {
                    chord.Time += startTime;
                    chord.ChordId += lastChordId;
                    if (chord.ChordNotes?.Count > 0)
                    {
                        foreach (var cn in chord.ChordNotes)
                        {
                            cn.Time += startTime;

                            if (cn.BendValues?.Count > 0)
                            {
                                for (int i = 0; i < cn.BendValues.Count; i++)
                                {
                                    cn.BendValues[i] = new BendValue(cn.BendValues[i].Time + startTime, cn.BendValues[i].Step);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void UpdateAnchors(RS2014Song song, float startTime)
        {
            foreach (var level in song.Levels)
            {
                foreach (var anchor in level.Anchors)
                {
                    anchor.Time += startTime;
                }
            }
        }

        private void UpdateHandShapes(RS2014Song song, float startTime, int lastChordId)
        {
            foreach (var level in song.Levels)
            {
                foreach (var hs in level.HandShapes)
                {
                    hs.StartTime += startTime;
                    hs.EndTime += startTime;
                    hs.ChordId += lastChordId;
                }
            }
        }

        private void UpdateSectionNumbers(RS2014Song combined)
        {
            Dictionary<string, int> numbers = new Dictionary<string, int>();
            foreach (var section in combined.Sections)
            {
                if (numbers.ContainsKey(section.Name))
                {
                    section.Number = ++numbers[section.Name];
                }
                else
                {
                    numbers.Add(section.Name, 1);
                    section.Number = 1;
                }
            }
        }

        private void CombineArrangementProperties(RS2014Song combined, RS2014Song next)
        {
            string[] skip = { "Represent", "BonusArrangement", "PathLead", "PathRhythm", "PathBass" };
            var properties = combined.ArrangementProperties.GetType().GetProperties();

            foreach (var property in properties)
            {
                if (skip.Contains(property.Name))
                    continue;

                if ((byte)property.GetValue(next.ArrangementProperties)! != (byte)property.GetValue(combined.ArrangementProperties)!)
                    property.SetValue(combined.ArrangementProperties, (byte)1);
            }
        }

        private void CombineTones(RS2014Song combined, RS2014Song next, float startTime)
        {
            if (combined.ToneChanges is null)
                combined.ToneChanges = new ToneCollection();
            if (next.ToneChanges is null)
                next.ToneChanges = new ToneCollection();

            string? currentTone = combined.ToneBase;
            if (combined.ToneChanges.Count > 0)
                currentTone = combined.ToneChanges[^1].Name;

            if (next.ToneChanges.Count > 0 || (next.ToneChanges.Count == 0 && currentTone != next.ToneBase))
            {
                // Add a tone change for the base tone of the next song
                if (next.ToneBase != null)
                    next.ToneChanges.Insert(0, new Tone(next.ToneBase, next.StartBeat - startTime, 0));

                for (int i = 0; i < next.ToneChanges.Count; i++)
                {
                    var t = next.ToneChanges[i];
                    next.ToneChanges[i] = new Tone(t.Name, t.Time + startTime, t.Id);

                    TryAddTone(combined, t);
                }

                combined.ToneChanges.AddRange(next.ToneChanges);
            }
        }

        private void TryAddTone(RS2014Song combined, Tone t)
        {
            if (combined.ToneA == t.Name ||
                combined.ToneB == t.Name ||
                combined.ToneC == t.Name ||
                combined.ToneD == t.Name)
            {
                return;
            }

            if (combined.ToneA == null)
            {
                combined.ToneA = t.Name;
                return;
            }

            if (combined.ToneB == null)
            {
                combined.ToneB = t.Name;
                return;
            }

            if (combined.ToneC == null)
            {
                combined.ToneC = t.Name;
                return;
            }

            if (combined.ToneD == null)
            {
                combined.ToneD = t.Name;
                return;
            }

            Console.WriteLine($"Too many tone changes, cannot fit in tone {t.Name}");
        }
    }
}
