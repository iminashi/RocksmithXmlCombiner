using Avalonia.Controls;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace RSXmlCombinerGUI
{
    internal interface IDialogServices
    {
        public Task<string[]> OpenFileDialog(string title, List<FileDialogFilter> filters, bool multiSelect = false, string initialFilename = "");

        public Task<string> SaveFileDialog(string title, List<FileDialogFilter> filters, string initialFilename = "");

        public Task<string> OpenFolderDialog(string title);
    }
}
