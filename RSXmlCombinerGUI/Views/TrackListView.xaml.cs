using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RSXmlCombinerGUI.Views
{
    public class TrackListView : UserControl
    {
        public TrackListView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
