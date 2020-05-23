using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace RSXmlCombinerGUI.Models
{
    [JsonConverter(typeof(ArrangementConverter))]
    public abstract class Arrangement : IComparable<Arrangement>
    {
        public string FileName { get; set; } = string.Empty;
        public ArrangementType ArrangementType { get; protected set; }

        public int CompareTo([AllowNull] Arrangement other)
        {
            if (other is Arrangement)
                return ArrangementType.CompareTo(other.ArrangementType);

            return -1;
        }
    }
}
