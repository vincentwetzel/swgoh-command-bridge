using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using swgoh_command_bridge.UI.ViewModels;
using swgoh_command_bridge.UI.Views;

namespace swgoh_command_bridge.UI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var composition = ApplicationComposition.CreateDefault();
            ThemeManager.Apply(composition.Settings.CurrentSettings.Theme);
            var viewModel = new MainWindowViewModel(composition);
            var window = new MainWindow
            {
                DataContext = viewModel,
            };

            window.Opened += async (_, _) => await viewModel.InitializeAsync();
            window.Closed += (_, _) => composition.Dispose();
            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
