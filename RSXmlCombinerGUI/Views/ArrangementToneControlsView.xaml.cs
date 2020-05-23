using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.ReactiveUI;

using ReactiveUI;

using RSXmlCombinerGUI.Models;
using RSXmlCombinerGUI.ViewModels;

using System.Linq;
using System.Reactive.Disposables;

namespace RSXmlCombinerGUI.Views
{
    public class ArrangementToneControlsView : ReactiveUserControl<ArrangementToneControlsViewModel>
    {
        public Button EditTonesButton => this.FindControl<Button>("EditTonesButton");
        public Panel HackPanel => this.FindControl<Panel>("HackPanel");
        public ComboBox BaseToneCombo => this.FindControl<ComboBox>("BaseToneCombo");

        public ArrangementToneControlsView()
        {
            this.WhenActivated(disposables =>
            {
                int index = ViewModel.Tracks.IndexOf(ViewModel.Parent);
                if (index == 0)
                    HackPanel.IsEnabled = false;

                if (ViewModel.Model.ToneNames?.Count > 0)
                    HackPanel.IsVisible = false;

                this.Bind(ViewModel,
                    x => x.Model.BaseTone,
                    x => x.BaseToneCombo.SelectedItem)
                    .DisposeWith(disposables);

                // Doesn't work, use XAML binding
                /*this.OneWayBind(ViewModel,
                    x => x.ToneNames,
                    x => x.BaseToneCombo.Items)
                    .DisposeWith(disposables);*/

                this.OneWayBind(ViewModel,
                    x => x.Model,
                    x => x.EditTonesButton.Background,
                    model =>
                    {
                        if (model is InstrumentalArrangement arr)
                        {
                            if (arr.ToneReplacements.Count == 0 || arr.ToneReplacements.Any(kv => string.IsNullOrEmpty(kv.Value)))
                                return Brushes.Red;
                            else
                                return Brushes.LightGreen;
                        }
                        return Brushes.Red;
                    })
                    .DisposeWith(disposables);
            });

            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
