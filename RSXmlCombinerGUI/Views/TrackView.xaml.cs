using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using Avalonia.VisualTree;

using ReactiveUI;

using RSXmlCombinerGUI.ViewModels;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace RSXmlCombinerGUI.Views
{
    public class TrackView : ReactiveUserControl<TrackViewModel>
    {
        public TextBlock AudioFileText => this.FindControl<TextBlock>("AudioFileText");
        public Button OpenAudioButton => this.FindControl<Button>("OpenAudioButton");
        public NumericUpDown TrimAmountNumeric => this.FindControl<NumericUpDown>("TrimAmountNumeric");
        public StackPanel TrimPanel => this.FindControl<StackPanel>("TrimPanel");

        public TrackView()
        {
            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel,
                    x => x.AudioFile,
                    x => x.AudioFileText.Text,
                    Path.GetFileName)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.AudioFile,
                    x => x.AudioFileText.IsVisible,
                    value => !(value is null))
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.OpenAudio,
                    x => x.OpenAudioButton)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.AudioFile,
                    x => x.OpenAudioButton.Content,
                    value => (value is null) ? "Open..." : "Change...")
                    .DisposeWith(disposables);

                this.Bind(ViewModel,
                    x => x.TrimAmount,
                    x => x.TrimAmountNumeric.Value)
                    .DisposeWith(disposables);
            });
            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
