using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using ReactiveUI;

using RSXmlCombinerGUI.ViewModels;

using System.Reactive.Disposables;

namespace RSXmlCombinerGUI.Views
{
    public class TrackListView : ReactiveUserControl<TrackListViewModel>
    {
        public Button AddTrackButton => this.FindControl<Button>("AddTrackButton");
        public Button ImportButton => this.FindControl<Button>("ImportButton");
        public Button NewProjectButton => this.FindControl<Button>("NewProjectButton");
        public Button OpenProjectButton => this.FindControl<Button>("OpenProjectButton");
        public Button SaveProjectButton => this.FindControl<Button>("SaveProjectButton");
        public Button CombineAudioButton => this.FindControl<Button>("CombineAudioButton");
        public Button CombineArrangementsButton => this.FindControl<Button>("CombineArrangementsButton");
        public TextBlock StatusText => this.FindControl<TextBlock>("StatusText");
        public TextBlock CombineAudioErrorText => this.FindControl<TextBlock>("CombineAudioErrorText");
        public TextBox CombinedTitleTextBox => this.FindControl<TextBox>("CombinedTitleTextBox");
        public CheckBox CoercePhrasesCheckBox => this.FindControl<CheckBox>("CoercePhrasesCheckBox");
        public CheckBox AddTrackNamesCheckBox => this.FindControl<CheckBox>("AddTrackNamesCheckBox");

        public TrackListView()
        {
            this.WhenActivated(disposables =>
            {
                this.BindCommand(ViewModel,
                    x => x.AddTrack,
                    x => x.AddTrackButton)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.ToolkitImport,
                    x => x.ImportButton)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.NewProject,
                    x => x.NewProjectButton)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.OpenProject,
                    x => x.OpenProjectButton)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.SaveProject,
                    x => x.SaveProjectButton)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.CombineAudioFiles,
                    x => x.CombineAudioButton)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.CombineArrangements,
                    x => x.CombineArrangementsButton)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.StatusMessage,
                    x => x.StatusText.Text)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.CombineAudioError,
                    x => x.CombineAudioErrorText.Text)
                    .DisposeWith(disposables);

                this.Bind(ViewModel,
                    x => x.CombinedTitle,
                    x => x.CombinedTitleTextBox.Text)
                    .DisposeWith(disposables);

                this.Bind(ViewModel,
                    x => x.CoercePhrases,
                    x => x.CoercePhrasesCheckBox.IsChecked)
                    .DisposeWith(disposables);

                this.Bind(ViewModel,
                    x => x.AddTrackNamesToLyrics,
                    x => x.AddTrackNamesCheckBox.IsChecked)
                    .DisposeWith(disposables);
            });

            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
