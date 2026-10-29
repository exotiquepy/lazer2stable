using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Exotique.App.ViewModels;
using Exotique.Core;

namespace Exotique.App;

public sealed class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();

            // Erreur dans l'interface : on l'enregistre et on l'affiche au lieu de fermer l'appli.
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                string? log = CrashLog.Write(e.Exception);
                if (desktop.MainWindow?.DataContext is MainViewModel vm)
                {
                    vm.ShowError(e.Exception.Message, log);
                    e.Handled = true;
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
