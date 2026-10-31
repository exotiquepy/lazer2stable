using System.Runtime.InteropServices;
using System.Text;

namespace Lazer2Stable.Core;

/// <summary>Dossiers de l'appli : %APPDATA%\lazer2stable (réglages) et %LOCALAPPDATA%\lazer2stable (rapports, erreurs, dernière copie).</summary>
public static class AppPaths
{
    public const string Name = "lazer2stable";

    /// <summary>Nom des dossiers jusqu'à la v2.4 : les réglages et la dernière copie y sont repris.</summary>
    private const string OldName = "Exotique";

    public static string Roaming => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), Name);
    public static string Local => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name);
    public static string OldRoaming => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), OldName);
    public static string OldLocal => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), OldName);
}

public static class AppInfo
{
    private static readonly Version AssemblyVersion = typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>« v2.1 » (le dernier chiffre seulement s'il n'est pas nul).</summary>
    public static string DisplayVersion =>
        AssemblyVersion.Build > 0 ? $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}" : $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}";
}

/// <summary>Écrit les erreurs inattendues dans %LOCALAPPDATA%\lazer2stable\errors pour pouvoir les envoyer.</summary>
public static class CrashLog
{
    public static string Directory =>
        Path.Combine(AppPaths.Local, "errors");

    public static string? Write(Exception? e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = Path.Combine(Directory, $"erreur-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"lazer2stable {AppInfo.DisplayVersion}, {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Système : {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
            sb.AppendLine();
            sb.AppendLine(e?.ToString() ?? "(erreur sans détail)");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
            return path;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Dernier recours quand l'interface ne peut plus rien afficher : boîte de message Windows.</summary>
    public static void ShowFatal(Exception? e, string? logPath)
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            string text = Loc.T("FatalMessage") + Environment.NewLine + Environment.NewLine +
                          (e?.Message ?? "") + Environment.NewLine + Environment.NewLine +
                          (logPath != null ? Loc.T("FatalLogged") + Environment.NewLine + logPath : "");
            MessageBoxW(IntPtr.Zero, text, AppPaths.Name, 0x10 /* MB_ICONERROR */);
        }
        catch
        {
            // rien d'autre à faire
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
