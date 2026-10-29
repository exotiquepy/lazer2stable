using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Exotique.Core;

public sealed record LazerLocation(string StorageDir, string RealmPath)
{
    public string FilesDir => Path.Combine(StorageDir, "files");

    /// <summary>Chemin d'un fichier dans le stockage lazer : files/a/ab/abcdef...</summary>
    public string PathForHash(string hash) =>
        Path.Combine(FilesDir, hash[..1], hash[..2], hash);
}

/// <param name="SongsDir">Dossier Songs.</param>
/// <param name="InstallDir">Dossier d'installation (osu!.exe, collection.db), s'il est connu.</param>
public sealed record StableLocation(string SongsDir, string? InstallDir)
{
    public string? CollectionDbPath => InstallDir == null ? null : Path.Combine(InstallDir, "collection.db");
    public string? ExePath => InstallDir == null ? null : Path.Combine(InstallDir, "osu!.exe");
}

public static class Paths
{
    private const string RealmFileName = "client.realm";

    // ------------------------------------------------------------------ lazer

    /// <summary>Trouve les données lazer : chemin donné, sinon emplacements par défaut.</summary>
    public static LazerLocation? FindLazer(string? hint = null)
    {
        if (!string.IsNullOrWhiteSpace(hint))
            return LazerAt(hint);

        foreach (string candidate in DefaultLazerDirs())
        {
            var found = LazerAt(candidate);
            if (found != null) return found;
        }

        return null;
    }

    /// <summary>Accepte le dossier de données, le dossier par défaut (avec storage.ini) ou client.realm lui-même.</summary>
    public static LazerLocation? LazerAt(string path)
    {
        try
        {
            path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
        }
        catch
        {
            return null;
        }

        if (File.Exists(path) && path.EndsWith(".realm", StringComparison.OrdinalIgnoreCase))
            return new LazerLocation(Path.GetDirectoryName(path)!, path);

        if (!Directory.Exists(path))
            return null;

        string realm = Path.Combine(path, RealmFileName);
        if (File.Exists(realm))
            return new LazerLocation(path, realm);

        // Emplacement personnalisé : le dossier par défaut contient storage.ini avec FullPath = ...
        string? custom = ReadIniValue(Path.Combine(path, "storage.ini"), "FullPath");
        if (!string.IsNullOrWhiteSpace(custom))
        {
            string customRealm = Path.Combine(custom, RealmFileName);
            if (File.Exists(customRealm))
                return new LazerLocation(custom, customRealm);
        }

        return null;
    }

    private static IEnumerable<string> DefaultLazerDirs()
    {
        // Windows : %APPDATA%\osu ; Linux : ~/.local/share/osu ; macOS : ~/Library/Application Support/osu
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
            yield return Path.Combine(appData, "osu");

        string localData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localData))
            yield return Path.Combine(localData, "osu");

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home))
            yield return Path.Combine(home, "Library", "Application Support", "osu");
    }

    // ----------------------------------------------------------------- stable

    public static StableLocation? FindStable(string? hint = null)
    {
        if (!string.IsNullOrWhiteSpace(hint))
            return StableAt(hint);

        foreach (string stableDir in StableInstallCandidates())
        {
            string? songs = SongsDirOf(stableDir);
            if (songs != null) return new StableLocation(songs, stableDir);
        }

        return null;
    }

    /// <summary>Accepte le dossier Songs ou le dossier d'installation de stable.</summary>
    public static StableLocation? StableAt(string path)
    {
        try
        {
            path = Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
        }
        catch
        {
            return null;
        }

        if (!Directory.Exists(path))
            return null;

        if (LooksLikeStableInstall(path))
        {
            string? songs = SongsDirOf(path);
            return songs == null ? null : new StableLocation(songs, path);
        }

        // Dossier Songs : l'installation est en général le dossier parent.
        string? parent = Path.GetDirectoryName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        string? install = parent != null && LooksLikeStableInstall(parent) ? parent : null;
        return new StableLocation(path, install);
    }

    private static bool LooksLikeStableInstall(string dir) =>
        File.Exists(Path.Combine(dir, "osu!.exe")) || File.Exists(Path.Combine(dir, "osu!.db"));

    private static string? SongsDirOf(string stableDir)
    {
        if (!Directory.Exists(stableDir))
            return null;

        // BeatmapDirectory peut être changé dans osu!.<pseudo>.cfg (chemin absolu ou relatif).
        try
        {
            foreach (string cfg in Directory.EnumerateFiles(stableDir, "osu!.*.cfg"))
            {
                string? dir = ReadIniValue(cfg, "BeatmapDirectory");
                if (string.IsNullOrWhiteSpace(dir)) continue;

                string full = Path.IsPathRooted(dir) ? dir : Path.Combine(stableDir, dir);
                if (Directory.Exists(full))
                    return Path.GetFullPath(full);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        string songs = Path.Combine(stableDir, "Songs");
        return Directory.Exists(songs) ? songs : null;
    }

    private static IEnumerable<string> StableInstallCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (OperatingSystem.IsWindows())
        {
            foreach (string dir in StableDirsFromRegistry())
                if (seen.Add(dir))
                    yield return dir;
        }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(local))
        {
            string dir = Path.Combine(local, "osu!");
            if (seen.Add(dir))
                yield return dir;
        }
    }

    [SupportedOSPlatform("windows")]
    private static List<string> StableDirsFromRegistry()
    {
        string[] keys =
        [
            @"osustable.File.osz\shell\open\command",
            @"osu!\shell\open\command",
            @"osu\shell\open\command",
        ];

        var results = new List<string>();

        foreach (var root in new[] { Registry.CurrentUser.OpenSubKey(@"Software\Classes"), Registry.ClassesRoot })
        {
            if (root == null) continue;

            foreach (string key in keys)
            {
                try
                {
                    using var k = root.OpenSubKey(key);
                    if (k?.GetValue(null) is not string command) continue;

                    string? exe = ExtractExePath(command);
                    if (exe == null) continue;
                    if (exe.Contains("osulazer", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!Path.GetFileName(exe).Equals("osu!.exe", StringComparison.OrdinalIgnoreCase)) continue;

                    string? dir = Path.GetDirectoryName(exe);
                    if (dir != null) results.Add(dir);
                }
                catch
                {
                    // clé illisible : on passe
                }
            }
        }

        return results;
    }

    private static string? ExtractExePath(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int end = command.IndexOf('"', 1);
            return end > 1 ? command[1..end] : null;
        }

        int exe = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return exe > 0 ? command[..(exe + 4)] : null;
    }

    // ---------------------------------------------------------------- commun

    private static string? ReadIniValue(string file, string key)
    {
        try
        {
            if (!File.Exists(file)) return null;

            foreach (string raw in File.ReadLines(file))
            {
                int eq = raw.IndexOf('=');
                if (eq <= 0) continue;
                if (!raw[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) continue;
                return raw[(eq + 1)..].Trim();
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }

        return null;
    }
}
