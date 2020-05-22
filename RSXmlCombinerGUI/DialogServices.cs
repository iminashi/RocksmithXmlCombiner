using Avalonia.Controls;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace RSXmlCombinerGUI
{
    internal sealed class DialogServices : IDialogServices
    {
        public static readonly List<FileDialogFilter> AudioFileFiltersOpen = new List<FileDialogFilter>
        {
            new FileDialogFilter
            {
                Name = "Audio Files",
                Extensions = new List<string> { "ogg", "wav" }
            }
        };

        public static readonly List<FileDialogFilter> AudioFileFiltersSave = new List<FileDialogFilter>
        {
            new FileDialogFilter
            {
                Name = "Wave Files",
                Extensions = new List<string> { "wav" }
            },
            new FileDialogFilter
            {
                Name = "Ogg Files",
                Extensions = new List<string> { "ogg" }
            }
        };

        public static readonly List<FileDialogFilter> RocksmithXmlFileFilters = new List<FileDialogFilter>
        {
            new FileDialogFilter
            {
                Name = "Rocksmith 2014 Arrangements",
                Extensions = new List<string> { "xml" }
            }
        };

        public static readonly List<FileDialogFilter> ToolkitXmlFileFilters = new List<FileDialogFilter>
        {
            new FileDialogFilter
            {
                Name = "Toolkit Template Files",
                Extensions = new List<string> { "dlc.xml" }
            }
        };

        public static readonly List<FileDialogFilter> ProjectFileFilters = new List<FileDialogFilter>
        {
            new FileDialogFilter
            {
                Name = "Project files",
                Extensions = new List<string> { "rscproj" }
            }
        };

        private readonly Window parentWindow;

        public DialogServices(Window parent)
        {
            parentWindow = parent;
        }

        public async Task<string[]> OpenFileDialog(string title, List<FileDialogFilter> filters, bool multiSelect = false, string initialFilename = "")
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = title,
                Filters = filters,
                AllowMultiple = multiSelect,
                InitialFileName = initialFilename
            };

            return await openFileDialog.ShowAsync(parentWindow);
        }

        public async Task<string> SaveFileDialog(string title, List<FileDialogFilter> filters, string initialFilename = "")
        {
            var saveFileDialog = new SaveFileDialog
            {
                Title = title,
                Filters = filters,
                InitialFileName = initialFilename
            };

            return await saveFileDialog.ShowAsync(parentWindow);
        }

        public async Task<string> OpenFolderDialog(string title)
        {
            var openFolderDialog = new OpenFolderDialog
            {
                Title = title
            };

            return await openFolderDialog.ShowAsync(parentWindow);
        }
    }
}
