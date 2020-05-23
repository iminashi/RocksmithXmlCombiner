using RSXmlCombinerGUI.Models;

using System.Collections.Generic;

namespace RSXmlCombinerGUI
{
    public static class CommonTonesRepository
    {
        private static Dictionary<ArrangementType, string[]> CommonToneNames { get; } = new Dictionary<ArrangementType, string[]>();

        public static string[] GetCommonTones(ArrangementType arrangementType)
        {
            if (!CommonToneNames.ContainsKey(arrangementType))
                CommonToneNames.Add(arrangementType, new string[5]);

            return CommonToneNames[arrangementType];
        }

        internal static void SetCommonTones(ArrangementType arrangementType, string[] value)
        {
            CommonToneNames[arrangementType] = value;
        }

        internal static Dictionary<ArrangementType, string[]> GetCopy()
        {
            return new Dictionary<ArrangementType, string[]>(CommonToneNames);
        }
    }
}
