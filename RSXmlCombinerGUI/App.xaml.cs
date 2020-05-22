using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RSXmlCombinerGUI.ViewModels;
using RSXmlCombinerGUI.Views;
using Splat;

namespace RSXmlCombinerGUI
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow { DataContext = new MainWindowViewModel() };

                // Register dialog services as singleton
                Locator.CurrentMutable.RegisterConstant(new DialogServices(desktop.MainWindow), typeof(IDialogServices));
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
