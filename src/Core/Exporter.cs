using System.IO.Compression;
using System.Text;

namespace Exotique.Core;

/// <summary>Fichier non copié : nom refusé (chemin dangereux) ou absent du stockage lazer.</summary>
public sealed record ExportWarning(string SetName, bool RejectedName, string File)
{
    public override string ToString() => Loc.F(RejectedName ? "WarnRejectedName" : "WarnMissingFile", SetName, File);
}

public sealed class ExportResult
{
    public List<(PlanItem Item, string Destination)> Exported { get; } = [];
    public List<(PlanItem Item, string Error)> Failed { get; } = [];
    public List<ExportWarning> Warnings { get; } = [];
    public long BytesCopied { get; set; }
    public bool Cancelled { get; set; }
    public bool DiskFull { get; set; }
}

/// <param name="Done">Maps terminées.</param>
/// <param name="Current">Map en cours de copie (null à la fin).</param>
public sealed record ExportProgress(int Done, int Total, PlanItem? Current, long BytesDone, long BytesTotal);

public static class Exporter
{
    public const string TempFolderName = StableIndex.TempFolderName;

    /// <param name="oszDir">null : dossiers directement dans Songs ; sinon : un .osz par set dans ce dossier.</param>
    /// <param name="ct">Annulation : la map en cours est terminée, puis la copie s'arrête.</param>
    public static ExportResult Run(IReadOnlyList<PlanItem> items, LazerLocation lazer, string songsDir, string? oszDir,
        StableIndex stable, IProgress<ExportProgress>? progress = null, CancellationToken ct = default)
    {
        var result = new ExportResult();
        long totalBytes = Math.Max(1, items.Sum(i => i.Bytes));

        // Les copies se font dans un dossier temporaire puis sont déplacées d'un coup :
        // une copie interrompue ne laisse jamais une map à moitié copiée dans Songs.
        string tempRoot = Path.Combine(oszDir ?? songsDir, TempFolderName);
        DeleteQuietly(tempRoot);
        Directory.CreateDirectory(tempRoot);

        var usedOszNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (oszDir != null)
        {
            Directory.CreateDirectory(oszDir);
            foreach (string f in Directory.EnumerateFiles(oszDir, "*.osz"))
                usedOszNames.Add(Path.GetFileNameWithoutExtension(f));
        }

        try
        {
            for (int i = 0; i < items.Count; i++)
            {
                if (ct.IsCancellationRequested)
                {
                    result.Cancelled = true;
                    break;
                }

                var item = items[i];
                progress?.Report(new ExportProgress(i, items.Count, item, result.BytesCopied, totalBytes));

                try
                {
                    string destination = oszDir == null
                        ? CopyAsFolder(item, lazer, songsDir, tempRoot, stable, result)
                        : WriteOsz(item, lazer, oszDir, tempRoot, usedOszNames, result);

                    result.Exported.Add((item, destination));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
                {
                    result.Failed.Add((item, e.Message));
                    if (e is IOException io && IsDiskFull(io))
                    {
                        result.DiskFull = true;
                        break;
                    }
                }
            }
        }
        finally
        {
            DeleteQuietly(tempRoot);
        }

        progress?.Report(new ExportProgress(result.Exported.Count + result.Failed.Count, items.Count, null, result.BytesCopied, totalBytes));
        return result;
    }

    private static string CopyAsFolder(PlanItem item, LazerLocation lazer, string songsDir, string tempRoot, StableIndex stable, ExportResult result)
    {
        string folderName = UniqueName(FolderName(item.Set), n => stable.FolderNames.Contains(n) || Directory.Exists(Path.Combine(songsDir, n)));
        string temp = Path.Combine(tempRoot, folderName);
        Directory.CreateDirectory(temp);

        foreach (var file in item.Set.Files)
        {
            string? rel = SafeRelativePath(file.Filename);
            if (rel == null)
            {
                result.Warnings.Add(new ExportWarning(item.Set.DisplayName, true, file.Filename));
                continue;
            }

            string src = lazer.PathForHash(file.Hash);
            if (!File.Exists(src))
            {
                result.Warnings.Add(new ExportWarning(item.Set.DisplayName, false, file.Filename));
                continue;
            }

            string dst = Path.Combine(temp, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
            File.Copy(src, dst, overwrite: true);
            result.BytesCopied += new FileInfo(src).Length;
        }

        string final = Path.Combine(songsDir, folderName);
        Directory.Move(temp, final);
        stable.FolderNames.Add(folderName);
        return final;
    }

    private static string WriteOsz(PlanItem item, LazerLocation lazer, string oszDir, string tempRoot, HashSet<string> usedNames, ExportResult result)
    {
        string name = UniqueName(FolderName(item.Set), n => usedNames.Contains(n));
        string temp = Path.Combine(tempRoot, name + ".osz");
        var entries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create, Encoding.UTF8))
        {
            foreach (var file in item.Set.Files)
            {
                string? rel = SafeRelativePath(file.Filename);
                string src = lazer.PathForHash(file.Hash);

                if (rel == null)
                {
                    result.Warnings.Add(new ExportWarning(item.Set.DisplayName, true, file.Filename));
                    continue;
                }

                if (!File.Exists(src))
                {
                    result.Warnings.Add(new ExportWarning(item.Set.DisplayName, false, file.Filename));
                    continue;
                }

                string entryName = rel.Replace('\\', '/');
                if (!entries.Add(entryName)) continue;

                zip.CreateEntryFromFile(src, entryName, CompressionLevel.Fastest);
                result.BytesCopied += new FileInfo(src).Length;
            }
        }

        string final = Path.Combine(oszDir, name + ".osz");
        File.Move(temp, final);
        usedNames.Add(name);
        return final;
    }

    // ------------------------------------------------------------------ noms

    private static readonly char[] InvalidWindowsChars = ['<', '>', ':', '"', '/', '\\', '|', '?', '*'];

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Nom à la façon de stable : « 123456 Artiste - Titre ».</summary>
    public static string FolderName(LazerSet set)
    {
        string artistTitle = $"{set.Artist} - {set.Title}".Trim(' ', '-');
        string name = set.OnlineId > 0
            ? $"{set.OnlineId} {artistTitle}".Trim()
            : artistTitle.Length > 0
                ? $"{artistTitle} ({set.Creator})".Replace(" ()", "")
                : set.Id.ToString();

        // Comme stable : les caractères interdits sont retirés du nom de dossier (pas remplacés).
        name = new string(name.Where(c => c >= 32 && Array.IndexOf(InvalidWindowsChars, c) < 0).ToArray());
        while (name.Contains("  ", StringComparison.Ordinal)) name = name.Replace("  ", " ", StringComparison.Ordinal);
        name = SanitizeSegment(name);

        // Stable est une appli .NET Framework : on garde les chemins courts (limite de 260 caractères).
        if (name.Length > 120)
            name = name[..120].TrimEnd(' ', '.');

        return name.Length == 0 ? set.Id.ToString() : name;
    }

    public static string UniqueName(string baseName, Func<string, bool> taken)
    {
        if (!taken(baseName)) return baseName;
        for (int i = 2; ; i++)
        {
            string candidate = $"{baseName} ({i})";
            if (!taken(candidate)) return candidate;
        }
    }

    /// <summary>
    /// Transforme un nom de fichier lazer (avec « / » pour les sous-dossiers) en chemin relatif sûr :
    /// pas de « .. », pas de chemin absolu, pas de caractère interdit sous Windows.
    /// </summary>
    public static string? SafeRelativePath(string filename)
    {
        string[] parts = filename.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return null;

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (p is "." or "..") return null;

            p = SanitizeSegment(p);
            if (p.Length == 0) return null;
            parts[i] = p;
        }

        return Path.Combine(parts);
    }

    private static string SanitizeSegment(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(c < 32 || Array.IndexOf(InvalidWindowsChars, c) >= 0 ? '_' : c);

        string result = sb.ToString().Trim().TrimEnd('.', ' ');

        string stem = result.Split('.')[0];
        if (ReservedWindowsNames.Contains(stem))
            result = "_" + result;

        return result;
    }

    // ---------------------------------------------------------------- divers

    private static bool IsDiskFull(IOException e)
    {
        const int ERROR_HANDLE_DISK_FULL = 0x27, ERROR_DISK_FULL = 0x70, ENOSPC = 28;
        int code = e.HResult & 0xFFFF;
        return code is ERROR_HANDLE_DISK_FULL or ERROR_DISK_FULL or ENOSPC;
    }

    public static void DeleteQuietly(string dir)
    {
        try
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // sera nettoyé au prochain lancement
        }
    }
}
