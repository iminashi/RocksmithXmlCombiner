using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using DynamicData.Binding;

using ReactiveUI;

using RSXmlCombinerGUI.ViewModels;

using System;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace RSXmlCombinerGUI.Views
{
    public class TrackView : ReactiveUserControl<TrackViewModel>
    {
        public TextBlock TrackNumberText => this.FindControl<TextBlock>("TrackNumberText");
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

                // If ListBox virtualization is not used
                /*ViewModel.Parent.Tracks.ObserveCollectionChanges()
                    .Select(_ => ViewModel.Parent.Tracks.IndexOf(ViewModel) != 0)
                    .StartWith(ViewModel.Parent.Tracks.IndexOf(ViewModel) != 0)
                    .BindTo(this, x => x.TrimPanel.IsVisible)
                    .DisposeWith(disposables);

                ViewModel.Parent.Tracks.ObserveCollectionChanges()
                    .Select(_ => ViewModel.Parent.Tracks.IndexOf(ViewModel) + 1 + ". ")
                    .StartWith(ViewModel.Parent.Tracks.IndexOf(ViewModel) + 1 + ". ")
                    .BindTo(this, x => x.TrackNumberText.Text)
                    .DisposeWith(disposables);*/
            });

            InitializeComponent();
        }

        // Correctly updates the values based on the index of the view model when ListBox virtualization is used
        protected override void OnDataContextChanged(EventArgs e)
        {
            base.OnDataContextChanged(e);

            int index = ViewModel.Parent.Tracks.IndexOf(ViewModel);
            TrackNumberText.Text = index + 1 + ". ";
            TrimPanel.IsVisible = index != 0;
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
