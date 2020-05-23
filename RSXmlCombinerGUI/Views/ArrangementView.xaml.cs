using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.ReactiveUI;

using ReactiveUI;

using RSXmlCombinerGUI.Models;
using RSXmlCombinerGUI.ViewModels;

using System;
using System.IO;
using System.Linq;
using System.Reactive.Disposables;

namespace RSXmlCombinerGUI.Views
{
    public class ArrangementView : ReactiveUserControl<ArrangementViewModel>
    {
        public TextBlock ArrangementName => this.FindControl<TextBlock>("ArrangementName");
        public TextBlock FileNameShort => this.FindControl<TextBlock>("FileNameShort");
        public Button SelectFileButton => this.FindControl<Button>("SelectFileButton");
        public StackPanel MainPanel => this.FindControl<StackPanel>("MainPanel");
        public ContentControl ToneControls => this.FindControl<ContentControl>("ToneControls");

        public ArrangementView()
        {
            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel,
                    x => x.Name,
                    x => x.ArrangementName.Text)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.Model,
                    x => x.ArrangementName.Foreground,
                    GetTitleBrush)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.Model,
                    x => x.BorderBrush,
                    GetTitleBrush)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.Model,
                    x => x.FileNameShort.IsVisible,
                    model => !(model is null))
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.Model,
                    x => x.FileNameShort.Text,
                    model => (model is null) ? string.Empty : Path.GetFileNameWithoutExtension(model.FileName))
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.Model,
                    x => x.SelectFileButton.Content,
                    model => (model is null) ? "Open..." : "Change...")
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.SelectArrangement,
                    x => x.SelectFileButton)
                    .DisposeWith(disposables);

                this.OneWayBind(ViewModel,
                    x => x.ToneControls,
                    x => x.ToneControls.Content)
                    .DisposeWith(disposables);
            });

            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private ISolidColorBrush GetTitleBrush(Arrangement? arrangement) =>
            arrangement?.ArrangementType switch
            {
                ArrangementType.Lead => Brushes.Orange,
                ArrangementType.Rhythm => Brushes.Green,
                ArrangementType.Combo => Brushes.DeepPink,
                ArrangementType.Bass => Brushes.Blue,
                ArrangementType.Vocals => Brushes.DarkRed,
                ArrangementType.JVocals => Brushes.DarkRed,
                ArrangementType.ShowLights => Brushes.DarkViolet,
                _ => Brushes.Gray,
            };
    }
}
