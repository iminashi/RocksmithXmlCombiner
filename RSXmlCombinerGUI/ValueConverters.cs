using Avalonia.Data.Converters;

using RSXmlCombinerGUI.Models;
using RSXmlCombinerGUI.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RSXmlCombinerGUI
{
    public static class ValueConverters
    {
        /// <summary>
        /// Used for deciding the text on the open/change buttons.
        /// </summary>
        public static readonly IValueConverter OpenButtonText = new FuncValueConverter<object, string>
            (o => o is null ? "Open..." : "Change...");

        /// <summary>
        /// Returns false if the given view model is the first in the given collection.
        /// Used for hiding the trim part for the first track.
        /// </summary>
        public static readonly IMultiValueConverter IndexToVisibility = new FuncMultiValueConverter<object, bool>(
            v =>
            {
                var values = v.ToArray();
                if (values[0] is TrackViewModel tvm && values[1] is Collection<TrackViewModel> c)
                {
                    return c.IndexOf(tvm) != 0;
                }

                return true;
            });

        /// <summary>
        /// Converts the index of the given view model into a string.
        /// </summary>
        public static readonly IMultiValueConverter IndexToString = new FuncMultiValueConverter<object, string>(
            v =>
            {
                var values = v.ToArray();
                if (values[0] is TrackViewModel tvm && values[1] is Collection<TrackViewModel> c)
                {
                    return c.IndexOf(tvm) + 1 + ". ";
                }

                return "?. ";
            });

        public static readonly IMultiValueConverter CommonTonesForArrangementType = new FuncMultiValueConverter<object, string[]>(
            v =>
            {
                var values = v.ToArray();
                if (values[0] is InstrumentalArrangement arr && values[1] is Dictionary<ArrangementType, string[]> dict)
                {
                    return dict[arr.ArrangementType].AsSpan(1).ToArray();
                }

                return new[] { "ERROR" };
            });

        public static readonly IMultiValueConverter HackConverter = new FuncMultiValueConverter<object, string>(
            v =>
            {
                var values = v.ToArray();
                if (values[0] is object || values[1] is null)
                    return string.Empty;
                else if (values[1] is InstrumentalArrangement arr)
                    return arr.BaseTone;
                else
                    return string.Empty;
            });
    }
}
