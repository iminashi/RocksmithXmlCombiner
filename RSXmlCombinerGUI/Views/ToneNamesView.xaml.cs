using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RSXmlCombinerGUI.Views
{
    public class ToneNamesView : UserControl
    {
        public ToneNamesView()
        {
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
