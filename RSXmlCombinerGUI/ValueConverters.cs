using Avalonia.Data.Converters;

using RSXmlCombinerGUI.ViewModels;

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

        public static readonly IMultiValueConverter HackConverter = new FuncMultiValueConverter<object, string>(
            v =>
            {
                var values = v.ToArray();
                if (values[0] is object || values[1] is null)
                    return string.Empty;
                else if (values[1] is string str)
                    return str;
                else
                    return string.Empty;
            });
    }
}
