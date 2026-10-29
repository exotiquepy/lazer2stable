using System.Collections.Concurrent;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Exotique.Core;

/// <summary>Ce que stable possède déjà : identifiants de sets, de difficultés, et MD5 des fichiers .osu.</summary>
public sealed partial class StableIndex
{
    public const string TempFolderName = ".exotique-tmp";

    public HashSet<int> SetIds { get; } = [];
    public HashSet<int> BeatmapIds { get; } = [];
    public HashSet<string> Md5s { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> FolderNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int FolderCount { get; private set; }
    public int OsuFileCount { get; private set; }
    public int OszCount { get; private set; }
    public int UnreadableFiles { get; private set; }

    /// <param name="progress">Inventaire : (fichiers trouvés, -1) ; lecture : (lus, total).</param>
    public static StableIndex Scan(string songsDir, IProgress<(int Done, int Total)>? progress = null, CancellationToken ct = default)
    {
        var index = new StableIndex();
        var enumOptions = new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = false, AttributesToSkip = 0 };

        // 1. Dossiers : le nom commence souvent par l'ID du set (« 123456 Artiste - Titre »).
        foreach (string dir in Directory.EnumerateDirectories(songsDir, "*", enumOptions))
        {
            string name = Path.GetFileName(dir);
            if (name.Equals(TempFolderName, StringComparison.OrdinalIgnoreCase)) continue;

            index.FolderNames.Add(name);
            index.FolderCount++;
            if (LeadingId(name) is int id) index.SetIds.Add(id);
        }

        // 2. Tous les .osu (y compris dans des sous-dossiers), lus en parallèle.
        // L'inventaire d'abord, pour connaître le total et afficher une vraie progression.
        var osuFiles = new List<string>();
        foreach (string p in Directory.EnumerateFiles(songsDir, "*.osu",
                     new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, AttributesToSkip = 0 }))
        {
            if (IsInTempFolder(songsDir, p)) continue;
            osuFiles.Add(p);
            if (osuFiles.Count % 500 == 0) progress?.Report((osuFiles.Count, -1));
        }

        int total = osuFiles.Count;
        progress?.Report((0, total));

        var results = new ConcurrentBag<OsuInfo>();
        int done = 0, unreadable = 0;

        Parallel.ForEach(osuFiles, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, path =>
        {
            try
            {
                results.Add(OsuInfo.From(File.ReadAllBytes(path)));
            }
            catch (IOException) { Interlocked.Increment(ref unreadable); }
            catch (UnauthorizedAccessException) { Interlocked.Increment(ref unreadable); }

            int n = Interlocked.Increment(ref done);
            if (n % 200 == 0) progress?.Report((n, total));
        });
        progress?.Report((done, total));

        foreach (var info in results) index.Add(info);
        index.OsuFileCount = results.Count;
        index.UnreadableFiles = unreadable;

        // 3. Les .osz posés dans Songs (stable les importera au prochain lancement).
        foreach (string osz in Directory.EnumerateFiles(songsDir, "*.osz", enumOptions))
        {
            index.OszCount++;
            if (LeadingId(Path.GetFileNameWithoutExtension(osz)) is int id) index.SetIds.Add(id);

            try
            {
                using var zip = ZipFile.OpenRead(osz);
                foreach (var entry in zip.Entries)
                {
                    if (!entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)) continue;
                    using var s = entry.Open();
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    index.Add(OsuInfo.From(ms.ToArray()));
                }
            }
            catch
            {
                index.UnreadableFiles++;
            }
        }

        return index;
    }

    private void Add(OsuInfo info)
    {
        Md5s.Add(info.Md5);
        if (info.BeatmapId > 0) BeatmapIds.Add(info.BeatmapId);
        if (info.SetId > 0) SetIds.Add(info.SetId);
    }

    private static bool IsInTempFolder(string songsDir, string path)
    {
        string rel = Path.GetRelativePath(songsDir, path);
        return rel.StartsWith(TempFolderName + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
               || rel.StartsWith(TempFolderName + '/', StringComparison.OrdinalIgnoreCase);
    }

    private static int? LeadingId(string name)
    {
        var m = LeadingIdRegex().Match(name);
        return m.Success && int.TryParse(m.Groups[1].Value, out int id) && id > 0 ? id : null;
    }

    [GeneratedRegex(@"^(\d{1,9})(?:\s|$)")]
    private static partial Regex LeadingIdRegex();
}

/// <summary>En-tête d'un fichier .osu : version de format, IDs, et MD5 du fichier complet.</summary>
public sealed record OsuInfo(string Md5, int FormatVersion, int BeatmapId, int SetId)
{
    public static OsuInfo From(byte[] content)
    {
        string md5 = Convert.ToHexString(MD5.HashData(content)).ToLowerInvariant();

        int version = -1, beatmapId = -1, setId = -1;

        using var reader = new StreamReader(new MemoryStream(content), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        string? line;
        int lineNo = 0;
        while ((line = reader.ReadLine()) != null && lineNo++ < 200)
        {
            line = line.Trim();
            if (line.Length == 0) continue;

            if (version < 0 && line.StartsWith("osu file format v", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(line.AsSpan("osu file format v".Length), out version);
                continue;
            }

            // Les IDs sont dans [Metadata], avant [Difficulty] : inutile de lire plus loin.
            if (line.StartsWith("[Difficulty]", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("[Events]", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("[HitObjects]", StringComparison.OrdinalIgnoreCase))
                break;

            if (TryValue(line, "BeatmapID", out int b)) beatmapId = b;
            else if (TryValue(line, "BeatmapSetID", out int s)) setId = s;
        }

        return new OsuInfo(md5, version, beatmapId, setId);
    }

    /// <summary>Lit seulement la version de format (première ligne), sans tout charger.</summary>
    public static int ReadFormatVersion(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        for (int i = 0; i < 5; i++)
        {
            string? line = reader.ReadLine()?.Trim();
            if (line == null) break;
            if (line.StartsWith("osu file format v", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.AsSpan("osu file format v".Length), out int v))
                return v;
        }

        return -1;
    }

    private static bool TryValue(string line, string key, out int value)
    {
        value = -1;
        if (!line.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return false;
        int colon = line.IndexOf(':');
        if (colon < key.Length) return false;
        if (line[key.Length..colon].Trim().Length != 0) return false;
        return int.TryParse(line.AsSpan(colon + 1).Trim(), out value);
    }
}
