using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using swgoh_command_bridge.UI.Views;
using swgoh_command_bridge.UI.ViewModels;

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
            if (Environment.GetCommandLineArgs().Any(argument =>
                    string.Equals(argument, "--mod-visual-preview", StringComparison.OrdinalIgnoreCase)))
            {
                desktop.MainWindow = new ModVisualPreviewWindow
                {
                    DataContext = new ModVisualPreviewViewModel()
                };
                base.OnFrameworkInitializationCompleted();
                return;
            }

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
