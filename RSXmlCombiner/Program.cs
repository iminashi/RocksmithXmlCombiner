using Rocksmith2014Xml;

using System;
using System.Globalization;
using XmlCombiners;

namespace RSXmlCombinerCLI
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Rocksmith 2014 XML Combiner v0.1");
                Console.WriteLine();
                Console.WriteLine("Usage: RSXmlCombiner [-x] filename1 filename2 ...");
                Console.WriteLine("The optional parameter moves everything \"left\" by that amount in seconds, i.e. it trims leading silence.");
                Console.WriteLine();
                Console.WriteLine("Example: RSXmlCombiner -7.5 file1.xml file2.xml file3.xml");
                return;
            }

            float trimSilenceAmount = 0f;
            var fileNames = args.AsSpan();
            if (args[0].StartsWith("-"))
            {
                trimSilenceAmount = float.Parse(args[0].Substring(1), NumberFormatInfo.InvariantInfo);
                Console.WriteLine($"Trimming leading silence from each subsequent file by {trimSilenceAmount:F3}s");
                fileNames = fileNames.Slice(1);
            }

            var combiner = new InstrumentalCombiner();

            for (int i = 0; i < fileNames.Length; i++)
            {
                RS2014Song next = RS2014Song.Load(fileNames[i]);
                if (HasDDLevels(next, fileNames[i]))
                    return;

                combiner.AddNext(next, trimSilenceAmount, i == fileNames.Length - 1);
            }

            string combinedFileName = $"Combined_{combiner.ArrangementType}_RS2.xml";
            combiner.Save(combinedFileName);
        }

        private static bool HasDDLevels(RS2014Song song, string fileName)
        {
            if (song.Levels.Count > 1)
            {
                Console.WriteLine($"File {fileName} has more than 1 DD level. Aborting.");
                return true;
            }

            return false;
        }
    }
}
