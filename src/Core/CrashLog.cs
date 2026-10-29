using System.Runtime.InteropServices;
using System.Text;

namespace Exotique.Core;

public static class AppInfo
{
    private static readonly Version AssemblyVersion = typeof(AppInfo).Assembly.GetName().Version ?? new Version(0, 0);

    /// <summary>« v2.1 » (le dernier chiffre seulement s'il n'est pas nul).</summary>
    public static string DisplayVersion =>
        AssemblyVersion.Build > 0 ? $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}.{AssemblyVersion.Build}" : $"v{AssemblyVersion.Major}.{AssemblyVersion.Minor}";
}

/// <summary>Écrit les erreurs inattendues dans %LOCALAPPDATA%\Exotique\erreurs pour pouvoir les envoyer.</summary>
public static class CrashLog
{
    public static string Directory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Exotique", "erreurs");

    public static string? Write(Exception? e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(Directory);
            string path = Path.Combine(Directory, $"erreur-{DateTime.Now:yyyy-MM-dd_HHmmss}.txt");
            var sb = new StringBuilder();
            sb.AppendLine($"Exotique {AppInfo.DisplayVersion} : erreur du {DateTime.Now:dd/MM/yyyy à HH:mm:ss}");
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
            MessageBoxW(IntPtr.Zero, text, "Exotique", 0x10 /* MB_ICONERROR */);
        }
        catch
        {
            // rien d'autre à faire
        }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}
