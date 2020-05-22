using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RSXmlCombinerGUI.Views
{
    public class CommonTonesView : UserControl
    {
        public CommonTonesView()
        {
            this.InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
