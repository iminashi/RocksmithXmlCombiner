using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RSXmlCombinerGUI.Views
{
    public class TrackView : UserControl
    {
        public TrackView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
