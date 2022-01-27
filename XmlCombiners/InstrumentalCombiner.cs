using Rocksmith2014.XML;
using Rocksmith2014.XML.Extensions;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace XmlCombiners
{
    public sealed class InstrumentalCombiner
    {
        public InstrumentalArrangement? CombinedArrangement { get; private set; }

        private int ArrangementNumber { get; set; }

        private float TempoSum { get; set; }

        public void Save(string fileName, bool coercePhrases = false)
        {
            if (CombinedArrangement is null)
                throw new InvalidOperationException("Cannot save an empty arrangement.");

            CombinedArrangement.MetaData.AverageTempo = TempoSum / ArrangementNumber;

            CleanupToneChanges(CombinedArrangement);

            CombinePhrases(CombinedArrangement);

            MergeNoguitarSections(CombinedArrangement);

            CombineChords(CombinedArrangement);

            if (coercePhrases && CombinedArrangement.Levels.Count == 1)
                CoercePhrasesAndSections(CombinedArrangement);

            CombinedArrangement.Save(fileName);
            Console.WriteLine($"Saved combined file as {fileName}");
        }

        public void AddNext(InstrumentalArrangement next, int songLength, int trimAmount, bool condenseIntoOnePhrase, bool isLast = false)
        {
            bool isFirstArrangement = CombinedArrangement is null;
            ArrangementNumber++;

            if (condenseIntoOnePhrase)
                CondenseIntoOnePhase(next, songLength, isFirstArrangement, isLast);

            RemoveExtraBeats(next);

            if (!isLast && !condenseIntoOnePhrase)
                RemoveEndPhrase(next);

            // Adding the first arrangement
            if (isFirstArrangement)
            {
                CombinedArrangement = next;
                CombinedArrangement.MetaData.SongLength = songLength;
                // Remove the transcription track in case one is present
                CombinedArrangement.TranscriptionTrack = new Level();
                return;
            }
            else if (!condenseIntoOnePhrase)
            {
                RemoveCountPhrase(next);
            }

            int startTime = CombinedArrangement!.MetaData.SongLength - trimAmount;
            short lastMeasure = FindLastMeasure(CombinedArrangement);
            short lastChordId = (short)CombinedArrangement.ChordTemplates.Count;
            int lastPhraseId = CombinedArrangement.Phrases.Count;

            if (!condenseIntoOnePhrase && (CombinedArrangement.Levels.Count > 1 || next.Levels.Count > 1))
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

            CombinedArrangement.MetaData.SongLength += songLength - trimAmount;
            TempoSum += next.MetaData.AverageTempo;
            CombineArrangementProperties(CombinedArrangement, next);
        }

        private void CondenseIntoOnePhase(InstrumentalArrangement arr, int songLength, bool isFirst, bool isLast)
        {
            // Duplicate notes etc. into higher levels for phrases whose max difficulty is lower than the highest max difficulty 
            for (int i = 1; i < arr.PhraseIterations.Count; i++)
            {
                var pi = arr.PhraseIterations[i];
                if (arr.Phrases[pi.PhraseId].Name.Equals("END", StringComparison.OrdinalIgnoreCase))
                    break;

                int startTime = pi.Time;
                var endTime = (i + 1 == arr.PhraseIterations.Count) ? songLength : arr.PhraseIterations[i + 1].Time;
                int maxDiff = arr.Phrases[pi.PhraseId].MaxDifficulty;
                var maxLevel = arr.Levels[maxDiff];
                if (maxDiff < arr.Levels.Count - 1)
                {
                    var notes =
                        from n in maxLevel.Notes
                        where n.Time >= startTime && n.Time < endTime
                        select new Note(n);

                    var chords =
                        from c in maxLevel.Chords
                        where c.Time >= startTime && c.Time < endTime
                        select new Chord(c);

                    var handshapes =
                        from hs in maxLevel.HandShapes
                        where hs.Time >= startTime && hs.Time < endTime
                        select new HandShape(hs);

                    var anchors =
                        from a in maxLevel.Anchors
                        where a.Time >= startTime && a.Time < endTime
                        select new Anchor(a);

                    for (int lvl = maxDiff + 1; lvl < arr.Levels.Count; lvl++)
                    {
                        arr.Levels[lvl].Notes.AddRange(notes);
                        arr.Levels[lvl].Chords.AddRange(chords);
                        arr.Levels[lvl].HandShapes.AddRange(handshapes);
                        arr.Levels[lvl].Anchors.AddRange(anchors);
                    }
                }
            }

            // Sort everything by time
            for (int lvl = 0; lvl < arr.Levels.Count; lvl++)
            {
                arr.Levels[lvl].Notes.Sort((a, b) => a.Time.CompareTo(b.Time));
                arr.Levels[lvl].Chords.Sort((a, b) => a.Time.CompareTo(b.Time));
                arr.Levels[lvl].HandShapes.Sort((a, b) => a.Time.CompareTo(b.Time));
                arr.Levels[lvl].Anchors.Sort((a, b) => a.Time.CompareTo(b.Time));
            }

            // Store the times of the COUNT, END and the first proper phrase
            int endPhraseId = arr.Phrases.FindIndex(p => p.Name.Equals("END", StringComparison.OrdinalIgnoreCase));
            int? endPhraseTime = arr.PhraseIterations.Find(p => p.PhraseId == endPhraseId)?.Time;
            int countPhraseTime = arr.PhraseIterations[0].Time;
            int firstPhraseTime = arr.PhraseIterations[1].Time;

            // Clear the phrases, sections and linked difficulty levels
            arr.NewLinkedDiffs.Clear();
            arr.Phrases.Clear();
            arr.PhraseIterations.Clear();
            arr.Sections.Clear();

            // Recreate the COUNT phrase
            if (isFirst)
            {
                arr.Phrases.Add(new Phrase("COUNT", 0, PhraseMask.None));
                arr.PhraseIterations.Add(new PhraseIteration(countPhraseTime, arr.Phrases.Count - 1));
            }

            // Create the one phrase and section for the track
            int pTime = isFirst ? firstPhraseTime : arr.StartBeat;
            arr.Phrases.Add(new Phrase("track" + ArrangementNumber, (byte)(arr.Levels.Count - 1), PhraseMask.None));
            var mainPi = new PhraseIteration(pTime, arr.Phrases.Count - 1)
            {
                HeroLevels = new HeroLevels(
                    easy: (byte)((arr.Levels.Count - 1) / 3),
                    medium: (byte)((arr.Levels.Count - 1) / 2),
                    hard: (byte)(arr.Levels.Count - 1))
            };
            arr.PhraseIterations.Add(mainPi);
            arr.Sections.Add(new Section("riff", pTime, 1));

            // Recreate the END phrase and final noguitar section
            if (isLast && endPhraseTime.HasValue)
            {
                arr.Phrases.Add(new Phrase("END", 0, PhraseMask.None));
                arr.PhraseIterations.Add(new PhraseIteration(endPhraseTime.Value, arr.Phrases.Count - 1));
                arr.Sections.Add(new Section("noguitar", endPhraseTime.Value, 1));
            }
        }

        private void UpdatePhraseNames(InstrumentalArrangement song)
        {
            foreach (var phrase in song.Phrases)
            {
                if (!phrase.Name.Equals("END", StringComparison.OrdinalIgnoreCase) && !phrase.Name.Equals("noguitar", StringComparison.OrdinalIgnoreCase))
                    phrase.Name = $"arr{ArrangementNumber}{phrase.Name}";
            }
        }

        private static void CleanupToneChanges(InstrumentalArrangement song)
        {
            if (song.Tones.Changes is null)
                return;

            // Remove tones not included in the four tones
            for (int i = song.Tones.Changes.Count - 1; i >= 0; i--)
            {
                if (Array.IndexOf(song.Tones.Names, song.Tones.Changes[i].Name) == -1)
                    song.Tones.Changes.RemoveAt(i);
            }

            // Remove duplicate tone changes
            for (int i = song.Tones.Changes.Count - 2; i >= 0; i--)
            {
                if (song.Tones.Changes[i].Name == song.Tones.Changes[i + 1].Name)
                    song.Tones.Changes.RemoveAt(i + 1);
            }
        }

        private static void MergeNoguitarSections(InstrumentalArrangement song)
        {
            var anyRemoved = false;

            for (int i = 0; i < song.Sections.Count - 1; i++)
            {
                if (song.Sections[i].Name == "noguitar" && song.Sections[i + 1].Name == "noguitar")
                {
                    var time = song.Sections[i + 1].Time;

                    song.Sections.RemoveAt(i + 1);
                    song.PhraseIterations.RemoveAll(x => x.Time == time);

                    anyRemoved = true;
                }
            }

            // Fix the section numbers
            if(anyRemoved)
            {
                short counter = 1;
                foreach(var section in song.Sections)
                {
                    if (section.Name == "noguitar")
                    {
                        section.Number = counter++;
                    }
                }
            }
        }

        private static void CombinePhrases(InstrumentalArrangement song)
        {
            for (int id1 = song.Phrases.Count - 1; id1 >= 0; id1--)
            {
                for (int id2 = 0; id2 < id1; id2++)
                {
                    if (song.Phrases[id1].Name == song.Phrases[id2].Name)
                    {
                        // If there are DD levels, all the phrases should have unique names
                        // There may be multiple noguitar phrases that can be combined
                        if (song.Levels.Count > 1)
                            Debug.Assert(song.Phrases[id2].Name == "noguitar");

                        // Remove the phrase at the higher position
                        song.Phrases.RemoveAt(id1);

                        // Correct the phrase IDs for phrase iterations and linked difficulties
                        foreach (var pi in song.PhraseIterations)
                        {
                            if (pi.PhraseId == id1)
                                pi.PhraseId = id2;
                            else if (pi.PhraseId > id1)
                                pi.PhraseId--;
                        }
                        foreach (var nld in song.NewLinkedDiffs)
                        {
                            for (int p = 0; p < nld.PhraseIds.Count; p++)
                            {
                                if (nld.PhraseIds[p] == id1)
                                    nld.PhraseIds[p] = id2;
                                else if (nld.PhraseIds[p] > id1)
                                    nld.PhraseIds[p]--;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private static void CombineChords(InstrumentalArrangement combined)
        {
            for (short id1 = (short)(combined.ChordTemplates.Count - 1); id1 >= 0; id1--)
            {
                for (short id2 = 0; id2 < id1; id2++)
                {
                    if (IsSameChordTemplate(combined.ChordTemplates[id1], combined.ChordTemplates[id2]))
                    {
                        combined.ChordTemplates.RemoveAt(id1);

                        // Correct the chord IDs for chords and hand shapes in each level
                        foreach (var level in combined.Levels)
                        {
                            foreach (var chord in level.Chords)
                            {
                                if (chord.ChordId == id1)
                                    chord.ChordId = id2;
                                else if (chord.ChordId > id1)
                                    chord.ChordId--;
                            }
                            foreach (var hs in level.HandShapes)
                            {
                                if (hs.ChordId == id1)
                                    hs.ChordId = id2;
                                else if (hs.ChordId > id1)
                                    hs.ChordId--;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private static bool IsSameChordTemplate(ChordTemplate first, ChordTemplate second)
        {
            if (first.Name != second.Name || first.DisplayName != second.DisplayName)
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
            if (CombinedArrangement is not null)
            {
                CombinedArrangement.MetaData.Title = combinedTitle;
                CombinedArrangement.MetaData.TitleSort = combinedTitle;
            }
        }

        private static void CoercePhrasesAndSections(InstrumentalArrangement song)
        {
            var phraseIterations = song.PhraseIterations;

            // Combine possible multiple phrases inside a single section
            for (int i = 1; i < song.Sections.Count - 2; i++)
            {
                int piIndex = phraseIterations.FindIndexByTime(song.Sections[i].Time);
                if (piIndex != -1)
                {
                    while (piIndex < phraseIterations.Count - 1 && song.Sections.FindIndexByTime(phraseIterations[piIndex + 1].Time) == -1)
                    {
                        phraseIterations.RemoveAt(piIndex + 1);
                    }
                }
            }

            while (phraseIterations.Count > 100)
            {
                // Search for the smallest section
                int smallestIndex = 1;
                int smallestLength = int.MaxValue;
                for (int i = 1; i < song.Sections.Count - 2; i++)
                {
                    // Skip noguitar sections and sections surrounded by noguitar sections
                    if (song.Sections[i].Name == "noguitar" ||
                        (song.Sections[i - 1].Name == "noguitar" &&
                         song.Sections[i + 1].Name == "noguitar"))
                    {
                        continue;
                    }

                    int length = song.Sections[i + 1].Time - song.Sections[i].Time;
                    if (length < smallestLength)
                    {
                        smallestIndex = i;
                        smallestLength = length;
                    }
                }

                int piIndex = phraseIterations.FindIndexByTime(song.Sections[smallestIndex].Time);

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
                    int prevLength = song.Sections[smallestIndex].Time - song.Sections[smallestIndex - 1].Time;
                    int nextLength = song.Sections[smallestIndex + 2].Time - song.Sections[smallestIndex + 1].Time;

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

        private static void RemoveExtraBeats(InstrumentalArrangement song)
        {
            int songLength = song.MetaData.SongLength;

            for (int i = song.Ebeats.Count - 1; i >= 0; i--)
            {
                if (song.Ebeats[i].Time >= songLength)
                    song.Ebeats.RemoveAt(i);
                else
                    break;
            }
        }

        private static void RemoveEndPhrase(InstrumentalArrangement song)
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

        private static void RemoveCountPhrase(InstrumentalArrangement song)
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

        private static short FindLastMeasure(InstrumentalArrangement song)
        {
            for (int i = song.Ebeats.Count - 1; i >= 0; i--)
            {
                if (song.Ebeats[i].Measure > 0)
                    return song.Ebeats[i].Measure;
            }

            return 0;
        }

        private static void UpdatePhraseIterations(InstrumentalArrangement song, int startTime, int lastPhraseId)
        {
            foreach (var pi in song.PhraseIterations)
            {
                pi.Time += startTime;
                pi.PhraseId += lastPhraseId;
            }
        }

        private static void UpdateLinkedDiffs(InstrumentalArrangement song, int lastPhraseId)
        {
            foreach (var nld in song.NewLinkedDiffs)
            {
                for (int i = 0; i < nld.PhraseIds.Count; i++)
                {
                    nld.PhraseIds[i] += lastPhraseId;
                }
            }
        }

        private static void UpdateBeats(InstrumentalArrangement song, int startTime, short lastMeasure)
        {
            short measureCounter = 1;
            foreach (var beat in song.Ebeats)
            {
                if (beat.Measure >= 0)
                    beat.Measure = (short)(lastMeasure + measureCounter++);

                beat.Time += startTime;
            }
        }

        private static void UpdateSections(InstrumentalArrangement song, int startTime)
        {
            foreach (var section in song.Sections)
            {
                section.Time += startTime;
            }
        }

        private static void UpdateEvents(InstrumentalArrangement song, int startTime)
        {
            foreach (var @event in song.Events)
            {
                @event.Time += startTime;
            }
        }

        private static void UpdateNotes(InstrumentalArrangement song, int startTime)
        {
            foreach (var note in song.Levels.SelectMany(l => l.Notes))
            {
                note.Time += startTime;

                if (note.IsBend)
                {
                    for (int i = 0; i < note.BendValues!.Count; i++)
                    {
                        note.BendValues[i] = new BendValue(note.BendValues[i].Time + startTime, note.BendValues[i].Step);
                    }
                }
            }
        }

        private static void UpdateChords(InstrumentalArrangement song, int startTime, short lastChordId)
        {
            foreach (var chord in song.Levels.SelectMany(l => l.Chords))
            {
                chord.Time += startTime;
                chord.ChordId += lastChordId;
                if (chord.HasChordNotes)
                {
                    foreach (var cn in chord.ChordNotes!)
                    {
                        cn.Time += startTime;

                        if (cn.IsBend)
                        {
                            for (int i = 0; i < cn.BendValues!.Count; i++)
                            {
                                cn.BendValues[i] = new BendValue(cn.BendValues[i].Time + startTime, cn.BendValues[i].Step);
                            }
                        }
                    }
                }
            }
        }

        private static void UpdateAnchors(InstrumentalArrangement song, int startTime)
        {
            foreach (var anchor in song.Levels.SelectMany(l => l.Anchors))
            {
                anchor.Time += startTime;
            }
        }

        private static void UpdateHandShapes(InstrumentalArrangement song, int startTime, short lastChordId)
        {
            foreach (var hs in song.Levels.SelectMany(l => l.HandShapes))
            {
                hs.StartTime += startTime;
                hs.EndTime += startTime;
                hs.ChordId += lastChordId;
            }
        }

        private static void UpdateSectionNumbers(InstrumentalArrangement combined)
        {
            Dictionary<string, short> numbers = new Dictionary<string, short>();
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

        private static void CombineArrangementProperties(InstrumentalArrangement combined, InstrumentalArrangement next)
        {
            string[] skip = { "Represent", "BonusArrangement", "PathLead", "PathRhythm", "PathBass" };
            var properties = combined.MetaData.ArrangementProperties.GetType().GetProperties();

            foreach (var property in properties)
            {
                if (skip.Contains(property.Name))
                    continue;

                if ((bool)property.GetValue(next.MetaData.ArrangementProperties)! != (bool)property.GetValue(combined.MetaData.ArrangementProperties)!)
                    property.SetValue(combined.MetaData.ArrangementProperties, true);
            }
        }

        private static void CombineTones(InstrumentalArrangement combined, InstrumentalArrangement next, int startTime)
        {
            if (combined.Tones.Changes is null)
                combined.Tones.Changes = new List<ToneChange>();
            if (next.Tones.Changes is null)
                next.Tones.Changes = new List<ToneChange>();

            string? currentTone = combined.Tones.BaseToneName;
            if (combined.Tones.Changes.Count > 0)
                currentTone = combined.Tones.Changes[^1].Name;

            if (next.Tones.Changes.Count > 0 ||
               (next.Tones.Changes.Count == 0 && currentTone != next.Tones.BaseToneName))
            {
                // Add a tone change for the base tone of the next song
                if (next.Tones.BaseToneName is not null)
                    next.Tones.Changes.Insert(0, new ToneChange(next.Tones.BaseToneName, next.StartBeat - startTime, 0));

                for (int i = 0; i < next.Tones.Changes.Count; i++)
                {
                    var t = next.Tones.Changes[i];
                    next.Tones.Changes[i] = new ToneChange(t.Name, t.Time + startTime, t.Id);

                    TryAddTone(combined, t);
                }

                combined.Tones.Changes.AddRange(next.Tones.Changes);
            }
        }

        private static void TryAddTone(InstrumentalArrangement combined, ToneChange t)
        {
            // Check if the tone name already exists
            if (Array.IndexOf(combined.Tones.Names, t.Name) >= 0)
                return;

            // Try to add the tone name to the first available slot
            for (int i = 0; i < combined.Tones.Names.Length; i++)
            {
                if (combined.Tones.Names[i] is null)
                {
                    combined.Tones.Names[i] = t.Name;
                    return;
                }
            }

            Console.WriteLine($"Too many tone changes, cannot fit in tone {t.Name}");
        }
    }
}
