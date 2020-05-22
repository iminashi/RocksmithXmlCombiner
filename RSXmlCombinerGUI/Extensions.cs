using Rocksmith2014Xml;

using RSXmlCombinerGUI.Models;

using System.Diagnostics;
using System.Threading.Tasks;
using System.Xml;

namespace RSXmlCombinerGUI
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

        public static void UpdateBaseTone(this RS2014Song song, InstrumentalArrangement arr)
        {
            song.ToneBase = arr.BaseTone;
        }

        public static string ToXmlRootElement(this ArrangementType type)
        {
            switch (type)
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
            }
        }

        public static bool IsInstrumental(this ArrangementType type)
            => type == ArrangementType.Bass ||
               type == ArrangementType.Lead ||
               type == ArrangementType.Rhythm;

        public static int GetDifficultyLevels(this string fileName)
        {
            using XmlReader reader = XmlReader.Create(fileName);

            if(reader.ReadToFollowing("levels"))
                return int.Parse(reader.GetAttribute("count"));

            return -1;
        }
    }
}
