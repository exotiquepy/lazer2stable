using System.Runtime.InteropServices;
using Avalonia;

namespace Lazer2Stable.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Filets de sécurité : une erreur imprévue est enregistrée dans un fichier que l'on peut envoyer.
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Lazer2Stable.Core.CrashLog.ShowFatal(ex, Lazer2Stable.Core.CrashLog.Write(ex));
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Lazer2Stable.Core.CrashLog.Write(e.Exception);
            e.SetObserved();
        };

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

        // Rendu logiciel si on le demande (LAZER2STABLE_SOFTWARE=1) ou si on tourne sous Wine :
        // l'accélération graphique y est souvent absente.
        if (OperatingSystem.IsWindows() && (Environment.GetEnvironmentVariable("LAZER2STABLE_SOFTWARE") == "1" || IsWine()))
        {
            builder = builder.With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.Software],
                CompositionMode = [Win32CompositionMode.RedirectionSurface],
            });
        }

        return builder;
    }

    private static bool IsWine()
    {
        try
        {
            return NativeLibrary.TryLoad("ntdll.dll", out var ntdll) && NativeLibrary.TryGetExport(ntdll, "wine_get_version", out _);
        }
        catch
        {
            return false;
        }
    }
}
