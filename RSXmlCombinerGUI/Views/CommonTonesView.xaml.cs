using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;

using ReactiveUI;

using RSXmlCombinerGUI.ViewModels;

using System.Reactive.Disposables;

namespace RSXmlCombinerGUI.Views
{
    public class CommonTonesView : ReactiveUserControl<CommonTonesViewModel>
    {
        public ItemsControl ArrangementsList => this.FindControl<ItemsControl>("ArrangementsList");
        public Button OkButton => this.FindControl<Button>("OkButton");

        public CommonTonesView()
        {
            this.WhenActivated(disposables =>
            {
                this.OneWayBind(ViewModel,
                    x => x.Arrangements,
                    x => x.ArrangementsList.Items)
                    .DisposeWith(disposables);

                this.BindCommand(ViewModel,
                    x => x.Ok,
                    x => x.OkButton)
                    .DisposeWith(disposables);
            });

            InitializeComponent();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
    }
}
