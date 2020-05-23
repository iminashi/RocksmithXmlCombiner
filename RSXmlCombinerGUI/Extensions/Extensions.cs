using Avalonia.LogicalTree;

using Rocksmith2014Xml;

using RSXmlCombinerGUI.Models;

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;

namespace RSXmlCombinerGUI.Extensions
{
    public static class Extensions
    {
        /// <summary>
        /// Runs a process asynchronously.
        /// </summary>
        /// <param name="process"></param>
        /// <returns>The exit code of the process.</returns>
        public static Task<int> RunAsync(this Process process)
        {
            var tcs = new TaskCompletionSource<int>();

            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) => tcs.SetResult(process.ExitCode);
            process.Start();

            return tcs.Task;
        }

        public static void ReplaceToneNames(this RS2014Song song, Dictionary<string, string> toneReplacements)
        {
            foreach (var kv in toneReplacements)
            {
                if (song.ToneBase == kv.Key)
                    song.ToneBase = kv.Value;
                if (song.ToneA == kv.Key)
                    song.ToneA = kv.Value;
                if (song.ToneB == kv.Key)
                    song.ToneB = kv.Value;
                if (song.ToneC == kv.Key)
                    song.ToneC = kv.Value;
                if (song.ToneD == kv.Key)
                    song.ToneD = kv.Value;

                if (song.Tones != null)
                {
                    foreach (var tone in song.Tones)
                    {
                        if (tone.Name == kv.Key)
                            tone.Name = kv.Value;
                    }
                }
            }

            // Make sure that every tone name is unique
            HashSet<string> uniqueTones = new HashSet<string>();
            if (song.ToneB != null) uniqueTones.Add(song.ToneB);
            if (song.ToneC != null) uniqueTones.Add(song.ToneC);
            if (song.ToneD != null) uniqueTones.Add(song.ToneD);
            if (song.ToneA != null) uniqueTones.Remove(song.ToneA);

            song.ToneB = song.ToneC = song.ToneD = null;

            char toneChar = 'B';
            var songType = song.GetType();
            foreach (var toneName in uniqueTones)
            {
                var toneProp = songType.GetProperty("Tone" + toneChar);
                toneProp.SetValue(song, toneName);
                toneChar++;
            }
        }

        public static void UpdateBaseTone(this RS2014Song song, InstrumentalArrangement arr)
        {
            song.ToneBase = arr.BaseTone;
        }

        public static void AddTitleToLyrics(this Vocals vocals, string title, float startBeat)
        {
            float displayTime = 3f;
            float startTime = startBeat;

            // Ensure that the title will not overlap with existing lyrics
            if (vocals.Count > 0 && vocals[0].Time < startTime + displayTime)
                displayTime = startTime - vocals[0].Time - 0.1f;

            // Don't add the title if it will be displayed for less than half a second
            if (displayTime > 0.5f)
            {
                var words = title.Split(' ');
                float length = displayTime / words.Length;
                for (int wi = words.Length - 1; wi >= 0; wi--)
                {
                    vocals.Insert(0, new Vocal(startTime + (length * wi), length, words[wi]));
                }
            }
        }

        public static string ToXmlRootElement(this ArrangementType type)
        {
            return type switch
            {
                var t when t.IsInstrumental() => "song",
                var t when t.IsVocals() => "vocals",
                ArrangementType.ShowLights => "showlights",
                _ => throw new System.Exception("BAD")
            };
            /*switch (type)
            {
                case ArrangementType.Lead:
                case ArrangementType.Rhythm:
                case ArrangementType.Bass:
                    return "song";
                case ArrangementType.Vocals:
                    return "vocals";
                case ArrangementType.ShowLights:
                    return "showlights";
                default:
                    return string.Empty;
            }*/
        }

        public static bool IsInstrumental(this ArrangementType type)
            => type.Is(ArrangementType.Instrumental);

        public static bool IsVocals(this ArrangementType type)
            => type.Is(ArrangementType.VocalsArrangement);

        public static int GetDifficultyLevels(this string fileName)
        {
            using XmlReader reader = XmlReader.Create(fileName);

            if (reader.ReadToFollowing("levels"))
                return int.Parse(reader.GetAttribute("count"));

            return -1;
        }

        public static T? GetLogicalAncestor<T>(this ILogical @this)
            where T : class
        {
            var parent = @this.GetLogicalParent();
            while(!(parent is null))
            {
                if(parent is T target)
                {
                    return target;
                }
            }

            return null;
        }

        public static bool Is(this ArrangementType @this, ArrangementType type)
            => (@this & type) != 0;
    }
}
